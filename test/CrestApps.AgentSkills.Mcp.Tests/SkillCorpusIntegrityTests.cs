using CrestApps.AgentSkills.Mcp.Services;
using System.Text.RegularExpressions;
using Xunit;

namespace CrestApps.AgentSkills.Mcp.Tests;

/// <summary>
/// Guards the authored skill corpus under <c>src/CrestApps.AgentSkills</c>: every skill directory
/// must contain a parseable <c>SKILL.md</c> whose front-matter <c>name</c> equals its directory
/// name, names must be globally unique across all source roots, and descriptions must avoid the
/// unsafe raw colon-space sequence that breaks the front-matter YAML parser.
/// </summary>
public sealed partial class SkillCorpusIntegrityTests
{
    private const int MaxDescriptionLength = 1024;

    private static readonly string[] _sourceRoots =
    [
        "orchardcore",
        "crestapps-orchardcore",
        "crestapps-core",
    ];

    public static TheoryData<string> AllSkillDirectories()
    {
        var data = new TheoryData<string>();

        foreach (var dir in EnumerateSkillDirectories())
        {
            data.Add(dir);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllSkillDirectories))]
    public void Skill_HasParseableFrontMatterWithMatchingName(string skillDirectory)
    {
        var skillFile = Path.Combine(skillDirectory, "SKILL.md");
        var expectedName = Path.GetFileName(skillDirectory);

        Assert.True(File.Exists(skillFile), $"Missing SKILL.md in '{skillDirectory}'.");

        var content = File.ReadAllText(skillFile);

        Assert.True(
            SkillFrontMatterParser.TryParse(content, out var name, out var description, out _),
            $"SKILL.md in '{expectedName}' has invalid front-matter (must open and close with '---').");

        Assert.Equal(expectedName, name);
        Assert.False(string.IsNullOrWhiteSpace(description), $"Skill '{expectedName}' has an empty description.");
        Assert.True(
            description.Length <= MaxDescriptionLength,
            $"Skill '{expectedName}' description has {description.Length} characters; the maximum is {MaxDescriptionLength}.");
    }

    [Theory]
    [MemberData(nameof(AllSkillDirectories))]
    public void Skill_DescriptionHasNoUnsafeColon(string skillDirectory)
    {
        var skillFile = Path.Combine(skillDirectory, "SKILL.md");
        var expectedName = Path.GetFileName(skillDirectory);

        var content = File.ReadAllText(skillFile);

        Assert.True(SkillFrontMatterParser.TryParse(content, out _, out var description, out _));

        // An unquoted YAML scalar that contains ": " (colon followed by a space) can be
        // misparsed as a nested mapping. Descriptions in this corpus are intentionally plain
        // single-line scalars, so this sequence must not appear.
        Assert.False(
            description.Contains(": ", StringComparison.Ordinal),
            $"Skill '{expectedName}' description contains an unsafe ': ' sequence: {description}");
    }

    [Fact]
    public void SkillNames_AreGloballyUnique()
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<string>();

        foreach (var dir in EnumerateSkillDirectories())
        {
            var name = Path.GetFileName(dir);

            if (seen.TryGetValue(name, out var firstDir))
            {
                duplicates.Add($"'{name}' appears in both '{firstDir}' and '{dir}'.");
            }
            else
            {
                seen[name] = dir;
            }
        }

        Assert.True(duplicates.Count == 0, "Duplicate skill names found:\n" + string.Join("\n", duplicates));
    }

    [Fact]
    public void MarkdownFiles_HaveBalancedCodeFencesAndValidLocalLinks()
    {
        var invalidFences = new List<string>();
        var brokenLinks = new List<string>();

        foreach (var file in EnumerateMarkdownFiles())
        {
            var lines = File.ReadAllLines(file);
            var fence = '\0';

            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                var trimmed = lines[lineNumber].TrimStart();

                if (fence == '\0' &&
                    trimmed.StartsWith('`') &&
                    !trimmed.StartsWith("```", StringComparison.Ordinal) &&
                    trimmed.IndexOf('`', 1) < 0)
                {
                    invalidFences.Add($"{file}:{lineNumber + 1} has a lone or incomplete backtick fence.");
                }

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    if (fence is '\0' or '`')
                    {
                        fence = fence == '\0' ? '`' : '\0';
                    }
                }
                else if (trimmed.StartsWith("~~~", StringComparison.Ordinal))
                {
                    if (fence is '\0' or '~')
                    {
                        fence = fence == '\0' ? '~' : '\0';
                    }
                }
            }

            if (fence != '\0')
            {
                invalidFences.Add($"{file} has an unclosed code fence.");
            }

            var content = File.ReadAllText(file);

            foreach (Match match in MarkdownLinkRegex().Matches(content))
            {
                var href = match.Groups["href"].Value;

                if (href.Length == 0 ||
                    href.StartsWith('#') ||
                    Uri.TryCreate(href, UriKind.Absolute, out _))
                {
                    continue;
                }

                var path = Uri.UnescapeDataString(href.Split('#', 2)[0]);

                if (path.Length == 0)
                {
                    continue;
                }

                var target = Path.GetFullPath(path, Path.GetDirectoryName(file)!);

                if (!File.Exists(target) && !Directory.Exists(target))
                {
                    brokenLinks.Add($"{file} links to missing local path '{href}'.");
                }
            }
        }

        Assert.True(invalidFences.Count == 0, "Invalid Markdown fences found:\n" + string.Join("\n", invalidFences));
        Assert.True(brokenLinks.Count == 0, "Broken local Markdown links found:\n" + string.Join("\n", brokenLinks));
    }

    [Fact]
    public void OrchardCorePackageExamples_TargetCurrentMajor()
    {
        var outdatedReferences = new List<string>();

        foreach (var file in EnumerateMarkdownFiles())
        {
            var content = File.ReadAllText(file);

            foreach (Match match in OrchardCorePackageReferenceRegex().Matches(content))
            {
                if (int.Parse(match.Groups["major"].Value) < 3)
                {
                    outdatedReferences.Add($"{file}: {match.Value}");
                }
            }
        }

        Assert.True(
            outdatedReferences.Count == 0,
            "Orchard Core package examples targeting a version older than 3.x were found:\n" +
            string.Join("\n", outdatedReferences));
    }

    private static IEnumerable<string> EnumerateSkillDirectories()
    {
        var skillsRoot = LocateSkillsRoot();

        foreach (var root in _sourceRoots)
        {
            var rootPath = Path.Combine(skillsRoot, root);

            if (!Directory.Exists(rootPath))
            {
                continue;
            }

            foreach (var dir in Directory.EnumerateDirectories(rootPath))
            {
                yield return dir;
            }
        }
    }

    private static IEnumerable<string> EnumerateMarkdownFiles()
    {
        var skillsRoot = LocateSkillsRoot();

        foreach (var root in _sourceRoots)
        {
            var rootPath = Path.Combine(skillsRoot, root);

            if (!Directory.Exists(rootPath))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private static string LocateSkillsRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "CrestApps.AgentSkills");

            if (Directory.Exists(Path.Combine(candidate, "orchardcore")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate 'src/CrestApps.AgentSkills' by walking up from the test base directory.");
    }

    [GeneratedRegex(@"\[[^\]]+\]\((?<href>[^)]+)\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(
        @"<PackageReference\b(?=[^>]*\bInclude=""OrchardCore\.[^""]+"")(?=[^>]*\bVersion=""(?<major>\d+))[^>]*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex OrchardCorePackageReferenceRegex();
}

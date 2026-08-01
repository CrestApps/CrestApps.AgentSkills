using CrestApps.AgentSkills.Mcp.Services;
using Xunit;

namespace CrestApps.AgentSkills.Mcp.Tests;

/// <summary>
/// Guards the authored skill corpus under <c>src/CrestApps.AgentSkills</c>: every skill directory
/// must contain a parseable <c>SKILL.md</c> whose front-matter <c>name</c> equals its directory
/// name, names must be globally unique across all source roots, and descriptions must avoid the
/// unsafe raw colon-space sequence that breaks the front-matter YAML parser.
/// </summary>
public sealed class SkillCorpusIntegrityTests
{
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
}

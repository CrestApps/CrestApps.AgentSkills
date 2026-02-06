using System.Reflection;
using System.Security;

namespace CrestApps.OrchardCore.AgentSkills.Extensions;

public static class OrchardCoreAgentSkillsExtensions
{
    private const string SkillsRelativePath = ".agents/skills";

    /// <summary>
    /// Adds Orchard Core agent skills from the auto-mounted .agents/skills directory.
    /// Loads skills from the output directory where NuGet contentFiles are copied.
    /// </summary>
    /// <param name="builder">The agent builder instance.</param>
    /// <returns>The agent builder for chaining.</returns>
    public static IAgentBuilder AddOrchardCoreSkills(this IAgentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var path = Path.Combine(AppContext.BaseDirectory, SkillsRelativePath);

        builder.AddSkillsFromPath(path);

        return builder;
    }

    /// <summary>
    /// Discovers all skill files bundled with this NuGet package and copies them
    /// into the <c>.agents/skills</c> folder at the root of the solution or project.
    /// <para>
    /// The solution root is determined dynamically by walking up from the entry
    /// assembly's location until a <c>.sln</c> file is found.
    /// If no <c>.sln</c> is found, the entry assembly's directory is used as the fallback.
    /// </para>
    /// <para>
    /// This method is idempotent — it can be called multiple times safely.
    /// Existing files are overwritten to keep the package as the source of truth.
    /// </para>
    /// </summary>
    /// <param name="builder">The agent builder instance.</param>
    /// <returns>The agent builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the embedded skills source directory cannot be found.
    /// </exception>
    public static IAgentBuilder MountOrchardCoreSkills(this IAgentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var sourceDir = GetEmbeddedSkillsSourceDirectory();

        if (sourceDir is null || !Directory.Exists(sourceDir))
        {
            throw new InvalidOperationException(
                $"The embedded skills source directory could not be found. " +
                $"Ensure the CrestApps.OrchardCore.AgentSkills NuGet package is installed " +
                $"and that skill files are present in the output directory under '{SkillsRelativePath}'.");
        }

        try
        {
            var solutionRoot = FindSolutionRoot();
            var targetDir = Path.Combine(solutionRoot, SkillsRelativePath);

            CopyDirectory(sourceDir, targetDir);

            builder.AddSkillsFromPath(targetDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Swallow filesystem exceptions to remain safe in read-only or
            // containerized environments. Skills will not be mounted but the
            // application continues to function.
        }

        return builder;
    }

    /// <summary>
    /// Finds the directory containing the embedded skills shipped inside the NuGet package.
    /// Skills are located relative to the assembly that contains this type.
    /// </summary>
    private static string? GetEmbeddedSkillsSourceDirectory()
    {
        var assemblyLocation = typeof(OrchardCoreAgentSkillsExtensions).Assembly.Location;

        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);

            if (assemblyDir is not null)
            {
                // When NuGet contentFiles are copied to the output directory they
                // land under <output>/.agents/skills.
                var candidate = Path.Combine(assemblyDir, SkillsRelativePath);

                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        // Also check AppContext.BaseDirectory as a fallback.
        var fallback = Path.Combine(AppContext.BaseDirectory, SkillsRelativePath);

        return Directory.Exists(fallback) ? fallback : null;
    }

    /// <summary>
    /// Walks up the directory tree from the entry assembly's location to locate
    /// the solution root. Looks for <c>.sln</c> files as the indicator of the root.
    /// Falls back to the entry assembly directory.
    /// </summary>
    private static string FindSolutionRoot()
    {
        var startDir = GetStartDirectory();

        var current = new DirectoryInfo(startDir);

        while (current is not null)
        {
            if (current.EnumerateFiles("*.sln").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        // Fallback: use the starting directory when no solution root is found.
        return startDir;
    }

    private static string GetStartDirectory()
    {
        // Prefer the entry assembly location, but fall back to BaseDirectory
        // for environments like single-file publish where Location may be empty.
        var entryLocation = Assembly.GetEntryAssembly()?.Location;

        if (!string.IsNullOrEmpty(entryLocation))
        {
            var dir = Path.GetDirectoryName(entryLocation);

            if (dir is not null)
            {
                return dir;
            }
        }

        return AppContext.BaseDirectory;
    }

    /// <summary>
    /// Recursively copies all files and subdirectories from
    /// <paramref name="sourceDir"/> into <paramref name="targetDir"/>,
    /// overwriting existing files.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var subDir in Directory.EnumerateDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }
}

using ModelContextProtocol.Server;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Providers;

/// <summary>
/// Loads Orchard Core skill resource files from the filesystem and
/// produces <see cref="McpServerResource"/> instances for MCP registration.
/// Each <c>skill.yaml</c> and <c>examples/*.md</c> file becomes a resource.
/// </summary>
public sealed class FileSystemSkillResourceProvider
{
    private readonly string _skillsPath;

    public FileSystemSkillResourceProvider(string skillsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillsPath);
        _skillsPath = skillsPath;
    }

    /// <summary>
    /// Discovers all <c>skill.yaml</c> and example files under the skills
    /// directory and creates MCP resource instances from their contents.
    /// </summary>
    /// <returns>A list of <see cref="McpServerResource"/> instances.</returns>
    public IReadOnlyList<McpServerResource> GetResources()
    {
        if (!Directory.Exists(_skillsPath))
        {
            return [];
        }

        var resources = new List<McpServerResource>();

        foreach (var skillDir in Directory.EnumerateDirectories(_skillsPath))
        {
            var skillName = Path.GetFileName(skillDir);

            // Register skill.yaml as a resource
            var skillFile = Path.Combine(skillDir, "skill.yaml");
            if (File.Exists(skillFile))
            {
                var content = File.ReadAllText(skillFile);
                var resource = McpServerResource.Create(
                    () => content,
                    new McpServerResourceCreateOptions
                    {
                        Name = $"{skillName}/skill.yaml",
                        Description = $"Orchard Core skill definition for {skillName}",
                        UriTemplate = $"skills://{skillName}/skill.yaml",
                        MimeType = "text/yaml",
                    });
                resources.Add(resource);
            }

            // Register example files as resources
            var examplesDir = Path.Combine(skillDir, "examples");
            if (!Directory.Exists(examplesDir))
            {
                continue;
            }

            foreach (var exampleFile in Directory.EnumerateFiles(examplesDir))
            {
                var fileName = Path.GetFileName(exampleFile);
                var content = File.ReadAllText(exampleFile);
                var resource = McpServerResource.Create(
                    () => content,
                    new McpServerResourceCreateOptions
                    {
                        Name = $"{skillName}/examples/{fileName}",
                        Description = $"Orchard Core example for {skillName}",
                        UriTemplate = $"skills://{skillName}/examples/{fileName}",
                        MimeType = "text/markdown",
                    });
                resources.Add(resource);
            }
        }

        return resources;
    }
}

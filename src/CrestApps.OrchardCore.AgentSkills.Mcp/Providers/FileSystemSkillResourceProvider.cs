using CrestApps.OrchardCore.AgentSkills.Mcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Providers;

/// <summary>
/// Loads Orchard Core skill resource files via <see cref="IMcpResourceFileStore"/>
/// and produces <see cref="McpServerResource"/> instances for MCP registration.
/// Each <c>skill.yaml</c> and <c>examples/*.md</c> file becomes a resource.
/// Registered as a singleton — results are lazily loaded and cached.
/// </summary>
public sealed class FileSystemSkillResourceProvider
{
    private readonly IMcpResourceFileStore _fileStore;
    private readonly ILogger<FileSystemSkillResourceProvider> _logger;
    private IReadOnlyList<McpServerResource>? _resources;

    public FileSystemSkillResourceProvider(
        IMcpResourceFileStore fileStore,
        ILogger<FileSystemSkillResourceProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(logger);
        _fileStore = fileStore;
        _logger = logger;
    }

    /// <summary>
    /// Discovers all <c>skill.yaml</c> and example files under the skills
    /// directory and creates MCP resource instances from their contents.
    /// Results are lazily loaded and cached after the first call.
    /// </summary>
    public async Task<IReadOnlyList<McpServerResource>> GetResourcesAsync()
    {
        if (_resources is not null)
        {
            return _resources;
        }

        var resources = new List<McpServerResource>();

        await foreach (var skillDir in _fileStore.GetDirectoryContentAsync(null, includeSubDirectories: false))
        {
            if (!skillDir.IsDirectory)
            {
                continue;
            }

            var skillName = skillDir.Name;

            // Register skill.yaml as a resource.
            var skillYamlPath = NormalizePath($"{skillName}/skill.yaml");
            var skillInfo = await _fileStore.GetFileInfoAsync(skillYamlPath);

            if (skillInfo is not null)
            {
                string skillContent;

                try
                {
                    await using var stream = await _fileStore.GetFileStreamAsync(skillYamlPath);
                    using var reader = new StreamReader(stream);
                    skillContent = await reader.ReadToEndAsync();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Failed to read skill.yaml for skill '{SkillName}'.", skillName);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(skillContent))
                {
                    _logger.LogWarning("skill.yaml for skill '{SkillName}' is empty, skipping.", skillName);
                }
                else
                {
                    var resource = McpServerResource.Create(
                        () => skillContent,
                        new McpServerResourceCreateOptions
                        {
                            Name = $"{skillName}/skill.yaml",
                            Description = $"Orchard Core skill definition for {skillName}",
                            UriTemplate = $"skills://{skillName}/skill.yaml",
                            MimeType = "text/yaml",
                        });
                    resources.Add(resource);
                }
            }
            else
            {
                _logger.LogDebug("No skill.yaml found for skill '{SkillName}'.", skillName);
            }

            // Register example *.md files as resources.
            var examplesPath = NormalizePath($"{skillName}/examples");
            var examplesDir = await _fileStore.GetDirectoryInfoAsync(examplesPath);

            if (examplesDir is null)
            {
                continue;
            }

            await foreach (var entry in _fileStore.GetDirectoryContentAsync(examplesPath, includeSubDirectories: false))
            {
                if (entry.IsDirectory || !entry.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName = entry.Name;
                var filePath = NormalizePath(entry.Path);
                string exampleContent;

                try
                {
                    await using var stream = await _fileStore.GetFileStreamAsync(filePath);
                    using var reader = new StreamReader(stream);
                    exampleContent = await reader.ReadToEndAsync();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Failed to read example file '{FileName}' for skill '{SkillName}'.", fileName, skillName);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(exampleContent))
                {
                    _logger.LogDebug("Example file '{FileName}' for skill '{SkillName}' is empty, skipping.", fileName, skillName);
                    continue;
                }

                var resource = McpServerResource.Create(
                    () => exampleContent,
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

        _logger.LogInformation("Loaded {Count} MCP resources from agent skills.", resources.Count);
        _resources = resources;

        return _resources;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

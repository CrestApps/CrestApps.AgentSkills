using CrestApps.AgentSkills.Mcp.Abstractions;
using CrestApps.AgentSkills.Mcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CrestApps.AgentSkills.Mcp.Providers;

/// <summary>
/// Loads skill resource files via <see cref="ISkillFileStore"/>
/// and produces <see cref="McpServerResource"/> instances for MCP registration.
/// Each skill file (SKILL.md, SKILL.yaml, SKILL.yml) and <c>references/*.md</c>
/// file becomes a resource.
/// Registered as a singleton — results are lazily loaded and cached.
/// </summary>
public sealed class SkillResourceProvider : IResourceProvider
{
    private readonly ISkillFileStore _fileStore;
    private readonly ILogger<SkillResourceProvider> _logger;
    private IReadOnlyList<McpServerResource>? _resources;

    public SkillResourceProvider(
        ISkillFileStore fileStore,
        ILogger<SkillResourceProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(logger);
        _fileStore = fileStore;
        _logger = logger;
    }

    /// <summary>
    /// Discovers all skill files and reference files under the skills
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

            var skillDirName = skillDir.Name;

            // Register the skill file as a resource.
            var (skillFileName, skillContent, skillName, skillDescription) = await TryReadAndParseSkillFileAsync(skillDirName);

            if (skillFileName is not null && skillContent is not null && skillName is not null && skillDescription is not null)
            {
                var mimeType = GetMimeType(skillFileName);
                var resource = McpServerResource.Create(
                    () => skillContent,
                    new McpServerResourceCreateOptions
                    {
                        Name = $"{skillName}/{skillFileName}",
                        Description = skillDescription,
                        UriTemplate = $"skills://{skillName}/{skillFileName}",
                        MimeType = mimeType,
                    });
                resources.Add(resource);
            }
            else
            {
                _logger.LogDebug("No valid skill file found for skill '{SkillName}'.", skillDirName);
            }

            // Register reference *.md files as resources.
            var referencesPath = NormalizePath($"{skillDirName}/references");
            var referencesDir = await _fileStore.GetDirectoryInfoAsync(referencesPath);

            if (referencesDir is null)
            {
                continue;
            }

            await foreach (var entry in _fileStore.GetDirectoryContentAsync(referencesPath, includeSubDirectories: false))
            {
                if (entry.IsDirectory || !entry.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName = entry.Name;
                var filePath = NormalizePath(entry.Path);
                string referenceContent;

                try
                {
                    await using var stream = await _fileStore.GetFileStreamAsync(filePath);
                    using var reader = new StreamReader(stream);
                    referenceContent = await reader.ReadToEndAsync();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Failed to read reference file '{FileName}' for skill '{SkillName}'.", fileName, skillDirName);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(referenceContent))
                {
                    _logger.LogDebug("Reference file '{FileName}' for skill '{SkillName}' is empty, skipping.", fileName, skillDirName);
                    continue;
                }

                var resource = McpServerResource.Create(
                    () => referenceContent,
                    new McpServerResourceCreateOptions
                    {
                        Name = $"{skillDirName}/references/{fileName}",
                        Description = $"Reference for {skillDirName}",
                        UriTemplate = $"skills://{skillDirName}/references/{fileName}",
                        MimeType = "text/markdown",
                    });
                resources.Add(resource);
            }
        }

        _logger.LogInformation("Loaded {Count} MCP resources from agent skills.", resources.Count);
        _resources = resources;

        return _resources;
    }

    private async Task<(string? FileName, string? Content, string? Name, string? Description)> TryReadAndParseSkillFileAsync(
        string skillDirName)
    {
        foreach (var candidateFileName in SkillFileParser.SupportedSkillFileNames)
        {
            var skillPath = NormalizePath($"{skillDirName}/{candidateFileName}");
            var skillInfo = await _fileStore.GetFileInfoAsync(skillPath);

            if (skillInfo is null)
            {
                continue;
            }

            string content;

            try
            {
                await using var stream = await _fileStore.GetFileStreamAsync(skillPath);
                using var reader = new StreamReader(stream);
                content = await reader.ReadToEndAsync();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to read '{FileName}' for skill '{SkillName}'.", candidateFileName, skillDirName);
                continue;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Skill file '{FileName}' for skill '{SkillName}' is empty, skipping.", candidateFileName, skillDirName);
                continue;
            }

            if (!SkillFileParser.TryParse(candidateFileName, content, out var name, out var description, out _))
            {
                _logger.LogWarning(
                    "Skill file '{FileName}' for skill '{SkillName}' has invalid or missing required fields (name and description are required), skipping.",
                    candidateFileName, skillDirName);
                continue;
            }

            return (candidateFileName, content, name, description);
        }

        return (null, null, null, null);
    }

    private static string GetMimeType(string fileName)
    {
        if (fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return "text/markdown";
        }

        if (fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
        {
            return "text/yaml";
        }

        return "text/plain";
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

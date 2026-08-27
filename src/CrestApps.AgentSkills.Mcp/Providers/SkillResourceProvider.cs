using CrestApps.AgentSkills.Mcp.Abstractions;
using CrestApps.AgentSkills.Mcp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CrestApps.AgentSkills.Mcp.Providers;

/// <summary>
/// Loads skill resource files via <see cref="IAgentSkillFilesStore"/>
/// and produces <see cref="McpServerResource"/> instances for MCP registration.
/// Each skill file (SKILL.md, SKILL.yaml, SKILL.yml) and <c>references/*.md</c>
/// file becomes a resource.
/// Registered as a singleton — results are lazily loaded and cached.
/// </summary>
public sealed class SkillResourceProvider : IMcpResourceProvider
{
    private readonly IAgentSkillFilesStore _fileStore;
    private readonly ILogger<SkillResourceProvider> _logger;
    private readonly AgentSkillOptions _options;
    private IReadOnlyList<McpServerResource>? _resources;

    public SkillResourceProvider(
        IAgentSkillFilesStore fileStore,
        ILogger<SkillResourceProvider> logger,
        IOptions<AgentSkillOptions> options)
    {
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        _fileStore = fileStore;
        _logger = logger;
        _options = options.Value;
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
            var parsed = await TryReadAndParseSkillFileAsync(skillDirName);

            if (parsed is { } skill)
            {
                var channel = McpChannelResolver.Resolve(skill.Mcp, _options.DefaultMcpChannel, skill.Name, _logger);

                if (channel is McpChannel.Resource or McpChannel.Both)
                {
                    // Serve the front-matter-stripped body (matching the prompt), so internal
                    // YAML keys (license, metadata, version, ...) are never leaked to clients.
                    var body = skill.Body;
                    var mimeType = GetMimeType(skill.FileName);
                    var resource = McpServerResource.Create(
                        () => body,
                        new McpServerResourceCreateOptions
                        {
                            Name = $"{skill.Name}/{skill.FileName}",
                            Description = skill.Description,
                            UriTemplate = $"skills://{skill.Name}/{skill.FileName}",
                            MimeType = mimeType,
                        });
                    resources.Add(resource);
                }
                else if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Skill '{SkillName}' is configured for the '{Channel}' channel; not exposing SKILL as a resource.",
                        skill.Name, channel);
                }
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("No valid skill file found for skill '{SkillName}'.", skillDirName);
                }
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
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Reference file '{FileName}' for skill '{SkillName}' is empty, skipping.", fileName, skillDirName);
                    }

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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Loaded {Count} MCP resources from agent skills.", resources.Count);
        }
        _resources = resources;

        return _resources;
    }

    private async Task<ParsedSkill?> TryReadAndParseSkillFileAsync(string skillDirName)
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

            if (!SkillFileParser.TryParse(candidateFileName, content, out var name, out var description, out var body, out var mcp))
            {
                _logger.LogWarning(
                    "Skill file '{FileName}' for skill '{SkillName}' has invalid or missing required fields (name and description are required), skipping.",
                    candidateFileName, skillDirName);
                continue;
            }

            return new ParsedSkill(candidateFileName, name, description, body, mcp);
        }

        return null;
    }

    private readonly record struct ParsedSkill(string FileName, string Name, string Description, string Body, string? Mcp);

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

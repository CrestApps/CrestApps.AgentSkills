using CrestApps.OrchardCore.AgentSkills.Mcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Providers;

/// <summary>
/// Loads Orchard Core skill prompt files via <see cref="IMcpResourceFileStore"/>
/// and produces <see cref="McpServerPrompt"/> instances for MCP registration.
/// Each <c>prompts.md</c> file found in a skill directory becomes a prompt.
/// Registered as a singleton — results are lazily loaded and cached.
/// </summary>
public sealed class FileSystemSkillPromptProvider
{
    private readonly IMcpResourceFileStore _fileStore;
    private readonly ILogger<FileSystemSkillPromptProvider> _logger;
    private IReadOnlyList<McpServerPrompt>? _prompts;

    public FileSystemSkillPromptProvider(
        IMcpResourceFileStore fileStore,
        ILogger<FileSystemSkillPromptProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(logger);
        _fileStore = fileStore;
        _logger = logger;
    }

    /// <summary>
    /// Discovers all <c>prompts.md</c> files under the skills directory and
    /// creates MCP prompt instances from their contents.
    /// Results are lazily loaded and cached after the first call.
    /// </summary>
    public async Task<IReadOnlyList<McpServerPrompt>> GetPromptsAsync()
    {
        if (_prompts is not null)
        {
            return _prompts;
        }

        var prompts = new List<McpServerPrompt>();

        await foreach (var skillDir in _fileStore.GetDirectoryContentAsync(null, includeSubDirectories: false))
        {
            if (!skillDir.IsDirectory)
            {
                continue;
            }

            var skillName = skillDir.Name;
            var promptPath = NormalizePath($"{skillName}/prompts.md");
            var promptInfo = await _fileStore.GetFileInfoAsync(promptPath);

            if (promptInfo is null)
            {
                _logger.LogDebug("No prompts.md found for skill '{SkillName}', skipping.", skillName);
                continue;
            }

            string content;

            try
            {
                await using var stream = await _fileStore.GetFileStreamAsync(promptPath);
                using var reader = new StreamReader(stream);
                content = await reader.ReadToEndAsync();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to read prompts.md for skill '{SkillName}'.", skillName);
                continue;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("prompts.md for skill '{SkillName}' is empty, skipping.", skillName);
                continue;
            }

            var prompt = McpServerPrompt.Create(
                () => content,
                new McpServerPromptCreateOptions
                {
                    Name = skillName,
                    Description = $"Orchard Core prompt template for {skillName}",
                });

            prompts.Add(prompt);
        }

        _logger.LogInformation("Loaded {Count} MCP prompts from agent skills.", prompts.Count);
        _prompts = prompts;

        return _prompts;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

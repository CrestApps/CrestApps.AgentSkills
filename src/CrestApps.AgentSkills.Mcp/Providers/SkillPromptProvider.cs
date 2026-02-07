using CrestApps.AgentSkills.Mcp.Abstractions;
using CrestApps.AgentSkills.Mcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CrestApps.AgentSkills.Mcp.Providers;

/// <summary>
/// Loads skill files via <see cref="ISkillFileStore"/>
/// and produces <see cref="McpServerPrompt"/> instances for MCP registration.
/// Each skill file (SKILL.md, SKILL.yaml, SKILL.yml) found in a skill directory
/// becomes a prompt (using the body content).
/// Registered as a singleton — results are lazily loaded and cached.
/// </summary>
public sealed class SkillPromptProvider
{
    private readonly ISkillFileStore _fileStore;
    private readonly ILogger<SkillPromptProvider> _logger;
    private IReadOnlyList<McpServerPrompt>? _prompts;

    public SkillPromptProvider(
        ISkillFileStore fileStore,
        ILogger<SkillPromptProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(logger);
        _fileStore = fileStore;
        _logger = logger;
    }

    /// <summary>
    /// Discovers all skill files under the skills directory and
    /// creates MCP prompt instances from the body content.
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

            var skillDirName = skillDir.Name;
            var (skillFileName, content) = await TryReadSkillFileAsync(skillDirName);

            if (skillFileName is null || content is null)
            {
                _logger.LogDebug("No skill file found for skill '{SkillName}', skipping.", skillDirName);
                continue;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Skill file for skill '{SkillName}' is empty, skipping.", skillDirName);
                continue;
            }

            if (!SkillFileParser.TryParse(skillFileName, content, out var name, out var description, out var body))
            {
                _logger.LogWarning(
                    "Skill file '{FileName}' for skill '{SkillName}' has invalid or missing required fields (name and description are required), skipping.",
                    skillFileName, skillDirName);
                continue;
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                _logger.LogWarning("Skill file for skill '{SkillName}' has no body content, skipping.", skillDirName);
                continue;
            }

            var prompt = McpServerPrompt.Create(
                () => body,
                new McpServerPromptCreateOptions
                {
                    Name = name,
                    Description = description,
                });

            prompts.Add(prompt);
        }

        _logger.LogInformation("Loaded {Count} MCP prompts from agent skills.", prompts.Count);
        _prompts = prompts;

        return _prompts;
    }

    private async Task<(string? FileName, string? Content)> TryReadSkillFileAsync(string skillDirName)
    {
        foreach (var candidateFileName in SkillFileParser.SupportedSkillFileNames)
        {
            var skillPath = NormalizePath($"{skillDirName}/{candidateFileName}");
            var skillInfo = await _fileStore.GetFileInfoAsync(skillPath);

            if (skillInfo is null)
            {
                continue;
            }

            try
            {
                await using var stream = await _fileStore.GetFileStreamAsync(skillPath);
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();
                return (candidateFileName, content);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to read '{FileName}' for skill '{SkillName}'.", candidateFileName, skillDirName);
            }
        }

        return (null, null);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

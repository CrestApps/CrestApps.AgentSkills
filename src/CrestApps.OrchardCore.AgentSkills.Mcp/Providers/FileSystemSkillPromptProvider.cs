using ModelContextProtocol.Server;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Providers;

/// <summary>
/// Loads Orchard Core skill prompt files from the filesystem and
/// produces <see cref="McpServerPrompt"/> instances for MCP registration.
/// Each <c>prompts.md</c> file found in a skill directory becomes a prompt.
/// </summary>
public sealed class FileSystemSkillPromptProvider
{
    private readonly string _skillsPath;

    public FileSystemSkillPromptProvider(string skillsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillsPath);
        _skillsPath = skillsPath;
    }

    /// <summary>
    /// Discovers all <c>prompts.md</c> files under the skills directory and
    /// creates MCP prompt instances from their contents.
    /// </summary>
    /// <returns>A list of <see cref="McpServerPrompt"/> instances.</returns>
    public IReadOnlyList<McpServerPrompt> GetPrompts()
    {
        if (!Directory.Exists(_skillsPath))
        {
            return [];
        }

        var prompts = new List<McpServerPrompt>();

        foreach (var skillDir in Directory.EnumerateDirectories(_skillsPath))
        {
            var skillName = Path.GetFileName(skillDir);
            var promptsFile = Path.Combine(skillDir, "prompts.md");

            if (!File.Exists(promptsFile))
            {
                continue;
            }

            var content = File.ReadAllText(promptsFile);
            var prompt = McpServerPrompt.Create(
                () => content,
                new McpServerPromptCreateOptions
                {
                    Name = skillName,
                    Description = $"Orchard Core prompt template for {skillName}",
                });

            prompts.Add(prompt);
        }

        return prompts;
    }
}

using ModelContextProtocol.Server;

namespace CrestApps.AgentSkills.Mcp.Abstractions;

/// <summary>
/// Abstraction for loading MCP prompts from skill files.
/// </summary>
public interface IPromptProvider
{
    /// <summary>
    /// Discovers skill prompts and returns MCP prompt instances.
    /// </summary>
    Task<IReadOnlyList<McpServerPrompt>> GetPromptsAsync();
}

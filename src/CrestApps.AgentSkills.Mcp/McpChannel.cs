namespace CrestApps.AgentSkills.Mcp;

/// <summary>
/// Determines which MCP surface(s) a skill is exposed through.
/// </summary>
public enum McpChannel
{
    /// <summary>
    /// Expose the skill as an MCP prompt only.
    /// </summary>
    Prompt,

    /// <summary>
    /// Expose the skill as an MCP resource only.
    /// </summary>
    Resource,

    /// <summary>
    /// Expose the skill as both an MCP prompt and an MCP resource.
    /// </summary>
    Both,
}

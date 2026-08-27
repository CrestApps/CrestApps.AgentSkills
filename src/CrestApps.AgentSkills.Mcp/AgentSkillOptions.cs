namespace CrestApps.AgentSkills.Mcp;

/// <summary>
/// Configuration options for the Agent Skills MCP engine.
/// </summary>
public sealed class AgentSkillOptions
{
    /// <summary>
    /// Gets or sets the path to the skills directory.
    /// When <c>null</c>, the default path (<c>&lt;AppContext.BaseDirectory&gt;/.agents/skills</c>) is used.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the default MCP channel used for skills that do not declare an
    /// <c>mcp</c> front-matter key (or declare an unrecognized value).
    /// Defaults to <see cref="McpChannel.Both"/>, which preserves the original behavior of
    /// exposing every skill as both a prompt and a resource.
    /// </summary>
    public McpChannel DefaultMcpChannel { get; set; } = McpChannel.Both;
}

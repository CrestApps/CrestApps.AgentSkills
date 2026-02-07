namespace CrestApps.OrchardCore.AgentSkills.Mcp;

/// <summary>
/// Configuration options for Orchard Core agent skills in MCP.
/// </summary>
public sealed class OrchardCoreSkillOptions
{
    /// <summary>
    /// Gets or sets the path to the skills directory.
    /// When <c>null</c>, the default path (<c>&lt;AppContext.BaseDirectory&gt;/.agents/skills</c>) is used.
    /// </summary>
    public string? Path { get; set; }
}

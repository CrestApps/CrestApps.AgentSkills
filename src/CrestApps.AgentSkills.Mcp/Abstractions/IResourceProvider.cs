using ModelContextProtocol.Server;

namespace CrestApps.AgentSkills.Mcp.Abstractions;

/// <summary>
/// Abstraction for loading MCP resources from skill files.
/// </summary>
public interface IResourceProvider
{
    /// <summary>
    /// Discovers skill resources and returns MCP resource instances.
    /// </summary>
    Task<IReadOnlyList<McpServerResource>> GetResourcesAsync();
}

using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Services;

/// <summary>
/// A marker interface for the file store that provides access to MCP skill files.
/// Extends OrchardCore's <see cref="IFileStore"/> to enable DI-based file access
/// for MCP providers.
/// </summary>
public interface IMcpResourceFileStore : IFileStore
{
}

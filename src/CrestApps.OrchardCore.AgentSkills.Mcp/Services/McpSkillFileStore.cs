using OrchardCore.FileStorage.FileSystem;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Services;

/// <summary>
/// An implementation of <see cref="IMcpResourceFileStore"/> that reads MCP skill files
/// from the local filesystem using OrchardCore's <see cref="FileSystemStore"/>.
/// </summary>
public sealed class McpSkillFileStore : FileSystemStore, IMcpResourceFileStore
{
    /// <summary>
    /// Initializes a new instance of <see cref="McpSkillFileStore"/> rooted at the given path.
    /// </summary>
    /// <param name="fileSystemPath">The absolute path to the skills directory.</param>
    public McpSkillFileStore(string fileSystemPath)
        : base(fileSystemPath)
    {
    }
}

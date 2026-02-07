using CrestApps.AgentSkills.Mcp.Models;

namespace CrestApps.AgentSkills.Mcp.Abstractions;

/// <summary>
/// Abstraction for accessing skill files and directories.
/// Provides framework-agnostic file access for MCP skill discovery.
/// </summary>
public interface ISkillFileStore
{
    /// <summary>
    /// Enumerates the contents of a directory within the skill file store.
    /// </summary>
    /// <param name="subPath">
    /// The relative path of the directory to enumerate, or <c>null</c> for the root.
    /// </param>
    /// <param name="includeSubDirectories">
    /// <c>true</c> to include subdirectories in the enumeration; <c>false</c> for immediate children only.
    /// </param>
    /// <returns>An async enumerable of file and directory entries.</returns>
    IAsyncEnumerable<SkillFileEntry> GetDirectoryContentAsync(string? subPath, bool includeSubDirectories = false);

    /// <summary>
    /// Gets information about a file at the specified relative path.
    /// </summary>
    /// <param name="subPath">The relative path of the file.</param>
    /// <returns>A <see cref="SkillFileEntry"/> if the file exists; otherwise <c>null</c>.</returns>
    Task<SkillFileEntry?> GetFileInfoAsync(string subPath);

    /// <summary>
    /// Opens a read-only stream for the file at the specified relative path.
    /// </summary>
    /// <param name="subPath">The relative path of the file.</param>
    /// <returns>A <see cref="Stream"/> for reading the file contents.</returns>
    Task<Stream> GetFileStreamAsync(string subPath);

    /// <summary>
    /// Gets information about a directory at the specified relative path.
    /// </summary>
    /// <param name="subPath">The relative path of the directory.</param>
    /// <returns>A <see cref="SkillFileEntry"/> if the directory exists; otherwise <c>null</c>.</returns>
    Task<SkillFileEntry?> GetDirectoryInfoAsync(string subPath);
}

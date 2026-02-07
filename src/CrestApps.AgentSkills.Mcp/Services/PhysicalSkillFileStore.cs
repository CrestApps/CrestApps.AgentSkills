using CrestApps.AgentSkills.Mcp.Abstractions;
using CrestApps.AgentSkills.Mcp.Models;

namespace CrestApps.AgentSkills.Mcp.Services;

/// <summary>
/// A file store implementation that reads skill files from the physical filesystem
/// using <see cref="System.IO"/> APIs. Requires no external framework dependencies.
/// </summary>
public sealed class PhysicalSkillFileStore : ISkillFileStore
{
    private readonly string _basePath;

    /// <summary>
    /// Initializes a new instance of <see cref="PhysicalSkillFileStore"/>
    /// rooted at the given directory path.
    /// </summary>
    /// <param name="basePath">The absolute path to the skills directory.</param>
    public PhysicalSkillFileStore(string basePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        _basePath = basePath;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SkillFileEntry> GetDirectoryContentAsync(
        string? subPath,
        bool includeSubDirectories = false)
    {
        var fullPath = GetFullPath(subPath);

        if (!Directory.Exists(fullPath))
        {
            yield break;
        }

        var option = includeSubDirectories
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        await Task.CompletedTask;

        foreach (var directory in Directory.EnumerateDirectories(fullPath, "*", option))
        {
            var name = Path.GetFileName(directory);
            var relativePath = GetRelativePath(directory);
            yield return new SkillFileEntry
            {
                Name = name,
                Path = relativePath,
                IsDirectory = true,
            };
        }

        foreach (var file in Directory.EnumerateFiles(fullPath, "*", option))
        {
            var name = Path.GetFileName(file);
            var relativePath = GetRelativePath(file);
            yield return new SkillFileEntry
            {
                Name = name,
                Path = relativePath,
                IsDirectory = false,
            };
        }
    }

    /// <inheritdoc />
    public Task<SkillFileEntry?> GetFileInfoAsync(string subPath)
    {
        var fullPath = GetFullPath(subPath);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<SkillFileEntry?>(null);
        }

        var entry = new SkillFileEntry
        {
            Name = Path.GetFileName(fullPath),
            Path = NormalizePath(subPath),
            IsDirectory = false,
        };

        return Task.FromResult<SkillFileEntry?>(entry);
    }

    /// <inheritdoc />
    public Task<Stream> GetFileStreamAsync(string subPath)
    {
        var fullPath = GetFullPath(subPath);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task<SkillFileEntry?> GetDirectoryInfoAsync(string subPath)
    {
        var fullPath = GetFullPath(subPath);

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult<SkillFileEntry?>(null);
        }

        var entry = new SkillFileEntry
        {
            Name = Path.GetFileName(fullPath),
            Path = NormalizePath(subPath),
            IsDirectory = true,
        };

        return Task.FromResult<SkillFileEntry?>(entry);
    }

    private string GetFullPath(string? subPath)
    {
        if (string.IsNullOrEmpty(subPath))
        {
            return _basePath;
        }

        return Path.Combine(_basePath, subPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private string GetRelativePath(string fullPath)
    {
        var relative = Path.GetRelativePath(_basePath, fullPath);
        return NormalizePath(relative);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

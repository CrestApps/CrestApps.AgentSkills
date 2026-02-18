using CrestApps.AgentSkills.Mcp.Services;
using Xunit;

namespace CrestApps.AgentSkills.Mcp.Tests;

public sealed class PhysicalSkillFileStoreTests : IDisposable
{
    private readonly string _tempDir;

    public PhysicalSkillFileStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"skill-store-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetFileInfoAsync_ExistingFile_ReturnsEntry()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.txt"), "content", TestContext.Current.CancellationToken);

        var store = new DefaultAgentSkillFilesStore(_tempDir);
        var info = await store.GetFileInfoAsync("test.txt");

        Assert.NotNull(info);
        Assert.Equal("test.txt", info.Name);
        Assert.False(info.IsDirectory);
    }

    [Fact]
    public async Task GetFileInfoAsync_NonExistentFile_ReturnsNull()
    {
        var store = new DefaultAgentSkillFilesStore(_tempDir);
        var info = await store.GetFileInfoAsync("missing.txt");

        Assert.Null(info);
    }

    [Fact]
    public async Task GetDirectoryInfoAsync_ExistingDirectory_ReturnsEntry()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "subdir"));

        var store = new DefaultAgentSkillFilesStore(_tempDir);
        var info = await store.GetDirectoryInfoAsync("subdir");

        Assert.NotNull(info);
        Assert.Equal("subdir", info.Name);
        Assert.True(info.IsDirectory);
    }

    [Fact]
    public async Task GetDirectoryInfoAsync_NonExistentDirectory_ReturnsNull()
    {
        var store = new DefaultAgentSkillFilesStore(_tempDir);
        var info = await store.GetDirectoryInfoAsync("missing");

        Assert.Null(info);
    }

    [Fact]
    public async Task GetFileStreamAsync_ExistingFile_ReturnsReadableStream()
    {
        var expected = "Hello, World!";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "read.txt"), expected, TestContext.Current.CancellationToken);

        var store = new DefaultAgentSkillFilesStore(_tempDir);
        await using var stream = await store.GetFileStreamAsync("read.txt");
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, content);
    }

    [Fact]
    public async Task GetDirectoryContentAsync_EnumeratesDirectoriesAndFiles()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "dir1"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "dir2"));
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "file1.txt"), "content", TestContext.Current.CancellationToken);

        var store = new DefaultAgentSkillFilesStore(_tempDir);
        var entries = new List<Models.SkillFileEntry>();
        await foreach (var entry in store.GetDirectoryContentAsync(null))
        {
            entries.Add(entry);
        }

        Assert.Equal(3, entries.Count);
        Assert.Equal(2, entries.Count(e => e.IsDirectory));
        Assert.Single(entries, e => !e.IsDirectory);
    }

    [Fact]
    public async Task GetDirectoryContentAsync_EmptyDirectory_ReturnsEmpty()
    {
        var store = new DefaultAgentSkillFilesStore(_tempDir);
        var entries = new List<Models.SkillFileEntry>();
        await foreach (var entry in store.GetDirectoryContentAsync(null))
        {
            entries.Add(entry);
        }

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetDirectoryContentAsync_NonExistentPath_ReturnsEmpty()
    {
        var store = new DefaultAgentSkillFilesStore(_tempDir);
        var entries = new List<Models.SkillFileEntry>();
        await foreach (var entry in store.GetDirectoryContentAsync("nonexistent"))
        {
            entries.Add(entry);
        }

        Assert.Empty(entries);
    }

    [Fact]
    public void Constructor_ThrowsOnNullOrWhitespacePath()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultAgentSkillFilesStore(null!));
        Assert.Throws<ArgumentException>(() => new DefaultAgentSkillFilesStore(""));
        Assert.Throws<ArgumentException>(() => new DefaultAgentSkillFilesStore("   "));
    }
}

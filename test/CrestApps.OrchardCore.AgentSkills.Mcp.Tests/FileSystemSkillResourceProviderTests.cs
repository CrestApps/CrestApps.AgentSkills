using CrestApps.OrchardCore.AgentSkills.Mcp.Providers;
using CrestApps.OrchardCore.AgentSkills.Mcp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Tests;

public sealed class FileSystemSkillResourceProviderTests : IDisposable
{
    private readonly string _tempDir;

    public FileSystemSkillResourceProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agent-skills-tests-{Guid.NewGuid():N}");
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
    public async Task GetResourcesAsync_ReturnsSkillMdResources()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "test-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: test-skill\ndescription: A test skill.\n---\n# Test Skill");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var resources = await provider.GetResourcesAsync();

        // Assert
        Assert.Single(resources);
    }

    [Fact]
    public async Task GetResourcesAsync_ReturnsReferenceMdResources()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "ref-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: ref-skill\ndescription: Ref skill.\n---\n# Ref Skill");

        var refsDir = Path.Combine(skillDir, "references");
        Directory.CreateDirectory(refsDir);
        await File.WriteAllTextAsync(Path.Combine(refsDir, "example1.md"), "# Example 1");
        await File.WriteAllTextAsync(Path.Combine(refsDir, "example2.md"), "# Example 2");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var resources = await provider.GetResourcesAsync();

        // Assert: 1 SKILL.md + 2 reference .md files
        Assert.Equal(3, resources.Count);
    }

    [Fact]
    public async Task GetResourcesAsync_IgnoresNonMdReferenceFiles()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "filter-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: filter-skill\ndescription: Filter skill.\n---\n# Filter Skill");

        var refsDir = Path.Combine(skillDir, "references");
        Directory.CreateDirectory(refsDir);
        await File.WriteAllTextAsync(Path.Combine(refsDir, "example.md"), "# Valid");
        await File.WriteAllTextAsync(Path.Combine(refsDir, "data.json"), "{}");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var resources = await provider.GetResourcesAsync();

        // Assert: 1 SKILL.md + 1 .md reference (json ignored)
        Assert.Equal(2, resources.Count);
    }

    [Fact]
    public async Task GetResourcesAsync_SkipsSkillMdWithInvalidFrontMatter()
    {
        // Arrange: SKILL.md without proper front-matter
        var skillDir = Path.Combine(_tempDir, "invalid-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "# No front-matter\nJust markdown.");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var resources = await provider.GetResourcesAsync();

        // Assert
        Assert.Empty(resources);
    }

    [Fact]
    public async Task GetResourcesAsync_CachesResultsOnSubsequentCalls()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "cached-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: cached-skill\ndescription: Cached.\n---\n# Cached");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var first = await provider.GetResourcesAsync();
        var second = await provider.GetResourcesAsync();

        // Assert: same reference returned (cached)
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetResourcesAsync_ReturnsEmptyForEmptyDirectory()
    {
        // Arrange: empty directory
        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var resources = await provider.GetResourcesAsync();

        // Assert
        Assert.Empty(resources);
    }

    [Fact]
    public async Task GetResourcesAsync_SkipsEmptySkillMdFiles()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "empty-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), "   ");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var resources = await provider.GetResourcesAsync();

        // Assert: empty file should be skipped
        Assert.Empty(resources);
    }
}

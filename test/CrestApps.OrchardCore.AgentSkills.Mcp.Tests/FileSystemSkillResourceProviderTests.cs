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
    public async Task GetResourcesAsync_ReturnsSkillYamlResources()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "test-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "skill.yaml"), "name: test-skill\nversion: 1.0");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var resources = await provider.GetResourcesAsync();

        // Assert
        Assert.Single(resources);
    }

    [Fact]
    public async Task GetResourcesAsync_ReturnsExampleMdResources()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "example-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "skill.yaml"), "name: example-skill");

        var examplesDir = Path.Combine(skillDir, "examples");
        Directory.CreateDirectory(examplesDir);
        await File.WriteAllTextAsync(Path.Combine(examplesDir, "example1.md"), "# Example 1");
        await File.WriteAllTextAsync(Path.Combine(examplesDir, "example2.md"), "# Example 2");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var resources = await provider.GetResourcesAsync();

        // Assert: 1 skill.yaml + 2 example .md files
        Assert.Equal(3, resources.Count);
    }

    [Fact]
    public async Task GetResourcesAsync_IgnoresNonMdExampleFiles()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "filter-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "skill.yaml"), "name: filter-skill");

        var examplesDir = Path.Combine(skillDir, "examples");
        Directory.CreateDirectory(examplesDir);
        await File.WriteAllTextAsync(Path.Combine(examplesDir, "example.md"), "# Valid");
        await File.WriteAllTextAsync(Path.Combine(examplesDir, "data.json"), "{}");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var resources = await provider.GetResourcesAsync();

        // Assert: 1 skill.yaml + 1 .md example (json ignored)
        Assert.Equal(2, resources.Count);
    }

    [Fact]
    public async Task GetResourcesAsync_CachesResultsOnSubsequentCalls()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "cached-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "skill.yaml"), "name: cached");

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
    public async Task GetResourcesAsync_SkipsEmptyYamlFiles()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "empty-yaml-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "skill.yaml"), "   ");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillResourceProvider(
            fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

        // Act
        var resources = await provider.GetResourcesAsync();

        // Assert: empty yaml should be skipped
        Assert.Empty(resources);
    }
}

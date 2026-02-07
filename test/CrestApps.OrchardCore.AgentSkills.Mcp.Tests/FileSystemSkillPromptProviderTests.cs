using CrestApps.OrchardCore.AgentSkills.Mcp.Providers;
using CrestApps.OrchardCore.AgentSkills.Mcp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Tests;

public sealed class FileSystemSkillPromptProviderTests : IDisposable
{
    private readonly string _tempDir;

    public FileSystemSkillPromptProviderTests()
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
    public async Task GetPromptsAsync_ReturnsPromptsFromSkillDirectories()
    {
        // Arrange: create a skill directory with prompts.md
        var skillDir = Path.Combine(_tempDir, "test-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "prompts.md"), "# Test Prompt\nThis is a test.");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillPromptProvider(
            fileStore, NullLogger<FileSystemSkillPromptProvider>.Instance);

        // Act
        var prompts = await provider.GetPromptsAsync();

        // Assert
        Assert.Single(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_SkipsDirectoriesWithoutPromptsFile()
    {
        // Arrange: create a skill directory without prompts.md
        var skillDir = Path.Combine(_tempDir, "no-prompt-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "skill.yaml"), "name: test");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillPromptProvider(
            fileStore, NullLogger<FileSystemSkillPromptProvider>.Instance);

        // Act
        var prompts = await provider.GetPromptsAsync();

        // Assert
        Assert.Empty(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_CachesResultsOnSubsequentCalls()
    {
        // Arrange
        var skillDir = Path.Combine(_tempDir, "cached-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "prompts.md"), "# Cached");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillPromptProvider(
            fileStore, NullLogger<FileSystemSkillPromptProvider>.Instance);

        // Act
        var first = await provider.GetPromptsAsync();
        var second = await provider.GetPromptsAsync();

        // Assert: same reference returned (cached)
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetPromptsAsync_ReturnsEmptyForEmptyDirectory()
    {
        // Arrange: empty directory, no skill subdirectories
        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillPromptProvider(
            fileStore, NullLogger<FileSystemSkillPromptProvider>.Instance);

        // Act
        var prompts = await provider.GetPromptsAsync();

        // Assert
        Assert.Empty(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_SkipsEmptyPromptFiles()
    {
        // Arrange: create a skill directory with an empty prompts.md
        var skillDir = Path.Combine(_tempDir, "empty-prompt-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "prompts.md"), "   ");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillPromptProvider(
            fileStore, NullLogger<FileSystemSkillPromptProvider>.Instance);

        // Act
        var prompts = await provider.GetPromptsAsync();

        // Assert
        Assert.Empty(prompts);
    }
}

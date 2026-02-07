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
        // Arrange: create a skill directory with a valid SKILL.md
        var skillDir = Path.Combine(_tempDir, "test-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: test-skill\ndescription: A test skill.\n---\n# Test Prompt\nThis is a test.");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillPromptProvider(
            fileStore, NullLogger<FileSystemSkillPromptProvider>.Instance);

        // Act
        var prompts = await provider.GetPromptsAsync();

        // Assert
        Assert.Single(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_SkipsDirectoriesWithoutSkillMd()
    {
        // Arrange: create a skill directory without SKILL.md
        var skillDir = Path.Combine(_tempDir, "no-skill-md");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "README.md"), "# Not a skill");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillPromptProvider(
            fileStore, NullLogger<FileSystemSkillPromptProvider>.Instance);

        // Act
        var prompts = await provider.GetPromptsAsync();

        // Assert
        Assert.Empty(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_SkipsSkillMdWithInvalidFrontMatter()
    {
        // Arrange: SKILL.md without proper front-matter
        var skillDir = Path.Combine(_tempDir, "invalid-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "# No front-matter\nJust regular markdown.");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillPromptProvider(
            fileStore, NullLogger<FileSystemSkillPromptProvider>.Instance);

        // Act
        var prompts = await provider.GetPromptsAsync();

        // Assert
        Assert.Empty(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_SkipsSkillMdWithMissingRequiredFields()
    {
        // Arrange: SKILL.md with front-matter but missing description
        var skillDir = Path.Combine(_tempDir, "incomplete-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: incomplete-skill\n---\n# Some content");

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
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: cached-skill\ndescription: Cached.\n---\n# Cached");

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
    public async Task GetPromptsAsync_SkipsEmptySkillMdFiles()
    {
        // Arrange: create a skill directory with an empty SKILL.md
        var skillDir = Path.Combine(_tempDir, "empty-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), "   ");

        var fileStore = new McpSkillFileStore(_tempDir);
        var provider = new FileSystemSkillPromptProvider(
            fileStore, NullLogger<FileSystemSkillPromptProvider>.Instance);

        // Act
        var prompts = await provider.GetPromptsAsync();

        // Assert
        Assert.Empty(prompts);
    }
}

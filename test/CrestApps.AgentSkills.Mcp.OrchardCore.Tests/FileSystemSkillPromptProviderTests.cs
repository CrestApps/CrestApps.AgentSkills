using CrestApps.AgentSkills.Mcp.Providers;
using CrestApps.AgentSkills.Mcp.Services;
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
            "---\nname: test-skill\ndescription: A test skill.\n---\n# Test Prompt\nThis is a test.", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

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
        await File.WriteAllTextAsync(Path.Combine(skillDir, "README.md"), "# Not a skill", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

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
            "# No front-matter\nJust regular markdown.", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

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
            "---\nname: incomplete-skill\n---\n# Some content", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

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
            "---\nname: cached-skill\ndescription: Cached.\n---\n# Cached", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

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
        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

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
        await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), "   ", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        // Act
        var prompts = await provider.GetPromptsAsync();

        // Assert
        Assert.Empty(prompts);
    }
}

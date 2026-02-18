using CrestApps.AgentSkills.Mcp.Providers;
using CrestApps.AgentSkills.Mcp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrestApps.AgentSkills.Mcp.Tests;

public sealed class SkillPromptProviderTests : IDisposable
{
    private readonly string _tempDir;

    public SkillPromptProviderTests()
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
    public async Task GetPromptsAsync_ReturnsPromptsFromMarkdownSkillFiles()
    {
        var skillDir = Path.Combine(_tempDir, "test-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: test-skill\ndescription: A test skill.\n---\n# Test Prompt\nThis is a test.", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var prompts = await provider.GetPromptsAsync();

        Assert.Single(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_ReturnsPromptsFromYamlSkillFiles()
    {
        var skillDir = Path.Combine(_tempDir, "yaml-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.yaml"),
            "name: yaml-skill\ndescription: A YAML skill.\nbody: |\n  # YAML Prompt\n  This is a yaml test.", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var prompts = await provider.GetPromptsAsync();

        Assert.Single(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_ReturnsPromptsFromYmlSkillFiles()
    {
        var skillDir = Path.Combine(_tempDir, "yml-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.yml"),
            "name: yml-skill\ndescription: A YML skill.\nbody: |\n  # YML Prompt\n  This is a yml test.", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var prompts = await provider.GetPromptsAsync();

        Assert.Single(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_SkipsDirectoriesWithoutSkillFile()
    {
        var skillDir = Path.Combine(_tempDir, "no-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "README.md"), "# Not a skill", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var prompts = await provider.GetPromptsAsync();

        Assert.Empty(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_SkipsSkillWithInvalidFrontMatter()
    {
        var skillDir = Path.Combine(_tempDir, "invalid-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "# No front-matter\nJust regular markdown.", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var prompts = await provider.GetPromptsAsync();

        Assert.Empty(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_SkipsSkillWithMissingRequiredFields()
    {
        var skillDir = Path.Combine(_tempDir, "incomplete-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.yaml"),
            "name: incomplete-skill", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var prompts = await provider.GetPromptsAsync();

        Assert.Empty(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_SkipsYamlSkillWithNoBody()
    {
        var skillDir = Path.Combine(_tempDir, "no-body-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.yaml"),
            "name: no-body-skill\ndescription: A skill without body.", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var prompts = await provider.GetPromptsAsync();

        Assert.Empty(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_CachesResultsOnSubsequentCalls()
    {
        var skillDir = Path.Combine(_tempDir, "cached-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: cached-skill\ndescription: Cached.\n---\n# Cached", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var first = await provider.GetPromptsAsync();
        var second = await provider.GetPromptsAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetPromptsAsync_ReturnsEmptyForEmptyDirectory()
    {
        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var prompts = await provider.GetPromptsAsync();

        Assert.Empty(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_SkipsEmptySkillFiles()
    {
        var skillDir = Path.Combine(_tempDir, "empty-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), "   ", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var prompts = await provider.GetPromptsAsync();

        Assert.Empty(prompts);
    }

    [Fact]
    public async Task GetPromptsAsync_PrefersMarkdownOverYaml()
    {
        // Both SKILL.md and SKILL.yaml exist — SKILL.md should be preferred
        var skillDir = Path.Combine(_tempDir, "both-formats");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: md-skill\ndescription: From Markdown.\n---\n# MD Body", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.yaml"),
            "name: yaml-skill\ndescription: From YAML.\nbody: YAML Body", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillPromptProvider(
            fileStore, NullLogger<SkillPromptProvider>.Instance);

        var prompts = await provider.GetPromptsAsync();

        Assert.Single(prompts);
    }
}

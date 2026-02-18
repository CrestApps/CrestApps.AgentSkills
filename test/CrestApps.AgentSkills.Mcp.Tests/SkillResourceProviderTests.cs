using CrestApps.AgentSkills.Mcp.Providers;
using CrestApps.AgentSkills.Mcp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrestApps.AgentSkills.Mcp.Tests;

public sealed class SkillResourceProviderTests : IDisposable
{
    private readonly string _tempDir;

    public SkillResourceProviderTests()
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
    public async Task GetResourcesAsync_ReturnsMarkdownSkillResources()
    {
        var skillDir = Path.Combine(_tempDir, "test-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: test-skill\ndescription: A test skill.\n---\n# Test Skill", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillResourceProvider(
            fileStore, NullLogger<SkillResourceProvider>.Instance);

        var resources = await provider.GetResourcesAsync();

        Assert.Single(resources);
    }

    [Fact]
    public async Task GetResourcesAsync_ReturnsYamlSkillResources()
    {
        var skillDir = Path.Combine(_tempDir, "yaml-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.yaml"),
            "name: yaml-skill\ndescription: A YAML skill.\nbody: Some body", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillResourceProvider(
            fileStore, NullLogger<SkillResourceProvider>.Instance);

        var resources = await provider.GetResourcesAsync();

        Assert.Single(resources);
    }

    [Fact]
    public async Task GetResourcesAsync_ReturnsYmlSkillResources()
    {
        var skillDir = Path.Combine(_tempDir, "yml-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.yml"),
            "name: yml-skill\ndescription: A YML skill.\nbody: Some body", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillResourceProvider(
            fileStore, NullLogger<SkillResourceProvider>.Instance);

        var resources = await provider.GetResourcesAsync();

        Assert.Single(resources);
    }

    [Fact]
    public async Task GetResourcesAsync_ReturnsReferenceMdResources()
    {
        var skillDir = Path.Combine(_tempDir, "ref-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: ref-skill\ndescription: Ref skill.\n---\n# Ref Skill", TestContext.Current.CancellationToken);

        var refsDir = Path.Combine(skillDir, "references");
        Directory.CreateDirectory(refsDir);
        await File.WriteAllTextAsync(Path.Combine(refsDir, "example1.md"), "# Example 1", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(refsDir, "example2.md"), "# Example 2", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillResourceProvider(
            fileStore, NullLogger<SkillResourceProvider>.Instance);

        var resources = await provider.GetResourcesAsync();

        // 1 SKILL.md + 2 reference .md files
        Assert.Equal(3, resources.Count);
    }

    [Fact]
    public async Task GetResourcesAsync_IgnoresNonMdReferenceFiles()
    {
        var skillDir = Path.Combine(_tempDir, "filter-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: filter-skill\ndescription: Filter skill.\n---\n# Filter Skill", TestContext.Current.CancellationToken);

        var refsDir = Path.Combine(skillDir, "references");
        Directory.CreateDirectory(refsDir);
        await File.WriteAllTextAsync(Path.Combine(refsDir, "example.md"), "# Valid", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(refsDir, "data.json"), "{}", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillResourceProvider(
            fileStore, NullLogger<SkillResourceProvider>.Instance);

        var resources = await provider.GetResourcesAsync();

        // 1 SKILL.md + 1 .md reference (json ignored)
        Assert.Equal(2, resources.Count);
    }

    [Fact]
    public async Task GetResourcesAsync_SkipsInvalidSkillFiles()
    {
        var skillDir = Path.Combine(_tempDir, "invalid-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "# No front-matter\nJust markdown.", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillResourceProvider(
            fileStore, NullLogger<SkillResourceProvider>.Instance);

        var resources = await provider.GetResourcesAsync();

        Assert.Empty(resources);
    }

    [Fact]
    public async Task GetResourcesAsync_CachesResultsOnSubsequentCalls()
    {
        var skillDir = Path.Combine(_tempDir, "cached-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: cached-skill\ndescription: Cached.\n---\n# Cached", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillResourceProvider(
            fileStore, NullLogger<SkillResourceProvider>.Instance);

        var first = await provider.GetResourcesAsync();
        var second = await provider.GetResourcesAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetResourcesAsync_ReturnsEmptyForEmptyDirectory()
    {
        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillResourceProvider(
            fileStore, NullLogger<SkillResourceProvider>.Instance);

        var resources = await provider.GetResourcesAsync();

        Assert.Empty(resources);
    }

    [Fact]
    public async Task GetResourcesAsync_SkipsEmptySkillFiles()
    {
        var skillDir = Path.Combine(_tempDir, "empty-skill");
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), "   ", TestContext.Current.CancellationToken);

        var fileStore = new DefaultAgentSkillFilesStore(_tempDir);
        var provider = new SkillResourceProvider(
            fileStore, NullLogger<SkillResourceProvider>.Instance);

        var resources = await provider.GetResourcesAsync();

        Assert.Empty(resources);
    }
}

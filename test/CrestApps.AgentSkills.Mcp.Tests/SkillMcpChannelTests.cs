using System.Reflection;
using CrestApps.AgentSkills.Mcp;
using CrestApps.AgentSkills.Mcp.Providers;
using CrestApps.AgentSkills.Mcp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Xunit;

namespace CrestApps.AgentSkills.Mcp.Tests;

/// <summary>
/// Covers the <c>mcp</c> channel gating (Fix 1) and the resource body-strip
/// consistency with the prompt (Fix 3).
/// </summary>
public sealed class SkillMcpChannelTests : IDisposable
{
    private readonly string _tempDir;

    public SkillMcpChannelTests()
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

    private async Task WriteSkillAsync(string dirName, string content)
    {
        var skillDir = Path.Combine(_tempDir, dirName);
        Directory.CreateDirectory(skillDir);
        await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), content, TestContext.Current.CancellationToken);
    }

    private SkillPromptProvider CreatePromptProvider(AgentSkillOptions options = null)
        => new(new DefaultAgentSkillFilesStore(_tempDir), NullLogger<SkillPromptProvider>.Instance, Accessor(options));

    private SkillResourceProvider CreateResourceProvider(AgentSkillOptions options = null)
        => new(new DefaultAgentSkillFilesStore(_tempDir), NullLogger<SkillResourceProvider>.Instance, Accessor(options));

    private static IOptions<AgentSkillOptions> Accessor(AgentSkillOptions options)
        => Options.Create(options ?? new AgentSkillOptions());

    // ----- Channel gating: prompt provider -----

    [Fact]
    public async Task PromptProvider_DefaultBoth_EmitsPrompt()
    {
        await WriteSkillAsync("s", "---\nname: s\ndescription: d.\n---\n# Body");

        var prompts = await CreatePromptProvider().GetPromptsAsync();

        Assert.Single(prompts);
    }

    [Fact]
    public async Task PromptProvider_McpPrompt_EmitsPrompt()
    {
        await WriteSkillAsync("s", "---\nname: s\ndescription: d.\nmcp: prompt\n---\n# Body");

        var prompts = await CreatePromptProvider().GetPromptsAsync();

        Assert.Single(prompts);
    }

    [Fact]
    public async Task PromptProvider_McpResource_DoesNotEmitPrompt()
    {
        await WriteSkillAsync("s", "---\nname: s\ndescription: d.\nmcp: resource\n---\n# Body");

        var prompts = await CreatePromptProvider().GetPromptsAsync();

        Assert.Empty(prompts);
    }

    [Fact]
    public async Task PromptProvider_InvalidMcp_FallsBackToDefault()
    {
        await WriteSkillAsync("s", "---\nname: s\ndescription: d.\nmcp: bogus\n---\n# Body");

        // Default is Both, so a prompt is still emitted.
        var prompts = await CreatePromptProvider().GetPromptsAsync();

        Assert.Single(prompts);
    }

    [Fact]
    public async Task PromptProvider_DefaultChannelResource_OmitsPromptWhenKeyAbsent()
    {
        await WriteSkillAsync("s", "---\nname: s\ndescription: d.\n---\n# Body");

        var options = new AgentSkillOptions { DefaultMcpChannel = McpChannel.Resource };
        var prompts = await CreatePromptProvider(options).GetPromptsAsync();

        Assert.Empty(prompts);
    }

    // ----- Channel gating: resource provider -----

    [Fact]
    public async Task ResourceProvider_DefaultBoth_EmitsSkillResource()
    {
        await WriteSkillAsync("s", "---\nname: s\ndescription: d.\n---\n# Body");

        var resources = await CreateResourceProvider().GetResourcesAsync();

        Assert.Single(resources);
    }

    [Fact]
    public async Task ResourceProvider_McpResource_EmitsSkillResource()
    {
        await WriteSkillAsync("s", "---\nname: s\ndescription: d.\nmcp: resource\n---\n# Body");

        var resources = await CreateResourceProvider().GetResourcesAsync();

        Assert.Single(resources);
    }

    [Fact]
    public async Task ResourceProvider_McpPrompt_OmitsSkillResourceButKeepsReferences()
    {
        await WriteSkillAsync("s", "---\nname: s\ndescription: d.\nmcp: prompt\n---\n# Body");
        var refsDir = Path.Combine(_tempDir, "s", "references");
        Directory.CreateDirectory(refsDir);
        await File.WriteAllTextAsync(Path.Combine(refsDir, "ref.md"), "# Reference", TestContext.Current.CancellationToken);

        var resources = await CreateResourceProvider().GetResourcesAsync();

        // SKILL.md resource is suppressed; the reference file is still exposed.
        Assert.Single(resources);
        Assert.Contains(resources, r => r.ProtocolResource?.Uri == "skills://s/references/ref.md");
        Assert.DoesNotContain(resources, r => r.ProtocolResource?.Uri == "skills://s/SKILL.md");
    }

    [Fact]
    public async Task ResourceProvider_DefaultChannelPrompt_OmitsSkillResourceWhenKeyAbsent()
    {
        await WriteSkillAsync("s", "---\nname: s\ndescription: d.\n---\n# Body");

        var options = new AgentSkillOptions { DefaultMcpChannel = McpChannel.Prompt };
        var resources = await CreateResourceProvider(options).GetResourcesAsync();

        Assert.Empty(resources);
    }

    // ----- Body-strip consistency (Fix 3) -----

    [Fact]
    public async Task ResourceProvider_ServesFrontMatterStrippedBody()
    {
        var content =
            "---\n" +
            "name: s\n" +
            "description: d.\n" +
            "license: SECRET-LICENSE\n" +
            "metadata:\n" +
            "  author: private-author\n" +
            "---\n" +
            "# Real Body\nVisible content.";
        await WriteSkillAsync("s", content);

        var resources = await CreateResourceProvider().GetResourcesAsync();
        var resource = Assert.Single(resources);

        var served = ReadResourceText(resource);

        Assert.Equal("# Real Body\nVisible content.", served);
        Assert.DoesNotContain("SECRET-LICENSE", served, StringComparison.Ordinal);
        Assert.DoesNotContain("private-author", served, StringComparison.Ordinal);
        Assert.DoesNotContain("---", served, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResourceAndPrompt_ServeIdenticalBody()
    {
        var content = "---\nname: s\ndescription: d.\nlicense: MIT\n---\n# Shared Body\nText.";
        await WriteSkillAsync("s", content);

        var prompts = await CreatePromptProvider().GetPromptsAsync();
        var resources = await CreateResourceProvider().GetResourcesAsync();

        var promptBody = ReadPromptText(Assert.Single(prompts));
        var resourceBody = ReadResourceText(Assert.Single(resources));

        Assert.Equal(promptBody, resourceBody);
        Assert.Equal("# Shared Body\nText.", resourceBody);
    }

    private static string ReadResourceText(McpServerResource resource)
        => InvokeBackingFunction(resource);

    private static string ReadPromptText(McpServerPrompt prompt)
        => InvokeBackingFunction(prompt);

    private static string InvokeBackingFunction(object primitive)
    {
        var field = primitive.GetType().GetField(
            "<AIFunction>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        var function = (AIFunction)field!.GetValue(primitive)!;
        var result = function.InvokeAsync(new AIFunctionArguments(), CancellationToken.None)
            .AsTask().GetAwaiter().GetResult();

        return result?.ToString() ?? string.Empty;
    }
}

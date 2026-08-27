using CrestApps.AgentSkills.Mcp;
using CrestApps.OrchardCore.AgentSkills.Mcp.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Tests;

/// <summary>
/// Verifies that OrchardCoreSkillOptions flows through the standard options pipeline
/// and is bridged onto the underlying AgentSkillOptions.
/// </summary>
public sealed class OrchardCoreSkillOptionsBridgeTests
{
    [Fact]
    public void OrchardCoreOptions_AreResolvableViaIOptions_AndBridgeOntoAgentSkillOptions()
    {
        var services = new ServiceCollection();
        services.AddOrchardCoreAgentSkillServices(o => o.Path = "/custom/skills/path");

        using var provider = services.BuildServiceProvider();

        var orchardOptions = provider.GetRequiredService<IOptions<OrchardCoreSkillOptions>>().Value;
        var agentOptions = provider.GetRequiredService<IOptions<AgentSkillOptions>>().Value;

        Assert.Equal("/custom/skills/path", orchardOptions.Path);
        Assert.Equal("/custom/skills/path", agentOptions.Path);
    }

    [Fact]
    public void PostConfigure_OrchardCoreOptions_FlowsToAgentSkillOptions()
    {
        var services = new ServiceCollection();
        services.AddOrchardCoreAgentSkillServices(o => o.Path = "/initial");

        // A later post-configuration must be honored (the benefit of the options pipeline).
        services.Configure<OrchardCoreSkillOptions>(o => o.Path = "/overridden");

        using var provider = services.BuildServiceProvider();

        var agentOptions = provider.GetRequiredService<IOptions<AgentSkillOptions>>().Value;

        Assert.Equal("/overridden", agentOptions.Path);
    }
}

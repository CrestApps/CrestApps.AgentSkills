using CrestApps.OrchardCore.AgentSkills.Mcp.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Extensions;

/// <summary>
/// Extension methods for registering Orchard Core agent skills with an MCP server.
/// </summary>
public static class OrchardCoreSkillMcpExtensions
{
    private const string DefaultSkillsRelativePath = ".agents/skills";

    /// <summary>
    /// Registers Orchard Core agent skills as MCP prompts and resources.
    /// Skills are loaded at runtime from the NuGet package output directory.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder AddOrchardCoreSkills(this IMcpServerBuilder builder)
    {
        return builder.AddOrchardCoreSkills(_ => { });
    }

    /// <summary>
    /// Registers Orchard Core agent skills as MCP prompts and resources
    /// with optional configuration.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="configure">A delegate to configure skill options.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder AddOrchardCoreSkills(
        this IMcpServerBuilder builder,
        Action<OrchardCoreSkillOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new OrchardCoreSkillOptions();
        configure(options);

        var skillsPath = options.Path ?? Path.Combine(AppContext.BaseDirectory, DefaultSkillsRelativePath);

        var promptProvider = new FileSystemSkillPromptProvider(skillsPath);
        var prompts = promptProvider.GetPrompts();
        if (prompts.Count > 0)
        {
            builder.WithPrompts(prompts);
        }

        var resourceProvider = new FileSystemSkillResourceProvider(skillsPath);
        var resources = resourceProvider.GetResources();
        if (resources.Count > 0)
        {
            builder.WithResources(resources);
        }

        return builder;
    }
}

using CrestApps.AgentSkills.Mcp;
using CrestApps.AgentSkills.Mcp.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Extensions;

/// <summary>
/// Extension methods for registering Orchard Core agent skills with an MCP server
/// or with the dependency injection container.
/// Delegates to the generic <see cref="AgentSkillMcpExtensions"/> from <c>CrestApps.AgentSkills.Mcp</c>.
/// </summary>
public static class OrchardCoreSkillMcpExtensions
{
    /// <summary>
    /// Registers the Orchard Core agent skill services as singletons in the DI container.
    /// Does <b>not</b> eagerly load or attach them to an MCP server.
    /// The consumer is responsible for resolving providers and attaching them as needed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrchardCoreAgentSkillServices(this IServiceCollection services)
    {
        return services.AddAgentSkillServices();
    }

    /// <summary>
    /// Registers the Orchard Core agent skill services as singletons in the DI container
    /// with optional configuration.
    /// Does <b>not</b> eagerly load or attach them to an MCP server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure skill options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrchardCoreAgentSkillServices(
        this IServiceCollection services,
        Action<OrchardCoreSkillOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var orchardOptions = new OrchardCoreSkillOptions();
        configure(orchardOptions);

        return services.AddAgentSkillServices(options =>
        {
            options.Path = orchardOptions.Path;
        });
    }

    /// <summary>
    /// Registers Orchard Core agent skills as MCP prompts and resources.
    /// Skills are loaded at runtime from the NuGet package output directory.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder AddOrchardCoreSkills(this IMcpServerBuilder builder)
    {
        return builder.AddAgentSkills();
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

        var orchardOptions = new OrchardCoreSkillOptions();
        configure(orchardOptions);

        return builder.AddAgentSkills(options =>
        {
            options.Path = orchardOptions.Path;
        });
    }
}

using CrestApps.AgentSkills.Mcp;
using CrestApps.AgentSkills.Mcp.Extensions;
using CrestApps.OrchardCore.AgentSkills.Mcp.Adapters;
using Microsoft.Extensions.DependencyInjection;
using SdkPromptProvider = CrestApps.Core.AI.Mcp.Services.IMcpPromptProvider;
using SdkResourceProvider = CrestApps.Core.AI.Mcp.Services.IMcpResourceProvider;
using SkillPromptProvider = CrestApps.AgentSkills.Mcp.Abstractions.IMcpPromptProvider;
using SkillResourceProvider = CrestApps.AgentSkills.Mcp.Abstractions.IMcpResourceProvider;

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
    /// Also registers adapter implementations so that the SDK's default MCP server services
    /// automatically discover skill-file prompts and resources.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrchardCoreAgentSkillServices(this IServiceCollection services)
    {
        services.AddAgentSkillServices();

        // Register adapters that bridge AgentSkills' provider interfaces to the
        // CrestApps.Core SDK interfaces so the SDK's DefaultMcpServerPromptService
        // and DefaultMcpServerResourceService discover skill prompts/resources natively.
        services.AddSingleton<SdkPromptProvider>(sp =>
            new SkillPromptProviderAdapter(sp.GetRequiredService<SkillPromptProvider>()));
        services.AddSingleton<SdkResourceProvider>(sp =>
            new SkillResourceProviderAdapter(sp.GetRequiredService<SkillResourceProvider>()));

        return services;
    }

    /// <summary>
    /// Registers the Orchard Core agent skill services as singletons in the DI container
    /// with optional configuration.
    /// Also registers adapter implementations so that the SDK's default MCP server services
    /// automatically discover skill-file prompts and resources.
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

        services.AddAgentSkillServices(options =>
        {
            options.Path = orchardOptions.Path;
        });

        // Register adapters that bridge AgentSkills' provider interfaces to the
        // CrestApps.Core SDK interfaces so the SDK's DefaultMcpServerPromptService
        // and DefaultMcpServerResourceService discover skill prompts/resources natively.
        services.AddSingleton<SdkPromptProvider>(sp =>
            new SkillPromptProviderAdapter(sp.GetRequiredService<SkillPromptProvider>()));
        services.AddSingleton<SdkResourceProvider>(sp =>
            new SkillResourceProviderAdapter(sp.GetRequiredService<SkillResourceProvider>()));

        return services;
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

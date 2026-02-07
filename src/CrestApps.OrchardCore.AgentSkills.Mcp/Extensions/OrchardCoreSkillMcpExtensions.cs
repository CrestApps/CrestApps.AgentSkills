using CrestApps.OrchardCore.AgentSkills.Mcp.Providers;
using CrestApps.OrchardCore.AgentSkills.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Extensions;

/// <summary>
/// Extension methods for registering Orchard Core agent skills with an MCP server
/// or with the dependency injection container.
/// </summary>
public static class OrchardCoreSkillMcpExtensions
{
    private const string DefaultSkillsRelativePath = ".agents/skills";

    /// <summary>
    /// Registers the Orchard Core agent skill services (<see cref="IMcpResourceFileStore"/>,
    /// <see cref="FileSystemSkillPromptProvider"/>, and <see cref="FileSystemSkillResourceProvider"/>)
    /// as singletons in the DI container. Does <b>not</b> eagerly load or attach them to an MCP server.
    /// The consumer is responsible for resolving providers and attaching them as needed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrchardCoreAgentSkillServices(this IServiceCollection services)
    {
        return services.AddOrchardCoreAgentSkillServices(_ => { });
    }

    /// <summary>
    /// Registers the Orchard Core agent skill services (<see cref="IMcpResourceFileStore"/>,
    /// <see cref="FileSystemSkillPromptProvider"/>, and <see cref="FileSystemSkillResourceProvider"/>)
    /// as singletons in the DI container with optional configuration.
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

        var options = new OrchardCoreSkillOptions();
        configure(options);

        var skillsPath = options.Path
            ?? Path.Combine(AppContext.BaseDirectory, DefaultSkillsRelativePath);

        services.AddSingleton<IMcpResourceFileStore>(new McpSkillFileStore(skillsPath));
        services.AddSingleton<FileSystemSkillPromptProvider>();
        services.AddSingleton<FileSystemSkillResourceProvider>();

        return services;
    }

    /// <summary>
    /// Registers Orchard Core agent skills as MCP prompts and resources.
    /// Skills are loaded at runtime from the NuGet package output directory.
    /// The <see cref="IMcpResourceFileStore"/>, <see cref="FileSystemSkillPromptProvider"/>,
    /// and <see cref="FileSystemSkillResourceProvider"/> are registered as singletons
    /// and immediately used to populate the MCP server.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder AddOrchardCoreSkills(this IMcpServerBuilder builder)
    {
        return builder.AddOrchardCoreSkills(_ => { });
    }

    /// <summary>
    /// Registers Orchard Core agent skills as MCP prompts and resources
    /// with optional configuration. The <see cref="IMcpResourceFileStore"/>,
    /// <see cref="FileSystemSkillPromptProvider"/>, and <see cref="FileSystemSkillResourceProvider"/>
    /// are registered as singletons and immediately used to populate the MCP server.
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

        // Register the common services (file store, prompt provider, resource provider).
        builder.Services.AddOrchardCoreAgentSkillServices(configure);

        // Resolve the registered providers to eagerly load prompts/resources.
        using var sp = builder.Services.BuildServiceProvider();
        var promptProvider = sp.GetRequiredService<FileSystemSkillPromptProvider>();
        var resourceProvider = sp.GetRequiredService<FileSystemSkillResourceProvider>();

        // Eagerly load and register prompts/resources with the MCP server.
        var prompts = promptProvider.GetPromptsAsync().GetAwaiter().GetResult();
        if (prompts.Count > 0)
        {
            builder.WithPrompts(prompts);
        }

        var resources = resourceProvider.GetResourcesAsync().GetAwaiter().GetResult();
        if (resources.Count > 0)
        {
            builder.WithResources(resources);
        }

        return builder;
    }
}

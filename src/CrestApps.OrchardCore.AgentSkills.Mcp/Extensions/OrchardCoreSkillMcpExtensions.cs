using CrestApps.OrchardCore.AgentSkills.Mcp.Providers;
using CrestApps.OrchardCore.AgentSkills.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// and <see cref="FileSystemSkillResourceProvider"/> are registered as singletons.
    /// Prompts and resources are loaded directly during configuration and registered
    /// with the MCP server builder.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder AddOrchardCoreSkills(this IMcpServerBuilder builder)
    {
        return builder.AddOrchardCoreSkills(_ => { });
    }

    /// <summary>
    /// Registers Orchard Core agent skills as MCP prompts and resources
    /// with optional configuration. Prompts and resources are loaded directly
    /// during configuration (without building a service provider) and registered
    /// with the MCP server builder.
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

        // Resolve the skills path directly -- no service provider build needed.
        var options = new OrchardCoreSkillOptions();
        configure(options);
        var skillsPath = options.Path
            ?? Path.Combine(AppContext.BaseDirectory, DefaultSkillsRelativePath);

        // Load prompts/resources directly at configuration time.
        // These are temporary instances for config-time loading only;
        // the DI-registered singletons (with proper loggers) are used at runtime.
        var fileStore = new McpSkillFileStore(skillsPath);
        var promptProvider = new FileSystemSkillPromptProvider(fileStore, NullLogger<FileSystemSkillPromptProvider>.Instance);
        var resourceProvider = new FileSystemSkillResourceProvider(fileStore, NullLogger<FileSystemSkillResourceProvider>.Instance);

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

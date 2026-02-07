using CrestApps.AgentSkills.Mcp.Abstractions;
using CrestApps.AgentSkills.Mcp.Providers;
using CrestApps.AgentSkills.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.AgentSkills.Mcp.Extensions;

/// <summary>
/// Extension methods for registering Agent Skills with an MCP server
/// or with the dependency injection container.
/// </summary>
public static class AgentSkillMcpExtensions
{
    private const string DefaultSkillsRelativePath = ".agents/skills";

    /// <summary>
    /// Registers the Agent Skill services (<see cref="ISkillFileStore"/>,
    /// <see cref="SkillPromptProvider"/>, and <see cref="SkillResourceProvider"/>)
    /// as singletons in the DI container. Does <b>not</b> eagerly load or attach them to an MCP server.
    /// The consumer is responsible for resolving providers and attaching them as needed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentSkillServices(this IServiceCollection services)
    {
        return services.AddAgentSkillServices(_ => { });
    }

    /// <summary>
    /// Registers the Agent Skill services (<see cref="ISkillFileStore"/>,
    /// <see cref="SkillPromptProvider"/>, and <see cref="SkillResourceProvider"/>)
    /// as singletons in the DI container with optional configuration.
    /// Does <b>not</b> eagerly load or attach them to an MCP server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure skill options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentSkillServices(
        this IServiceCollection services,
        Action<AgentSkillOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AgentSkillOptions();
        configure(options);

        var skillsPath = options.Path
            ?? Path.Combine(AppContext.BaseDirectory, DefaultSkillsRelativePath);

        services.AddSingleton<ISkillFileStore>(new PhysicalSkillFileStore(skillsPath));
        services.AddSingleton<SkillPromptProvider>();
        services.AddSingleton<SkillResourceProvider>();

        return services;
    }

    /// <summary>
    /// Registers Agent Skills as MCP prompts and resources.
    /// Skills are loaded at runtime from the configured skills directory.
    /// The <see cref="ISkillFileStore"/>, <see cref="SkillPromptProvider"/>,
    /// and <see cref="SkillResourceProvider"/> are registered as singletons.
    /// Prompts and resources are loaded directly during configuration and registered
    /// with the MCP server builder.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder AddAgentSkills(this IMcpServerBuilder builder)
    {
        return builder.AddAgentSkills(_ => { });
    }

    /// <summary>
    /// Registers Agent Skills as MCP prompts and resources
    /// with optional configuration. Prompts and resources are loaded directly
    /// during configuration (without building a service provider) and registered
    /// with the MCP server builder.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="configure">A delegate to configure skill options.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder AddAgentSkills(
        this IMcpServerBuilder builder,
        Action<AgentSkillOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Register the common services (file store, prompt provider, resource provider).
        builder.Services.AddAgentSkillServices(configure);

        // Resolve the skills path directly -- no service provider build needed.
        var options = new AgentSkillOptions();
        configure(options);
        var skillsPath = options.Path
            ?? Path.Combine(AppContext.BaseDirectory, DefaultSkillsRelativePath);

        // Load prompts/resources directly at configuration time.
        // These are temporary instances for config-time loading only;
        // the DI-registered singletons (with proper loggers) are used at runtime.
        var fileStore = new PhysicalSkillFileStore(skillsPath);
        var promptProvider = new SkillPromptProvider(fileStore, NullLogger<SkillPromptProvider>.Instance);
        var resourceProvider = new SkillResourceProvider(fileStore, NullLogger<SkillResourceProvider>.Instance);

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

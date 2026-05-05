using ModelContextProtocol.Server;
using SdkProvider = CrestApps.Core.AI.Mcp.Services.IMcpResourceProvider;
using SkillProvider = CrestApps.AgentSkills.Mcp.Abstractions.IMcpResourceProvider;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Adapters;

/// <summary>
/// Adapts the AgentSkills <see cref="SkillProvider"/> to the
/// CrestApps.Core SDK <see cref="SdkProvider"/> so that skill-file
/// resources are automatically discovered by the SDK's default MCP server
/// resource service.
/// </summary>
public sealed class SkillResourceProviderAdapter : SdkProvider
{
    private readonly SkillProvider _inner;

    public SkillResourceProviderAdapter(SkillProvider inner)
    {
        _inner = inner;
    }

    public Task<IReadOnlyList<McpServerResource>> GetResourcesAsync()
    {
        return _inner.GetResourcesAsync();
    }
}

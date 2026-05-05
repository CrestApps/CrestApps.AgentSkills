using ModelContextProtocol.Server;
using SdkProvider = CrestApps.Core.AI.Mcp.Services.IMcpPromptProvider;
using SkillProvider = CrestApps.AgentSkills.Mcp.Abstractions.IMcpPromptProvider;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Adapters;

/// <summary>
/// Adapts the AgentSkills <see cref="SkillProvider"/> to the
/// CrestApps.Core SDK <see cref="SdkProvider"/> so that skill-file
/// prompts are automatically discovered by the SDK's default MCP server
/// prompt service.
/// </summary>
public sealed class SkillPromptProviderAdapter : SdkProvider
{
    private readonly SkillProvider _inner;

    public SkillPromptProviderAdapter(SkillProvider inner)
    {
        _inner = inner;
    }

    public Task<IReadOnlyList<McpServerPrompt>> GetPromptsAsync()
    {
        return _inner.GetPromptsAsync();
    }
}

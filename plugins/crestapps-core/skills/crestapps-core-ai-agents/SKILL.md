---
name: crestapps-core-ai-agents
description: Skill for AI agents in CrestApps.Core 2.0, specialized AIProfile records of type Agent that the primary model invokes as tools. Covers creating agent profiles with AgentMetadata availability (OnDemand or AlwaysAvailable), linking them with AgentInvocationMetadata, how AgentProxyTool runs an agent with tools disabled or through the orchestrator when AllowToolInvocation is set, the AgentInvocationDepth recursion guard, deployment inheritance and the chat slot, code-defined and system agents through IAIProfileProvider including the Tabular Data, PDF, Presentation and Word agents, chat-specific agents that read AIInvocationScope, exposing agents to MCP clients with McpServerOptions Agents and ExposeAllAgents, and SubAgent usage labels. Use it when delegating work to sub-agents, adding a framework-managed agent, publishing an agent over MCP, or troubleshooting an agent the model does not call.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Agents

An agent is an `AIProfile` with `Type = AIProfileType.Agent`. The primary model sees each available agent as a tool that takes one `prompt` argument and returns the agent's answer. Each agent has its own system prompt, deployment and settings, so instructions do not collide.

## Guidelines

- `AddCoreAIOrchestration()` (or `AddAISuite(...)`) registers `AgentToolRegistryProvider`; nothing else is needed.
- Every agent needs a `Name` and a strong `Description`. Agents with an empty name or description are skipped, and the description is all the model (or an MCP client) has to decide with.
- Use `OnDemand` for most agents and link them per profile. `AlwaysAvailable` agents are added to every request and cost tokens on every turn.
- By default an agent runs as one completion with tools disabled. Set `AgentMetadata.AllowToolInvocation = true` only when it needs its own tools; it then runs through its orchestrator, and agents inside it are still suppressed.
- Leave `ChatDeploymentName` empty to inherit the caller's chat deployment, or name a cheaper or more capable model for the agent.

## Create an Agent

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

var agent = await profileManager.NewAsync("translator");

agent.Type = AIProfileType.Agent;
agent.DisplayText = "Translator";
agent.Description = "Translates text between languages. Provide the target language and the text.";
agent.ChatDeploymentName = "gpt-5-mini";

agent.Put(new AIProfileMetadata { SystemMessage = "Translate faithfully. Return only the translation." });
agent.Put(new AgentMetadata { Availability = AgentAvailability.OnDemand });

await profileManager.CreateAsync(agent);
```

## Link Agents to a Chat Profile

```csharp
chatProfile.Put(new AgentInvocationMetadata
{
    Names = ["translator", "code-reviewer"],
});

await profileManager.UpdateAsync(chatProfile);
```

The names flow to `AICompletionContext.AgentNames`; `AgentToolRegistryProvider` returns those agents plus every `AlwaysAvailable` agent as `ToolRegistryEntrySource.Agent` entries.

## How an Agent Runs

`AgentProxyTool` resolves the agent through `IAIProfileManager.GetAsync(AIProfileType.Agent)` and then:

- **Tools disabled (default)**: builds the agent's completion context with `IAICompletionContextBuilder`, sets `DisableTools = true`, labels usage with `AIUsagePurposes.SubAgent`, resolves `ResolveSlotAsync(AIDeploymentSlotNames.Chat, context.ChatDeploymentName)` and calls `IAICompletionService.CompleteAsync` with the prompt.
- **`AllowToolInvocation = true`**: increments `AIInvocationContext.AgentInvocationDepth`, builds an orchestration context for the agent with `AgentNames` cleared, resolves `IOrchestratorResolver.Resolve(agent.OrchestratorName)` and collects the streamed text. Tool-capable agents only run while the depth is below one, so an agent can use its tools but never call another agent.

A missing agent or a failure returns a descriptive message to the primary model instead of throwing. Without an `AIInvocationScope` the depth is unknown and the agent runs with tools disabled.

```csharp
agent.Put(new AgentMetadata
{
    Availability = AgentAvailability.OnDemand,
    AllowToolInvocation = true,
});

agent.Put(new FunctionInvocationMetadata { Names = ["query-orders"] });
```

## Code-Defined and System Agents

Implement `IAIProfileProvider` for agents defined in code. Return agents only when `type == AIProfileType.Agent`; stored profiles with the same name win. Set `IsSystem = true` to keep an agent read-only and out of user-facing pickers while it stays available to orchestration and the A2A host.

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

public sealed class ComplianceAgentProvider : IAIProfileProvider
{
    public ValueTask<IReadOnlyList<AIProfile>> GetProfilesAsync(
        AIProfileType type,
        CancellationToken cancellationToken = default)
    {
        if (type != AIProfileType.Agent)
        {
            return ValueTask.FromResult<IReadOnlyList<AIProfile>>([]);
        }

        var agent = new AIProfile
        {
            Type = AIProfileType.Agent,
            Name = "compliance-checker",
            DisplayText = "Compliance Checker",
            Description = "Checks a draft reply against the company's compliance rules and lists violations.",
        };

        agent.Put(new AIProfileMetadata { SystemMessage = "List every rule the draft breaks, or say it is compliant." });
        agent.Put(new AgentMetadata
        {
            Availability = AgentAvailability.AlwaysAvailable,
            IsSystem = true,
        });

        return ValueTask.FromResult<IReadOnlyList<AIProfile>>([agent]);
    }
}
```

```csharp
builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIProfileProvider, ComplianceAgentProvider>());
```

A provider can offer an agent only to one chat. The chat response handler stores the chat interaction and session in `AIInvocationScope.Current` before it builds the orchestration context, so read `AIInvocationScope.Current?.ChatInteraction` or `?.ChatSession` inside `GetProfilesAsync`.

Built-in system agents follow the same pattern, each with `AllowToolInvocation` and hidden tools:

| Agent | Name | Registered by | Disable with |
|---|---|---|---|
| Tabular Data Agent | `tabular-data-agent` | document processing | |
| PDF Agent | `pdf-agent` | `AddPdf()` on the document processing builder | `PdfAgentOptions.Enabled = false` |
| Presentation Agent | `presentation-agent` | `AddOpenXml()` | `PresentationAgentOptions.Enabled = false` |
| Word Agent | `word-agent` | `AddWord()` | `WordAgentOptions.Enabled = false` |

## Agents as MCP Server Tools

An MCP server built with CrestApps.Core can publish agents as callable tools, one per allow-listed agent, named after the agent, described by its description, taking a single `prompt`.

```csharp
using CrestApps.Core.AI.Mcp.Models;

builder.Services.Configure<McpServerOptions>(options =>
{
    options.Agents = ["translator", "compliance-checker"];
    // or options.ExposeAllAgents = true;
});
```

- Agents have their own switch; `ExposeAllTools` never exposes agents.
- An agent without a description is not listed. `AgentAvailability` is ignored here. An agent whose name collides with an exposed tool is skipped with a warning.
- The agent's own tools are not filtered by the server's tool allow-list; what it uses internally is its own configuration.
- Each MCP tool call runs inside an `AIInvocationScope`, so `AllowToolInvocation` agents get their tools and citation-emitting tools register their citations.

See `crestapps-core-mcp` for the server setup itself.

## Troubleshooting

- **The model says "I will delegate this to..." and stops**: the agent did not reach the request. Check that it has a name and description, that it is linked or `AlwaysAvailable`, and that an `IAIProfileProvider` filtering by chat reads `AIInvocationScope.Current` rather than `IHttpContextAccessor`.
- **A tool-capable agent answers without using its tools**: it ran without an invocation scope (direct calls outside a chat) or inside another agent.
- **An agent is missing from pickers**: `AlwaysAvailable` and `IsSystem` agents are intentionally hidden from user selection.

## Upgrading from 1.x

- `AgentProxyTool` resolves the agent deployment through the `chat` slot (`ResolveSlotAsync`) instead of `ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, ...)`.
- An agent that leaves `ChatDeploymentName` empty inherits the delegating conversation's chat deployment.
- MCP tool calls now establish an invocation scope; previously tool-capable agents silently ran with tools disabled over MCP.
- Sub-agent requests are labeled `SubAgent` in usage records.

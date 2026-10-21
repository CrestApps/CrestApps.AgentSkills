---
name: crestapps-core-ai-profiles
description: Skill for designing CrestApps.Core 2.0 AI Profiles, the reusable contract that controls prompts, deployments, tools, agents, retrieval, memory, persona, chat mode and session behavior. Covers AIProfile fields and types (Chat, Utility, TemplatePrompt, Agent), chat and utility deployment selection resolved through slots, AIProfileMetadata, FunctionInvocationMetadata, AgentInvocationMetadata, PromptTemplateMetadata, AIDeploymentParametersMetadata chat and utility model parameters, AIPersonaMetadata, ChatModeProfileSettings with ConversationDeploymentName, PromptSecurityProfileSettings, ResponseHandlerProfileSettings, IAIProfileManager and code-defined IAIProfileProvider profiles, Markdown profile templates under Templates Profiles, the profile as the tool authorization boundary, and upgrading from the removed RealtimeDeploymentName and ChatMode Realtime. Use it when creating or editing profiles in code, choosing what belongs on a profile versus a deployment, or troubleshooting profile behavior.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Profiles

A deployment answers "which model runs". An AI Profile answers "how should this experience behave": instructions, deployments, orchestrator, tools, agents, knowledge, memory, persona, chat mode and post-session rules. AI Chat sessions, agents and orchestrators all read the profile.

## Guidelines

- Validate a connection and deployment with Chat Interactions first; move to a profile when the behavior must be reusable.
- Keep model choice in deployments and behavior in the profile. Reference deployments by name; the runtime resolves them through slots.
- Prefer several focused profiles over one overloaded profile. Use agents (`crestapps-core-ai-agents`) to delegate specialized work.
- Put identity and tone in a persona (`crestapps-core-ai-personas`) when several profiles should share it.
- The profile is the tool authorization boundary for AI sessions: every tool, MCP connection and agent the author attached runs for any caller allowed to use the profile, including anonymous ones. Only attach what every caller may use.

## Building Blocks

| Concept | Question it answers |
|---|---|
| Connection | How do I talk to a provider |
| Deployment | Which model runs and what can it do |
| Chat Interaction | Let me test this setup quickly |
| AI Profile | How should this experience behave |
| Persona | Who is the assistant |

## Profile Types

`AIProfile.Type` is `AIProfileType.Chat`, `Utility`, `TemplatePrompt` or `Agent`. The type changes how the framework uses the same record; agents are profiles of type `Agent` with a strong `Description`.

## Creating a Profile in Code

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

var profile = await profileManager.NewAsync("support-assistant");

profile.Type = AIProfileType.Chat;
profile.DisplayText = "Support Assistant";
profile.ChatDeploymentName = "gpt-5-chat";
profile.UtilityDeploymentName = "gpt-5-mini";
profile.WelcomeMessage = "Hi! How can I help with your order today?";

profile.Put(new AIProfileMetadata
{
    SystemMessage = "You help customers with orders. Never promise refunds.",
    Temperature = 0.2f,
    PastMessagesCount = 10,
});

profile.Put(new FunctionInvocationMetadata { Names = ["lookup-order"] });
profile.Put(new AgentInvocationMetadata { Names = ["translator"] });
profile.Put(new AIPersonaMetadata { PersonaId = "friendly-assistant" });
profile.Put(new MemoryMetadata { EnableUserMemory = true });

profile.Put(new AIDeploymentParametersMetadata
{
    Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [AIDeploymentParameterNames.ReasoningEffort] = "Low",
    },
    UtilityValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [AIDeploymentParameterNames.ReasoningEffort] = "None",
    },
});

var validation = await profileManager.ValidateAsync(profile);

if (validation.Succeeded)
{
    await profileManager.CreateAsync(profile);
}
```

`IAIProfileManager` is an `INamedCatalogManager<AIProfile>` plus `GetAsync(AIProfileType type)`. Register a profile store (`crestapps-core-yessql-stores` or `crestapps-core-entitycore-stores`).

## What Lives on a Profile

| Need | Where |
|---|---|
| Chat model and background model | `ChatDeploymentName`, `UtilityDeploymentName` (utility falls back to chat) |
| System prompt and sampling | `AIProfileMetadata` |
| Reusable prompt fragments | `PromptTemplateMetadata.Templates` (`TemplateId` plus `Parameters`) |
| Tools and agents | `FunctionInvocationMetadata.Names`, `AgentInvocationMetadata.Names`, `AIToolInstanceMetadata.ToolInstanceNames` |
| Model parameters | `AIDeploymentParametersMetadata` (`Values` for chat, `UtilityValues` for utility) |
| Persona | `AIPersonaMetadata` (or top-level `PersonaId` in profile JSON) |
| Voice and chat mode | `ChatModeProfileSettings` via `WithSettings(...)` |
| Anti-spam throttles | `PromptSecurityProfileSettings` via `WithSettings(...)` |
| First response handler | `ResponseHandlerProfileSettings.InitialResponseHandlerName` |
| Orchestrator | `OrchestratorName` (empty or unknown uses the default) |

Settings objects use the profile settings helpers:

```csharp
profile.WithSettings(new ChatModeProfileSettings
{
    ChatMode = ChatMode.Conversation,
    ConversationDeploymentName = "gpt-realtime",
    VoiceName = "marin",
});

profile.WithSettings(new PromptSecurityProfileSettings
{
    MaxMessagesPerWindow = 10,
    RateLimitWindow = TimeSpan.FromMinutes(2),
});

if (profile.TryGetSettings<ChatModeProfileSettings>(out var chatMode)) { /* ... */ }
```

The chat deployment means only "the text model this profile talks to". `ChatMode` (`TextInput`, `AudioInput`, `Conversation`) decides whether the conversation is spoken. In `Conversation` mode the realtime slot is resolved first (`ConversationDeploymentName`, then `DefaultRealtimeDeploymentName`, then the first realtime-capable deployment); the speech-to-text plus text-to-speech cascade is used only when no realtime-capable deployment resolves.

## Code-Defined Profiles

Implement `IAIProfileProvider` to contribute profiles that are not stored. `IAIProfileManager.GetAsync(type)` merges them in and a stored profile with the same name wins. During a chat the provider can read `AIInvocationScope.Current?.ChatInteraction` or `ChatSession`, because the chat response handler sets them before the context is built.

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

public sealed class BuiltInProfileProvider : IAIProfileProvider
{
    public ValueTask<IReadOnlyList<AIProfile>> GetProfilesAsync(
        AIProfileType type,
        CancellationToken cancellationToken = default)
    {
        if (type != AIProfileType.Chat)
        {
            return ValueTask.FromResult<IReadOnlyList<AIProfile>>([]);
        }

        var profile = new AIProfile
        {
            Type = AIProfileType.Chat,
            Name = "faq-bot",
            DisplayText = "FAQ Bot",
        };

        profile.Put(new AIProfileMetadata { SystemMessage = "Answer only from the FAQ." });

        return ValueTask.FromResult<IReadOnlyList<AIProfile>>([profile]);
    }
}
```

```csharp
builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIProfileProvider, BuiltInProfileProvider>());
```

## Profile Templates

Markdown profile templates live in `Templates/Profiles` under the content root or are embedded under `Templates/Profiles`. Front matter can set `ProfileType`, `ChatDeploymentName`, `UtilityDeploymentName`, `ConversationDeploymentName`, `ChatMode`, `VoiceName`, `OrchestratorName`, `WelcomeMessage`, `PromptTemplate`, `PromptSubject`, `TitleType`, `Temperature`, `TopP`, `MaxOutputTokens`, `PastMessagesCount`, `ToolNames`, `AgentNames`, `AgentAvailability`, `AllowToolInvocation`, `PersonaId`, `ModelParameters` and `UtilityModelParameters`; the body is the system message.

```markdown
---
Title: Deep research
ProfileType: Chat
ChatDeploymentName: gpt-5-chat
PersonaId: friendly-assistant
ModelParameters: reasoningEffort=High
UtilityModelParameters: reasoningEffort=Low
---

You are a meticulous research assistant.
```

## Troubleshooting

- **The profile lost its system message, tools and model parameters**: a context builder handler failed and was skipped; check the log for "Error in completion context building handler".
- **A selected system prompt template does not render**: in 2.0 `AIProfileSystemPromptTemplateProvider` ships with `AddCoreAITemplating()`; stored `ai-profile:` identifiers still resolve as aliases.
- **The profile speaks with a realtime model unexpectedly**: it is in `Conversation` mode and a realtime-capable deployment resolved. Remove the `realtime` feature from deployments to keep the cascade.

## Upgrading from 1.x

- `AIProfile.RealtimeDeploymentName` and `ChatMode.Realtime` are removed. A stored `RealtimeDeploymentName` is folded onto `ChatDeploymentName` when read, and the old chat deployment moves to `UtilityDeploymentName` when none was set. A profile whose chat deployment is realtime-capable then resolves to `Conversation` mode with that deployment carrying the conversation (`IAIDeploymentManager.ResolveConversationModeAsync`), and a stored `"Realtime"` chat mode reads as `TextInput`. Editors persist the folded shape the next time they save. A `ConversationDeploymentName` that is not realtime-capable is reported as misconfigured rather than silently replaced.
- `ProfileTemplateMetadata.RealtimeDeploymentName` is `[Obsolete]` and still supplies the deployment.
- Per-user tool gating no longer applies to AI sessions. `IAIToolAccessEvaluator` still applies to Chat Interaction sends.
- Deployments no longer have a purpose; see `crestapps-core-ai-model-capabilities`.

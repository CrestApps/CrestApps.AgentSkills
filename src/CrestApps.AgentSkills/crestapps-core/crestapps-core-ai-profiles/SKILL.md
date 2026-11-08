---
name: crestapps-core-ai-profiles
description: Skill for designing CrestApps.Core 2.0 AI Profiles, the reusable contract that controls prompts, deployments, tools, agents, retrieval, memory, persona, chat mode and session behavior. Covers AIProfile types, deployment selection through slots, AIProfileMetadata, FunctionInvocationMetadata, AgentInvocationMetadata, PromptTemplateMetadata, AIDeploymentParametersMetadata, ChatModeProfileSettings, IAIProfileManager and IAIProfileProvider, Markdown profile templates, the opt-in speaking styles, guardrails, conversation privacy and profile versions (AddSpeakingStyles, AddGuardrails, AddConversationPrivacy, AddProfileVersions), AIProfileSpeechSettings, AIProfileLanguageSettings, typed DataExtractionEntry fields, the profile as the tool authorization boundary, and upgrading notes. Use it when creating or editing profiles in code, choosing what belongs on a profile versus a deployment, redacting or versioning profiles, or troubleshooting profile behavior.
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
| Speaking styles | `AIProfileSpeakingStyleSettings.Styles` (needs `AddSpeakingStyles()`) |
| Refused topics | `AIProfileGuardrailSettings` (needs `AddGuardrails()`) |
| Redaction and retention | `AIProfileDataPrivacySettings` (needs `AddConversationPrivacy()`) |
| Speaking rate, pronunciation, fallback TTS, recognition, barge-in | `AIProfileSpeechSettings` |
| Allowed and starting languages | `AIProfileLanguageSettings` |
| Extracted fields and their types | `AIProfileDataExtractionSettings.DataExtractionEntries` |

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

## Opt-In Profile Features

Four features moved from the Orchard Core modules into the framework. Each is added on the AI suite and configured per profile through settings:

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddSpeakingStyles()
        .AddGuardrails()
        .AddConversationPrivacy()
        .AddProfileVersions(versions => versions.AddYesSqlStores())));
```

| Feature | Settings | What it does |
|---|---|---|
| Speaking styles | `AIProfileSpeakingStyleSettings` | Appends System Prompt templates of the `Speaking style` category to the system message (`AISpeakingStyleCompletionContextBuilderHandler`) |
| Guardrails | `AIProfileGuardrailSettings` | `AIGuardrailService` asks the utility deployment whether a customer message or reply falls under a refused `Guardrail topic` template and answers with `PlaceholderReply`; `IAIGuardrailHandler` is told |
| Conversation privacy | `AIProfileDataPrivacySettings` | `AIConversationPrivacyService.ProcessAsync`, run by the host on a schedule, redacts (`RedactPersonalData`) or strips (`MetadataOnly`) ended conversations and deletes them after `RetentionDays`; `NamesToKeep` protects the assistant's and business's names |
| Profile versions | none | Every save becomes an `AIProfileVersion` in `IAIProfileVersionStore`; compare with `AIProfileVersionComparer`, restore with `AIProfileVersionRestorer` |

The eight default speaking styles and eight guardrail topics are seeded as editable System Prompt templates by `IAISystemPromptTemplateSeeder.SeedAsync()`; call it once from a migration or setup step (see `crestapps-core-ai-templates`). Guardrails sit behind prompt security and the output filter in the chat hub (see `crestapps-core-prompt-security`). Full examples are in [profile-features-examples.md](references/profile-features-examples.md).

## Speech, Language and Typed Extraction

These settings need no registration and are ignored until set:

- `AIProfileSpeechSettings`: `SpeakingRate`, `SpeakingEmotion`, `PronunciationEntries`, `FallbackTextToSpeechDeploymentName`, `BoostedKeywords`, `RecognitionLanguages`, `InterruptionSensitivity`, `EnableBackchanneling` and `BackchannelPhrases`. The chat hub and the cascaded realtime pipeline apply them (see `crestapps-core-realtime-voice`).
- `AIProfileLanguageSettings`: `AllowedLanguages` and `StartingLanguage`. `AICompletionContextKeys.ConversationLanguage` overrides the starting language per conversation, and `IConversationLanguageService.ReportDetectedLanguageAsync` runs every `IConversationLanguageHandler` when the host reports a different allowed language.
- `DataExtractionEntry.FieldType` (`Text`, `Number`, `Boolean`, `Date`, `Enum`, `List`) and `AllowedValues`. `DataExtractionValueNormalizer` normalizes each value before it is stored and drops values that do not fit. A `List` collects items across messages, without duplicates.

```csharp
profile.AlterSettings<AIProfileLanguageSettings>(settings =>
{
    settings.AllowedLanguages = ["en", "es"];
    settings.StartingLanguage = "en";
});
```

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
- **A guardrail never triggers**: `AddGuardrails()` was not called, `CheckCustomer` and `CheckReplies` are both off, or the profile has no utility deployment (the check is skipped with a warning).
- **No speaking styles or guardrail topics to pick**: the defaults are not served read-only; run `IAISystemPromptTemplateSeeder.SeedAsync()` once.
- **Conversations are never redacted or deleted**: `AddConversationPrivacy()` schedules nothing; the host must call `ProcessAsync` per profile and commit afterwards.

## Upgrading Within 2.0 Previews

- A `PastMessagesCount` of `1` now keeps exactly one message. It used to send the whole history (the clients tested `> 1`). Clear the field to keep sending everything; blank, `0` or negative still keeps all.
- Profile versions on YesSql need the `AIProfileVersionIndex` table (`CreateAIProfileVersionIndexSchemaAsync`).

## Upgrading from 1.x

- `AIProfile.RealtimeDeploymentName` and `ChatMode.Realtime` are removed. A stored `RealtimeDeploymentName` is folded onto `ChatDeploymentName` when read, and the old chat deployment moves to `UtilityDeploymentName` when none was set. A profile whose chat deployment is realtime-capable then resolves to `Conversation` mode with that deployment carrying the conversation (`IAIDeploymentManager.ResolveConversationModeAsync`), and a stored `"Realtime"` chat mode reads as `TextInput`. Editors persist the folded shape the next time they save. A `ConversationDeploymentName` that is not realtime-capable is reported as misconfigured rather than silently replaced.
- `ProfileTemplateMetadata.RealtimeDeploymentName` is `[Obsolete]` and still supplies the deployment.
- Per-user tool gating no longer applies to AI sessions. `IAIToolAccessEvaluator` still applies to Chat Interaction sends.
- Deployments no longer have a purpose; see `crestapps-core-ai-model-capabilities`.

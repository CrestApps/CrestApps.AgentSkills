---
name: orchardcore-ai
description: Skill for the CrestApps.OrchardCore.AI foundation module (AI Services) in CrestApps.OrchardCore 3.0. Covers the CrestApps.OrchardCore.AI, AI.Chat.Core, AI.Chat.Api, AI.ConnectionManagement, AI.ToolInstances and AI.Personas features, the Artificial Intelligence admin screens and permissions, appsettings.json Connections, Deployments and DefaultParameters, GeneralAISettings and DefaultAIDeploymentSettings site settings read through IOptionsMonitor, the AIDeployment, DeleteAIDeployments and AIProfile recipe steps, AI profile types (Chat, Utility, TemplatePrompt, Agent), profiles created in migrations with IAIProfileManager, resolving deployments by slot with IAIDeploymentManager.ResolveSlotAsync, IAIClientFactory speech-to-text and text-to-speech clients, deployment plan steps, and upgrading from 2.x. Use this skill when requests mention Orchard Core AI, AI Services, AI profiles, AI deployments, AI settings, AIProfile recipe, ChatModeProfileSettings, AIProfileMetadata, AgentMetadata, or AI profiles in code.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Services

`CrestApps.OrchardCore.AI` is the Orchard foundation of the CrestApps AI suite. It wires the shared
CrestApps.Core AI services into Orchard Core and adds connection, deployment, profile, template, tool
instance and persona management, AI site settings, recipes, deployment steps and workflow activities.

## Guidelines

- Install `CrestApps.OrchardCore.AI` and at least one provider package (`CrestApps.OrchardCore.OpenAI`,
  `CrestApps.OrchardCore.OpenAI.Azure`, `CrestApps.OrchardCore.AzureAIInference`, `CrestApps.OrchardCore.Ollama`)
  in the web/startup project.
- `CrestApps.OrchardCore.AI` is dependency-only: a provider or a feature such as AI Chat enables it.
- A **connection** holds credentials, a **deployment** is one model used through a connection and declares
  **model capabilities**, and an **AI profile** names deployments by technical name. Where a deployment is
  used is a **slot** (chat, utility, embedding, ...) resolved from site defaults. See
  `orchardcore-ai-model-capabilities`.
- Never hardcode API keys. Use user secrets in development and environment variables or Azure Key Vault in
  production.
- Read settings-backed AI options through `IOptionsMonitor<T>.CurrentValue`, never `IOptions<T>`.
- The service APIs (`IAIClientFactory`, `IAICompletionService`, `IAIProfileManager`, orchestration) live in
  CrestApps.Core. Use `crestapps-core-ai-profiles` and `crestapps-core-ai-runtime` for framework detail.
- All recipe JSON uses the root `{ "steps": [ ... ] }` format, and C# classes are `sealed` (except view models).

## Features

| Feature ID | Name | Notes |
| --- | --- | --- |
| `CrestApps.OrchardCore.AI` | AI Services | Dependency only. Deployments, profiles, profile templates, AI settings, the **Cascaded Realtime** deployment provider |
| `CrestApps.OrchardCore.AI.Chat.Core` | AI Chat Services | Dependency only. Chat sessions, session processing, response-handler selection. Depends on `CrestApps.OrchardCore.AI.Prompting` |
| `CrestApps.OrchardCore.AI.Chat.Api` | AI Chat WebAPI | REST endpoints for chat and completion |
| `CrestApps.OrchardCore.AI.ConnectionManagement` | AI Connection Management | Provider connection editor and the `AIProviderConnections` recipe step. See `orchardcore-ai-connections` |
| `CrestApps.OrchardCore.AI.ToolInstances` | AI Tool Instances | See `orchardcore-ai-tool-instances` |
| `CrestApps.OrchardCore.AI.Personas` | AI Personas | See `orchardcore-ai-personas` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.ConnectionManagement",
        "CrestApps.OrchardCore.OpenAI",
        "CrestApps.OrchardCore.AI.Chat"
      ],
      "disable": []
    }
  ]
}
```

## Admin screens and permissions

| Screen | Permission |
| --- | --- |
| **Artificial Intelligence > Provider Connections** | `ManageProviderConnections` |
| **Artificial Intelligence > Deployments** | `ManageAIDeployments` |
| **Artificial Intelligence > Profiles** | `ManageAIProfiles` |
| **Artificial Intelligence > Templates** | `ManageAIProfileTemplates` |
| **Artificial Intelligence > Tool Instances** | `ManageAIToolInstances` |
| **Artificial Intelligence > Personas** | `ManageAIPersonas` |
| **Settings > Artificial Intelligence** (group `ai-settings`) | `ManageAIProfiles`, plus the permission each feature's card registers |

Permissions are declared in `AIPermissions` with `LocalizationSource.Create(...)` descriptions. Every list
(profiles `AIProfiles`, deployments `AIDeployments`, connections `AIProviderConnections`, templates
`AITemplates`, tool instances `AIToolInstances`, personas `AIPersonas`) renders through Orchard Core's
`AdminList` shape; configuration-backed deployments and connections render in a separate read-only list
(`AIDeploymentsReadOnly`, `AIProviderConnectionsReadOnly`). Rows are `{Model}_SummaryAdmin` (List layout)
and `SummaryAdminGrid` zones (Grid layout). See `orchardcore-admin-lists` to add columns or row actions.

## Configuration (appsettings.json)

Connections and deployments are two sibling arrays under `OrchardCore:CrestApps:AI`. Configuration
entries appear beside stored ones and are read-only in the admin.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "DefaultParameters": {
          "Temperature": 0,
          "MaxOutputTokens": 800,
          "PastMessagesCount": 10,
          "MaximumIterationsPerRequest": 10
        },
        "Connections": [
          {
            "Name": "openai-cloud",
            "ClientName": "OpenAI",
            "ApiKey": "<from a secret store>"
          }
        ],
        "Deployments": [
          {
            "Name": "chat-default",
            "ClientName": "OpenAI",
            "ConnectionName": "openai-cloud",
            "ModelName": "gpt-4o",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textGeneration", "toolCalling", "streaming" ]
              }
            }
          },
          {
            "Name": "embedding-default",
            "ClientName": "OpenAI",
            "ConnectionName": "openai-cloud",
            "ModelName": "text-embedding-3-large",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textEmbedding" ]
              }
            }
          }
        ]
      }
    }
  }
}
```

`DefaultParameters` keys are `Temperature`, `MaxOutputTokens`, `TopP`, `FrequencyPenalty`, `PresencePenalty`,
`PastMessagesCount`, `MaximumIterationsPerRequest` (default `10`), `AbsoluteMaximumIterationsPerRequest`
(`100`), `EnableOpenTelemetry` and `EnableDistributedCaching`. Environment variables use `__`, for example
`OrchardCore__CrestApps__AI__Connections__0__ApiKey`. The old `CrestApps_AI:DefaultParameters` and
`CrestApps_AI:Providers` sections are still read; use the names above for new configuration.

Per-provider connection keys are in `orchardcore-ai-providers`.

## AI site settings

**Settings > Artificial Intelligence** is built from cards contributed by enabled features.

| Settings class (recipe key) | Holds | Read through |
| --- | --- | --- |
| `GeneralAISettings` | `EnableAIUsageTracking`, `EnablePreemptiveMemoryRetrieval`, `OverrideMaximumIterationsPerRequest`/`MaximumIterationsPerRequest`, `OverrideEnableDistributedCaching`/`EnableDistributedCaching`, `OverrideEnableOpenTelemetry`/`EnableOpenTelemetry` | `IOptionsMonitor<GeneralAIOptions>` |
| `DefaultAIDeploymentSettings` | `DefaultChatDeploymentName`, `DefaultUtilityDeploymentName`, `DefaultEmbeddingDeploymentName`, `DefaultImageDeploymentName`, `DefaultVisionDeploymentName`, `DefaultSpeechToTextDeploymentName`, `DefaultTextToSpeechDeploymentName`, `DefaultTextToSpeechVoiceId`, `DefaultRealtimeDeploymentName` | the slot resolver |

```json
{
  "steps": [
    {
      "name": "Settings",
      "GeneralAISettings": {
        "EnableAIUsageTracking": true,
        "EnablePreemptiveMemoryRetrieval": false
      },
      "DefaultAIDeploymentSettings": {
        "DefaultChatDeploymentName": "chat-default",
        "DefaultUtilityDeploymentName": "chat-default",
        "DefaultEmbeddingDeploymentName": "embedding-default"
      }
    }
  ]
}
```

Saving **General** no longer reloads the tenant: `GeneralAIOptions` is refreshed live through
`AddSignalOptionsChangeTokenSource<GeneralAIOptions>()`. Code that reads it must use
`IOptionsMonitor<GeneralAIOptions>` (see `orchardcore-site-settings`).

## Deployments by recipe

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "chat-default",
          "ModelName": "gpt-4o",
          "ClientName": "OpenAI",
          "ConnectionName": "openai-cloud",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textGeneration", "toolCalling", "streaming", "structuredOutputs" ]
            }
          }
        }
      ]
    }
  ]
}
```

- An existing deployment is matched by `ItemId`, or by `Name` plus `ClientName`.
- A deployment that declares no capabilities and carries no legacy `Purpose` is imported as
  `textGeneration` only. Declare `toolCalling` and `streaming` explicitly for a chat model.
- Remove deployments with `{ "name": "DeleteAIDeployments", "DeploymentNames": [ "old-chat" ] }` or
  `"IncludeAll": true`.
- There is no `IsDefault` on a deployment any more. Defaults are the `DefaultAIDeploymentSettings` above.

## AI profiles

| Type | Purpose |
| --- | --- |
| `Chat` | An assistant people talk to (chat page, widget, automated SMS and voice) |
| `Utility` | Background work invoked from code or workflows |
| `TemplatePrompt` | A prompt run against an existing chat session (`PromptTemplate`, `PromptSubject`) |
| `Agent` | A specialist other profiles call as a tool. `Description` is required |

### Profile by recipe

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "support-assistant",
          "DisplayText": "Support Assistant",
          "Type": "Chat",
          "TitleType": "Generated",
          "WelcomeMessage": "How can I help?",
          "ChatDeploymentName": "chat-default",
          "UtilityDeploymentName": "chat-default",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a helpful support assistant.",
              "Temperature": 0.3,
              "MaxTokens": 2048,
              "PastMessagesCount": 10
            },
            "FunctionInvocationMetadata": { "Names": [] },
            "AgentInvocationMetadata": { "Names": [ "research-agent" ] },
            "PromptTemplateMetadata": {
              "Templates": [ { "TemplateId": "customer-support-tone" } ]
            },
            "AIDeploymentParametersMetadata": {
              "Values": { "reasoningEffort": "Medium" }
            }
          },
          "Settings": {
            "AIChatProfileSettings": { "IsOnAdminMenu": true },
            "ChatModeProfileSettings": { "ChatMode": "TextInput" }
          }
        }
      ]
    }
  ]
}
```

- The step is source-agnostic: omit `Source` and name deployments with `ChatDeploymentName` and
  `UtilityDeploymentName`. Leave them empty to use the site defaults.
- `TitleType` is `InitialPrompt` or `Generated`.
- `AIDeploymentParametersMetadata.Values` (chat deployment) and `.UtilityValues` (utility deployment) hold
  model parameter choices; a parameter the deployment does not declare is never sent.
- `AIProfileMetadata.InitialPrompt` is the **Opening message** (UI label "Start the conversation
  automatically"); the property name did not change.
- `ChatModeProfileSettings` holds `ChatMode` (`TextInput`, `AudioInput`, `Conversation`),
  `ConversationDeploymentName`, `VoiceName` and `EnableTextToSpeechPlayback`. See
  `orchardcore-ai-realtime-voice`.
- For an `Agent`, add `"AgentMetadata": { "Availability": "OnDemand" }` (or `AlwaysAvailable`) to `Properties`.
- To start from a profile template use `CreateAIProfileFromTemplate` (`orchardcore-ai-profile-templates`).

### Profile in a migration

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using OrchardCore.Data.Migration;

public sealed class SupportProfileMigrations : DataMigration
{
    private readonly IAIProfileManager _profileManager;

    public SupportProfileMigrations(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _profileManager.NewAsync();

        profile.Name = "support-assistant";
        profile.DisplayText = "Support Assistant";
        profile.Type = AIProfileType.Chat;

        profile.WithSettings(new AIProfileSettings
        {
            LockSystemMessage = true,
            IsRemovable = false,
        });

        profile.AlterSettings<ChatModeProfileSettings>(settings =>
        {
            settings.ChatMode = ChatMode.TextInput;
        });

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "You are a helpful support assistant.",
            Temperature = 0.3f,
            MaxTokens = 2048,
        });

        await _profileManager.CreateAsync(profile);

        return 1;
    }
}
```

Settings classes (`AIProfileSettings`, `ChatModeProfileSettings`, `ResponseHandlerProfileSettings`,
`AIChatProfileSettings`) live on `profile.Settings`: read them with `GetSettings<T>()`/`TryGetSettings<T>()`
and write them with `WithSettings`/`AlterSettings<T>()`. Metadata (`AIProfileMetadata`, `AgentMetadata`,
`PromptTemplateMetadata`) lives on `Properties`: use `Put`, `TryGet` and `GetOrCreate`.
`IAIProfileManager` is an `INamedCatalogManager<AIProfile>`: `NewAsync`, `ValidateAsync`, `CreateAsync`,
`UpdateAsync`, `FindByNameAsync`, `DeleteAsync`, plus `GetAsync(AIProfileType)`. There is no `SaveAsync`.
Register the migration with `services.AddDataMigration<SupportProfileMigrations>()`. Framework-level profile
construction is covered by `crestapps-core-ai-profiles`.

## Resolving deployments and clients in code

Pick a deployment for a slot, then create the client from it. The purpose-based APIs are gone.

```csharp
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;

public sealed class TranscriptionService
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;

    public TranscriptionService(IAIDeploymentManager deploymentManager, IAIClientFactory clientFactory)
    {
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
    }

    public async Task<string> TranscribeAsync(Stream audio, string deploymentName = null)
    {
        var deployment = await _deploymentManager.ResolveSlotAsync(
            AIDeploymentSlotNames.SpeechToText,
            deploymentName: deploymentName);

        if (deployment is null)
        {
            return null;
        }

        using var client = await _clientFactory.CreateSpeechToTextClientAsync(deployment);
        var response = await client.GetTextAsync(audio);

        return response.Text;
    }
}
```

- `ResolveSlotAsync(slot, deploymentName, clientName, fallbackDeploymentNames, cancellationToken)` walks
  the requested name, the site default for the slot, the fallback slot (utility falls back to chat), then
  the first capable deployment.
- `IAIClientFactory` creates chat clients, embedding generators, image generators, speech-to-text,
  text-to-speech and realtime clients from an `AIDeployment`. Each overload accepts a builder callback,
  for example `builder => builder.UseDefaultResilience().UseUsageLabels(purpose: "MyPurpose")`.
- Voices for a text-to-speech deployment come from `ISpeechVoiceResolver.GetSpeechVoicesAsync(deployment)`.
- Slots, capabilities and the deployment pickers are covered by `orchardcore-ai-model-capabilities`;
  usage labels by `orchardcore-ai-usage-analytics`.

## Recipes and deployment steps

| Recipe step | Deployment step (title) | Export selector |
| --- | --- | --- |
| `AIProviderConnections` | AI Provider Connections | `ConnectionIds` |
| `AIDeployment` | AI Deployments | `DeploymentNames` |
| `DeleteAIDeployments` | Delete AI Deployments | `DeploymentNames` |
| `AIProfile` | AI Profiles | `ProfileNames` |
| `AIProfileTemplate`, `CreateAIProfileFromTemplate` | AI Profile Templates | `TemplateNames` |
| `AIToolInstances` | AI Tool Instances | `InstanceIds` |
| `AIPersonas` | AI Personas | `PersonaIds` |

Every deployment step supports `IncludeAll`. Exported connections have their API keys blanked. The
CrestApps Recipes feature publishes the JSON schemas of these steps (see `orchardcore-crestapps-recipes`).

## Upgrading from 2.x

- `AIDeploymentPurpose`, `AIDeploymentType`, `AIDeployment.Purpose`/`Type`, `IsDefault`,
  `GetByPurposeAsync`, `GetAllByPurposeAsync` and `ResolveOrDefaultAsync(purpose, ...)` are removed. Use
  `GetAllBySlotAsync(AIDeploymentSlotNames.X)` and `ResolveSlotAsync(AIDeploymentSlotNames.X, ...)`.
- Replace `"Purpose"` in recipes and `appsettings.json` with `Properties.AIDeploymentMetadata.Features`.
  Stored records with a legacy purpose keep working because they are projected onto capabilities on read.
- `ChatMode.Realtime` and `RealtimeDeploymentName` are removed; a spoken conversation uses
  `ChatModeProfileSettings.ConversationDeploymentName` with `ChatMode.Conversation`.
- Read `GeneralAIOptions` through `IOptionsMonitor<GeneralAIOptions>`; `IOptions<T>` never sees a settings change.
- The profile editor's **Apply Templates** card is gone; **Add Profile** opens the **New AI Profile** picker.
- Admin list overrides must target the `AdminList` shapes; a driver that adds a row shape must also give it a
  `SummaryAdminGrid` location.

## Related skills

- `orchardcore-ai-connections` and `orchardcore-ai-providers` for connections and provider setup.
- `orchardcore-ai-model-capabilities` for capabilities, parameters and slots.
- `orchardcore-ai-usage-analytics` for metered usage and the AI Usage Analytics report.
- `orchardcore-ai-profile-templates` and `orchardcore-ai-prompting` for templates.
- `orchardcore-ai-chat`, `orchardcore-ai-chat-interactions`, `orchardcore-ai-realtime-voice` for chat surfaces.
- `orchardcore-ai-tools`, `orchardcore-ai-agent` and `orchardcore-ai-workflows` for tools, agents and workflows.
- `crestapps-core-ai-profiles` and `crestapps-core-ai-runtime` for the CrestApps.Core APIs.

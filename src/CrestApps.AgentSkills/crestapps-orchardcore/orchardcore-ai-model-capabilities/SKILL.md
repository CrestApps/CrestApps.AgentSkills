---
name: orchardcore-ai-model-capabilities
description: Skill for AI deployment model capabilities, model parameters and deployment slots in CrestApps.OrchardCore 3.0, which replace the removed AIDeploymentPurpose and AIDeploymentType. Covers the Model capabilities card on AI deployments, AIDeploymentMetadata Features and Parameters in recipes and appsettings.json, AIDeploymentFeatureNames such as textGeneration, toolCalling, textEmbedding and realtime, the reasoningEffort parameter, AIDeploymentSlotNames, IAIDeploymentManager.GetAllBySlotAsync and ResolveSlotAsync, GetSelectListBySlotAsync, IAIDeploymentCapabilityService, AIDeploymentParametersMetadata on profiles, AddAIDeploymentFeature, AddAIDeploymentParameter and AddAIDeploymentSlot, the read-time projection of legacy Purpose values and the stored-deployment capability migration. Use this skill when requests mention AI deployment capabilities, deployment slots, deployment purpose migration, model parameters, reasoning effort, or picking a chat, utility, embedding, vision or speech deployment in code.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Model Capabilities and Slots

A deployment says what its model can do (**capabilities**, also called trained features) and which
options it exposes (**model parameters**). Where a deployment gets used is a **slot**: each picker and each
resolver asks for a slot, and only deployments with the slot's required capability qualify. This replaces
the deployment *purpose* of 2.x. Everything here ships with the base `CrestApps.OrchardCore.AI` feature;
the types come from CrestApps.Core (`CrestApps.Core.AI.Models`, `CrestApps.Core.AI.Deployments`,
`CrestApps.Core.AI.Capabilities`).

## Guidelines

- Declare only what the model was trained for. An undeclared capability is never offered or sent; a
  falsely declared one is sent and rejected by the provider.
- At least one capability is required on every deployment.
- `textGeneration` is opt-out: a deployment with no capability metadata at all is treated as text capable.
  Every other capability is opt-in.
- Pick deployments by slot (`AIDeploymentSlotNames`), never by name pattern or provider.
- Resolve the utility slot through `ResolveSlotAsync` with the chat deployment as a fallback, not with two
  independent resolves joined by `??`.

## Capabilities

| Feature name (`AIDeploymentFeatureNames`) | Meaning |
| --- | --- |
| `textGeneration` | Answers text chat completions |
| `toolCalling` | Calls tools supplied with the request |
| `structuredOutputs` | Returns JSON that follows a supplied schema |
| `streaming` | Streams response updates |
| `reasoning` | Reasons before answering; unlocks `reasoningEffort` |
| `textEmbedding` | Generates embedding vectors (dedicated embedding models only) |
| `imageInput` | Understands images (fills the vision slot) |
| `imageOutput` | Generates images |
| `audioInput`, `audioOutput`, `videoInput`, `videoOutput` | Accepts or produces audio or video |
| `speechToText` | Dedicated transcription model such as Whisper |
| `textToSpeech` | Dedicated speech synthesis model |
| `realtime` | Holds speech-to-speech sessions. Excluded from the chat and utility slots |

New deployments start from the features each provider marks as enabled by default (text conversation,
tool calling and streaming for a chat model).

## Slots

| Slot (`AIDeploymentSlotNames`) | Value | Required capability | Site default (`DefaultAIDeploymentSettings`) |
| --- | --- | --- | --- |
| `Chat` | `chat` | `textGeneration`, excludes `realtime` | `DefaultChatDeploymentName` |
| `Utility` | `utility` | `textGeneration`, excludes `realtime`; falls back to `chat` | `DefaultUtilityDeploymentName` |
| `Embedding` | `embedding` | `textEmbedding` | `DefaultEmbeddingDeploymentName` |
| `Image` | `image` | `imageOutput` | `DefaultImageDeploymentName` |
| `Vision` | `vision` | `imageInput` | `DefaultVisionDeploymentName` |
| `SpeechToText` | `speechToText` | `speechToText` | `DefaultSpeechToTextDeploymentName` |
| `TextToSpeech` | `textToSpeech` | `textToSpeech` | `DefaultTextToSpeechDeploymentName` |
| `Realtime` | `realtime` | `realtime` | `DefaultRealtimeDeploymentName` |

Site defaults are set under **Settings > Artificial Intelligence > Default Deployments** or with the
`Settings` recipe step (`DefaultAIDeploymentSettings`, see `orchardcore-ai`). The chat deployment picker
on profiles, templates and chat interactions lists text-capable deployments; a speech-to-speech model goes
in the separate **Conversation deployment** picker (see `orchardcore-ai-realtime-voice`).

## Declare capabilities

In the admin, open **Artificial Intelligence > Deployments**, edit a deployment and use the
**Model capabilities** card (**Trained features** plus **Model parameters**). In recipes and configuration
the same data lives under `Properties.AIDeploymentMetadata`:

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "reasoning-default",
          "ClientName": "OpenAI",
          "ConnectionName": "openai-cloud",
          "ModelName": "o4-mini",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textGeneration", "reasoning", "streaming", "toolCalling" ],
              "Parameters": {
                "reasoningEffort": {
                  "AllowedValues": [ "Low", "Medium", "High" ],
                  "DefaultValue": "Medium"
                }
              }
            }
          }
        },
        {
          "Name": "vision-default",
          "ClientName": "OpenAI",
          "ConnectionName": "openai-cloud",
          "ModelName": "gpt-4.1-mini",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textGeneration", "imageInput", "streaming" ]
            }
          }
        }
      ]
    }
  ]
}
```

The `appsettings.json` form is identical inside `OrchardCore:CrestApps:AI:Deployments[]`.

### Model parameters

- `reasoningEffort` is the parameter that ships. It is a choice parameter with values `None` (shown as
  *Minimal*), `Low`, `Medium`, `High`, `ExtraHigh`, default `Medium`, and requires `reasoning`.
- A deployment narrows a parameter with `AllowedValues` and `DefaultValue`, and numeric parameters with
  `Minimum`, `Maximum` and `Step`. A parameter not listed under `Parameters` is not exposed.
- Profiles, profile templates and chat interactions show a **Model parameters** section that follows the
  selected deployment. Profile choices are stored in `AIDeploymentParametersMetadata`:

```json
{
  "Properties": {
    "AIDeploymentParametersMetadata": {
      "Values": { "reasoningEffort": "High" },
      "UtilityValues": { "reasoningEffort": "Low" }
    }
  }
}
```

**Use deployment default** stores nothing, and a request carrying a value the deployment does not allow
falls back to its `DefaultValue`.

## Pick and resolve deployments in code

```csharp
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Mvc.Rendering;

public sealed class SummaryDeploymentResolver
{
    private readonly IAIDeploymentManager _deploymentManager;

    public SummaryDeploymentResolver(IAIDeploymentManager deploymentManager)
    {
        _deploymentManager = deploymentManager;
    }

    // For a display driver's drop-down: deployments that can fill the slot, grouped by connection.
    public Task<IEnumerable<SelectListItem>> GetPickerItemsAsync()
        => _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.Utility);

    // Utility work: the selected utility deployment, else the site utility default,
    // else the profile's chat deployment, else the site chat default, else the first capable one.
    public ValueTask<AIDeployment> ResolveAsync(AIProfile profile, CancellationToken cancellationToken)
        => _deploymentManager.ResolveSlotAsync(
            AIDeploymentSlotNames.Utility,
            deploymentName: profile.UtilityDeploymentName,
            fallbackDeploymentNames: new Dictionary<string, string>
            {
                [AIDeploymentSlotNames.Chat] = profile.ChatDeploymentName,
            },
            cancellationToken: cancellationToken);
}
```

- `GetAllBySlotAsync(slotName, clientName, cancellationToken)` lists qualifying deployments;
  `GetSelectListBySlotAsync(slotName, standaloneGroupName)` (in `CrestApps.OrchardCore.AI.Core`) turns them
  into `SelectListItem`s grouped by connection, labelled `name (model)` when the two differ.
- `ResolveSlotAsync(slotName, deploymentName, clientName, fallbackDeploymentNames, cancellationToken)`
  returns `null` when nothing qualifies; handle that.
- Pass `clientName` to keep a companion deployment on the same provider, for example an embedding
  deployment for a chat deployment's provider:
  `ResolveSlotAsync(AIDeploymentSlotNames.Embedding, clientName: chatDeployment?.ClientName)`.

### Inspect a deployment's capabilities

`IAIDeploymentCapabilityService` (`CrestApps.Core.AI.Capabilities`) merges the registered definitions with
what a deployment declares:

```csharp
var capabilities = _capabilityService.GetCapabilities(deployment);

if (capabilities.SupportsFeature(AIDeploymentFeatureNames.Realtime))
{
    // A speech-to-speech model: do not send it a text completion.
}

var canChat = _capabilityService.SupportsFeatureOrUnconstrained(deployment, AIDeploymentFeatureNames.TextGeneration);
var realtimeDeployments = await _capabilityService.GetDeploymentsWithFeatureAsync(AIDeploymentFeatureNames.Realtime);
```

`AIDeploymentMetadata.SupportsFeature(name)` answers for the declared metadata alone.

## Register features, parameters and slots

A module or provider adds definitions without changing the framework:

```csharp
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Localization;

services.AddAIDeploymentFeature(
    "citations",
    new LocalizedString("citations", "Native citations"),
    feature =>
    {
        feature.Category = "Trained Features";
        feature.EnabledByDefault = false;
    });

services.AddAIDeploymentParameter(
    "citationStyle",
    new LocalizedString("citationStyle", "Citation style"),
    parameter =>
    {
        parameter.Kind = AIDeploymentParameterKind.Choice;
        parameter.AllowedValues =
        [
            new AIDeploymentParameterOption { Value = "inline", DisplayName = new LocalizedString("inline", "Inline") },
            new AIDeploymentParameterOption { Value = "footnote", DisplayName = new LocalizedString("footnote", "Footnote") },
        ];
        parameter.DefaultValue = "inline";
        parameter.RequiredFeature = "citations";
    });
```

`AIDeploymentParameterKind` is `Choice`, `Number`, `Integer`, `Boolean` or `Text`.
`services.AddAIDeploymentSlot(name, displayName, slot => { ... })` registers a slot; its descriptor sets
`RequiredFeature`, `ExcludedFeature`, `FallbackSlotName`, `AllowUnconstrained`, `Order` and
`GetDefaultDeploymentName` (a `Func<DefaultAIDeploymentSettings, string>`). A parameter is only useful
when something binds it into the request; the shipped `reasoningEffort` binder lives in CrestApps.Core.

## Upgrading from deployment purposes

No action is required for stored data. A record that still carries `Purpose`, `Capability` or `Type` (in
the store, a recipe, `appsettings.json` or the API) is projected onto capabilities whenever it is read:

| Legacy purpose | Capabilities |
| --- | --- |
| `Chat`, `Utility` | `textGeneration`, `toolCalling`, `streaming` (none if the deployment declares `realtime`) |
| `Embedding` | `textEmbedding` |
| `Image` | `imageOutput` |
| `Vision` | `imageInput` |
| `SpeechToText` | `speechToText` |
| `TextToSpeech` | `textToSpeech` |

The `AIDeploymentTypeMigrations` step (`UpdateFrom7`) also rewrites stored deployments so their JSON
declares these capabilities and drops the legacy field. Configuration deployments are untouched (they are
read-only and projected on every read). Code changes:

| 2.x | 3.0 |
| --- | --- |
| `GetByPurposeAsync(AIDeploymentPurpose.Chat)` / `GetAllByPurposeAsync` | `GetAllBySlotAsync(AIDeploymentSlotNames.Chat)` |
| `ResolveOrDefaultAsync(AIDeploymentPurpose.X, deploymentName: n)` | `ResolveSlotAsync(AIDeploymentSlotNames.X, deploymentName: n)` |
| `AIDeployment.Purpose`, `Type`, `SupportsPurpose`, `SupportsType`, `CanServeTextCompletion` | `IAIDeploymentCapabilityService.GetCapabilities(deployment).SupportsFeature(...)` |
| `"Purpose": "Embedding"` in recipes | `"Properties": { "AIDeploymentMetadata": { "Features": [ "textEmbedding" ] } }` |
| The **Purposes** checkboxes | The **Model capabilities** card |

Before 3.0 a deployment that had never declared capabilities could be saved with `Features = []`; it is
now offered the defaults instead.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| A deployment is missing from a picker | It does not declare the slot's required capability, or it declares `realtime` (excluded from chat and utility) |
| Tools or streaming stopped after an import | The recipe declared only `textGeneration`; add `toolCalling` and `streaming` |
| A parameter does not show on a profile | The selected deployment does not list it under `Parameters`, or its `RequiredFeature` is not declared |
| Background work runs on an unexpected model | Resolve utility with `fallbackDeploymentNames` carrying the chat deployment |

## Related skills

- `orchardcore-ai` for deployments, defaults and profiles.
- `orchardcore-ai-providers` for per-provider deployment examples.
- `orchardcore-ai-realtime-voice` for the realtime slot, conversation deployments and cascaded realtime.
- `orchardcore-ai-usage-analytics` for metering the clients created from these deployments.

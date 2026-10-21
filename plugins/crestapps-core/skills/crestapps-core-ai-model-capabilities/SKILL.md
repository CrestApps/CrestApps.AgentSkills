---
name: crestapps-core-ai-model-capabilities
description: Skill for metadata-driven deployment capabilities in CrestApps.Core 2.0. Covers trained model features such as textGeneration, toolCalling, streaming, reasoning, realtime, textEmbedding, speechToText and textToSpeech, model parameters such as reasoningEffort, declaring them with AIDeploymentMetadata or the Features and Parameters configuration shorthand for Azure App Service settings, registering custom features, parameters and slots with AddAIDeploymentFeature, AddAIDeploymentParameter, AddAIDeploymentSlot and AIDeploymentCapabilityOptions, IAIDeploymentCapabilityService, deployment slots and their resolution chain, AIDeploymentParametersMetadata chat and utility values, IAIDeploymentParameterBinder, UseModelParameters, capability enforcement, the ChatApi Responses routing for reasoning with tools, capability-aware chat UIs, and migrating from AIDeploymentPurpose. Use it when declaring what a model supports, adding a new knob, or fixing stripped tools, missing pickers, or reasoning-effort errors.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Model Capabilities

A deployment declares what its model can do. Two kinds of definitions are registered once at startup and referenced by name:

| Concept | Shape | Example |
|---|---|---|
| **Feature** | A trained capability the model has or lacks | `toolCalling`, `reasoning`, `realtime` |
| **Parameter** | A configurable option with a kind, allowed values, range and default | `reasoningEffort` |
| **Slot** | What this installation uses a deployment for; pairs a required feature with a site default | `chat`, `embedding`, `realtime` |

Capabilities live on the deployment. Slots are derived from them: declaring `textEmbedding` is what puts a model in the embedding picker. There is no purpose flag to set.

## Guidelines

- `AddCoreAIServices()` already calls `AddCoreAIDeploymentCapabilities()`, which calls `AddCoreAIDeploymentSlots()`. Call them directly only in hosts that compose raw registrations.
- Declare only trained traits as features. Provider-hosted tools (web search, computer use) are ordinary tools, not features.
- `textGeneration` is opt-out: a deployment with no metadata counts as text-capable and is unconstrained. Every other feature is opt-in. Once a deployment declares any metadata, everything it omits is enforced as unsupported.
- `speechToText` and `textToSpeech` mean a dedicated transcription or synthesis endpoint. `audioInput` and `audioOutput` mean a chat model that accepts or emits audio inline. Never mix them.
- Use `IAIDeploymentCapabilityService` to ask what a deployment supports. Do not read `AIDeploymentMetadata` directly in business code.
- See `crestapps-core-ai-runtime` for slot resolution in everyday code and the provider skills for provider-specific features.

## Built-in Definitions

| Feature | Constant | Default on new deployments |
|---|---|---|
| `textGeneration` | `AIDeploymentFeatureNames.TextGeneration` | yes |
| `toolCalling` | `AIDeploymentFeatureNames.ToolCalling` | yes |
| `streaming` | `AIDeploymentFeatureNames.Streaming` | yes |
| `structuredOutputs`, `reasoning`, `imageInput`, `imageOutput`, `audioInput`, `audioOutput`, `videoInput`, `videoOutput`, `realtime`, `textEmbedding`, `speechToText`, `textToSpeech` | matching `AIDeploymentFeatureNames` constants | no |

`reasoningEffort` (`AIDeploymentParameterNames.ReasoningEffort`) is a `Choice` parameter with values `None` (shown as Minimal), `Low`, `Medium`, `High`, `ExtraHigh`, default `Medium`, and `RequiredFeature = reasoning`. It binds to `ChatOptions.Reasoning.Effort`.

## Declaring Capabilities

In configuration, use the shorthand on the entry. It merges with `Properties:AIDeploymentMetadata` (shorthand features are added; each shorthand parameter overrides the declared one field by field):

```json
{
  "CrestApps": {
    "AI": {
      "Deployments": [
        {
          "ClientName": "OpenAI",
          "Name": "gpt-5-chat",
          "ModelName": "gpt-5",
          "ConnectionName": "openai",
          "Features": [ "textGeneration", "toolCalling", "streaming", "reasoning" ],
          "Parameters": {
            "reasoningEffort": { "AllowedValues": [ "None", "Low", "Medium", "High" ], "DefaultValue": "None" }
          }
        },
        {
          "ClientName": "OpenAI",
          "Name": "gpt-realtime",
          "ModelName": "gpt-realtime",
          "ConnectionName": "openai",
          "Features": [ "realtime" ]
        }
      ]
    }
  }
}
```

The shorthand exists because Azure App Service rejects setting names over 100 characters:

```text
CrestApps__AI__Deployments__0__Features__0 = textGeneration
CrestApps__AI__Deployments__0__Features__1 = toolCalling
CrestApps__AI__Deployments__0__Features__2 = reasoning
CrestApps__AI__Deployments__0__Parameters__reasoningEffort__DefaultValue = None
CrestApps__AI__Deployments__0__Parameters__reasoningEffort__AllowedValues__0 = None
```

In code, put `AIDeploymentMetadata` on the deployment's `Properties`:

```csharp
deployment.Put(new AIDeploymentMetadata
{
    Features = [AIDeploymentFeatureNames.TextGeneration, AIDeploymentFeatureNames.ToolCalling, AIDeploymentFeatureNames.Reasoning],
    Parameters = new Dictionary<string, AIDeploymentParameter>(StringComparer.OrdinalIgnoreCase)
    {
        [AIDeploymentParameterNames.ReasoningEffort] = new AIDeploymentParameter
        {
            AllowedValues = ["Low", "Medium", "High"],
            DefaultValue = "Medium",
        },
    },
});
```

A deployment-level `AIDeploymentParameter` may narrow `AllowedValues`, override `DefaultValue` (ignored when invalid), and override `Minimum`, `Maximum` and `Step`. `DefaultValue` and `AllowedValues` are kept as the exact text written, and numbers are read culture-invariantly.

A speech-to-speech-only model declares `realtime` and omits `textGeneration`. The `chat` and `utility` slots exclude `realtime`, so it never fills a text slot.

## Asking What a Deployment Supports

```csharp
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Models;

var capabilities = await capabilityService.GetCapabilitiesAsync("gpt-5-chat");

if (capabilities.SupportsFeature(AIDeploymentFeatureNames.Reasoning))
{
    var effort = capabilities.GetParameter(AIDeploymentParameterNames.ReasoningEffort);
}

var isTextCapable = capabilityService.SupportsFeatureOrUnconstrained(deployment, AIDeploymentFeatureNames.TextGeneration);
var isRealtime = await capabilityService.IsRealtimeDeploymentAsync("gpt-realtime");
var realtimeDeployments = await capabilityService.GetDeploymentsWithFeatureAsync(AIDeploymentFeatureNames.Realtime);
var vision = await capabilityService.ResolveDeploymentWithFeatureAsync(AIDeploymentFeatureNames.ImageInput, "gpt-5-chat");
```

`GetCapabilities` drops a parameter whose `RequiredFeature` is not declared, so a `reasoningEffort` value is never applied to a non-reasoning model. Descriptors are cloned; deployment overrides never mutate the registry.

## Slots

| Slot | Required | Excluded | Falls back to |
|---|---|---|---|
| `chat` | `textGeneration` | `realtime` | |
| `utility` | `textGeneration` | `realtime` | `chat` |
| `embedding` | `textEmbedding` | | |
| `image` | `imageOutput` | | |
| `vision` | `imageInput` | | |
| `speechToText` | `speechToText` | | |
| `textToSpeech` | `textToSpeech` | | |
| `realtime` | `realtime` | | |

`ResolveSlotAsync` walks one chain: explicit name, slot site default (`DefaultAIDeploymentSettings`), explicit name for the fallback slot, fallback site default, and only then the first capable deployment. `GetAllBySlotAsync` lists every eligible deployment for pickers.

```csharp
services.AddAIDeploymentSlot("moderation", new LocalizedString("moderation", "Moderation"), slot =>
{
    slot.RequiredFeature = AIDeploymentFeatureNames.TextGeneration;
    slot.ExcludedFeature = AIDeploymentFeatureNames.Realtime;
    slot.GetDefaultDeploymentName = static settings => settings.DefaultUtilityDeploymentName;
    slot.FallbackSlotName = AIDeploymentSlotNames.Utility;
    slot.AllowUnconstrained = true;
});
```

`AllowUnconstrained` lets a deployment with no metadata fill the slot; set it only for text slots.

## Registering Features and Parameters

`AddAIDeploymentFeature` and `AddAIDeploymentParameter` configure `AIDeploymentCapabilityOptions`. Registering an existing name updates its descriptor, so a provider module can refine another module's definition. You can also call `services.Configure<AIDeploymentCapabilityOptions>(o => o.AddFeature(...).AddParameter(...))` directly.

Parameter kinds (`AIDeploymentParameterKind`) are `Choice` (needs `AllowedValues`), `Number`, `Integer` (both honor `Minimum`, `Maximum`, `Step`), `Boolean` and `Text`. Values that are not valid for the kind fall back to the deployment default with a warning.

A complete custom parameter with its binder, declaration and stored value is in [model-capabilities-examples.md](references/model-capabilities-examples.md).

## Selected Values and Runtime Binding

Profiles, profile templates and chat interactions store selections in `AIDeploymentParametersMetadata`: `Values` for the chat deployment and `UtilityValues` for the utility deployment. Context builder handlers copy them onto `AICompletionContext.ModelParameters` and `UtilityModelParameters`. Setting `AICompletionContext.IsUtilityCompletion` makes the completion client resolve the utility deployment (falling back to chat) and apply the utility values.

Markdown profile templates accept `ModelParameters` and `UtilityModelParameters` front-matter keys with `name=value` pairs separated by `;` or new lines.

At request time:

1. `ModelParametersAICompletionServiceHandler` calls `IAIDeploymentParameterApplier.ApplyAsync(options, deployment, context, scope)` with `AIDeploymentParameterScope.Chat` or `Utility`.
2. Only parameters the deployment exposes are applied. A missing or invalid value uses the deployment default.
3. A matching `IAIDeploymentParameterBinder` shapes the request; otherwise the value goes to `ChatOptions.AdditionalProperties` as its primitive type.
4. `ModelFeaturesAICompletionServiceHandler` and the terminal `CapabilityEnforcingChatClient` remove tools and `ToolMode` without `toolCalling`, a JSON response format without `structuredOutputs`, and reasoning options without `reasoning`, and coerce an unsupported effort to the default. Without `streaming`, a streaming call is completed once and replayed as one update.

Code that resolves an `IChatClient` directly applies the same selections through the pipeline:

```csharp
var client = await clientFactory.CreateChatClientAsync(utilityDeployment, builder => builder
    .UseDefaultResilience()
    .UseUtilityModelParameters(parameterApplier, utilityDeployment, profile));

var chatClient = await clientFactory.CreateChatClientAsync(chatDeployment, builder => builder
    .UseModelParameters(parameterApplier, chatDeployment, completionContext, AIDeploymentParameterScope.Chat));
```

`AzureOpenAICompletionClient` builds SDK options directly, so a custom parameter without a binder cannot be forwarded there and is logged as a warning. `reasoningEffort` works on both paths; `ExtraHigh` is clamped to `High` on Azure and `None` is sent as `none`.

The Copilot and Claude orchestrators keep their own effort settings (`CopilotReasoningEffort`, `ClaudeEffortLevel`); deployment parameters do not apply to them.

## Reasoning with Tools on the Responses API

Reasoning models reject function tools together with a reasoning effort on `/v1/chat/completions`. On OpenAI's own endpoint and on Azure OpenAI, a request with tools and an effort other than `None` is sent to the Responses API (with `store: false`); everything else stays on Chat Completions. The connection's `ChatApi` setting (`OpenAIChatApis.Auto`, `ChatCompletions`, `Responses`) controls this; `Auto` keeps other OpenAI-compatible endpoints on Chat Completions. A reasoning deployment must declare the `reasoningEffort` parameter, or no effort is sent and Chat Completions rejects the tools.

## Capability-Aware Chat UIs

Choose the chat input from the effective deployment, not a global setting. Render a text box for text-capable deployments and only a "Start speaking" control when `IsRealtimeDeploymentAsync` is true. When the deployment picker can change without a reload, send the client the names returned by `GetDeploymentsWithFeatureAsync(AIDeploymentFeatureNames.Realtime)` and switch the input on change.

## Troubleshooting

- **The model describes a tool call but never makes one**: the deployment declares metadata without `toolCalling`. Look for the warning "does not declare the 'toolCalling' feature".
- **HTTP 400 about tools and reasoning effort**: declare `reasoning` and `reasoningEffort` (a `None` default for models that need it), and keep `ChatApi` at `Auto` or `Responses`.
- **A deployment is missing from a picker**: it does not declare the slot's required feature, or it declares the excluded `realtime` feature.
- **A configured deployment disappeared**: it declared no `Features`, or one of its values could not be read; the log names the entry.

## Upgrading from 1.x

- `AIDeploymentPurpose` and `AIDeploymentType` are replaced by capabilities plus slots. Replace `ResolveOrDefaultAsync(AIDeploymentPurpose.X, ...)` with `ResolveSlotAsync(AIDeploymentSlotNames.X, ...)` and `GetByPurposeAsync` or `GetAllByPurposeAsync` with `GetAllBySlotAsync`.
- Stored records and configuration that carry `Purpose`, `Capability` or `Type` are projected when read (`AIDeployment.OnDeserialized` and `AIDeploymentCatalogHandler`): `Embedding`, `Image`, `Vision`, `SpeechToText` and `TextToSpeech` add `textEmbedding`, `imageOutput`, `imageInput`, `speechToText` and `textToSpeech`; `Chat` or `Utility` adds `textGeneration` unless `realtime` is declared. Nothing else is added, so declare `toolCalling` and `streaming` on chat deployments.
- Rewriting stored JSON is optional cleanup. A host that kept the purpose in its own table column must migrate or stop reading it.
- Validation now says "At least one model capability is required".
- The changelog's early names (`AddAIModelFeature`, `IAIModelCapabilityService`, `AIModelFeatureDescriptor`) shipped as `AddAIDeploymentFeature`, `IAIDeploymentCapabilityService` and `AIDeploymentFeatureDescriptor`.

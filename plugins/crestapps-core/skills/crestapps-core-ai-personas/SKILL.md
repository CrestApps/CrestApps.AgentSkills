---
name: crestapps-core-ai-personas
description: Skill for AI personas in CrestApps.Core 2.0, reusable characters with instructions, an assistant name, language, voice and avatar that AI profiles, chat interactions and profile templates link to by name. Covers opting in with AddPersonas and AddYesSqlStores or AddEntityCoreStores, CreateAIPersonaIndexSchemaAsync, IAIPersonaManager, persona Markdown files under Templates Personas and AddEmbeddedPersonas, linking with AIPersonaMetadata or the top-level PersonaId, how the persona block is prepended to the system message with the assistant_name Liquid variable, voice selection with IAIPersonaResolver SelectVoiceAsync, the ResolvePersonaAsync and SelectPersonaVoiceAsync service-provider helpers, showing the assistant name and avatar with AIChatAssistantDisplay, and custom IAIPersonaProvider sources. Use it when giving assistants a consistent identity across profiles, or when a persona's instructions, name or voice do not apply.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Personas

A persona says **who** the assistant is: its instructions, the name it uses, its language, voice and avatar. The profile's system message and prompt templates still say **what** it does. A resource links to one optional persona by its technical name, so editing the persona changes every profile, interaction and template that links to it from the next message. Personas are not AI templates: a template is copied when used, a persona stays linked.

## Guidelines

- Personas are opt-in. Without `AddPersonas(...)`, persona links are kept but ignored and chats run with the profile's own instructions and voice.
- Link by `Name`, never by item id. Names survive export and import across tenants; ids do not. A persona's name cannot change once saved.
- Keep business rules in the profile. The persona block comes first in the system message, so the profile's rules win a conflict.
- Greetings stay with the profile's welcome message so one persona can serve profiles that open differently.
- Resolve personas only through `IAIPersonaResolver` (or the service-provider helpers) so chat, chat interactions, realtime and voice behave the same.

## Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddPersonas(personas => personas
            .AddYesSqlStores()          // or .AddEntityCoreStores()
            .AddEmbeddedPersonas(typeof(Program).Assembly))
    )
);
```

`AddPersonas` (or `services.AddCoreAIPersonas()`) registers `IAIPersonaManager`, `IAIPersonaResolver`, the persona catalog handler, `AIPersonaCompletionContextBuilderHandler` and `AIPersonaFileSystemProvider`, which reads `Templates/Personas` in the content root. A YesSql host also creates the index table once with `schemaBuilder.CreateAIPersonaIndexSchemaAsync(yesSqlStoreOptions)` next to its other index tables.

## What a Persona Holds

| `AIPersona` property | Purpose |
|---|---|
| `Name` | Technical name links point to; fixed after save |
| `DisplayText`, `Description` | Editor display |
| `Instructions` | Required persona prompt, rendered as Liquid |
| `AssistantName` | Name the assistant uses; shown on assistant messages |
| `Language` | BCP-47 tag used as the speech language when the caller names none |
| `VoiceName`, `VoiceDeploymentName` | Provider-specific voice and the deployment it was chosen from |
| `AvatarUrl` | Optional avatar image |

## Managing Personas

`IAIPersonaManager` is an `INamedCatalogManager<AIPersona>`. Reads merge saved personas with every `IAIPersonaProvider`; a saved persona replaces a provided one with the same name. Only saved personas can be updated or deleted.

```csharp
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Profiles;

var persona = await personaManager.NewAsync("friendly-assistant", new JsonObject
{
    ["DisplayText"] = "Friendly assistant",
    ["Instructions"] = "You are a warm, patient assistant for {{ company }}. Keep answers short.",
    ["AssistantName"] = "Ava",
    ["Language"] = "en-US",
});

var validation = await personaManager.ValidateAsync(persona);

if (validation.Succeeded)
{
    await personaManager.CreateAsync(persona);
}
```

Validation requires a unique name and instructions.

## Persona Files

```markdown
---
Title: Friendly assistant
Description: Warm, patient and plain-spoken.
AssistantName: Ava
Language: en-US
---

You are Ava, a warm and patient assistant. Use plain words, keep answers short,
and ask one question at a time when you need more detail.
```

Place files in `Templates/Personas` under the content root, or embed them under `Templates/Personas` in an assembly and call `AddEmbeddedPersonas(assembly)` (or `services.AddCoreAIPersonasFromAssembly(assembly)`). The path below that folder, without extension, becomes the technical name.

## Linking a Persona

```csharp
using CrestApps.Core.AI.Models;

profile.Put(new AIPersonaMetadata
{
    PersonaId = "friendly-assistant",
    Parameters = new Dictionary<string, object>
    {
        ["company"] = "Contoso",
    },
});

await profileManager.UpdateAsync(profile);
```

- The same metadata works on `ChatInteraction` and `AIProfileTemplate`.
- Profile JSON accepts a top-level `PersonaId`: an empty value unlinks, an absent key keeps the current link.
- Chat interactions save it from the `personaId` hub setting through `PersonaChatInteractionSettingsHandler`.
- A profile template file may add `PersonaId: friendly-assistant` to its front matter; profiles created from it copy the link.
- New chat sessions record the linked persona in `AIChatSession.PersonaId` for reporting.

## How It Is Applied

`AIPersonaCompletionContextBuilderHandler` runs in `BuiltAsync`, after the profile or interaction handlers produced the system message. It resolves the persona and calls `ResolvedAIPersona.PrependTo(systemMessage)`:

```text
Your name is Ava.
persona instructions (Liquid, with link Parameters and assistant_name)

selected prompt templates

the profile's or interaction's own system message
```

The resolved persona is also stored under `AICompletionContextKeys.Persona` in `AICompletionContext.AdditionalProperties`, and realtime sessions read their instructions from the same completion context. A link parameter named `assistant_name` overrides the persona's name in the Liquid variables. A Liquid error drops the instructions but keeps the rest of the persona. A deleted or unknown persona is logged and the request carries on without it.

## Voice

The chat hubs pick a voice in one order: the explicit voice (the caller's, or the profile's or interaction's own), then the persona's, then the deployment default.

```csharp
var persona = await serviceProvider.ResolvePersonaAsync(profile, cancellationToken);

var voice = await serviceProvider.SelectPersonaVoiceAsync(
    persona,
    explicitVoice: requestedVoice,
    speakingDeploymentName: speakingDeployment.Name,
    defaultVoice: defaultVoice,
    cancellationToken);
```

The persona's voice is skipped, with a warning, when its `VoiceDeploymentName` belongs to a different provider than the speaking deployment; for a cascaded realtime deployment the text-to-speech leg is compared. The persona's `Language` becomes the speech language when the caller names none. The `IServiceProvider` helpers return `null` (or the explicit and default voice) when personas are not registered, so shared code works in either kind of host.

## Assistant Name and Avatar in Chat

```csharp
using CrestApps.Core.AI.Profiles;

AIChatAssistantDisplay display = await serviceProvider.ResolveChatAssistantDisplayAsync(profile, cancellationToken);

var label = display.GetAssistantLabel("Assistant");
var avatarUrl = display.AvatarUrl;
```

Pass the values to `@crestapps/ai-chat-ui` as `assistantLabel` and `assistantAvatarUrl` (`data-coreai-chat-assistant-avatar-url`). A custom message template calls `getAssistantAvatarUrl(message)`; a custom typing indicator can use the `{{ assistantIcon }}` token. Without a persona the chat keeps its label and robot icon. `IAIPersonaResolver.ResolveChatAssistantDisplayAsync(resource)` and `ResolvedAIPersona.ToChatAssistantDisplay()` return the same shape.

## Custom Persona Source

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

public sealed class BrandPersonaProvider : IAIPersonaProvider
{
    public Task<IReadOnlyList<AIPersona>> GetPersonasAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AIPersona> personas =
        [
            new AIPersona
            {
                Name = "brand-voice",
                DisplayText = "Brand voice",
                Instructions = "You speak for Contoso: friendly, concise, never salesy.",
                AssistantName = "Contoso Assistant",
                Language = "en-US",
            },
        ];

        return Task.FromResult(personas);
    }
}
```

```csharp
builder.Services.AddSingleton<IAIPersonaProvider, BrandPersonaProvider>();
```

Provided personas are read-only; saving one with the same name in the persona store replaces it.

## Related Skills

- `crestapps-core-ai-profiles` for the profile fields a persona complements.
- `crestapps-core-context-builders` for where the persona handler runs.
- `orchardcore-ai-personas` for the Orchard Core module wrapper.

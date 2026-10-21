---
name: orchardcore-ai-personas
description: Skill for AI Personas in Orchard Core with the CrestApps AI module. Covers the CrestApps.OrchardCore.AI.Personas feature, reusable linked characters with instructions, assistant name, language, voice and avatar, the Personas catalog and ManageAIPersonas permission, linking a persona from AI profiles, profile templates, chat interactions and the AI completion with configuration workflow task, the persona block in the system message, voice resolution for chat, realtime and Contact Center calls, the AIPersonas recipe step and AIPersonaDeploymentStep, PersonaId and AIPersonaMetadata on profiles, seeding personas with IAIPersonaManager, and shipping persona files with AddCoreAIPersonasFromAssembly or an IAIPersonaProvider. Use this skill when requests mention Orchard Core AI personas, assistant name, assistant avatar, persona voice, or persona recipes. Strong matches include AIPersona, IAIPersonaManager, IAIPersonaProvider, IAIPersonaResolver, and AIPersonaMetadata.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Personas - Prompt Templates

## Configure AI Personas

You are an Orchard Core expert. Generate recipes and code that give AI profiles, profile templates, chat interactions and workflow tasks a reusable persona with the CrestApps AI module.

### Guidelines

- Enable `CrestApps.OrchardCore.AI.Personas` (**AI Personas**, ships in the `CrestApps.OrchardCore.AI` package, depends on `CrestApps.OrchardCore.AI`). Install the package in the web/startup project.
- A persona answers **who** the assistant is: personality, tone, assistant name, language, voice and avatar. The profile's system instructions and prompt templates still answer **what**: the task, rules and tools.
- A persona stays **linked**. Unlike a profile template, which is copied when used, an edit to a persona applies to every profile and interaction that links to it from the next message.
- Profiles, templates and interactions link to the persona's technical `Name`, which cannot change once saved. One persona per item, and it is optional.
- A tenant starts with no personas. Create them in the admin, import them with the `AIPersonas` recipe step, or register them in code.
- Without the feature, stored links are kept but ignored.
- A deleted or unknown persona is logged and the request runs without one.
- Recipe JSON uses `{ "steps": [...] }` and C# classes are `sealed`, except view models.

### What the Feature Adds

| Item | Detail |
|---|---|
| Admin catalog | **Artificial Intelligence → Personas** (`ai/personas`, `ai/persona/create`, `ai/persona/edit/{id}`), guarded by `ManageAIPersonas` |
| Admin list | Orchard Core `AdminList` named `AIPersonas` (columns `Name`, `Details`, `Actions`); see `orchardcore-admin-lists` |
| Pickers | **AI persona** on the profile and Profile template **Instructions** tab, the chat interaction **Settings** panel, and the **AI completion with configuration** workflow task (needs `CrestApps.OrchardCore.AI.Chat.Interactions` and `OrchardCore.Workflows`) |
| Chat UI | Assistant name and avatar on assistant messages and the typing indicator in AI Chat, both widgets and Chat Interactions |
| Recipe step | `AIPersonas` (needs `OrchardCore.Recipes.Core`) |
| Deployment step | **AI Personas** (`AIPersonaDeploymentStep` with `IncludeAll` and `PersonaIds`, needs `OrchardCore.Deployment`) |
| Session record | `AIChatSession.PersonaId` records the persona a session started with |

### Create Personas and Link a Profile by Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Personas"
      ],
      "disable": []
    },
    {
      "name": "AIPersonas",
      "personas": [
        {
          "Name": "front-desk",
          "DisplayText": "Front desk",
          "Description": "Gracious and efficient.",
          "Instructions": "You are {{ assistant_name }}, a gracious, efficient front-desk assistant.",
          "AssistantName": "Ava",
          "Language": "en-US",
          "VoiceName": "alloy",
          "VoiceDeploymentName": "gpt-realtime",
          "AvatarUrl": "/media/avatars/ava.png"
        }
      ]
    },
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "support",
          "DisplayText": "Support",
          "Type": "Chat",
          "PersonaId": "front-desk"
        }
      ]
    }
  ]
}
```

- An `AIPersonas` entry is matched by `ItemId`, then by `Name`, and updated in place; otherwise it is created. `Name` and `Instructions` are required for a new persona. `DisplayText` defaults to `Name`. `CreatedUtc`, `OwnerId`, `Author` and `Properties` are kept from an export.
- On `AIProfile`, a top-level `PersonaId` links the persona; an empty value unlinks it and leaving the key out keeps the current link. Exported profiles carry the link in `Properties.AIPersonaMetadata` (`PersonaId`, plus optional Liquid `Parameters`).
- Profiles and personas can be imported in either order, because the link is the technical name.
- With `CrestApps.OrchardCore.Recipes` enabled, the tenant exposes JSON schemas for the step, the deployment step and the profile's `PersonaId`. See `orchardcore-crestapps-recipes`.

### What the Model Receives

On every request the persona block goes first in the system message:

1. "Your name is ..." when the persona has an assistant name
2. the persona's instructions (Liquid; `assistant_name` holds the assistant name)
3. the selected prompt templates
4. the profile's or interaction's own system instructions

Business rules come last, so they win any conflict with the persona's personality.

### Voice Resolution

| Surface | Order |
|---|---|
| Chat sessions and realtime calls | Voice chosen for the call or session, then the profile or interaction voice, then the persona voice, then the deployment default |
| Turn-based automated calls | Activity voice, then subject flow voice, then the persona voice (only when chosen on a deployment of the same provider as the call's text-to-speech deployment), then the telephony provider default |

- Voice names belong to one provider. The persona remembers `VoiceDeploymentName` and its voice is used only on deployments of the same provider; elsewhere the deployment default is used and the log says why.
- A persona `Language` sets the listening language of a spoken conversation when nothing else does, and turn-based calls speak and listen in it (`es-MX` listens for `es`). If the provider refuses the voice or language, the line is spoken again without the persona.
- On the profile editor, an empty **Voice** shows **Use persona voice (...)** when the persona has one.

See `orchardcore-ai-realtime-voice` and `orchardcore-telephony` for the call side.

### Seed Saved Personas in a Migration

Saved personas are ordinary catalog records: editors can change or delete them and they export.

```csharp
public sealed class PersonaMigrations : DataMigration
{
    private readonly IAIPersonaManager _personaManager;

    public PersonaMigrations(IAIPersonaManager personaManager)
    {
        _personaManager = personaManager;
    }

    public async Task<int> CreateAsync()
    {
        var persona = await _personaManager.NewAsync("front-desk", new JsonObject
        {
            ["DisplayText"] = "Front desk",
            ["Instructions"] = "You are {{ assistant_name }}, a gracious, efficient front-desk assistant.",
            ["AssistantName"] = "Ava",
        });

        var result = await _personaManager.ValidateAsync(persona);

        if (result.Succeeded)
        {
            await _personaManager.CreateAsync(persona);
        }

        return 1;
    }
}

[RequireFeatures("CrestApps.OrchardCore.AI.Personas")]
public sealed class PersonasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<PersonaMigrations>();
    }
}
```

### Ship Provided Personas as Files

A provided persona is read from code every time: every tenant that enables the module gets it, editors cannot delete it, it is marked **From file**, and it is not exported. Saving one in the editor creates a saved persona with the same name that replaces it; deleting that saved copy falls back to the file.

Put Markdown files in the module's `Templates/Personas` folder and register the assembly:

```csharp
[RequireFeatures("CrestApps.OrchardCore.AI.Personas")]
public sealed class PersonasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIPersonasFromAssembly(typeof(PersonasStartup).Assembly);
    }
}
```

`Templates/Personas/front-desk.md` (the file name is the technical name):

```markdown
---
Title: Front desk
Description: Gracious and efficient.
AssistantName: Ava
Language: en-US
---

You are {{ assistant_name }}, a gracious, efficient front-desk assistant.
```

A site can also drop such files into `Templates/Personas` under its content root. For any other source, implement `IAIPersonaProvider` and register it with `services.AddSingleton<IAIPersonaProvider, MyPersonaProvider>()`.

### Deleting

Profiles that use a deleted persona keep working without one. Deleting a persona that AI profiles still use shows how many were affected.

### Related Skills

- `orchardcore-ai-profile-templates` for blueprints that are copied rather than linked
- `orchardcore-ai-prompting` for prompt templates that come after the persona block
- `orchardcore-ai-workflows` for the **AI completion with configuration** task
- `crestapps-core-ai-profiles` for the framework persona API

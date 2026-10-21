---
name: orchardcore-ai-prompting
description: Skill for the CrestApps.OrchardCore.AI.Prompting feature (AI Prompt Templates) in CrestApps.OrchardCore 3.0. Covers feature-aware prompt file discovery under Templates/Prompts, prompt front matter (Title, Description, Category, IsListable, parameters), the Prompt templates picker on AI profiles, profile templates, chat interactions and the AI Completion using Direct Config workflow task, PromptTemplateMetadata and PromptTemplateSelectionEntry in recipes, saved System Prompt templates rendered by AIProfileSystemPromptTemplateProvider under their technical names, PromptTemplateSelectionService and ITemplateService, OrchardCoreTemplateService, and the renderAITemplate scripting method. Use this skill when requests mention Orchard Core AI prompt templates, reusable system prompts, Templates/Prompts, prompt selection, PromptTemplateMetadata, System Prompt templates, renderAITemplate, or ITemplateService in Orchard Core.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "2.0"
---

# Orchard Core AI Prompt Templates

Prompt templates are reusable, Liquid-rendered instruction fragments. Editors pick them on AI profiles,
profile templates, chat interactions and the direct-config workflow task; they render, in order, before the
entity's own system message.

| Need | Use |
| --- | --- |
| A reusable instruction fragment shipped with code | A prompt file under `Templates/Prompts/` in a module |
| A reusable instruction fragment managed by administrators | A **System Prompt** template under **Artificial Intelligence > Templates** |
| Reusable defaults for creating whole profiles | A **Profile** template; see `orchardcore-ai-profile-templates` |

## Feature

| Item | Value |
| --- | --- |
| Feature | AI Prompt Templates, `CrestApps.OrchardCore.AI.Prompting` |
| Depends on | `CrestApps.OrchardCore.AI` |
| Enabled by | `CrestApps.OrchardCore.AI.Chat.Core` (AI Chat Services) depends on it, so any chat feature turns it on |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [ "CrestApps.OrchardCore.AI.Prompting" ],
      "disable": []
    }
  ]
}
```

The feature replaces `ITemplateService` with the Orchard-aware `OrchardCoreTemplateService` and adds a
module template provider. Module prompt files are filtered by the tenant's enabled features and IDs are
de-duplicated case-insensitively.

## Author prompt files

```text
Templates/Prompts/customer-support-tone.md                        -> module's default feature
Templates/Prompts/MyCompany.OrchardCore.Support.Escalation/escalate.md -> only while that feature is enabled
```

A file at the root of `Templates/Prompts/` belongs to the module's default feature; a file in a first-level
folder named after a feature ID is available only while that feature is enabled. The template ID is the
file name without its extension. Front matter describes the template in the picker:

```md
---
Title: Customer support tone
Description: Friendly, concise tone for support assistants
Category: Support
IsListable: true
---

Keep answers short, friendly and factual.
Never promise refunds; offer to connect the customer with the team instead.
```

`IsListable: false` hides a template from the picker while code can still render it (the shipped
`profile-builder-coach` and `sms-conclusion-analysis` prompts do this). Templates can declare parameters
that editors fill in as JSON **Template parameters**; the front matter syntax for parameters and the Liquid
context are covered by `crestapps-core-ai-templates`.

## Select templates on profiles and interactions

The **Prompt templates** picker (**Add prompt template**) appears on the **Instructions** card of AI
profiles and profile templates, the **Settings** tab of chat interactions, and the **AI Completion using
Direct Config** workflow task (when chat interactions and workflows are enabled). It lists module prompt
files plus saved **System Prompt** templates. The selection is stored as `PromptTemplateMetadata`:

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
          "Properties": {
            "PromptTemplateMetadata": {
              "Templates": [
                { "TemplateId": "customer-support-tone" },
                { "TemplateId": "escalate", "Parameters": { "team": "Billing" } }
              ]
            },
            "AIProfileMetadata": {
              "SystemMessage": "Answer questions about orders and shipping."
            }
          }
        }
      ]
    }
  ]
}
```

In code, set it with `profile.Put(new PromptTemplateMetadata { Templates = [new PromptTemplateSelectionEntry { TemplateId = "customer-support-tone" }] })`
(`CrestApps.Core.AI.Models`).

## Saved System Prompt templates

A template created under **Artificial Intelligence > Templates** with the **System Prompt** source holds
`SystemPromptTemplateMetadata.SystemMessage`. In 3.0 it is served by CrestApps.Core's
`AIProfileSystemPromptTemplateProvider` under its **technical name**, which is the ID the picker stores.
Saved templates are read on every request (not cached tenant-wide), so an edit applies on the next message.
A module prompt file with the same ID wins over a saved template.

```json
{
  "steps": [
    {
      "name": "AIProfileTemplate",
      "Templates": [
        {
          "Name": "legal-disclaimer",
          "DisplayText": "Legal disclaimer",
          "Source": "SystemPrompt",
          "Properties": {
            "SystemPromptTemplateMetadata": {
              "SystemMessage": "Never give legal advice. Suggest consulting a lawyer."
            }
          }
        }
      ]
    }
  ]
}
```

## Render templates from code and scripts

- `PromptTemplateSelectionService` lists and finds selectable templates through `ITemplateService`; resolve
  `ITemplateService` (CrestApps.Core templates) to render a template by ID with arguments.
- With `CrestApps.OrchardCore.AI` and `OrchardCore.Recipes.Core` enabled, Orchard scripting has
  `renderAITemplate`:

```javascript
renderAITemplate("customer-support-tone")
renderAITemplate("customer-support-tone", { siteName: "Contoso" })
```

The one-argument form returns the stored content; the two-argument form renders it with the object as
Liquid variables. It reads prompt templates known to `ITemplateService`, not profile templates.

## Upgrading from 2.x

- A saved System Prompt template selected on a profile or interaction now renders. Before, it could be
  picked but was not found at run time, and the request also lost the profile's own system message,
  parameters, tools and agents.
- `PromptTemplateSelectionService` no longer takes an `IAIProfileTemplateManager`; custom subclasses or
  callers must not depend on that constructor.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| A prompt file is not in the picker | It is under `Templates/Prompts/`, its owning feature is enabled, and `IsListable` is not `false` |
| A selected template renders nothing | The stored `TemplateId` matches the file name or the System Prompt template's technical name |
| A saved template edit is ignored | A module prompt file with the same ID takes priority |
| `renderAITemplate` is undefined | Enable `CrestApps.OrchardCore.AI` and `OrchardCore.Recipes.Core` |

## Related skills

- `orchardcore-ai-profile-templates` for Profile templates and the New AI Profile picker.
- `orchardcore-ai` for the profile recipe and metadata.
- `orchardcore-ai-workflows` for the AI completion workflow tasks.
- `crestapps-core-ai-templates` for template parsing, parameters and Liquid rendering.

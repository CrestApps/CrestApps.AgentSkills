---
name: orchardcore-ai-profile-templates
description: Skill for reusable AI profile templates in CrestApps.OrchardCore 3.0. Covers the Templates screen and ManageAIProfileTemplates permission, the Profile and SystemPrompt template sources, the scenario-first New AI Profile picker and setup step (ProfileWizardController, ai/profile/new routes), the shipped starter scenarios, template files in Templates/Profiles and App_Data/AITemplates/Profiles, profile template front matter including Featured, Icon, Order and RequiresFeatures (ProfileScenarioMetadata), IAIProfileTemplateApplicationHandler and AIProfileTemplateAppliedContext, the AIProfileTemplate and CreateAIProfileFromTemplate recipe steps with ProfileTemplateMetadata, the AI Profile Templates deployment step, and upgrading from the removed Apply Templates card. Use this skill when requests mention AI profile templates, AIProfileTemplate, CreateAIProfileFromTemplate, New AI Profile wizard, profile starting points, featured scenarios, or provisioning AI profiles from templates.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Profile Templates

An AI profile template is a reusable starting point for AI profiles. It is how every new profile begins in
3.0: **Add Profile** opens the **New AI Profile** picker, the user picks a template (or **Blank profile**),
fills a short setup step, and the profile is created from the template on the server.

## Basics

| Item | Value |
| --- | --- |
| Feature | `CrestApps.OrchardCore.AI` (base AI module) |
| Admin | **Artificial Intelligence > Templates** (list `AITemplates`), permission `ManageAIProfileTemplates` |
| Manager | `INamedSourceCatalogManager<AIProfileTemplate>` (`IAIProfileTemplateManager`); `AIProfileTemplate` is a `SourceCatalogEntry` |
| Sources (`AITemplateSources`) | `Profile` (creates profiles) and `SystemPrompt` (an instruction fragment; see `orchardcore-ai-prompting`) |

A template is identified by `ItemId`, or by `Name` plus `Source`. Feature-specific defaults and settings
all live in the template's `Properties`; templates have no separate `Settings` member.

## The New AI Profile flow

| Route | Purpose |
| --- | --- |
| `/Admin/ai/profile/new` | Opens the profile list with the picker showing |
| `/Admin/ai/profile/new/{templateId}` | Setup step: title, technical name, optional chat deployment; **Create profile** posts back |
| `/Admin/ai/profile/create?templateId=...` | Still opens the full editor prefilled from a template |

The picker lists **Blank profile** first, then featured scenarios, then every other template that has the
`Profile` source, is listable (`IsListable`, default `true`) and whose profile type is not
`TemplatePrompt`. A card whose `RequiresFeatures` are not enabled is greyed out with the feature to enable.
**Artificial Intelligence > Templates** shows a **Create profile** button on profile templates for users
who can manage AI profiles.

The profile is built from the template itself (not from the editor form), so every value reaches the
profile, including values the editor has no field for, and template documents are copied and indexed.

Shipped starting points: **Website assistant**, **Answers from your docs**, **Guided intake**,
**Background summarizer** (featured), building-block agents (Research, Writer, Summarizer, Data Analyst,
Reviewer, Planner, Executor, Code Assistant), plus SMS and phone starting points that appear when the SMS
automation and Automated Voice features are enabled.

## Template files

| Location | Scope |
| --- | --- |
| `Templates/Profiles/*.md` in a module | Every tenant where the module is enabled |
| `App_Data/AITemplates/Profiles/*.md` | Every tenant |
| `App_Data/Sites/{TenantName}/AITemplates/Profiles/*.md` | One tenant |

The file name without extension is the template ID. Front matter carries the profile defaults; the body is
the system message.

```md
---
Title: Order status assistant
Description: Answers order and shipping questions for your store's customers.
Category: Talk to people
IsListable: true
Featured: true
Icon: fa-solid fa-truck
Order: 5
RequiresFeatures: CrestApps.OrchardCore.AI.Chat
ProfileType: Chat
TitleType: Generated
WelcomeMessage: Hi! Ask me about your order.
Temperature: 0.3
---

You help customers with order status and shipping questions. Never promise delivery dates you cannot see.
```

| Key | Meaning |
| --- | --- |
| `Featured` | Lists the template among featured scenarios |
| `Icon` | Font Awesome class for the card |
| `Order` | Position within the category (a category's first scenario places the category) |
| `RequiresFeatures` | Comma-separated feature IDs that must be enabled |
| `ProfileDescription` | The agent's own description (what other agents read), separate from the card text |
| `InitialPrompt` | The **Opening message**; turns on **Start the conversation automatically**. May use Liquid |

`Featured`, `Icon`, `Order` and `RequiresFeatures` are stored as `ProfileScenarioMetadata` on the template
and never copied to a profile; a value that does not parse is ignored. A value that starts with a Liquid tag
or contains a colon followed by a space must use a `Key: |` block with the text on the next indented line.
The parser trims continuation lines and drops blank ones, so keep multi-line values short.

## Finish a profile built from a template

```csharp
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;

public sealed class TagProfileTemplateApplicationHandler : IAIProfileTemplateApplicationHandler
{
    public Task AppliedAsync(AIProfileTemplateAppliedContext context)
    {
        // context.Profile already has its ItemId; context.Template is the source template.
        return Task.CompletedTask;
    }
}
```

Register with `services.AddScoped<IAIProfileTemplateApplicationHandler, TagProfileTemplateApplicationHandler>()`.
Handlers run once, just before a profile built by the New AI Profile flow is persisted, never while the
editor is merely prefilled. AI Profile Documents uses this hook to give the profile its own document copies.
The `CreateAIProfileFromTemplate` recipe step applies the template directly and does not call these handlers.

## Recipes

```json
{
  "steps": [
    {
      "name": "AIProfileTemplate",
      "Templates": [
        {
          "Name": "order-status",
          "DisplayText": "Order status assistant",
          "Source": "Profile",
          "Description": "Answers order and shipping questions.",
          "Category": "Talk to people",
          "IsListable": true,
          "Properties": {
            "ProfileTemplateMetadata": {
              "ProfileType": "Chat",
              "TitleType": "Generated",
              "ChatDeploymentName": "chat-default",
              "SystemMessage": "You help customers with order status and shipping questions.",
              "WelcomeMessage": "Hi! Ask me about your order.",
              "Temperature": 0.3,
              "ToolNames": [],
              "AgentNames": []
            },
            "AIProfileSettings": { "LockSystemMessage": false }
          }
        }
      ]
    },
    {
      "name": "CreateAIProfileFromTemplate",
      "Profiles": [
        {
          "TemplateId": "order-status",
          "Name": "order-status-prod",
          "DisplayText": "Order Status",
          "ChatDeploymentName": "chat-default",
          "Settings": {
            "AIChatProfileSettings": { "IsOnAdminMenu": true }
          }
        }
      ]
    }
  ]
}
```

- `AIProfileTemplate` requires `Name`, `DisplayText` and `Source`; top-level keys read are `ItemId`, `Name`,
  `Source`, `DisplayText`, `Description`, `Category`, `IsListable` and `Properties` (merged into existing
  properties). Put profile defaults in `Properties.ProfileTemplateMetadata` (`ProfileType`,
  `ChatDeploymentName`, `UtilityDeploymentName`, `ConversationDeploymentName`, `ChatMode`, `VoiceName`,
  `OrchestratorName`, `SystemMessage`, `WelcomeMessage`, `TitleType`, `PromptTemplate`, `PromptSubject`,
  model parameters, `ToolNames`, `AgentNames`, `Description`, `AgentAvailability`,
  `InitialResponseHandlerName`).
- `CreateAIProfileFromTemplate` requires `TemplateId` (an ID or a name) of a `Profile` template. It applies
  the template, then the explicit item values; the top-level `Settings` object overrides the generated
  profile's settings. An existing profile with the same `ItemId` or `Name` is updated. A missing template
  or one with another source adds a recipe error and skips the item.
- Export templates with the **AI Profile Templates** deployment step (`IncludeAll` or `TemplateNames`).
- Confirm item shapes against the schemas the CrestApps Recipes feature publishes (`orchardcore-crestapps-recipes`).

## Capabilities on templates

Templates can select tools, tool instances, agents, MCP and A2A connections, data sources, documents,
memory, persona and prompt templates when those features are enabled; profiles created from the template
inherit them. Live capabilities and post-session processing tools (`AIProfilePostSessionSettings.ToolNames`)
are separate selections; configure both when post-session work needs the same tools.

## Upgrading from 2.x

- The profile editor's **Apply Templates** card is removed; the New AI Profile picker is the one way to
  start from a template. `Create?templateId=` links still prefill the editor.
- `ProfileTemplateMetadata.RealtimeDeploymentName` is obsolete. It still applies (as the conversation
  deployment, with **Conversation** chat mode); use `ConversationDeploymentName` in new templates.
- The template editor's **Deployments** card is now **Deployments & Interactions** and holds chat mode,
  voice and text-to-speech playback; its chat deployment picker lists text-capable deployments only.
- Documents copied from a template onto a new profile are now indexed for retrieval.

## Related skills

- `orchardcore-ai` for the profile model and `AIProfile` recipe.
- `orchardcore-ai-prompting` for System Prompt templates and prompt files.
- `orchardcore-ai-profile-builder` for building a profile by role-play.
- `orchardcore-ai-personas` for persona selection on templates.
- `crestapps-core-ai-templates` for the shared template file parser.

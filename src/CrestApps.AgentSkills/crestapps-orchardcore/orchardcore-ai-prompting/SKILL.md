---
name: orchardcore-ai-prompting
description: Skill for using CrestApps AI prompt files and Orchard Core AI profile templates. Covers feature-aware prompt discovery, AIProfileTemplate sources, module and App_Data profile paths, prompt selection, and template rendering. Use this skill when requests mention Orchard Core AI Prompting, prompt templates, profile templates, Templates/Prompts, Templates/Profiles, AITemplates, AIProfileTemplate, SourceCatalogEntry, or INamedSourceCatalogManager.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "2.0"
---

# Orchard Core AI Prompting

## Choose the correct template surface

Use **prompt files** for reusable rendered instructions, and use **AI profile
templates** for reusable Orchard-managed profile defaults. They are related but
are not interchangeable.

| Need | Use |
|---|---|
| Reuse a Liquid-rendered prompt in a profile, interaction, or script | A prompt file discovered by `CrestApps.OrchardCore.AI.Prompting` |
| Prepopulate a new AI profile with provider, deployment, capability, and chat settings | An `AIProfileTemplate` with `Source = Profile` |
| Maintain a reusable system-message template in the AI template catalog | An `AIProfileTemplate` with `Source = SystemPrompt` |

`AIProfileTemplate` is a `SourceCatalogEntry`. Its Orchard manager is
`INamedSourceCatalogManager<AIProfileTemplate>`, not
`INamedCatalogManager<AIProfileTemplate>`. Recipes that import one must supply
the `Name`, `DisplayText`, and `Source` fields. The `Name` plus `Source` pair
identifies an existing entry when `ItemId` is absent.

## Enable the right feature

Enable `CrestApps.OrchardCore.AI.Prompting` when editors need to select prompt
files in AI profiles, profile templates, or chat interactions. The base AI
feature owns the `AIProfileTemplate` catalog and its Templates screen.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Prompting"
      ],
      "disable": []
    }
  ]
}
```

The prompting feature replaces the default template service with an
Orchard-aware service. It filters module prompt files by the active tenant's
enabled features and de-duplicates template IDs case-insensitively.

## Author prompt files

Put prompt files in an Orchard module at:

```text
Templates/Prompts/
```

The module provider reads assets below that directory. A root-level prompt is
associated with the module's default feature. A prompt in a first-level
subdirectory is associated with the feature whose ID is that subdirectory
name, so it is available only while that feature is enabled.

For example, a module can include:

```text
Templates/Prompts/customer-support.md
Templates/Prompts/MyCompany.OrchardCore.Support/agent-handoff.md
```

Use a supported template parser format and give every prompt a stable
file-based ID. The ID is the file name without its extension. Do not depend on
a runtime `AIProfileTemplate` record to resolve a prompt-file ID.

When `CrestApps.OrchardCore.AI` and `OrchardCore.Recipes.Core` are enabled,
Orchard scripting can render a discovered prompt file:

```javascript
renderAITemplate("customer-support-intro", {
  audience: "support agents"
})
```

The optional second argument is an object of variables for the Liquid context.

## Author profile-template files

Profile-template discovery is owned by the base AI module, not by the
Prompting feature. The supported paths differ by source:

| Source | Path |
|---|---|
| Embedded Orchard module asset | `Templates/Profiles/` |
| Global application data | `App_Data/AITemplates/Profiles/` |
| Tenant application data | `App_Data/Sites/{TenantName}/AITemplates/Profiles/` |

Module profile files are read as module assets. Application-data profile files
are read from the global and active tenant folders. Do not substitute
`App_Data/Templates/Profiles` or `AITemplates/SystemMessages`; those are not
the Orchard providers' profile-template paths.

The file name without its extension is the profile template ID. Keep the front
matter and body compatible with the shared CrestApps.Core profile-template
parser. For a runtime-managed template, use **Artificial Intelligence →
Templates** instead of creating a prompt file.

## Use templates in Orchard editors

After the relevant features are enabled:

- AI profile editors can select discovered prompt files.
- Chat interaction editors can select discovered prompt files.
- Profile-source AI profile templates can select discovered prompt files.
- The base AI Templates screen manages `AIProfileTemplate` entries and applies
  profile-source templates while creating a profile.

Profile templates can carry profile defaults such as deployments, orchestrator
metadata, tools, agents, and prompting selections. They do not replace the
provider connection and deployment setup. Configure that separately before
creating a profile from a template.

## Import a runtime profile template

Use the AI profile template recipe step for catalog entries. Keep it in the
recipe root and set the source explicitly:

```json
{
  "steps": [
    {
      "name": "AIProfileTemplate",
      "Templates": [
        {
          "Name": "customer-support",
          "DisplayText": "Customer Support",
          "Source": "Profile",
          "Description": "Defaults for a customer support AI profile.",
          "Properties": {}
        }
      ]
    }
  ]
}
```

Use `Source = SystemPrompt` only for a runtime system-prompt catalog entry.
Do not represent a `Templates/Prompts` file as a profile-template record just
to render it.

## Troubleshooting

| Symptom | Check |
|---|---|
| A prompt is not selectable | Verify its module asset is below `Templates/Prompts/` and its owning feature is enabled. |
| A profile file is ignored | Use `Templates/Profiles/` for a module, or an `App_Data/AITemplates/Profiles/` path for application data. |
| A recipe creates a duplicate template | Supply `Source` and keep the `Name` unique for that source. |
| A prompt cannot render in scripting | Enable the base AI and `OrchardCore.Recipes.Core` features and use the prompt file ID. |

## Related skills

- For provider connections and deployments that a profile template references,
  use `orchardcore-ai-providers`.
- For selecting prompt-backed behavior in workflow completions, use
  `orchardcore-ai-workflows`.
- For tools selected by a profile or profile template, use
  `orchardcore-ai-tools`.

---
name: orchardcore-ai-profile-templates
description: Skill for managing reusable AI profile templates in Orchard Core. Covers the Artificial Intelligence Templates admin screen, profile defaults, capabilities, AIProfileTemplate recipes, CreateAIProfileFromTemplate recipes, template sources, and tenant provisioning. Use this skill when requests mention AI profile templates, AIProfileTemplate, CreateAIProfileFromTemplate, or related CrestApps OrchardCore AI implementation.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Profile Templates

AI profile templates provide reusable Orchard-managed defaults for profiles.
They can standardize the provider, deployment, orchestrator, prompt templates,
tools, agents, memory settings, and chat settings used by new profiles.

After enabling the base AI feature, manage templates at
**Artificial Intelligence → Templates**. The available fields depend on the
enabled AI features and providers.

## Template sources

The `Source` identifies what the template creates. The `Profile` source
contains profile defaults. Other sources, such as `SystemPrompt`, may be
provided by enabled features. A template with source `Profile` can be used to
create profiles and can carry selected tool instances and capabilities.

## Import profile templates by recipe

The `AIProfileTemplate` recipe step requires `Name`, `DisplayText`, and
`Source`. When `ItemId` is omitted, the import resolves an existing template
by the combined `Name` and `Source` before creating a new item.

```json
{
  "steps": [
    {
      "name": "AIProfileTemplate",
      "Templates": [
        {
          "Name": "customer-support",
          "DisplayText": "Customer Support",
          "Source": "Profile"
        }
      ]
    }
  ]
}
```

Use the exact property name and collection shape from the local recipe schema
when generating a deployment. Feature-specific metadata and settings are both
added to the template `Properties` object; `AIProfileTemplate` items do not
have a separate `Settings` member. Settings objects such as
`AIProfileSettings`, `AIChatProfileSettings`, and
`AIProfilePostSessionSettings` are nested inside `Properties`. The separate
top-level `Settings` object exists only on `CreateAIProfileFromTemplate` items,
where it overrides the generated profile's settings.

## Create a profile from a template

Use `CreateAIProfileFromTemplate` when provisioning a profile from a reusable
profile template:

```json
{
  "steps": [
    {
      "name": "CreateAIProfileFromTemplate",
      "Profiles": [
        {
          "TemplateId": "customer-support",
          "Name": "customer-support-prod",
          "DisplayText": "Customer Support",
          "ChatDeploymentName": "gpt-4o"
        }
      ]
    }
  ]
}
```

`TemplateId` must identify a template whose source is `Profile`. The step
copies the template values first, then applies explicit values from the recipe.
Values omitted from the recipe remain inherited. A missing template or a
template with another source produces a recipe error and skips that item.

Use the local `AIProfileTemplate` and
`CreateAIProfileFromTemplate` schemas from the CrestApps Recipes feature to
confirm the exact item property names before importing a deployment recipe.

## Capabilities and post-session processing

Templates can select tools, tool instances, agents, and other capabilities.
Profiles created from a template inherit those selections. Live capabilities
and post-session processing capabilities are separate; configure both when a
post-session workflow must use the same tools.

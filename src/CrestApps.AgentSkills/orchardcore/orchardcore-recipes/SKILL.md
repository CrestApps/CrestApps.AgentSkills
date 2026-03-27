---
name: orchardcore-recipes
description: Skill for creating Orchard Core recipes. Covers recipe structure, content type definitions, content items, feature enablement, and recipe steps. Use this skill when requests mention Orchard Core Recipes, Create a Recipe, Required Reference Workflow, Recipe Schema References, Recipe Structure, Common Recipe Steps, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.ContentManagement, OrchardCore.ContentTypes, OrchardCore.Title, OrchardCore.Autoroute, ContentDefinition, ContentTypeSettings, TitlePart, <step>.schema.json, schema.json. It also helps with Recipe Structure, Common Recipe Steps, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Recipes - Prompt Templates

## Create a Recipe

You are an Orchard Core expert. Generate a recipe JSON file for configuring an Orchard Core site.

### Guidelines

- Recipes are JSON files with a specific structure containing steps.
- Each step has a `name` property that determines its type.
- Steps are executed in order during recipe execution.
- Use `Feature` step to enable features before configuring them.
- Use `ContentDefinition` step to define content types and parts.
- Use `Content` step to create content items.
- Recipe files are placed in the module's `Recipes` folder.
- Treat `references/recipe-schemas/recipe.schema.json` as the authoritative schema for the full recipe document.
- Before writing any step payload, open `references/recipe-schemas/index.json`, find the exact step file, and use that `<step>.schema.json` as the authoritative contract for allowed properties, required properties, casing, and enum values.
- Do not invent step names, property names, or alternate casing. Copy the exact `name` value required by the step schema.
- When using `ContentDefinition`, also consult the `orchardcore-content-fields` and `orchardcore-content-parts` skills for valid field settings, editors, display modes, and part-specific options, then make sure the final JSON still conforms to `references/recipe-schemas/ContentDefinition.schema.json`.
- If a step schema and an example conflict, follow the schema.

### Required Reference Workflow

1. Start with `references/recipe-schemas/recipe.schema.json` to shape the root recipe object.
2. Open `references/recipe-schemas/index.json` to locate the per-step schema file for every step you plan to emit.
3. Validate each step against its own `.schema.json` file before returning the final recipe.
4. For `ContentDefinition` recipes, combine the schema with the specialized content-field and content-part skills instead of guessing field editors or settings.

### Recipe Schema References

- `references/recipe-schemas/recipe.schema.json` — full recipe schema with the `steps` array.
- `references/recipe-schemas/index.json` — map of recipe step names to per-step schema files.
- `references/recipe-schemas/*.schema.json` — one file per supported recipe step.
- `references/recipe-schemas/README.md` — guidance on how to use the generated schema files with this skill.

### Recipe Structure

```json
{
  "name": "{{RecipeName}}",
  "displayName": "{{DisplayName}}",
  "description": "{{Description}}",
  "author": "{{Author}}",
  "website": "{{Website}}",
  "version": "1.0.0",
  "issetuprecipe": false,
  "categories": ["{{Category}}"],
  "tags": [],
  "steps": []
}
```

This structure is illustrative. The generated schema files in `references/recipe-schemas/` are the source of truth.

### Common Recipe Steps

#### Feature Step

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.ContentManagement",
        "OrchardCore.ContentTypes",
        "OrchardCore.Title",
        "OrchardCore.Autoroute"
      ],
      "disable": []
    }
  ]
}
```

Use `references/recipe-schemas/feature.schema.json` for the exact schema, especially the valid feature IDs and property casing.

#### Content Definition Step

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "{{ContentTypeName}}",
          "DisplayName": "{{DisplayName}}",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true,
              "Versionable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {}
            }
          ]
        }
      ],
      "ContentParts": []
    }
  ]
}
```

Use `references/recipe-schemas/ContentDefinition.schema.json` for the step wrapper and content-definition structure, then consult `orchardcore-content-fields` and `orchardcore-content-parts` for field- and part-specific option catalogs.

#### Content Step

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "{{unique-id}}",
          "ContentType": "{{ContentTypeName}}",
          "DisplayText": "{{Title}}",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "{{Title}}"
          }
        }
      ]
    }
  ]
}
```

Use `references/recipe-schemas/content.schema.json` for the exact step contract.

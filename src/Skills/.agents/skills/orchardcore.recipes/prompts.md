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

### Common Recipe Steps

#### Feature Step

```json
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
```

#### Content Definition Step

```json
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
```

#### Content Step

```json
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
```

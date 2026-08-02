---
name: orchardcore-crestapps-recipes
description: Skill for CrestApps recipe schema and execution extensions in Orchard Core. Covers generated JSON Schema for registered recipe handlers, feature-gated step schemas, content definition and item schemas, custom IRecipeStep providers, IContentSchemaDefinition contributions, and recipe execution through deployment handlers. Use this skill when requests mention CrestApps recipe schemas, recipe validation, JSON Schema recipe tooling, RecipeSchemaService, RecipeExecutionService, IRecipeStep, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.Recipes, CrestApps.OrchardCore.Recipes.Core, RecipeSchemaService, RecipeExecutionService, IContentSchemaDefinition, PartSchemaDefinitionBase, and FieldSchemaDefinitionBase.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Recipes

## Generate accurate Orchard Core recipe schemas

You are an Orchard Core expert. Use the CrestApps Recipes module to discover and validate the shape of recipe steps. This module does not replace Orchard Core's recipe handlers. It contributes JSON Schema for the handlers and feature-specific recipe surfaces available in the enabled tenant.

### Guidelines

- Install `CrestApps.OrchardCore.Recipes` in the web or startup project.
- Enable the exact `CrestApps.OrchardCore.Recipes` feature. Its manifest depends on `OrchardCore.Recipes.Core`.
- Keep every recipe JSON example wrapped in a root object containing `"steps"`.
- Recipe steps execute only when their owning Orchard Core or CrestApps feature is enabled. A schema provider does not implement the underlying runtime behavior.
- `RecipeSchemaService` discovers `NamedRecipeStepHandler` names and registered `IRecipeStep` providers, then builds a cached composed recipe schema.
- When a known handler has no specialized schema, `RecipeSchemaService` provides a permissive fallback with the `name` discriminator.
- `RecipeExecutionService` imports an in-memory recipe through registered deployment target handlers. Check its Boolean result and surface failures rather than treating it as guaranteed success.
- Register custom `IRecipeStep` implementations as scoped services.
- Use `IContentSchemaDefinition` to extend `ContentDefinition`, `ReplaceContentDefinition`, and content-item schema support for a custom part or field.
- Prefer `PartSchemaDefinitionBase` for content parts and `FieldSchemaDefinitionBase` for content fields. They mark the schema type and cache settings schemas.
- Register feature-specific schema contributors behind the feature that owns the part or field.
- Do not use a raw content item payload where a content-definition settings envelope is required. Definitions and values have distinct schema locations.
- Use sealed classes and file-scoped namespaces in C# examples. View Models may remain unsealed for model binding.

### Feature overview

| Feature ID | Purpose |
|---|---|
| `CrestApps.OrchardCore.Recipes` | JSON Schema services and feature-gated step schema providers |
| `OrchardCore.Recipes.Core` | Orchard Core recipe infrastructure required by the CrestApps feature |

### Enable Recipes

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Recipes"
      ],
      "disable": []
    }
  ]
}
```

## Built-in schema behavior

The module always registers schemas for `Settings`, `CustomSettings`, `Command`, and `Recipes`. It conditionally adds schemas for enabled features, including `Feature`, `ContentDefinition`, `ReplaceContentDefinition`, `DeleteContentDefinition`, `content`, `Users`, `Roles`, media, Lucene, and index-profile operations.

Additional CrestApps modules can register runtime handlers and `IRecipeStep` schemas. For example, Users registers `IndexUsers` with `OrchardCore.Users`, while Content Fields and Roles contribute content schemas when their features and CrestApps Recipes are enabled.

## Retrieve schemas in application code

Inject `RecipeSchemaService` from a scoped service, controller, or tool implementation. It can return a complete schema, one schema narrowed by step name, or a schema directly for a registered step.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;

namespace MyModule;

public sealed class RecipeSchemaLookup
{
    private readonly RecipeSchemaService _recipeSchemaService;

    public RecipeSchemaLookup(RecipeSchemaService recipeSchemaService)
    {
        _recipeSchemaService = recipeSchemaService;
    }

    public ValueTask<JsonSchema> GetContentDefinitionSchemaAsync(CancellationToken cancellationToken)
        => _recipeSchemaService.GetRecipeSchemaAsync("ContentDefinition", cancellationToken);
}
```

`GetRecipeSchemaAsync("ContentDefinition")` returns `null` if the requested name is not among the currently enabled and registered step names. Do not cache a schema indefinitely across feature changes; the service already uses a one-hour cache for discovered names.

## Define a custom recipe-step schema

Use `IRecipeStep` for schema provision. The owning module must separately provide the actual `NamedRecipeStepHandler` if the step is executable.

```csharp
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace MyModule;

public sealed class ProductSettingsRecipeStep : IRecipeStep
{
    public string Name => "ProductSettings";

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const("ProductSettings")),
                ("AllowBackorders", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)))
            .Required("name")
            .AdditionalProperties(false)
            .Build();

        return ValueTask.FromResult(schema);
    }
}
```

```csharp
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ProductSettingsRecipeStep>();
    }
}
```

The schema above validates a payload shape. It does not create settings by itself. Pair it with a handler registered through Orchard Core recipe infrastructure when it represents a new executable step.

## Extend content definition schemas

Use `FieldSchemaDefinitionBase` for a custom field. Its settings schema belongs under `ContentPartFieldDefinitionRecords[].Settings`; its value schema belongs in a content item.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using Json.Schema;

namespace MyModule;

public sealed class RatingFieldSchemaDefinition : FieldSchemaDefinitionBase
{
    public override string Name => "RatingField";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("RatingFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Maximum", new JsonSchemaBuilder().Type(SchemaValueType.Integer)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Value", new JsonSchemaBuilder().Type(SchemaValueType.Integer)))
            .AdditionalProperties(true);
}
```

```csharp
services.AddScoped<IContentSchemaDefinition, RatingFieldSchemaDefinition>();
```

The corresponding definition recipe must use the same settings envelope:

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentParts": [
        {
          "Name": "ReviewPart",
          "ContentPartFieldDefinitionRecords": [
            {
              "Name": "Rating",
              "ContentFieldDefinition": {
                "Name": "RatingField"
              },
              "Settings": {
                "RatingFieldSettings": {
                  "Maximum": 5
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

Then `content` recipes use the field's payload properties, not its definition settings:

```json
{
  "steps": [
    {
      "name": "content",
      "data": [
        {
          "ContentType": "Review",
          "ReviewPart": {
            "Rating": {
              "Value": 5
            }
          }
        }
      ]
    }
  ]
}
```

## Execute a generated recipe

`RecipeExecutionService.ExecuteRecipeAsync()` serializes the JSON node into a temporary deployment package and invokes deployment target handlers in sequence. It returns `false` if an exception occurs and logs the error.

```csharp
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core.Services;

namespace MyModule;

public sealed class GeneratedRecipeExecutor
{
    private readonly RecipeExecutionService _recipeExecutionService;

    public GeneratedRecipeExecutor(RecipeExecutionService recipeExecutionService)
    {
        _recipeExecutionService = recipeExecutionService;
    }

    public Task<bool> ExecuteAsync(JsonObject recipe)
        => _recipeExecutionService.ExecuteRecipeAsync(recipe);
}
```

Validate generated JSON against `RecipeSchemaService` before execution, authorize the caller for the resulting operations, and do not accept untrusted commands or deployment payloads without review.

## Troubleshooting

- A missing specialized schema usually means its owning feature is disabled, not that the recipe step is unsupported.
- Use exact case and names from the registered handler for a discriminator. The schema service compares lookup names without case sensitivity, but recipe handler behavior remains authoritative.
- If custom part settings are absent from `ContentDefinition`, ensure the feature registered `IContentSchemaDefinition` while Recipes is enabled.
- Content item schema contribution is recursive for the `content` step. Do not add a duplicate part contributor merely to restate each attached field.

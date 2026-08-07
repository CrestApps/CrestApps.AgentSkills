---
name: orchardcore-crestapps-recipes
description: Skill for CrestApps recipe schema and execution extensions in Orchard Core. Covers generated JSON Schema for recipe handlers, content definition and item schemas, custom IRecipeStep providers, IContentSchemaDefinition contributions, extensible schemas for the Layers, Sitemaps, deployment, AdminMenu, Queries, UrlRewriting, Placements and WorkflowType steps, live tenant example values, and recipe execution. Use this skill when requests mention CrestApps recipe schemas, JSON Schema recipe tooling, RecipeSchemaService, RecipeExecutionService, IRecipeStep, or custom rule conditions, operators, sources, admin nodes, placement filters, or workflow activities. Strong matches include CrestApps.OrchardCore.Recipes, IRuleConditionSchemaDefinition, ISitemapSourceSchemaDefinition, IDeploymentStepSchemaDefinition, IAdminNodeSchemaDefinition, IQuerySourceSchemaDefinition, IRewriteRuleSourceSchemaDefinition, IPlacementNodeFilterSchemaDefinition, IWorkflowActivitySchemaDefinition, and IRecipeSchemaExampleService.
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
- Extend a composed step, rather than replacing it, when it is one of `Layers`, `Sitemaps`, `deployment`, `AdminMenu`, `Queries`, `UrlRewriting` or `Placements`. Each composes its schema from per-provider contributions you register with the matching `Add...Schema<T>()` extension.
- Derive a schema definition from the family base class and register it as a scoped service behind the feature that owns the underlying condition, source, step, node, filter or activity.
- Surface live tenant values through `IRecipeSchemaExampleService` and the `context.Examples` snapshot with `.WithSuggestions(...)`. Emit them as non-restrictive `examples`, never as an `enum`, so custom values stay valid.
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

The `Layers`, `Sitemaps`, `deployment`, `AdminMenu`, `Queries`, `UrlRewriting` and `Placements` steps, along with the `WorkflowType` step, are composed from per-provider contributions. Every built-in condition, operator, source, step, node, filter and activity is described out of the box for its enabled feature, and custom modules extend them through the `Add...Schema<T>()` extensions described below.

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

## Extend composed recipe step schemas

Several Orchard Core recipe steps are polymorphic: they nest conditions, sources, steps, nodes, filters or activities that each own their own shape. The CrestApps Recipes module composes the schema for these steps from per-provider contributions, so a custom module describes only its own type and the shared members are added for it. Register each contribution as a scoped service behind the feature that owns the underlying type.

| Step | Interface | Base class | Registration | Feature gate |
|---|---|---|---|---|
| `Layers` (rule conditions) | `IRuleConditionSchemaDefinition` | `RuleConditionSchemaDefinitionBase`, `OperandConditionSchemaDefinitionBase`, `ConditionGroupSchemaDefinitionBase` | `AddRuleConditionSchema<T>()` | `OrchardCore.Layers` |
| `Layers` (condition operators) | `IRuleConditionOperatorSchemaDefinition` | `RuleConditionOperatorSchemaDefinitionBase`, `StringOperatorSchemaDefinitionBase` | `AddRuleConditionOperatorSchema<T>()` | `OrchardCore.Layers` |
| `Sitemaps` | `ISitemapSourceSchemaDefinition` | `SitemapSourceSchemaDefinitionBase` | `AddSitemapSourceSchema<T>()` | `OrchardCore.Sitemaps` |
| `deployment` | `IDeploymentStepSchemaDefinition` | `DeploymentStepSchemaDefinitionBase` | `AddDeploymentStepSchema<T>()` | `OrchardCore.Deployment` |
| `AdminMenu` | `IAdminNodeSchemaDefinition` | `AdminNodeSchemaDefinitionBase` | `AddAdminNodeSchema<T>()` | `OrchardCore.AdminMenu` |
| `Queries` | `IQuerySourceSchemaDefinition` | `QuerySourceSchemaDefinitionBase` | `AddQuerySourceSchema<T>()` | `OrchardCore.Queries` |
| `UrlRewriting` | `IRewriteRuleSourceSchemaDefinition` | `RewriteRuleSourceSchemaDefinitionBase` | `AddRewriteRuleSourceSchema<T>()` | `OrchardCore.UrlRewriting` |
| `Placements` | `IPlacementNodeFilterSchemaDefinition` | `PlacementNodeFilterSchemaDefinitionBase` | `AddPlacementNodeFilterSchema<T>()` | `OrchardCore.Placements` |
| `WorkflowType` | `IWorkflowActivitySchemaDefinition` | `WorkflowActivitySchemaDefinitionBase` | `AddWorkflowActivitySchema<T>()` | `OrchardCore.Workflows` |

The base classes live in `CrestApps.OrchardCore.Recipes.Core.Schemas.<Family>` (for example `...Schemas.Rules`, `...Schemas.Sitemaps`, `...Schemas.Deployment`). The composition service only describes contributions whose owning feature is enabled, and unknown types still validate through a permissive fallback.

### Contribute a rule condition and operator

The `Layers` step composes each layer's `LayerRule`. Derive from `OperandConditionSchemaDefinitionBase` for a value-plus-operator condition, from `ConditionGroupSchemaDefinitionBase` for a nesting group, or from `RuleConditionSchemaDefinitionBase` directly. The base adds the shared `$type`, `Name` and `ConditionId` members, the recursive `Conditions` array for groups, and the `Operation` member for operand conditions.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

namespace MyModule;

public sealed class TenantConditionSchema : OperandConditionSchemaDefinitionBase
{
    public override string Name { get; } = "TenantCondition";

    public override string TypeDiscriminator { get; } = "MyModule.Rules.TenantCondition, MyModule";

    protected override string DisplayText => "Tenant";

    protected override string Description => "Evaluates the current tenant name against a value.";

    protected override string ValueDescription => "The tenant name the operator compares the current tenant against.";
}
```

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;
using Json.Schema;

namespace MyModule;

public sealed class RegexMatchOperatorSchema : RuleConditionOperatorSchemaDefinitionBase
{
    public override string Name { get; } = "RegexMatchOperator";

    public override string TypeDiscriminator { get; } = "MyModule.Rules.RegexMatchOperator, MyModule";

    protected override string DisplayText => "Matches pattern";

    protected override string Description => "Matches when the value satisfies the regular expression.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Pattern", RuleConditionSchemaBuilders.String("The regular expression evaluated against the value."));
    }
}
```

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures("OrchardCore.Layers")]
public sealed class LayersRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRuleConditionSchema<TenantConditionSchema>();
        services.AddRuleConditionOperatorSchema<RegexMatchOperatorSchema>();
    }
}
```

### Contribute a deployment step

Derive from `DeploymentStepSchemaDefinitionBase`. It exposes `StepType` (the CLR step type name, not `Name`), the shared members, and describes the `Step` payload from `GetPropertyDefinitions`.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace MyModule;

public sealed class ProductCatalogDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => "ProductCatalogDeploymentStep";

    protected override string DisplayText => "Product catalog";

    protected override string Description => "Exports the product catalog.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeArchived", new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description("When true, archived products are included."));
    }
}
```

Register it with `services.AddDeploymentStepSchema<ProductCatalogDeploymentStepSchema>();` behind `[RequireFeatures("OrchardCore.Deployment")]`.

### Contribute a sitemap source, query source, rewrite rule source, admin node, placement filter or workflow activity

Every family follows the same shape: override the identity members, override the shared metadata (`DisplayText`, `Description`, `RequiredProperties`), and return the type's own members from `GetPropertyDefinitions(<Family>SchemaContext context)`. The exceptions are the placement filter, which returns a value schema from `GetValueSchema(...)` keyed by `Key`, and the query source, which is keyed by `Name` (its `Source` value) and has no `$type`.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Queries;
using Json.Schema;

namespace MyModule;

public sealed class GraphQuerySourceSchema : QuerySourceSchemaDefinitionBase
{
    public override string Name { get; } = "Graph";

    protected override string DisplayText => "Graph";

    protected override string Description => "Runs a graph query.";

    protected override IEnumerable<string> RequiredProperties => ["Template"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(QuerySourceSchemaContext context)
    {
        yield return ("Template", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The graph query text."));
    }
}
```

Register it with `services.AddQuerySourceSchema<GraphQuerySourceSchema>();` behind the owning feature. A workflow activity uses `WorkflowActivitySchemaDefinitionBase` and additionally describes its `Category`, `Outcomes` and `HasDynamicOutcomes`.

## Surface live tenant example values

Recipe schema definitions surface well-known tenant values as non-restrictive JSON Schema `examples`, so the generated schema reflects the live tenant while still accepting any custom value. Read the snapshot from `IRecipeSchemaExampleService.GetExamplesAsync()` or, inside a family definition, from the `context.Examples` property.

`RecipeSchemaExamples` exposes `ContentTypeNames`, `ContentPartNames`, `CultureNames`, `RoleNames` and `IndexProfileNames`. Emit them with the `.WithSuggestions(IEnumerable<string>)` builder extension, which adds `examples` and adds nothing when the list is empty.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

namespace MyModule;

public sealed class StereotypeConditionSchema : OperandConditionSchemaDefinitionBase
{
    public override string Name { get; } = "StereotypeCondition";

    public override string TypeDiscriminator { get; } = "MyModule.Rules.StereotypeCondition, MyModule";

    protected override string ValueDescription => "The content type value compared against the current request.";

    protected override IEnumerable<string> GetValueExamples(RecipeSchemaExamples examples) => examples.ContentTypeNames;
}
```

For families whose `GetPropertyDefinitions` receives the context, call `context.Examples` and pass the list to `.WithSuggestions(...)` on the relevant property, exactly as the built-in `ContentTypesSitemapSource`, `ResetIndexDeploymentStep` and content workflow activities do. Never turn an example list into an `enum`; a schema that rejects an unlisted value breaks valid recipes when the tenant later adds a type, role, culture or index.

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
- If a custom condition, operator, source, step, node, filter or activity is missing from a composed step, confirm it is registered with the matching `Add...Schema<T>()` extension behind the correct `[RequireFeatures(...)]` gate and that both that feature and CrestApps Recipes are enabled.
- Match a deployment step definition on `StepType`, not `Name`, and match a query source on its `Source` value, which the base exposes as `Name`.
- If tenant example values are absent from an exported schema, the export host did not register the source service (`IContentDefinitionManager`, `ILocalizationService`, `IRoleService` or `IIndexProfileStore`); at runtime the values come from the live tenant. Missing examples never fail validation, because examples are non-restrictive.

---
name: orchardcore-recipes
description: Skill for authoring Orchard Core recipes and custom recipe steps. Covers recipe discovery, recipe manifests, execution, built-in step contracts, nested recipes, migration recipes, and end-to-end IRecipeStepHandler development. Use this skill when requests mention Orchard Core Recipes, Create a Recipe, Recipe Steps, Recipe Discovery, Recipe Manifest, IRecipeHarvester, IRecipeExecutor, AddRecipeExecutionStep, or Custom Recipe Step Handlers. Strong matches include work with OrchardCore.Recipes, IRecipeStepHandler, NamedRecipeStepHandler, RecipeExecutionContext, RecipeDescriptor, ContentDefinition, WorkflowType, DeploymentPlansRecipeStep, and recipe.schema.json. It also helps with Recipe Structure, Common Recipe Steps, Recipe Schema References, and the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Recipes

Recipes are JSON documents that configure a tenant through ordered steps. Use a recipe for repeatable setup, environment provisioning, deployment imports, or migration updates. Steps are processed in order and their names are case-insensitive at handler dispatch, but use the registered casing below and in the schemas.

## Recipe location, discovery, and execution

- Place an extension recipe at `Recipes/<name>.recipe.json` in a module or theme. The extension's manifest makes that extension discoverable; there is no separate recipe manifest file.
- `RecipeHarvester` iterates enabled extensions and reads each extension's `Recipes` folder. The application recipe harvester also contributes application-level recipes.
- Set `issetuprecipe` to `true` only for recipes intended to appear during tenant setup.
- Run a discovered recipe from **Configuration → Recipes**, during setup, or from another recipe using the `Recipes` step. A data migration may execute a recipe in its module/theme `Migrations` folder through `IRecipeMigrator`.
- Programmatic execution uses `IRecipeExecutor.ExecuteAsync(executionId, recipeDescriptor, environment, cancellationToken)`. A `RecipeDescriptor` carries the recipe metadata, base path, file provider, and recipe file.

Use `.recipe.json`, not arbitrary `.json`, for discovered extension recipes. Recipes can contain JSON comments, but generated automation should emit strict JSON unless comments are intentionally needed.

## Authoring rules

- Enable features before invoking their recipe steps.
- Keep content definition, settings, and workflow records under source control.
- Use variables and helpers such as `[js: uuid()]`, `[file:text('...')]`, `[env:NAME]`, and `[appsettings:Section:Key]` rather than inventing stable IDs or embedding secrets.
- All examples below are complete recipe fragments. Preserve the `{ "steps": [ ... ] }` root wrapper when composing a recipe.
- For recipe-generated content, use expressions to generate `ContentItemId`, `ContentItemVersionId`, and timestamps rather than manually invented IDs.

## Required reference workflow

1. Start with `references/recipe-schemas/recipe.schema.json` for the root document.
2. Open `references/recipe-schemas/index.json` and locate the schema for every planned step.
3. Validate every step against that exact schema. Do not invent a step name, property, casing, or enum value.
4. For `ContentDefinition`, combine its schema with `orchardcore-content-fields` and `orchardcore-content-parts` guidance, then validate the final JSON again.

### Recipe schema references

- `references/recipe-schemas/recipe.schema.json` — full recipe document schema.
- `references/recipe-schemas/index.json` — step-name to schema mapping.
- `references/recipe-schemas/*.schema.json` — per-step contracts.
- `references/recipe-schemas/README.md` — schema usage guidance.

## Recipe structure

```json
{
  "name": "MyModule.Baseline",
  "displayName": "My Module Baseline",
  "description": "Creates the baseline configuration.",
  "author": "My Team",
  "website": "https://example.invalid",
  "version": "1.0.0",
  "issetuprecipe": false,
  "categories": [ "Configuration" ],
  "tags": [ "baseline" ],
  "variables": {
    "now": "[js: new Date().toISOString()]"
  },
  "steps": []
}
```

## Common built-in steps

The schema references remain the authority for each payload. These examples show the release/3.0 handler names and common casing.

### Enable features, set site settings, and define content

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.ContentTypes",
        "OrchardCore.Title"
      ],
      "disable": []
    },
    {
      "name": "Settings",
      "SiteName": "Example",
      "TimeZoneId": "UTC"
    },
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Article",
          "DisplayName": "Article",
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

### Import content and compose recipes

```json
{
  "variables": {
    "now": "[js: new Date().toISOString()]"
  },
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js: uuid()]",
          "ContentItemVersionId": "[js: uuid()]",
          "ContentType": "Article",
          "DisplayText": "Welcome",
          "Latest": true,
          "Published": true,
          "CreatedUtc": "[js: variables('now')]",
          "ModifiedUtc": "[js: variables('now')]",
          "PublishedUtc": "[js: variables('now')]",
          "TitlePart": {
            "Title": "Welcome"
          }
        }
      ]
    },
    {
      "name": "Recipes",
      "Values": [
        {
          "executionid": "MyModule.Extras",
          "name": "MyModule.Extras"
        }
      ]
    }
  ]
}
```

`Recipes` runs a harvested recipe by its root `name`; `executionid` distinguishes that execution from other runs.

### Other common registered steps

Use the matching schema before emitting any of these: `Themes`, `Layers`, `Queries`, `Media`, `MediaProfile`, `Roles`, `Users`, `Templates`, `AdminTemplates`, `AdminMenu`, `Placement`, `WorkflowType`, `deployment`, `LuceneIndex`, `LuceneIndexRebuild`, `LuceneIndexReset`, `ElasticsearchIndex`, `ElasticsearchIndexRebuild`, `ElasticsearchIndexReset`, `CreateOrUpdateIndexProfile`, `RebuildIndex`, `ResetIndex`, `Translations`, `Sitemaps`, `FeatureProfiles`, `CustomSettings`, `CustomUserSettings`, and provider-specific authentication settings.

## Define workflow types and deployment plans

`WorkflowType` imports serialized workflow definitions. Create workflows in the admin editor, export them through the workflow deployment step, and use that exported `data` payload instead of hand-authoring activity IDs and transitions.

```json
{
  "steps": [
    {
      "name": "WorkflowType",
      "data": [
        {
          "WorkflowTypeId": "[js: uuid()]",
          "Name": "Example workflow",
          "IsEnabled": true,
          "Activities": [],
          "Transitions": []
        }
      ]
    }
  ]
}
```

The lowercase `deployment` step creates deployment plans. Its `Type` values must match enabled deployment step factories.

```json
{
  "steps": [
    {
      "name": "deployment",
      "Plans": [
        {
          "Name": "Configuration",
          "Steps": [
            {
              "Type": "AllFeaturesDeploymentStep",
              "Step": {
                "Id": "[js: uuid()]",
                "Name": "AllFeatures"
              }
            }
          ]
        }
      ]
    }
  ]
}
```

## Write a custom recipe step

Implement `IRecipeStepHandler` when a handler must inspect several names. For one named step, derive from `NamedRecipeStepHandler`; it compares `context.Name` case-insensitively and invokes `HandleAsync` only when the name matches. Release/3.0 does not use a `[RecipeStep]` attribute for handler discovery: registration is explicit.

`RecipeExecutionContext` provides `ExecutionId`, `Name`, `Step` as a `JsonObject`, `RecipeDescriptor`, nested recipes, and an `Errors` collection. Add a user-actionable error to `context.Errors` when input cannot be applied; do not silently ignore malformed required data.

```csharp
using System.Text.Json.Nodes;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Settings;

namespace MyModule.Recipes;

public sealed class MySettingsStep : NamedRecipeStepHandler
{
    private readonly ISiteService _siteService;

    public MySettingsStep(ISiteService siteService)
        : base("MySettings")
    {
        _siteService = siteService;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var enabled = context.Step["Enabled"]?.GetValue<bool>();

        if (enabled is null)
        {
            context.Errors.Add("MySettings requires an Enabled value.");
            return;
        }

        var site = await _siteService.LoadSiteSettingsAsync();
        site ??= new SiteSettings();
        site.Properties["MyModuleSettings"] = new JsonObject
        {
            ["Enabled"] = enabled.Value,
        };

        await _siteService.UpdateSiteSettingsAsync(site);
    }
}
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<MySettingsStep>();
    }
}
```

The exact API is `AddRecipeExecutionStep<TImplementation>()`; it registers the handler as a scoped `IRecipeStepHandler`. Add the feature that contains this startup before the custom step in a consuming recipe.

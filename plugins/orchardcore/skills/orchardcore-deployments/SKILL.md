---
name: orchardcore-deployments
description: Skill for authoring Orchard Core deployment plans and custom deployment steps. Covers plan creation, plan recipes, package export and import, export targets, remote deployment, built-in step selection, DeploymentPermissions, custom steps with LocalizationSource Category and Title labels, and end-to-end extensibility. Use this skill when requests mention Orchard Core Deployments, Create a Deployment Plan, Deployment Step Types, Export or Import a Deployment Package, Remote Deployment, Deployment Plan Recipe Export, Creating Deployment Steps in Code, deployment step categories or titles, or upgrading custom deployment steps to Orchard Core 4.0. Strong matches include OrchardCore.Deployment, OrchardCore.Deployment.Remote, IDeploymentSource, IDeploymentTargetProvider, IDeploymentTargetHandler, DeploymentStep, DeploymentPermissions, LocalizationSource, AddDeployment, AddDeploymentTargetHandler, DeploymentPlansRecipeStep, and DeploymentPlanResult.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Deployments

Use deployment plans to export configuration or content as a recipe package, then import that package into another tenant. A plan is an ordered collection of deployment steps. Enable the feature that owns each selected step on the source and target tenant before creating, exporting, or importing the plan.

## Create and run a deployment plan

1. Enable `OrchardCore.Deployment` and the features that contribute the required steps.
2. In **Tools → Deployments → Plans**, select **Add Deployment Plan**, then open the plan and use **Add Step**. The step picker groups steps by category and can be filtered by category.
3. Configure each step and arrange the steps in the order they should be written to `Recipe.json`.
4. Select **Execute**, then a deployment target. **File Download** generates a `.zip` package; `OrchardCore.Deployment.Remote` adds remote instances as targets.
5. On the target site, go to **Tools → Deployments** and use **Package Import** (`.zip` package or `.json` recipe) or **JSON Import** (recipe JSON in an editor). Importing executes the recipe immediately.

Order steps deliberately. For example, enable features first, define content types before content, configure indexes before requesting a rebuild, and create workflow definitions only after their activity-providing features are enabled.

Package export and import extract and build archives in the tenant-scoped temporary directory provided by `ITempDirectoryProvider`, configured with `OrchardCore:TempDirectory:Path` (see `orchardcore-temp-file-storage`). On hosts with a small system temp volume, point it at a larger mounted file share.

## Permissions

Use the static members of `OrchardCore.Deployment.DeploymentPermissions`:

| Member | Name | Purpose |
|---|---|---|
| `ManageDeploymentPlan` | Manage deployment plans | Create, edit, reorder, and delete plans and their steps. |
| `Export` | Export Data | Execute a plan and export its package. |
| `Import` | Import Data | Import packages or recipe JSON. Security-critical; grant only to trusted roles. |
| `ManageRemoteInstances` | Manage remote instances | Manage remote instances (Deployment.Remote). |
| `ManageRemoteClients` | Manage remote clients | Manage remote clients (Deployment.Remote). |
| `ExportRemoteInstances` | Export to remote instances | Execute a plan to a remote instance (Deployment.Remote). |

## Create a plan with a recipe

The deployment recipe step is named lowercase `deployment`. Its `Type` must be the registered deployment-step factory name, normally the class name, and `Step` must serialize the corresponding `DeploymentStep`. The plan creation fails without changes if any type is unavailable because its feature is disabled.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Deployment",
        "OrchardCore.ContentTypes",
        "OrchardCore.Contents"
      ],
      "disable": []
    },
    {
      "name": "deployment",
      "Plans": [
        {
          "Name": "Configuration",
          "Steps": [
            {
              "Type": "ContentDefinitionDeploymentStep",
              "Step": {
                "Id": "[js: uuid()]",
                "Name": "ContentDefinition"
              }
            },
            {
              "Type": "AllContentDeploymentStep",
              "Step": {
                "Id": "[js: uuid()]",
                "Name": "AllContent"
              }
            }
          ]
        }
      ]
    }
  ]
}
```

The `deployment` recipe step creates or updates plans; it does **not** export their package. Export or execute the resulting plan from the Deployment Plans UI. The **Deployment Plans** step (`DeploymentPlanDeploymentStep`) exports plan definitions in this format.

## Built-in deployment steps

Only steps whose owning feature is enabled appear in the picker. The set registered by framework modules is below. Steps that rebuild, reset, or export to a target are operational steps rather than configuration export steps.

| Area | Registered step types |
|---|---|
| Deployment | `CustomFileDeploymentStep`, `RecipeFileDeploymentStep`, `DeploymentPlanDeploymentStep`, `JsonRecipeDeploymentStep` |
| Content | `AllContentDeploymentStep`, `ContentDeploymentStep`, `ContentItemDeploymentStep`, `ExportContentToDeploymentTargetDeploymentStep`, `QueryBasedContentDeploymentStep` |
| Content definitions | `ContentDefinitionDeploymentStep`, `ReplaceContentDefinitionDeploymentStep`, `DeleteContentDefinitionDeploymentStep` |
| Core configuration | `AllFeaturesDeploymentStep`, `SiteSettingsDeploymentStep`, `ThemesDeploymentStep`, `PlacementsDeploymentStep`, `AllLayersDeploymentStep`, `AllRolesDeploymentStep`, `AllQueriesDeploymentStep`, `CustomSettingsDeploymentStep`, `AllRateLimitPoliciesDeploymentStep` |
| Media and templates | `MediaDeploymentStep`, `AllMediaProfilesDeploymentStep`, `AllTemplatesDeploymentStep`, `AllAdminTemplatesDeploymentStep`, `AllShortcodeTemplatesDeploymentStep` |
| Localization, users, tenants | `TranslationsDeploymentStep`, `AllDataTranslationsDeploymentStep`, `AllUsersDeploymentStep`, `CustomUserSettingsDeploymentStep`, `AllFeatureProfilesDeploymentStep` |
| Workflow, menus, maps, search settings | `AllWorkflowTypeDeploymentStep`, `AdminMenuDeploymentStep`, `AllSitemapsDeploymentStep`, `SearchSettingsDeploymentStep` |
| Data | `DataPipelinesDeploymentStep` (all or selected pipelines, without their secrets; see `orchardcore-data-pipelines`) |
| Indexing | `IndexProfileDeploymentStep`, `RebuildIndexDeploymentStep`, `ResetIndexDeploymentStep` |
| Lucene | `LuceneIndexDeploymentStep`, `LuceneIndexRebuildDeploymentStep`, `LuceneIndexResetDeploymentStep` |
| Elasticsearch | `ElasticsearchIndexDeploymentStep`, `ElasticsearchIndexRebuildDeploymentStep`, `ElasticsearchIndexResetDeploymentStep` |
| Azure AI Search | `AzureAISearchIndexDeploymentStep`, `AzureAISearchIndexRebuildDeploymentStep`, `AzureAISearchIndexResetDeploymentStep` |
| Authentication | `FacebookLoginDeploymentStep`, `AzureADDeploymentStep`, `MicrosoftAccountDeploymentStep`, `OpenIdServerDeploymentStep`, `OpenIdValidationDeploymentStep` |

Do not guess step JSON. Create the plan in the admin UI and use its export as the authoritative payload, especially for steps with feature-specific options.

## Remote deployment

Enable `OrchardCore.Deployment.Remote` on the source and target along with `OrchardCore.Deployment`. On the source, register the target under **Remote Instances**; on the target, create a matching entry under **Remote Clients**. Executing a plan to a remote instance pushes the package to the target's import endpoint, which imports it. See `orchardcore-deployment-remote` for the full setup. Treat the API keys as production secrets, use HTTPS, and ensure the target has every feature required by the imported steps.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Deployment",
        "OrchardCore.Deployment.Remote",
        "OrchardCore.Recipes"
      ],
      "disable": []
    }
  ]
}
```

## Write a custom deployment step

Implement all three pieces in the module that owns the data.

1. Derive a serializable model from `DeploymentStep`. In its parameterless constructor set a stable `Name`, plus `Category` and `Title` as `LocalizationSource` values created with the step type as context. Add only settings that the editor needs.
2. Implement `IDeploymentSource`, preferably by deriving from `DeploymentSourceBase<TStep>`, and append valid recipe JSON to `DeploymentPlanResult.Steps` or files through `result.FileBuilder`.
3. Add a `DisplayDriver<DeploymentStep, TStep>` and the corresponding summary, thumbnail, and edit Razor shapes when the step is configurable.
4. Register the source, step factory, JSON derived type, and driver with the exact `AddDeployment<TSource, TStep, TDisplayDriver>()` overload.
5. To add an execution destination (an entry of the **Execute** dialog), implement `IDeploymentTargetProvider`. To change how imported packages are processed, implement `IDeploymentTargetHandler` and register it with `AddDeploymentTargetHandler<TImplementation>()`.

`Category` groups the step in the **Add Step** picker. `Title` is the step's display name and the heading and breadcrumb of its add and edit screens, so the edit view needs no heading of its own. Both are `[JsonIgnore]`, never persisted with the plan, and translated at display time with `IStringLocalizerFactory.Localize()` using the step type as PO context (for example `msgctxt "MyModule.Deployment.MySettingsDeploymentStep"`). Keep them in `static readonly` fields so every step instance shares them.

Every object added to `DeploymentPlanResult.Steps` is a recipe step consumed on import. Emit an existing recipe step or ship and enable a matching custom `IRecipeStepHandler` on the target tenant. The example emits `MySettings`, so its companion recipe handler must be available there.

```csharp
using System.Text.Json.Nodes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace MyModule.Deployment;

public sealed class MySettingsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource s_category = LocalizationSource.Create<MySettingsDeploymentStep>("Settings");
    private static readonly LocalizationSource s_title = LocalizationSource.Create<MySettingsDeploymentStep>("My Settings");

    public MySettingsDeploymentStep()
    {
        Name = "MySettings";
        Category = s_category;
        Title = s_title;
    }

    public bool IncludeSecrets { get; set; }
}

public sealed class MySettingsDeploymentSource
    : DeploymentSourceBase<MySettingsDeploymentStep>
{
    protected override Task ProcessAsync(
        MySettingsDeploymentStep step,
        DeploymentPlanResult result)
    {
        result.Steps.Add(new JsonObject
        {
            ["name"] = "MySettings",
            ["Enabled"] = true,
        });

        return Task.CompletedTask;
    }
}
```

```csharp
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace MyModule.Deployment;

public sealed class MySettingsDeploymentStepDriver
    : DisplayDriver<DeploymentStep, MySettingsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(
        MySettingsDeploymentStep step,
        BuildDisplayContext context)
        => CombineAsync(
            View("MySettingsDeploymentStep_Fields_Summary", step)
                .Location("Summary", "Content"),
            View("MySettingsDeploymentStep_Fields_Thumbnail", step)
                .Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(MySettingsDeploymentStep step, BuildEditorContext context)
        => Initialize<MySettingsDeploymentStepViewModel>("MySettingsDeploymentStep_Fields_Edit", model =>
        {
            model.IncludeSecrets = step.IncludeSecrets;
        }).Location("Content");

    public override async Task<IDisplayResult> UpdateAsync(MySettingsDeploymentStep step, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(step, Prefix, x => x.IncludeSecrets);

        return Edit(step, context);
    }
}

public class MySettingsDeploymentStepViewModel
{
    public bool IncludeSecrets { get; set; }
}
```

Add `Views/Items/MySettingsDeploymentStep.Fields.Summary.cshtml`, `Views/Items/MySettingsDeploymentStep.Fields.Thumbnail.cshtml`, and `Views/Items/MySettingsDeploymentStep.Fields.Edit.cshtml`.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<
            MySettingsDeploymentSource,
            MySettingsDeploymentStep,
            MySettingsDeploymentStepDriver>();
    }
}
```

`AddDeployment<TSource, TStep, TDisplayDriver>()` registers the source, a deployment-step factory, derived JSON metadata, and the display driver. Use `AddDeployment<TSource, TStep>()` for a step without an editor, and `AddDeploymentWithoutSource<TStep, TDisplayDriver>()` only for a step that has no source, such as `RecipeFileDeploymentStep`.

## Deployment targets and target handlers

- `IDeploymentTargetProvider.GetDeploymentTargetsAsync()` returns the `DeploymentTarget` entries (`Name`, `Description`, `Route`) offered by **Execute**. `FileDownloadDeploymentTargetProvider` and the remote instances provider are examples.
- `IDeploymentTargetHandler.ImportFromFileAsync(IFileProvider)` processes an imported package. Register it with `services.AddDeploymentTargetHandler<MyDeploymentTargetHandler>();`. The Recipes module registers `RecipeDeploymentTargetHandler`, which executes the package's recipe.

## Upgrading from 3.x

- `DeploymentStep.Category` and `DeploymentStep.Title` are `LocalizationSource` instead of `LocalizedString`. Assign them in the parameterless constructor:
  - Before: `public MyStep(IStringLocalizer<MyStep> S) : this() { Category = S["Content"]; }`
  - After: `Category = LocalizationSource.Create<MyStep>("Content");` in `public MyStep()`.
- Remove constructors that took an `IStringLocalizer<TStep>` only to set the labels; built-in steps no longer have them. Existing PO translations keep working because the context is still the step type.
- Code that displays `step.Category` or `step.Title` must translate it with `IStringLocalizerFactory.Localize(step.Title)`.
- `OrchardCore.Deployment.CommonPermissions` and `OrchardCore.Deployment.Remote.Permissions` were removed. Use `DeploymentPermissions.ManageDeploymentPlan`, `Export`, `Import`, `ManageRemoteInstances`, `ManageRemoteClients`, and `ExportRemoteInstances` (replaces `Remote.Permissions.Export`). Stored role permission names are unchanged.
- Deployment and recipe archives are written under `OrchardCore:TempDirectory:Path` instead of `Path.GetTempPath()`.

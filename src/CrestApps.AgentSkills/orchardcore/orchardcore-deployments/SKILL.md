---
name: orchardcore-deployments
description: Skill for authoring Orchard Core deployment plans and custom deployment steps. Covers plan creation, plan recipes, package export and import, export targets, remote deployment, built-in step selection, DeploymentPermissions, custom steps overriding Name and the LocalizationSource Category and Title labels with CommonLocalizationSources, deployment file path validation, and end-to-end extensibility. Use this skill when requests mention Orchard Core Deployments, a deployment plan, deployment step types, exporting or importing a deployment package, remote deployment, creating deployment steps in code, step categories or titles, or upgrading custom deployment steps to Orchard Core 4.0. Strong matches include OrchardCore.Deployment, OrchardCore.Deployment.Remote, IDeploymentSource, IDeploymentTargetProvider, IDeploymentTargetHandler, DeploymentStep, DeploymentPermissions, AddDeployment, DeploymentPlansRecipeStep, and DeploymentPlanResult.
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

The **Custom File** step rejects a file name that is rooted (`/...`), fully qualified (`C:\...`) or that escapes the package folder (`../...`), and `TemporaryFileBuilder.SetFileAsync()` throws `InvalidOperationException` for such paths, so a custom deployment source must pass package-relative paths to `result.FileBuilder`. When a plan fails while exporting to a file, the error is logged and an error notification is shown on the plan page instead of an unhandled exception.

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

1. Derive a serializable model from `DeploymentStep`. Override the virtual `Name` getter with a stable name, and the read-only virtual `Category` and `Title` properties with shared `LocalizationSource` instances, each marked `[JsonIgnore]`. Add only settings that the editor needs.
2. Implement `IDeploymentSource`, preferably by deriving from `DeploymentSourceBase<TStep>`, and append valid recipe JSON to `DeploymentPlanResult.Steps` or files through `result.FileBuilder`.
3. Add a `DisplayDriver<DeploymentStep, TStep>` and the corresponding summary, thumbnail, and edit Razor shapes when the step is configurable.
4. Register the source, step factory, JSON derived type, and driver with the exact `AddDeployment<TSource, TStep, TDisplayDriver>()` overload.
5. To add an execution destination (an entry of the **Execute** dialog), implement `IDeploymentTargetProvider`. To change how imported packages are processed, implement `IDeploymentTargetHandler` and register it with `AddDeploymentTargetHandler<TImplementation>()`.

`Category` groups the step in the **Add Step** picker. Use a member of `CommonLocalizationSources` (namespace `OrchardCore.Localization`, package `OrchardCore.Abstractions`) when steps of several modules share the category: `Configuration`, `Content`, `ContentManagement`, `Development`, `Infrastructure`, `Search`, `Security` or `Settings`. Declare a module-only category and the `Title` in an `internal static class {Module}LocalizationSources` at the module root. `Title` is the step's display name and the heading and breadcrumb of its add and edit screens, so the edit view needs no heading of its own.

Both are never persisted with the plan, always read from the step's type, and translated at display time with `IStringLocalizerFactory.Localize()`. The PO context is the class that declares the source: `msgctxt "OrchardCore.Localization.CommonLocalizationSources"` for shared categories, `msgctxt "MyModule.MyModuleLocalizationSources"` for the module's own sources. `Name` is persisted; its setter only restores the stored name of a step whose type is unavailable (`UnknownDeploymentStep`).

Always repeat `[JsonIgnore]` on the `Category` and `Title` overrides: `System.Text.Json` does not inherit the base property's attribute, and a `LocalizationSource` cannot be serialized, so saving a plan containing the step fails without it.

Every object added to `DeploymentPlanResult.Steps` is a recipe step consumed on import. Emit an existing recipe step or ship and enable a matching custom `IRecipeStepHandler` on the target tenant. The example emits `MySettings`, so its companion recipe handler must be available there.

```csharp
using OrchardCore.Localization;

namespace MyModule;

internal static class MyModuleLocalizationSources
{
    public static readonly LocalizationSource MySettings = LocalizationSource.Create("My Settings", typeof(MyModuleLocalizationSources));
}
```

```csharp
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace MyModule.Deployment;

public sealed class MySettingsDeploymentStep : DeploymentStep
{
    public override string Name => "MySettings";

    [JsonIgnore]
    public override LocalizationSource Category => CommonLocalizationSources.Settings;

    [JsonIgnore]
    public override LocalizationSource Title => MyModuleLocalizationSources.MySettings;

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

- `DeploymentStep.Category` and `DeploymentStep.Title` are read-only virtual `LocalizationSource` properties instead of settable `LocalizedString` ones, and `Name` is virtual:
  - Before: `public MyStep(IStringLocalizer<MyStep> S) { Name = "MyStep"; Category = S["Content"]; Title = S["My Step"]; }`
  - After: `public override string Name => "MyStep";`, `[JsonIgnore] public override LocalizationSource Category => CommonLocalizationSources.Content;` and `[JsonIgnore] public override LocalizationSource Title => MyModuleLocalizationSources.MyStep;`.
  - Assigning `Category` or `Title` in a constructor no longer compiles.
- Remove constructors that took an `IStringLocalizer<TStep>` only to set the labels; built-in steps no longer have them.
- PO contexts of the built-in step labels changed: shared categories such as "Content Management" use `msgctxt "OrchardCore.Localization.CommonLocalizationSources"`, and titles use the module's `*LocalizationSources` class (for example `OrchardCore.Lucene.LuceneLocalizationSources`) instead of the step type. Update the `msgctxt` of PO entries that translated these labels; entries without `msgctxt` still apply.
- Code that displays `step.Category` or `step.Title` must translate it with `IStringLocalizerFactory.Localize(step.Title)`.
- `OrchardCore.Deployment.CommonPermissions` and `OrchardCore.Deployment.Remote.Permissions` were removed. Use `DeploymentPermissions.ManageDeploymentPlan`, `Export`, `Import`, `ManageRemoteInstances`, `ManageRemoteClients`, and `ExportRemoteInstances` (replaces `Remote.Permissions.Export`). Stored role permission names are unchanged.
- Deployment and recipe archives are written under `OrchardCore:TempDirectory:Path` instead of `Path.GetTempPath()`.
- Custom File step file names and paths passed to `TemporaryFileBuilder.SetFileAsync()` (the `result.FileBuilder` of file exports) that are rooted, drive-qualified or contain `..` segments escaping the package folder are now rejected.

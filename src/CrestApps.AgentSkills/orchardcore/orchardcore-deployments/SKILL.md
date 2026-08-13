---
name: orchardcore-deployments
description: Skill for authoring Orchard Core deployment plans and custom deployment steps. Covers plan creation, plan recipes, export targets, remote deployment, built-in step selection, and end-to-end extensibility. Use this skill when requests mention Orchard Core Deployments, Create a Deployment Plan, Deployment Step Types, Export or Import a Deployment Package, Remote Deployment, Deployment Plan Recipe Export, or Creating Deployment Steps in Code. Strong matches include work with OrchardCore.Deployment, OrchardCore.Deployment.Remote, IDeploymentSource, IDeploymentTargetHandler, DeploymentStep, AddDeployment, AddDeploymentTargetHandler, DeploymentPlansRecipeStep, and DeploymentPlanResult. It also helps with Deployment Plan Recipe Export, custom step display drivers, and the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Deployments

Use deployment plans to export configuration or content as a recipe package, then import that package into another tenant. A plan is an ordered collection of deployment steps. Enable the feature that owns each selected step on the source and target tenant before creating, exporting, or importing the plan.

## Create and run a deployment plan

1. Enable `OrchardCore.Deployment` and the features that contribute the required steps.
2. In **Configuration → Deployment Plans**, create a plan, add steps, and configure each step.
3. Use the plan's **Export** action to download a deployment package. It is a recipe which the target imports through the Recipes administration UI or setup flow.
4. Use the plan's **Execute** action only when a configured deployment target is available. The Recipes feature supplies the normal recipe deployment target.

Order steps deliberately. For example, enable features first, define content types before content, configure indexes before requesting a rebuild, and create workflow definitions only after their activity-providing features are enabled.

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

The `deployment` recipe step creates or updates plans; it does **not** export their package. Export or execute the resulting plan from the Deployment Plans UI.

## Built-in deployment steps in release/3.0

Only steps whose owning feature is enabled appear in the picker. The complete set registered by framework modules is below. Steps that rebuild, reset, or export to a target are operational steps rather than configuration export steps.

| Area | Registered step types |
|---|---|
| Deployment | `CustomFileDeploymentStep`, `RecipeFileDeploymentStep`, `DeploymentPlanDeploymentStep`, `JsonRecipeDeploymentStep` |
| Content | `AllContentDeploymentStep`, `ContentDeploymentStep`, `ContentItemDeploymentStep`, `ExportContentToDeploymentTargetDeploymentStep` |
| Content definitions | `ContentDefinitionDeploymentStep`, `ReplaceContentDefinitionDeploymentStep`, `DeleteContentDefinitionDeploymentStep` |
| Core configuration | `AllFeaturesDeploymentStep`, `SiteSettingsDeploymentStep`, `ThemesDeploymentStep`, `PlacementsDeploymentStep`, `AllLayersDeploymentStep`, `AllRolesDeploymentStep`, `AllQueriesDeploymentStep`, `CustomSettingsDeploymentStep` |
| Media and templates | `MediaDeploymentStep`, `AllMediaProfilesDeploymentStep`, `AllTemplatesDeploymentStep`, `AllAdminTemplatesDeploymentStep`, `AllShortcodeTemplatesDeploymentStep` |
| Localization, users, tenants | `TranslationsDeploymentStep`, `AllDataTranslationsDeploymentStep`, `AllUsersDeploymentStep`, `CustomUserSettingsDeploymentStep`, `AllFeatureProfilesDeploymentStep` |
| Workflow, menus, maps, search settings | `AllWorkflowTypeDeploymentStep`, `AdminMenuDeploymentStep`, `AllSitemapsDeploymentStep`, `SearchSettingsDeploymentStep` |
| Indexing | `IndexProfileDeploymentStep`, `RebuildIndexDeploymentStep`, `ResetIndexDeploymentStep` |
| Lucene | `LuceneIndexDeploymentStep`, `LuceneIndexRebuildDeploymentStep`, `LuceneIndexResetDeploymentStep` |
| Elasticsearch | `ElasticsearchIndexDeploymentStep`, `ElasticsearchIndexRebuildDeploymentStep`, `ElasticsearchIndexResetDeploymentStep` |
| Azure AI Search | `AzureAISearchIndexDeploymentStep`, `AzureAISearchIndexRebuildDeploymentStep`, `AzureAISearchIndexResetDeploymentStep` |
| Authentication | `FacebookLoginDeploymentStep`, `AzureADDeploymentStep`, `MicrosoftAccountDeploymentStep`, `OpenIdServerDeploymentStep`, `OpenIdValidationDeploymentStep` |

Do not guess step JSON. Create the plan in the admin UI and use its export as the authoritative payload, especially for steps with feature-specific options.

## Remote deployment

Enable `OrchardCore.Deployment.Remote` on the source and target along with `OrchardCore.Deployment`. In **Configuration → Remote Instances**, register the remote endpoint; in **Remote Clients**, configure the client credentials/permissions used to call it. The remote UI supports exporting a local plan to a remote instance and importing a remote plan/package locally. Treat the endpoint and its credentials as production secrets, use HTTPS, and ensure the target has every feature required by the imported steps.

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

1. Derive a serializable model from `DeploymentStep`, set a stable `Name`, and add only settings that the editor needs.
2. Implement `IDeploymentSource`, preferably by deriving from `DeploymentSourceBase<TStep>`, and append valid recipe JSON to `DeploymentPlanResult.Steps` or files through `result.FileBuilder`.
3. Add a `DisplayDriver<DeploymentStep, TStep>` and the corresponding summary, thumbnail, and edit Razor shapes when the step is configurable.
4. Register the source, step factory, JSON derived type, and driver with the exact `AddDeployment<TSource, TStep, TDisplayDriver>()` overload.
5. If implementing a destination for executing packages rather than a source step, implement `IDeploymentTargetHandler` and register it separately with `AddDeploymentTargetHandler<TImplementation>()`.

Every object added to `DeploymentPlanResult.Steps` is a recipe step consumed on import. Emit an existing recipe step or ship and enable a matching custom `IRecipeStepHandler` on the target tenant. The example emits `MySettings`, so its companion recipe handler must be available there.

```csharp
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace MyModule.Deployment;

public sealed class MySettingsDeploymentStep : DeploymentStep
{
    public MySettingsDeploymentStep()
    {
        Name = nameof(MySettingsDeploymentStep);
    }

    public MySettingsDeploymentStep(IStringLocalizer<MySettingsDeploymentStep> localizer)
        : this()
    {
        Category = localizer["Settings"];
    }
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
}
```

Add `Views/Items/MySettingsDeploymentStep.Fields.Summary.cshtml` and `Views/Items/MySettingsDeploymentStep.Fields.Thumbnail.cshtml`. Add an edit view and override `Edit`/`UpdateAsync` when the step has editable properties.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
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

`AddDeployment<TSource, TStep, TDisplayDriver>()` registers the source transiently, a deployment-step factory, derived JSON metadata, and the display driver. Use `AddDeploymentWithoutSource<TStep, TDisplayDriver>()` only for a step that has no source, such as `RecipeFileDeploymentStep`.

## Deployment target handlers

An `IDeploymentTargetHandler` consumes a generated `DeploymentPlanResult` for a specific target. It is not a replacement for `IDeploymentSource`. Register it independently:

```csharp
services.AddDeploymentTargetHandler<MyDeploymentTargetHandler>();
```

The framework Recipes module demonstrates this split by registering `RecipeDeploymentTargetHandler`, while the Deployment module registers plan sources and steps.

---
name: orchardcore-features
description: Explains OrchardCore feature management including enabling and disabling features, the Features admin list with bulk Enable and Disable actions, programmatic control with IShellFeaturesManager and UpdateFeaturesAsync, dependencies, manifest declarations, recipe activation, the AllFeatures deployment step, and feature guards. Covers FeatureAttribute (including EnabledByDependencyOnly) versus RequireFeaturesAttribute gating, composing a startup for every tenant with RequiredStartupAttribute, and contributing features with IFeaturesProvider. Use this skill when requests mention OrchardCore Features, Feature Management from the Admin Dashboard, Programmatic Feature Control with IShellFeaturesManager, Enabling and Disabling Features, FeatureAttribute vs RequireFeaturesAttribute, EnabledByDependencyOnly, [Feature], [RequireFeatures] or [RequiredStartup] on a Startup class, FeaturesBulkAction, FeaturesPermissions, theme features, or closely related Orchard Core setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# OrchardCore Features

Features are enabled per tenant. Manage them at **Configuration → Features** or
through `IShellFeaturesManager`. Resolve IDs from the module manifest rather
than inferring them from an assembly or package name.

## Features Admin List

- Requires the `ManageFeatures` permission (`OrchardCore.Features.FeaturesPermissions.ManageFeatures`), granted to `Administrator` by default.
- Enabling a feature also enables its dependencies; disabling a feature also disables the features that depend on it. Always-enabled features cannot be turned off.
- Select several features and use **Actions** → **Enable** or **Disable** for bulk changes. The `FeaturesBulkAction` enum has `None`, `Enable`, and `Disable`.
- The list renders one `AdminList` per category, all named `Features` (`FeaturesAdminList.Name`), so it follows the site's List/Grid layout (see `orchardcore-admin-lists`).
- Extra `[Feature]` attributes declared in a theme's manifest alongside `[Theme]` are listed with their category and an **Enable** button.

## Query and Update Features

```csharp
public sealed class FeatureToggleService
{
    private readonly IShellFeaturesManager _features;

    public FeatureToggleService(IShellFeaturesManager features)
    {
        _features = features;
    }

    public async Task EnableAsync(string featureId)
    {
        var available = await _features.GetAvailableFeaturesAsync();
        var feature = available.SingleOrDefault(x => x.Id == featureId)
            ?? throw new InvalidOperationException($"Feature '{featureId}' is unavailable.");

        await _features.UpdateFeaturesAsync(
            featuresToDisable: [],
            featuresToEnable: [feature],
            force: false);
    }
}
```

`UpdateFeaturesAsync` makes a single atomic update when enabling and disabling
features together:

```csharp
await _features.UpdateFeaturesAsync(
    featuresToDisable: [luceneFeature],
    featuresToEnable: [elasticsearchFeature],
    force: false);
```

## Declare a Feature vs. Gate on a Feature

There are two attributes, and picking the right one keeps the tenant's
**Features** list small and understandable. Prefer gating over declaring: every
new feature is one more switch the user must learn, enable, and remember.

| Goal | Use | Effect |
|---|---|---|
| Give the user a switch to turn a capability on or off | `[assembly: Feature(...)]` (`FeatureAttribute`) | A managed feature that appears in **Configuration → Features** |
| Group reusable services so several features share them without repeating registrations | `[assembly: Feature(..., EnabledByDependencyOnly = true)]` | A feature the user cannot toggle directly; auto-enabled whenever a dependent feature is enabled |
| Scope a `Startup`'s registrations to a specific declared feature | `[Feature("FeatureId")]` on the `StartupBase` class | Those services register only when that feature is enabled |
| Extend, override, or add behavior only when other features are present | `[RequireFeatures("A", "B")]` on the `StartupBase` class | Those services register only when **all** listed features are enabled — no new feature is introduced |

### Declare a managed feature with `FeatureAttribute`

Use a declared feature when the capability is standalone and the user should
choose whether it runs. See **Manifest Declarations** below for the `[assembly:
Feature(...)]` syntax.

For a feature that only exists to **group reusable services** — services several
other features consume — mark it `EnabledByDependencyOnly = true`. It is enabled
automatically by dependency and is hidden from the direct enable/disable list, so
you register the shared services once instead of repeating them in every
consuming feature.

```csharp
[assembly: Feature(
    Id = "MyModule.Core",
    Name = "My Module Core",
    Description = "Shared services consumed by the other My Module features.",
    Category = "My Module",
    EnabledByDependencyOnly = true
)]
```

Other features then depend on `MyModule.Core`, which activates it and its shared
services automatically. Explicitly enabling such a feature is only useful for a
custom module that wants to consume those services on its own.

### Gate behavior with `RequireFeaturesAttribute`

Use `[RequireFeatures(...)]` to add or override behavior that is only relevant
when another feature is present, **without** introducing a new feature the user
must manage. This is the preferred tool whenever the behavior is an extension of
existing features rather than an independent capability.

```csharp
[RequireFeatures("OrchardCore.Contents", "OrchardCore.Workflows")]
public sealed class ContentWorkflowStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registered only when both Contents and Workflows are enabled.
        // No separate feature is added for the user to discover or toggle.
        services.AddScoped<IWorkflowTask, PublishContentTask>();
    }
}
```

A module may have several `StartupBase` classes: an unattributed one for its
core services, plus `[RequireFeatures(...)]` classes that light up integrations
as their target features come online.

### Compose a startup for every tenant with `RequiredStartupAttribute`

`[RequiredStartup]` (namespace `OrchardCore.Modules`, Orchard Core 4.0) composes a
public component type for every tenant even when the feature that defines it is
disabled. Its services are attributed to `Application.DefaultFeatureId`. Combine
it with `[RequireFeatures(...)]` to compose it only when all listed features are
enabled. Use it sparingly, for plumbing that must exist regardless of the
module's own feature state.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

[RequiredStartup]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class AlwaysOnDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registered for every tenant where Deployment is enabled,
        // even if this module's own feature is disabled.
    }
}
```

### Contribute features with `IFeaturesProvider`

`IFeaturesProvider` (`OrchardCore.Environment.Extensions.Features`) builds the
`IFeatureInfo` list of each extension. In 4.0 the extension manager resolves
every registered provider (`GetServices<IFeaturesProvider>()`) instead of a
single one, so a host-level subsystem can add its own provider (as
`OrchardCore.DisplayManagement` does for themes) instead of replacing the
default. Custom implementations that replaced the single registration must be
added to the collection instead.

## Manifest Declarations

Use the current Pascal-case named arguments supported by the manifest
attributes:

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "My Module",
    Author = "My Organization",
    Version = "1.0.0",
    Description = "Provides reporting.",
    Category = "Content"
)]

[assembly: Feature(
    Id = "MyModule.Reporting",
    Name = "Reporting",
    Description = "Provides reporting dashboards.",
    Category = "Content",
    Dependencies = ["OrchardCore.Contents", "OrchardCore.Queries"]
)]
```

Key manifest options on `FeatureAttribute`:

- `Dependencies` — features that must be enabled first; enabling this feature enables them.
- `EnabledByDependencyOnly = true` — the feature is hidden from direct enable/disable and is only turned on when a dependent feature is enabled. Use it to group shared services (see **Declare a managed feature with `FeatureAttribute`** above).
- `IsAlwaysEnabled = true` — an always-on feature that cannot be disabled.
- `DefaultTenantOnly = true` — only the default (root) tenant can enable or disable the feature.

Do not bypass dependency validation with `force` unless the tenant lifecycle
explicitly requires it.

## Export Features with a Deployment Plan

Add the **All Features** deployment step (`AllFeatures`, category
**Infrastructure**) to a deployment plan to export a `Feature` recipe step with
the enabled features, and the disabled ones unless **Ignore disabled features**
(`IgnoreDisabledFeatures`) is checked.

## Recipe Activation

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        "OrchardCore.Title"
      ],
      "disable": [
        "OrchardCore.Lucene"
      ]
    }
  ]
}
```

For current search feature IDs, use `OrchardCore.Elasticsearch` or
`OrchardCore.Lucene` for the provider and their `OrchardCore.Search.*`
features only when the corresponding search integration is needed.

## Upgrading from 3.x

- The **Toggle** bulk action was removed from the Features admin list and `FeaturesBulkAction.Toggle` no longer exists; call `FeatureService.EnableOrDisableFeaturesAsync` with `FeaturesBulkAction.Enable` or `FeaturesBulkAction.Disable`.
- `OrchardCore.Features.Permissions.ManageFeatures` was removed; use `OrchardCore.Features.FeaturesPermissions.ManageFeatures`.
- `IFeaturesProvider` is resolved as a collection; register additional providers instead of replacing the default one.

---
name: orchardcore-features
description: Explains OrchardCore feature management including enabling and disabling features, programmatic control with IShellFeaturesManager and UpdateFeaturesAsync, dependencies, manifest declarations, recipe activation, and feature guards. Use this skill when requests mention OrchardCore Features, Enabling the Features Module, Feature Management from the Admin Dashboard, Programmatic Feature Control with IShellFeaturesManager, Querying Feature States, Enabling and Disabling Features, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# OrchardCore Features

Features are enabled per tenant. Manage them at **Configuration → Features** or
through `IShellFeaturesManager`. Resolve IDs from the module manifest rather
than inferring them from an assembly or package name.

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

Use `[RequireFeatures]` on a startup class when a service is available only
with optional features:

```csharp
[RequireFeatures("OrchardCore.Contents", "OrchardCore.Workflows")]
public sealed class ContentWorkflowStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowTask, PublishContentTask>();
    }
}
```

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

An always-on feature uses `IsAlwaysEnabled = true`. Do not bypass dependency
validation with `force` unless the tenant lifecycle explicitly requires it.

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

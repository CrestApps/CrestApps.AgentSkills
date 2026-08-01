# OrchardCore Feature Examples

## Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        "OrchardCore.Title",
        "OrchardCore.Html",
        "OrchardCore.Layers",
        "OrchardCore.Widgets"
      ]
    }
  ]
}
```

## Programmatic Update

```csharp
public sealed class FeatureActivator
{
    private readonly IShellFeaturesManager _features;

    public FeatureActivator(IShellFeaturesManager features)
    {
        _features = features;
    }

    public async Task SwitchSearchProviderAsync()
    {
        var available = await _features.GetAvailableFeaturesAsync();
        var lucene = available.Single(x => x.Id == "OrchardCore.Lucene");
        var elasticsearch = available.Single(x => x.Id == "OrchardCore.Elasticsearch");

        await _features.UpdateFeaturesAsync(
            featuresToDisable: [lucene],
            featuresToEnable: [elasticsearch],
            force: false);
    }
}
```

## Manifest With Dependencies

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "My Custom Module",
    Author = "My Organization",
    Version = "1.0.0",
    Description = "Provides custom reporting.",
    Category = "Content"
)]

[assembly: Feature(
    Id = "MyModule.Reporting",
    Name = "Reporting",
    Description = "Reporting dashboards.",
    Category = "Content",
    Dependencies = ["MyModule.Core", "OrchardCore.Contents"]
)]
```

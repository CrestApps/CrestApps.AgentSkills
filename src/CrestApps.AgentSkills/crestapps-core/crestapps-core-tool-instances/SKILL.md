---
name: crestapps-core-tool-instances
description: Skill for registering user-configured CrestApps.Core AI tool instances and their source blueprints.
---

# CrestApps.Core Tool Instances - Prompt Templates

## Create Configurable AI Tools

You are a CrestApps.Core expert. Generate code and guidance for developer-authored tool sources and user-configured `AIToolInstance` entries.

### Guidelines

- A source implements `IAIToolInstanceSource` and turns one configured `AIToolInstance` into an `AITool`.
- An instance stores its source name in `Source`, its stable unique name in `Name`, a model-facing explanation in `Description`, and source-specific settings in `Properties`.
- Register sources with `AddSource<TSource>(name, configure)`. The source is a keyed scoped `IAIToolInstanceSource` using the same `name` stored on each instance.
- Register a persistence provider such as `AddYesSqlStores()` or `AddEntityCoreStores()` on the tool-instances builder. The feature does not choose one for you.
- Use `instance.GetFunctionName()` and `instance.Description` in a source. The generated function name uses the `tool_instance_` prefix, provider-safe characters, a 64-character cap, and a deterministic hash when normalization is lossy.

### Register a Source

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddToolInstances(toolInstances => toolInstances
            .AddYesSqlStores()
            .AddSource<MyToolInstanceSource>("my-service", options =>
            {
                options.DisplayName = new LocalizedString("my-service", "My Service");
                options.Description = new LocalizedString("my-service", "Calls the configured service.");
                options.Category = new LocalizedString("Integrations", "Integrations");
            }))));
```

`AddToolInstances(...)` registers the catalog and completion-context handler. Its `useDefaultRegistry` argument is `true` by default, which also registers `ToolInstanceRegistryProvider`.

### Implement a Source

```csharp
using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;

public sealed class MyToolInstanceSource : IAIToolInstanceSource
{
    public AITool CreateTool(AIToolInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var settings = instance.TryGet<MyToolSettings>(out var stored)
            ? stored
            : new MyToolSettings();

        var functionName = instance.GetFunctionName();
        var description = string.IsNullOrWhiteSpace(instance.Description)
            ? functionName
            : instance.Description;

        return CreateConfiguredTool(functionName, description, settings);
    }

    private static AITool CreateConfiguredTool(
        string functionName,
        string description,
        MyToolSettings settings)
    {
        throw new NotImplementedException();
    }
}

public sealed class MyToolSettings
{
    public string Endpoint { get; set; }
}
```

Replace `CreateConfiguredTool` with the application's `AITool` implementation. Keep credentials in instance settings protected with ASP.NET Core Data Protection; never expose those values as model arguments.

### Control Exposure

`ToolInstanceRegistryProvider` looks up names in `AICompletionContext.ToolInstanceNames`, resolves each instance from `INamedCatalog<AIToolInstance>`, then resolves its keyed source and creates a `ToolRegistryEntry`.

For per-instance authorization, derive a **sealed** provider and override `ShouldIncludeInstanceAsync(AIToolInstance, AICompletionContext, CancellationToken)`. Register that provider instead of the default by calling `AddToolInstances(useDefaultRegistry: false)`.


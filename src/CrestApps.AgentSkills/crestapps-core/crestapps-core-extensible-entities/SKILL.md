---
name: crestapps-core-extensible-entities
description: Skill for storing typed extension data on CrestApps.Core extensible entities (ExtensibleEntity, CatalogItem, AIProfile, AIDeployment, ChatInteraction and others). Covers the Properties bag and JsonExtensionDataConverter, the Put, TryGet, GetOrCreate, Get, Alter, Has and Remove extension methods with their generic constraints, key naming by type name, supplying the same metadata from configuration or recipes through the Properties object (for example AIDeploymentMetadata capabilities), and configuring ExtensibleEntityJsonOptions through the options pattern or the static ExtensibleEntityExtensions.JsonSerializerOptions. Use it when attaching custom metadata to CrestApps.Core models or troubleshooting how that metadata serializes.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Extensible Entities

You are a CrestApps.Core expert. Generate code for typed metadata stored in `ExtensibleEntity.Properties`.

## Guidelines

- `ExtensibleEntity` (namespace `CrestApps.Core`) exposes `Properties`, an ordinal-ignore-case `IDictionary<string, object>`. `CatalogItem` derives from it, so every catalog model (profiles, deployments, connections, interactions, personas, data sources) carries a property bag.
- `Properties` serializes as a nested `Properties` JSON object through `JsonExtensionDataConverter`, which keeps JSON values as `JsonNode`. Do not replace it with `[JsonExtensionData]`; flattened root-level values are not read back as metadata.
- Use the extension methods in `ExtensibleEntityExtensions` instead of casting dictionary values.
- The default key is the type name (`typeof(T).Name`), so keep one metadata object per type. Use `Put(name, value)` and `Get<T>(name)` for explicit keys.
- Name metadata classes uniquely; two classes with the same name in different namespaces share a key.

## Methods

| Method | Constraint | Behavior |
|---|---|---|
| `Put<T>(value)` | `T : new()` | Stores under `typeof(T).Name` and returns the entity |
| `Put(name, value)` | none | Stores under an explicit key |
| `TryGet<T>(out value)` | `T : class` | Returns `false` when the key is missing or null; no allocation |
| `GetOrCreate<T>()` | `T : new()` | Returns the stored value or a **new, unstored** instance |
| `Get<T>(name)` | none | Reads an explicit key, or `default` |
| `Alter<T>(action)` | `T : new()` | Gets or creates, runs the action, and stores the result |
| `Has<T>()` | none | Checks for the type-name key |
| `Remove<T>()` | none | Removes the type-name key |

`TryGet`, `GetOrCreate`, `Get` and `Alter` accept an optional `JsonSerializerOptions` to override the shared options for one call.

## Typed Metadata

```csharp
using CrestApps.Core;

namespace MyApp.Metadata;

public sealed class BillingMetadata
{
    public string InvoicePrefix { get; set; }

    public decimal MonthlyBudget { get; set; }
}
```

```csharp
profile.Put(new BillingMetadata
{
    InvoicePrefix = "INV",
    MonthlyBudget = 250m,
});

if (profile.TryGet<BillingMetadata>(out var billing))
{
    logger.LogInformation("Budget {Budget}", billing.MonthlyBudget);
}

profile.Alter<BillingMetadata>(metadata => metadata.MonthlyBudget = 300m);

// Persist the change through the manager or catalog; the bag is only in memory until saved.
await profileManager.UpdateAsync(profile);
```

`GetOrCreate<T>()` does not store the new instance. Call `Put(...)` after mutating it, or use `Alter<T>(...)`.

## Framework Metadata Uses the Same Bag

Framework features keep their settings in the bag. For example, deployment capabilities are an `AIDeploymentMetadata` entry:

```csharp
using CrestApps.Core;
using CrestApps.Core.AI.Models;

deployment.Put(new AIDeploymentMetadata
{
    Features =
    [
        AIDeploymentFeatureNames.TextGeneration,
        AIDeploymentFeatureNames.ToolCalling,
        AIDeploymentFeatureNames.Streaming,
    ],
});
```

Because the key is the type name, configuration and recipes supply the same data under `Properties`:

```json
{
  "Name": "gpt-4.1",
  "ConnectionName": "primary-openai",
  "ModelName": "gpt-4.1",
  "Properties": {
    "AIDeploymentMetadata": {
      "Features": [ "textGeneration", "toolCalling", "streaming" ]
    }
  }
}
```

Configured deployments also accept `Features` and `Parameters` directly on the entry as a shorter form of the same keys; see `crestapps-core-ai-model-capabilities`.

## Configure Serialization

`AddCrestAppsCore(...)` registers `ExtensibleEntityJsonOptions` and an internal hosted service that copies `ExtensibleEntityJsonOptions.SerializerOptions` into the static `ExtensibleEntityExtensions.JsonSerializerOptions` when the host starts.

```csharp
builder.Services.Configure<ExtensibleEntityJsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new MoneyJsonConverter());
});
```

`ExtensibleEntityJsonOptions.CreateDefaultSerializerOptions()` sets `WhenWritingNull`, `JsonObjectCreationHandling.Populate`, case-insensitive names, skipped comments, trailing commas, number reading from strings, and `JsonStringEnumConverter`.

Without a started host (a console tool, a unit test that builds a `ServiceProvider` directly, or a host that does not call `AddCrestAppsCore`), assign the static options before the first read or write:

```csharp
var options = ExtensibleEntityJsonOptions.CreateDefaultSerializerOptions();
options.Converters.Add(new MoneyJsonConverter());

ExtensibleEntityExtensions.JsonSerializerOptions = options;
```

`JsonSerializerOptions` becomes read-only after its first use, so configure it during startup only.

## Troubleshooting

- **Metadata comes back empty after a save**: the entity was mutated through `GetOrCreate<T>()` without `Put`, or the change never reached a commit boundary.
- **Values appear at the JSON root**: a custom serializer bypassed `JsonExtensionDataConverter`; serialize the entity with the property's converter intact.
- **A custom converter is ignored**: the static options were already used before the hosted service ran; set `ExtensibleEntityExtensions.JsonSerializerOptions` earlier or avoid reading entities before host start.

## Upgrading from 1.x

- `AIDeployment.Purpose` and `AIDeployment.Type` are gone; capabilities live in the `AIDeploymentMetadata` entry of the bag. Records that still carry a legacy purpose are projected onto `AIDeploymentMetadata` features when they are deserialized.

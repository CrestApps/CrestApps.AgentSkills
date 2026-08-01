---
name: orchardcore-time-zones
description: Skill for managing friendly named IANA time-zone maps in CrestApps Orchard Core. Covers mapped time-zone selectors, catalog management, recipe import, deployment export, seeded maps, and consuming ITimeZoneSelectListProvider. Use this skill when requests mention Orchard Core time zones, friendly time-zone labels, time-zone maps, TimeZoneMaps recipes, or time-zone deployment. Strong matches include work with CrestApps.OrchardCore.TimeZones, TimeZoneMap, ITimeZoneSelectListProvider, MappedTimeZoneSelectListProvider, TimeZoneMapStep, and TimeZoneMapDeploymentStep.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Time Zones - Prompt Templates

## Configure Friendly Time-Zone Maps

You are an Orchard Core expert. Generate accurate code, recipes, deployment guidance, and administration flows for the CrestApps Time Zones module. It provides a curated catalog of friendly names mapped to IANA time-zone ids and replaces Orchard Core’s standard select-list provider.

### Guidelines

- Install `CrestApps.OrchardCore.TimeZones` in the web/startup project.
- Enable `CrestApps.OrchardCore.TimeZones`; it depends on `OrchardCore.Recipes.Core`.
- Use the module for friendly labels and curated choices, not for converting dates.
- Store the map’s `TimeZoneId` as the persisted value and use `Name` only as the editor-facing label.
- Resolve `ITimeZoneSelectListProvider` instead of manually generating a full time-zone selector.
- Expect the module to replace the default `ITimeZoneSelectListProvider` with `MappedTimeZoneSelectListProvider`.
- Keep `TimeZoneMap.Name` unique and treat it as immutable after creation.
- Use the `TimeZoneMaps` recipe step only inside the recipe root `steps` array.
- Enable `OrchardCore.Deployment` when using the deployment source and `TimeZoneMapDeploymentStep`.
- Use IANA identifiers such as `America/New_York`, not Windows time-zone ids.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Feature and Services

| Item | Value |
|---|---|
| Package | `CrestApps.OrchardCore.TimeZones` |
| Feature ID | `CrestApps.OrchardCore.TimeZones` |
| Entity | `TimeZoneMap` |
| Selector abstraction | `ITimeZoneSelectListProvider` |
| Replacement implementation | `MappedTimeZoneSelectListProvider` |
| Recipe step | `TimeZoneMaps` |
| Deployment step | `TimeZoneMapDeploymentStep` |

### Enable the Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.TimeZones"
      ],
      "disable": []
    }
  ]
}
```

## How Maps Work

`TimeZoneMap` is a catalog entry that joins an editor-friendly `Name` with an IANA `TimeZoneId`. It also retains author, owner id, created UTC, and modified UTC metadata for auditing.

For example, a map may store:

| Name | TimeZoneId |
|---|---|
| Eastern Time (US & Canada) | `America/New_York` |
| India Standard Time | `Asia/Kolkata` |
| Japan Standard Time | `Asia/Tokyo` |

The initial migration executes an embedded `default-timezones` recipe, creating common worldwide starter maps. Those maps are editable or removable after setup.

## Admin Management

After enabling the feature, manage maps from the Time Zones administration area. The feature registers the `ManageTimeZoneMaps` permission and an admin navigation provider.

Create one record per approved choice:

1. Choose a clear unique name.
2. Enter a valid IANA time-zone identifier.
3. Save the map.
4. Edit or delete outdated mappings when business policies change.

Names are unique and immutable after creation. Update a mapping only when the existing semantic meaning should retain its identifier; create a new map when the label represents a new choice.

## Consume the Mapped Selector

The module replaces Orchard Core’s time-zone selector service:

```csharp
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Modules;

namespace MyCompany.OrchardCore.Scheduling;

public sealed class ScheduleTimeZoneOptions
{
    private readonly ITimeZoneSelectListProvider _timeZoneSelectListProvider;

    public ScheduleTimeZoneOptions(ITimeZoneSelectListProvider timeZoneSelectListProvider)
    {
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
    }

    public ValueTask<IReadOnlyList<SelectListItem>> GetItemsAsync()
    {
        return _timeZoneSelectListProvider.GetTimeZoneSelectListItemsAsync();
    }
}
```

`ITimeZoneSelectListProvider.GetTimeZoneSelectListItemsAsync()` returns display
items. `GetTimeZoneSelectListAsync(CancellationToken)` returns key/value pairs
when that is the consumer's required shape. Resolve the interface rather than
hard-coding a time-zone array or bypassing the curated maps.

`MappedTimeZoneSelectListProvider` orders maps by `Name` and then `TimeZoneId`. Consumers receive select-list items and matching key/value data based on the catalog.

## Import Maps with Recipes

The `TimeZoneMaps` recipe step creates or updates maps. It matches an existing map by `ItemId` when supplied, then falls back to the unique `Name`.

```json
{
  "steps": [
    {
      "name": "TimeZoneMaps",
      "Maps": [
        {
          "Name": "Eastern Time (US & Canada)",
          "TimeZoneId": "America/New_York",
          "OwnerId": "[js: parameters('AdminUserId')]",
          "Author": "[js: parameters('AdminUsername')]"
        },
        {
          "Name": "India Standard Time",
          "TimeZoneId": "Asia/Kolkata",
          "OwnerId": "[js: parameters('AdminUserId')]",
          "Author": "[js: parameters('AdminUsername')]"
        }
      ]
    }
  ]
}
```

The recipe step can also accept `CreatedUtc` and `ModifiedUtc` to preserve audit history. Only enable the step in tenants where `CrestApps.OrchardCore.TimeZones` and `OrchardCore.Recipes.Core` are enabled.

## Export Maps with Deployment

Enable `OrchardCore.Deployment` to register the deployment source and `TimeZoneMapDeploymentStep`. In a deployment plan, select all maps or a subset. The exported payload uses the same `TimeZoneMaps` recipe shape and can be imported into a destination tenant.

Use deployment when moving curated time-zone governance between tenants. It exports maps, not user profile choices or arbitrary schedule data.

## Choosing Correct IDs

Use IANA identifiers:

| Correct | Do not use |
|---|---|
| `America/New_York` | `Eastern Standard Time` |
| `Europe/London` | `GMT Standard Time` |
| `Asia/Kolkata` | `India Standard Time` |

The friendly name can use business language, but the mapped value must remain an IANA identifier understood by the target date/time infrastructure.

## Customization Pattern

Use catalog management rather than replacing the select-list provider. A custom form can consume the same service and persist the chosen IANA id:

```csharp
namespace MyCompany.OrchardCore.Scheduling;

public sealed class AppointmentSettings
{
    public string TimeZoneId { get; set; } = string.Empty;
}
```

Validate a submitted `TimeZoneId` against the mapped selector before persisting it. This prevents an editor from submitting an unsupported or typo-prone arbitrary id.

Do not create a View Model as `sealed` if it is used for model binding. Domain and service classes should remain `sealed`.

## Troubleshooting

| Symptom | Check |
|---|---|
| Orchard selectors still show the full list | Ensure the Time Zones feature is enabled in the active tenant |
| A friendly label is missing | Create its `TimeZoneMap` entry and verify its unique name |
| A schedule fails to resolve a zone | Correct the stored value to a valid IANA `TimeZoneId` |
| Recipe import has no effect | Use the `TimeZoneMaps` step within `{ "steps": [...] }` and enable recipe support |
| Deployment step is unavailable | Enable `OrchardCore.Deployment` in addition to the Time Zones feature |
| Direct selector construction ignores maps | Inject `ITimeZoneSelectListProvider` so the replacement service is used |

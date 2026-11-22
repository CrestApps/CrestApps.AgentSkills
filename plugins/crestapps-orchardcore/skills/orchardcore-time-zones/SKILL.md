---
name: orchardcore-time-zones
description: Skill for friendly named IANA time-zone maps with the CrestApps Time Zones module in Orchard Core, and for showing dates in the local time zone. Covers the Tools > Time Zones admin list, the ManageTimeZoneMaps permission, ids validated against the tz database, the mapped ITimeZoneSelectListProvider that limits the site Default Time Zone and user time zone menus, the TimeZoneMaps recipe step, deployment export, seeded starter maps, and the CrestApps rule that every date and time is shown in the user's or site's zone, never UTC (ILocalClock, the DateTime shape, DateTime.SpecifyKind, ToUtcLenientAsync). Use this skill when requests mention Orchard Core time zones, friendly time-zone labels, time-zone maps, a missing time zone in a selector, TimeZoneMaps recipes, time-zone deployment, or dates showing in UTC. Strong matches include CrestApps.OrchardCore.TimeZones, TimeZoneMap, ITimeZoneSelectListProvider, MappedTimeZoneSelectListProvider, TimeZoneMapStep, ILocalClock and ToUtcLenientAsync.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Time Zones

## Configure Friendly Time-Zone Maps

You are an Orchard Core expert. Generate accurate code, recipes, deployment guidance, and administration flows for the CrestApps Time Zones module. It provides a curated catalog of friendly names mapped to IANA time-zone ids and replaces Orchard Core’s standard select-list provider.

### Guidelines

- Install `CrestApps.OrchardCore.TimeZones` in the web/startup project.
- Enable `CrestApps.OrchardCore.TimeZones`; it depends on `OrchardCore.Recipes.Core`.
- Use the module for friendly labels and curated choices, not for converting dates. Convert with Orchard Core's `ILocalClock` (see [Show Dates in the Local Time Zone](#show-dates-in-the-local-time-zone)).
- Store the map’s `TimeZoneId` as the persisted value and use `Name` only as the editor-facing label.
- Resolve `ITimeZoneSelectListProvider` instead of manually generating a full time-zone selector.
- Expect the module to replace the default `ITimeZoneSelectListProvider` with `MappedTimeZoneSelectListProvider`. Once enabled, every menu built from it (including the site **Default Time Zone** and the **User Time Zone** profile setting, and the Omnichannel contact and activity screens) offers only mapped zones.
- Keep `TimeZoneMap.Name` unique and treat it as immutable after creation. Each `TimeZoneId` can be mapped only once.
- `TimeZoneMapHandler` validates `TimeZoneId` against the NodaTime tz database (`DateTimeZoneProviders.Tzdb`) and stores the canonical id; unknown ids are rejected.
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

After enabling the feature, manage maps under **Tools > Time Zones**. The screen requires `TimeZonesConstants.Permissions.ManageTimeZoneMaps` (*Manage time zone maps*, declared with `LocalizationSource`), granted to **Administrator** by default. The list follows Orchard Core's `AdminList` standard (List or Grid layout per site and user; see the framework `orchardcore-admin-lists` skill) and shows each map's `TimeZoneId`, author display name and latest created or modified time as badges. Rows are the `TimeZoneMap_SummaryAdmin` shape.

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

`ITimeZoneSelectListProvider.GetTimeZoneSelectListItemsAsync()` returns `SelectListItem`s with the map `Name` as text and the `TimeZoneId` as value. `GetTimeZoneSelectListAsync(CancellationToken)` returns the same data as key/value pairs (key `TimeZoneId`, value `Name`). Resolve the interface rather than hard-coding a time-zone array or bypassing the curated maps.

`MappedTimeZoneSelectListProvider` orders maps by `Name` and then `TimeZoneId`. Consumers receive select-list items and matching key/value data based on the catalog.

## Show Dates in the Local Time Zone

CrestApps.OrchardCore 3.0 (#1303) made every module show dates and times in the local time zone instead of UTC: views and admin lists, reports and their CSV, Excel and PDF exports, emails, SMS, notifications, notices, timeline and audit text, and AI prompts that mention the current time. The local zone is what Orchard Core's `ILocalClock` returns: the user's own zone when **User Time Zone** (`OrchardCore.Users.TimeZone`) is on, else the site's **Default Time Zone** (both offered from the mapped list when this module is on). The TimeZones module itself did not change; follow the same rules in your own code.

| Rule | Do |
|---|---|
| Razor | `@await DisplayAsync(await New.DateTime(Utc: value, Format: "g"))`, or `ILocalClock.ConvertToLocalAsync` |
| C# | Inject `ILocalClock` and call `ConvertToLocalAsync(utc)` |
| Value read back from YesSql or JSON | Mark it first: `DateTime.SpecifyKind(utc, DateTimeKind.Utc)`; an unspecified kind is treated as server-local |
| Never | `DateTime.ToLocalTime()` (the server's zone, UTC in production), formatting a stored UTC value directly (`utc.ToString("g")`, `T["... {0:g}", utc]`), or a "(UTC)" label |
| Reports | Group rows by the local hour, weekday or day; Contact Center reports use `ReportTimeZone`, which reads `ILocalClock` |
| Editors | Show date-times in local time and convert the entered value back with `ILocalClock.ToUtcLenientAsync(local)` |
| JSON to the browser | The UTC instant with a `Z`; the page formats it |
| Exceptions | A pure date with no time (a due date, a tax effective date); a value deliberately shown in a contact's or lead's own zone; machine formats (APIs, recipe and JSON schemas, import/export columns, files scripts read) stay UTC |

`ToUtcLenientAsync` is an extension in `CrestApps.OrchardCore.Calendars` (`LocalClockExtensions`, project `CrestApps.OrchardCore.Calendars.Abstractions`). Unlike `ILocalClock.ConvertToUtcAsync`, a time a daylight-saving change skips moves forward by the gap and a time that happens twice takes the earlier one, so a form never fails on it.

```csharp
using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using OrchardCore.Modules;

public sealed class RenewalDateFormatter
{
    private readonly ILocalClock _localClock;

    public RenewalDateFormatter(ILocalClock localClock)
    {
        _localClock = localClock;
    }

    public async Task<string> FormatAsync(DateTime storedUtc)
    {
        var local = await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(storedUtc, DateTimeKind.Utc));

        return local.ToString("g", CultureInfo.CurrentCulture);
    }

    public Task<DateTime> ToUtcAsync(DateTime enteredLocal)
        => _localClock.ToUtcLenientAsync(enteredLocal);
}
```

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

`TimeZoneMapStep` is built on the shared `NamedCatalogRecipeStep<TimeZoneMap>` (matched by `ItemId`, then by `Name`), and `TimeZoneMapHandler` on `AuditedCatalogEntryHandlerBase<TimeZoneMap>` (`TimeZoneMap` implements `IAuditedModel`). The import reads only `Name` and `TimeZoneId`; the handler stamps `CreatedUtc`, `ModifiedUtc`, and, when a user runs the recipe, `OwnerId` and `Author`. `OwnerId`, `Author`, `CreatedUtc` and `ModifiedUtc` in a recipe (including the exported values) are currently not restored. The step handler is registered when `OrchardCore.Recipes.Core` is enabled, which the feature depends on. With `CrestApps.OrchardCore.Recipes` enabled, the step also has a JSON Schema (`TimeZoneMaps`) for validation and editor autocomplete; see the `orchardcore-crestapps-recipes` skill.

## Export Maps with Deployment

Enable `OrchardCore.Deployment` to register the deployment source and `TimeZoneMapDeploymentStep`. In a deployment plan, add **Time Zone Maps** (category **Infrastructure**; both labels are `LocalizationSource` values) and select all maps or a subset. The exported payload uses the same `TimeZoneMaps` recipe shape and can be imported into a destination tenant.

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

## Upgrading from 2.x

- The **Tools > Time Zones** list renders through Orchard Core's `AdminList` shape. A theme override of the whole list view must be redone; the List layout row is `TimeZoneMap_SummaryAdmin`, and the Grid layout is customized with `SummaryAdminGrid` placement.
- The `ManageTimeZoneMaps` description and the deployment step's title and category are `LocalizationSource` values, translated from PO files with the declaring type as context.
- The create and edit pages put **Save** and **Cancel** in Orchard Core's sticky bottom action bar (`<div class="action-bar-bottom">`) instead of an `ocat-wrapper` row; a theme override of `Views/Admin/Create.cshtml` or `Edit.cshtml` must move its buttons there, ordered as Orchard Core 4.0 orders actions: **Cancel** first, the `btn-primary` **Save** last.

## Troubleshooting

| Symptom | Check |
|---|---|
| Orchard selectors still show the full list | Ensure the Time Zones feature is enabled in the active tenant |
| The site or user time zone menu lacks a zone | Add a map for it; once the feature is on, only mapped zones are offered |
| Saving a map fails with "already exists" | Another map uses that `TimeZoneId`; each id can be mapped once |
| A friendly label is missing | Create its `TimeZoneMap` entry and verify its unique name |
| A schedule fails to resolve a zone | Correct the stored value to a valid IANA `TimeZoneId` |
| Recipe import has no effect | Use the `TimeZoneMaps` step within `{ "steps": [...] }` and enable recipe support |
| Deployment step is unavailable | Enable `OrchardCore.Deployment` in addition to the Time Zones feature |
| Direct selector construction ignores maps | Inject `ITimeZoneSelectListProvider` so the replacement service is used |
| A page or report shows UTC times | Convert through `ILocalClock` (or the `DateTime` shape) and mark stored values with `DateTime.SpecifyKind(..., DateTimeKind.Utc)` first |
| Times shift by the server's offset | `ToLocalTime()` or an unspecified `DateTimeKind` was used; use `ILocalClock` |
| Saving a time on a DST change day fails | Convert entered times with `ILocalClock.ToUtcLenientAsync` instead of `ConvertToUtcAsync` |

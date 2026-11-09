---
name: orchardcore-assets
description: Skill for the CrestApps Orchard Core Assets module (CrestApps.OrchardCore.Assets) and its Fleet feature. Covers asset types with categories, attributes and meters, numbered assets with an owner, a parent hierarchy, a location, warranty and documents, append-only meter readings where a cumulative meter can not go back, owners picked through the shared party sources (IPartySource) as a user, account or CRM contact, Fleet vehicles and equipment booked on their own Equipment calendars, vans that carry stock at their own Inventory StockLocation, the Assets recipe and deployment steps, permissions, and adding sections to the asset editor with DisplayDriver of Asset. Use this skill when requests mention equipment, installed customer units, vehicles, fleet, meter readings, warranties or van stock in Orchard Core. Strong matches include Asset, AssetType, AssetMeterReading, AssetFleet, AssetService, AssetManager, AssetOwnerDirectory, FleetCalendarHandler, ManageAssets and ViewAssets.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Assets and Fleet

## Keep the Things a Business Services or Uses

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Assets module, which keeps a business's own vehicles, equipment and tools and the units installed at its customers. It references no vertical module, so field service, CRM and others build on it.

### Guidelines

- Install `CrestApps.OrchardCore.Assets` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- Contracts are in `CrestApps.OrchardCore.Assets.Abstractions` (models, `AssetsConstants`, `AssetsPermissions`, `AssetOwners`); services in `CrestApps.OrchardCore.Assets.Core` (`AssetService`, `AssetManager`, `AssetTypeManager`, `AssetOwnerDirectory`).
- Save assets through `AssetManager` (a `CatalogManager<Asset>`) so `AssetHandler` numbers, validates and stamps them, and Fleet handlers run. Record readings through `AssetService.RecordReadingAsync`.
- An asset's `OwnerKind` `Customer` holds a **user id** (party kind `User`), not a Customers record id. `Account` holds an Accounts content item id, `Contact` a CRM contact content item id.
- Fleet needs `CrestApps.OrchardCore.Calendars.Resources`; van stock also needs `CrestApps.OrchardCore.Inventory`.
- Work orders on assets are in `orchardcore-work-orders`; equipment booked for visits in `orchardcore-dispatch`; geocoding in `orchardcore-geo`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Assets | `CrestApps.OrchardCore.Assets` | `CrestApps.OrchardCore.Resources` |
| Fleet | `CrestApps.OrchardCore.Assets.Fleet` | `CrestApps.OrchardCore.Assets`, `CrestApps.OrchardCore.Calendars.Resources` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Assets",
        "CrestApps.OrchardCore.Assets.Fleet",
        "CrestApps.OrchardCore.Inventory"
      ],
      "disable": []
    }
  ]
}
```

| Feature on | What Assets adds |
|---|---|
| `OrchardCore.Users` | The `Customer` owner kind (`UserPartySource`, search by user name or email). |
| `CrestApps.OrchardCore.Accounts` | The `Account` owner kind (Accounts registers `AccountPartySource`) and an **Assets** card on the account page. |
| `CrestApps.OrchardCore.Omnichannel.Managements` | The `Contact` owner kind (`ContactPartySource`) and an **Assets** card on CRM contacts. |
| `CrestApps.OrchardCore.Inventory` with Fleet | A stock location per asset that carries stock (`FleetStockLocationHandler`). |
| `CrestApps.OrchardCore.Dispatch.Api` | `api/assets/v1` endpoints (see `orchardcore-dispatch`). |

## Records

All records live in the `Assets` YesSql collection.

| Record | Key properties |
|---|---|
| `AssetType` | `Name`, `Category` (`Equipment`, `Vehicle`, `Tool`, `CustomerUnit`, `Component`, `Facility`), `Description`, `Attributes` (`Name`, `Kind` `Text`/`Number`/`Date`/`YesNo`), `Meters` (`Name`, `Unit`, `Cumulative` true by default). Names are unique within a type. |
| `Asset` | `Number` (next `A-00042` when empty, unique), `Name`, `AssetTypeId`, `ProductId`, `SerialNumber`, `Manufacturer`, `Model`, `OwnerKind` (`Internal`, `Customer`, `Account`, `Contact`), `OwnerId` (required unless internal), `Location` (an `Address`, coordinates once geocoded), `ParentAssetId`, `Status` (`Active`, `InService`, `OutOfService`, `Retired`), `InstallDate`, `Warranty` (`StartDate`, `EndDate`, `Provider`, `Terms`, `Covers(date)`), `Attributes` (name to value), `Documents` (media paths), `Fleet` |
| `AssetMeterReading` | `AssetId`, `Meter`, `Value`, `Utc`, `Source` (`Manual`, `WorkOrder`, `Api`), `SourceId`, `RecordedBy`. Only ever added. |
| `AssetFleet` | `Schedulable`, `CalendarId`, `Plate`, `Vin`, `Capacity`, `FuelType`, `HomeBase`, `DefaultDriverWorkerId`, `CarriesStock`, `StockLocationId` |

`AssetService`:

- `RecordReadingAsync(reading)` refuses a meter the type doesn't have and a cumulative reading lower than the last one before it; returns the errors.
- `ReadingsAsync(assetId, take)`, `ChildrenAsync(assetId)` and `AncestorsAsync(asset)` read history and hierarchy. An asset can't be inside itself, directly or further up.

## Owners

Each owner kind maps to a party kind (`AssetOwners.ToPartyKind`) and `asset.ToParty()` returns a `PartyRef`. Only kinds that have a registered `IPartySource` can be chosen. `AssetOwnerDirectory` names owners for the list, page and editor (an unknown owner shows its id). The pickers call `Admin/api/crestapps/assets/owners/search?kind=&query=` (`ViewAssets`), which returns up to 50 `{ value, text }` items. The asset list filters with `ownerKind` and `ownerId`.

Add another kind of party in the module that keeps the records:

```csharp
public sealed class VendorPartySource : IPartySource
{
    private readonly VendorStore _vendors; // your module's store

    public VendorPartySource(VendorStore vendors)
    {
        _vendors = vendors;
    }

    public string Kind => "Vendor";

    public async Task<IReadOnlyList<PartyOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
        => [.. (await _vendors.SearchAsync(query, count, cancellationToken)).Select(vendor => new PartyOption(vendor.Id, vendor.Name))];

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        => (await _vendors.GetManyAsync(ids, cancellationToken)).ToDictionary(vendor => vendor.Id, vendor => vendor.Name);
}

// Startup: services.AddPartySource<VendorPartySource>();
```

Asset owner kinds themselves are a fixed enum; a new party kind is useful to other party consumers (`IPartyDirectory`) such as work order pickers.

## Fleet

- **Equipment calendars.** For an asset with `Fleet.Schedulable`, `FleetCalendarHandler` creates (or finds by source `Asset` and the asset id) a calendar of the Calendars `Equipment` type, which allows one booking at a time, and keeps its name. Dispatch books required equipment there with a **Reservation**.
- **Van stock.** With Inventory on, an asset with `Fleet.CarriesStock` gets its own Inventory `StockLocation` (`OwnerKind` `Asset`, `OwnerId` the asset id), stored in `Fleet.StockLocationId`. Parts used on a job come off it with `IInventoryService.ConsumeAsync(stockItemId, locationId, quantity, referenceType, referenceId, reason, actor)`, which refuses more than is available and records a `Consumed` movement with the reference. A van's location never fulfills orders.
- Inventory stock locations live in a `StockLocation` store (`ItemId`, `Name`, `Address`, `Position`, `OwnerKind`, `OwnerId`); a migration moves locations kept in the old settings into it, keeping ids and order. See `orchardcore-inventory`.

## Admin UI

| Menu | Route | Permission |
|---|---|---|
| **Assets > Assets** | `assets`, `assets/view/{id}`, `assets/edit/{id?}`, `assets/delete/{id}` | `ViewAssets` (list, page), `ManageAssets` (edit, readings) |
| **Assets > Asset Types** | `assets/types`, `assets/types/edit/{id?}` | `ManageAssets` |

## Permissions

| Permission | Display name | Implied by | Default role |
|---|---|---|---|
| `ManageAssets` | Manage assets | none | Administrator |
| `ViewAssets` | View assets | `ManageAssets` | Administrator |

## Recipes and Deployment

The `Assets` step (needs `OrchardCore.Recipes.Core`) creates or updates `Types` then `Assets` (parents first) by `ItemId`, with the editor's checks. Property names are the model names and enums are names.

```json
{
  "steps": [
    {
      "name": "Assets",
      "Types": [
        {
          "ItemId": "4f50djz4d49v2hdqg8ynxj481n",
          "Name": "Boiler",
          "Category": "CustomerUnit",
          "Description": "A customer's gas boiler.",
          "Attributes": [ { "Name": "Fuel", "Kind": "Text" }, { "Name": "Output (kW)", "Kind": "Number" } ],
          "Meters": [ { "Name": "Run hours", "Unit": "h", "Cumulative": true } ]
        },
        { "ItemId": "4frxjzg9rc0mtph04y78qk38dy", "Name": "Service van", "Category": "Vehicle" }
      ],
      "Assets": [
        {
          "ItemId": "4fs9svm62gjxb5tzqxya3q8q20",
          "Name": "Training boiler",
          "AssetTypeId": "4f50djz4d49v2hdqg8ynxj481n",
          "SerialNumber": "BX200-0451",
          "Manufacturer": "Northwind Heating",
          "Model": "BX200",
          "Status": "Active",
          "OwnerKind": "Internal",
          "InstallDate": "2026-04-01",
          "Warranty": { "StartDate": "2026-04-01", "EndDate": "2031-03-31", "Provider": "Northwind Heating" },
          "Location": { "AddressLine1": "1 Depot Way", "City": "Boston", "Region": "MA", "PostalCode": "02118", "Country": "US" },
          "Attributes": { "Fuel": "Natural gas", "Output (kW)": "24" }
        },
        {
          "ItemId": "4fvne1bd88btzjbfsx0fjs17gp",
          "Name": "Van 1",
          "AssetTypeId": "4frxjzg9rc0mtph04y78qk38dy",
          "Status": "Active",
          "OwnerKind": "Internal",
          "Fleet": { "Schedulable": true, "Plate": "FS-1001", "CarriesStock": true }
        }
      ]
    }
  ]
}
```

- Use 26-character lowercase Orchard Core ids for `ItemId` so `AssetTypeId` and `ParentAssetId` references survive reruns.
- The **Asset types and assets** deployment step (category **Asset**, `AssetsDeploymentStep`, `IncludeAssets` true by default) exports types and, unless unticked, assets parents first. Meter readings are never exported.

## Extending the Asset Editor

The editor (`assets/edit/{id?}`) is built with `IDisplayManager<Asset>`; the built-in fields are the `AssetFields_Edit` shape at `Content:1`. The asset's own rules (`AssetHandler` validation through `AssetManager`) run after every section and report under the `Asset` prefix. When anything fails the asset isn't saved and the editor cancels the session, so records a section wrote are discarded too.

Keep your section's data in your own records keyed by the asset id. Don't write to `Asset.Attributes`: the built-in driver rebuilds it from the type's attribute definitions on every save.

```csharp
// Your module's own storage for the extra data.
public interface ICalibrationStore
{
    Task<DateOnly?> GetAsync(string assetId);

    Task SaveAsync(string assetId, DateOnly? calibratedOn);
}

public sealed class AssetCalibrationDisplayDriver : DisplayDriver<Asset>
{
    private readonly ICalibrationStore _calibrations;

    internal readonly IStringLocalizer S;

    public AssetCalibrationDisplayDriver(ICalibrationStore calibrations, IStringLocalizer<AssetCalibrationDisplayDriver> stringLocalizer)
    {
        _calibrations = calibrations;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(Asset asset, BuildEditorContext context)
        => Initialize<AssetCalibrationViewModel>("AssetCalibration_Edit", async model =>
        {
            model.CalibratedOn = await _calibrations.GetAsync(asset.ItemId);
        }).Location("Content:5");

    public override async Task<IDisplayResult> UpdateAsync(Asset asset, UpdateEditorContext context)
    {
        var model = new AssetCalibrationViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.CalibratedOn > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.CalibratedOn)}", S["The calibration date can't be in the future."]);
        }
        else
        {
            await _calibrations.SaveAsync(asset.ItemId, model.CalibratedOn);
        }

        return Edit(asset, context);
    }
}

public class AssetCalibrationViewModel
{
    public DateOnly? CalibratedOn { get; set; }
}

// Startup: services.AddDisplayDriver<Asset, AssetCalibrationDisplayDriver>();
```

To react whenever an asset is saved (from the editor, a recipe or code), add an `ICatalogEntryHandler<Asset>` deriving from `CatalogEntryHandlerBase<Asset>` (`CrestApps.Core.Handlers`) and override `CreatingAsync`, `UpdatingAsync` or `ValidatingAsync`, as Fleet does; register it with `services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Asset>, THandler>())`.

## Gotchas

| Symptom | Check |
|---|---|
| Owner kind missing in the editor | Its party source isn't registered: enable `OrchardCore.Users`, Accounts, or Omnichannel Management. |
| "Choose who owns the asset." | Non-internal owners need an `OwnerId`. |
| Reading refused | The meter isn't on the type, or a cumulative value is lower than the previous reading. |
| No Equipment calendar | Enable Fleet (and Calendars Resources) and set `Fleet.Schedulable`. |
| No van stock location | Enable Inventory with Fleet and set `Fleet.CarriesStock`. |
| Coordinates vanished | A changed address drops its old coordinates until it is geocoded again. |

Related skills: `orchardcore-work-orders`, `orchardcore-dispatch`, `orchardcore-geo`, `orchardcore-inventory`, `orchardcore-calendars`, `orchardcore-accounts`, `orchardcore-addresses`, `orchardcore-report-builder`.

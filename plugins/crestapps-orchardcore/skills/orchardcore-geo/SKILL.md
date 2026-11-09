---
name: orchardcore-geo
description: Skill for the CrestApps Orchard Core Geo module (CrestApps.OrchardCore.Geo) and its provider modules Geo.AzureMaps, Geo.GoogleMaps and Geo.Osrm. Covers GeoPoint, IGeoService geocoding, travel-time matrices and routes from providers chosen in Settings, Maps and Geocoding, cached travel times with the haversine estimate as a fallback, Google travel times never cached, service territories by GeoJSON shape or postal code with ITerritoryResolver, background geocoding with IGeocodeQueue and IGeocodeTarget, the Address coordinates, the crestapps-map Leaflet wrapper, the Geo recipe step, deployment steps and permissions. Use this skill when requests mention maps, geocoding, travel times, routing, service territories, postal code areas or Leaflet in Orchard Core. Strong matches include IGeoService, IGeocodingProvider, ITravelTimeProvider, IRouteProvider, ServiceTerritory, ITerritoryResolver, ITerritoryMemberSource, IGeocodeQueue, IGeocodeTarget, GeoSettings and ManageGeoTerritories.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Geo, Maps and Territories

## Work With Places From Any Module

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Geo module, which gives every module a shared way to work with places: points, geocoding in the background with retries, travel times with a built-in estimate, service territories, and a Leaflet map component. It references no vertical module.

### Guidelines

- Install `CrestApps.OrchardCore.Geo` and any provider package (`CrestApps.OrchardCore.Geo.AzureMaps`, `CrestApps.OrchardCore.Geo.GoogleMaps`, `CrestApps.OrchardCore.Geo.Osrm`) in the web/startup project. The `CrestApps.OrchardCore.Cms.Core.Targets` bundle includes all four.
- Contracts are in `CrestApps.OrchardCore.Geo.Abstractions` (`GeoPoint`, providers, `IGeoService`, `ITerritoryResolver`, `IGeocodeQueue`, `IGeocodeTarget`, `ITerritoryMemberSource`); services in `CrestApps.OrchardCore.Geo.Core` (`DefaultGeoService`, `HaversineTravelTimeProvider`, `GeoMath`, `GeoShape`, `TerritoryMatcher`, `PolylineEncoding`).
- Call `IGeoService`, never a provider directly: it picks the providers named in the settings, caches travel times, and falls back to the built-in estimate.
- Geocode records in the background with `IGeocodeQueue.EnqueueAsync` and write results back in an `IGeocodeTarget`; don't geocode inline on save.
- The default tile server is OpenStreetMap's, for trying things out only. A live site must use its own or a commercial tile service.
- Provider keys are encrypted with data protection, never shown again, and never exported.
- Addresses are covered by `orchardcore-addresses`; Dispatch uses travel times and territories (see `orchardcore-dispatch`).
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Geo, Maps and Territories | `CrestApps.OrchardCore.Geo` | `CrestApps.OrchardCore.Resources` |
| Azure Maps | `CrestApps.OrchardCore.Geo.AzureMaps` | `CrestApps.OrchardCore.Geo` |
| Google Maps | `CrestApps.OrchardCore.Geo.GoogleMaps` | `CrestApps.OrchardCore.Geo` |
| OSRM | `CrestApps.OrchardCore.Geo.Osrm` | `CrestApps.OrchardCore.Geo` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Geo",
        "CrestApps.OrchardCore.Geo.AzureMaps"
      ],
      "disable": []
    }
  ]
}
```

## Points, Providers and IGeoService

`GeoPoint` is a WGS84 `readonly record struct GeoPoint(double Latitude, double Longitude)` with `IsValid` and `GeoPoint.From(double? latitude, double? longitude)` (returns `null` for missing or invalid values).

| Contract | Members |
|---|---|
| `IGeocodingProvider` | `Name`, `DisplayName`, `GeocodeAsync(GeocodeQuery)`, `ReverseGeocodeAsync(GeoPoint)` returning `GeocodeCandidate`s (`Point`, `Confidence`, `FormattedAddress`, `PostalCode`, `Country`) |
| `ITravelTimeProvider` | `Name`, `DisplayName`, `GetMatrixAsync(origins, destinations, departureUtc, TravelOptions)` returning a `TravelMatrix` of `TravelLeg(Duration, DistanceMeters)`; `MaxCacheDuration` (default `null`) |
| `IRouteProvider` | `Name`, `DisplayName`, `GetRouteAsync(points)` returning a `GeoRoute` (`Line`, `DistanceMeters`, `Duration`, `Instructions`) |
| `IGeoService` | `GeocodeAsync`, `ReverseGeocodeAsync`, `GetMatrixAsync`, `GetRouteAsync` |

`IGeoService` behavior:

- **Geocoding** throws the provider's errors so callers can retry, and returns a `GeocodeResponse` with no provider when none is set up.
- **Travel times** are cached in the distributed cache (points rounded to about 10 m, departures to the quarter hour) for `MatrixCacheMinutes`, capped by the provider's `MaxCacheDuration`, and fall back to the haversine estimate when the provider is missing or fails.
- **Routes** fall back to straight lines.
- `HaversineTravelTimeProvider` (name `Haversine`) is always registered: great-circle distance (`GeoMath.DistanceMeters`) times `RoadFactor` (1.3) at `AverageSpeedKmh` (40) or `TravelOptions.SpeedKmh` (such as a territory's `TravelSpeedKmh`).

```csharp
public sealed class DepotDistanceService
{
    private readonly IGeoService _geoService;

    public DepotDistanceService(IGeoService geoService)
    {
        _geoService = geoService;
    }

    public async Task<TimeSpan> DriveTimeAsync(GeoPoint depot, GeoPoint customer, CancellationToken cancellationToken)
    {
        var matrix = await _geoService.GetMatrixAsync([depot], [customer], DateTime.UtcNow, new TravelOptions { SpeedKmh = 35 }, cancellationToken);

        return matrix[0, 0].Duration;
    }
}
```

## Provider Modules

Each provider adds its section to **Settings > Maps and Geocoding** and is then chosen there by name. A provider throws `GeoProviderException` when it has no key or address, its key is refused, or it finds no route; `IGeoService` then falls back, and the geocoding queue retries. Each HTTP client uses the standard resilience handler.

| Provider name | Settings | Geocoding | Travel times | Routes |
|---|---|---|---|---|
| `AzureMaps` (Azure Maps) | `AzureMapsSettings.Key` (sent as `subscription-key`) | Search and reverse geocoding | Route matrix with traffic | Route directions with instructions |
| `GoogleMaps` (Google Maps) | `GoogleMapsSettings.Key` | Geocoding API | Distance Matrix with traffic; `MaxCacheDuration` is zero, so **never cached** (Google's terms) | Directions API, polyline decoded by `PolylineEncoding` |
| `Osrm` (OSRM) | `OsrmSettings.BaseUrl`, `Profile` (`driving`); no key | none | Table service | Route service with step sentences |

Choosing a geocoding provider wakes requests that waited for one (`IGeocodeQueue.WakeAsync`).

## Settings

**Settings > Maps and Geocoding** (site settings group `geo`, `GeoSettings`, permission `ManageGeoSettings`): `GeocodingProvider`, `TravelTimeProvider`, `RouteProvider`, `AverageSpeedKmh` (40), `RoadFactor` (1.3), `MatrixCacheMinutes` (60), `TileUrl`, `TileAttribution`, `DefaultLatitude`, `DefaultLongitude`, `DefaultZoom`. The page also shows how many addresses are waiting, geocoded and not found, and the latest problem.

```json
{
  "steps": [
    {
      "name": "Settings",
      "GeoSettings": {
        "GeocodingProvider": "AzureMaps",
        "TravelTimeProvider": "AzureMaps",
        "RouteProvider": "AzureMaps",
        "AverageSpeedKmh": 40,
        "RoadFactor": 1.3,
        "MatrixCacheMinutes": 60,
        "TileUrl": "https://tiles.example.com/{z}/{x}/{y}.png",
        "TileAttribution": "&copy; Example Tiles",
        "DefaultLatitude": 42.36,
        "DefaultLongitude": -71.06,
        "DefaultZoom": 10
      }
    }
  ]
}
```

## Service Territories

`ServiceTerritory` (in the `Geo` collection): `Name`, `ParentId`, `Shape` (GeoJSON Polygon or MultiPolygon as a geometry, Feature or FeatureCollection), `PostalCodes` (a trailing `*` matches a prefix), `TimeZoneId`, `TravelSpeedKmh`, `Members` (`Kind`, `Id`, `Role` `Primary` or `Secondary`). The handler tidies postal codes, checks the shape parses (`GeoShape.TryParse`), and refuses a territory inside itself.

`ITerritoryResolver.ResolveAsync(GeoPoint? point, string postalCode)` (and the pure `TerritoryMatcher.Match`) returns the territory with the smallest shape containing the point, else the one with the postal code (exact before the longest prefix), as a `TerritoryMatch(Territory, MatchedBy)` where `MatchedBy` is `Shape` or `PostalCode`.

Territory members are listed through every `ITerritoryMemberSource` (`Kind`, `DisplayName`, `ListAsync()` of `(Id, Name)`); Dispatch adds `Worker` (Technicians) and `Team` (Teams). Members of a kind no source lists any more are kept.

## Background Geocoding

`IGeocodeQueue.EnqueueAsync(targetType, targetId, GeocodeQuery)` keeps one `GeocodeRequest` per record, replacing an earlier one. The **Geocode addresses** background task runs every minute (`ProcessDueAsync`):

- The best candidate is written back through the `IGeocodeTarget` whose `TargetType` matches, with the provider name as `source`.
- A provider error is retried after 1, 5, 30 and 120 minutes, then given up after five attempts; no match or a missing record is given up at once.
- With no provider set up, requests wait an hour at a time without counting an attempt.

Built-in targets include Work Orders (`WorkOrderServiceAddress`) and Workforce team locations and worker home bases. A full custom target is in [references/geo-extensions.md](references/geo-extensions.md).

`Address` (Addresses module) has optional `Latitude`, `Longitude`, `GeocodedUtc` and `GeocodeSource` (a provider name, or `Manual` when pinned), copied by `Clone`.

## Map Component

CrestApps Resources defines the scripts and styles `leaflet` (1.9.4), `leaflet-markercluster` (with style `leaflet-markercluster-default`), `leaflet-geoman` (2.20.4), and the `crestapps-map` script. The Geo module's `_GeoMap` partial (model `GeoSettings`, `ViewData` `MapId`, `MapHeight`, `MapLabel`) renders a map element with the site's tiles and the scripts.

```javascript
const map = window.crestappsMap.create('my-map');           // reads data-tile-url, data-attribution, data-latitude, data-longitude, data-zoom
window.crestappsMap.showShapes(map, [{ label: 'Boston', geoJson: shapeJson }]);
window.crestappsMap.addMarkers(map, [{ latitude: 42.36, longitude: -71.06, label: 'Depot' }], { fit: true });
window.crestappsMap.pin(map, latitudeInput, longitudeInput); // a draggable pin that writes two inputs
window.crestappsMap.editShapes(map, textarea);               // Geoman drawing kept as a GeoJSON FeatureCollection
```

## Admin UI and Permissions

| Menu | Route | Permission |
|---|---|---|
| **Field Service > Territories** | `geo/territories`, `geo/territories/edit/{id?}` | `ManageGeoTerritories` |
| **Settings > Maps and Geocoding** | site settings group `geo` | `ManageGeoSettings` |

| Permission | Display name | Default role |
|---|---|---|
| `ManageGeoTerritories` | Manage service territories | Administrator |
| `ManageGeoSettings` | Manage maps and geocoding settings | Administrator |

## Recipes and Deployment

The `Geo` step (needs `OrchardCore.Recipes.Core`) creates or updates `Territories` by `ItemId`, parents first, with the editor's checks.

```json
{
  "steps": [
    {
      "name": "Geo",
      "Territories": [
        { "ItemId": "4fs7kjkcahkr0q5vh31ktg943y", "Name": "Boston", "PostalCodes": [ "021*" ], "TimeZoneId": "America/New_York", "TravelSpeedKmh": 35 },
        {
          "ItemId": "4f0n4hbx15vatb9tg9ykcbsym1",
          "Name": "Cambridge",
          "ParentId": "4fs7kjkcahkr0q5vh31ktg943y",
          "PostalCodes": [ "02138", "02139" ],
          "TimeZoneId": "America/New_York",
          "Shape": "{\"type\":\"Polygon\",\"coordinates\":[[[-71.16,42.35],[-71.06,42.35],[-71.06,42.40],[-71.16,42.40],[-71.16,42.35]]]}"
        }
      ]
    }
  ]
}
```

- `Shape` is a string holding GeoJSON (longitude before latitude).
- Deployment steps: **Service territories** (category **Geo**, `GeoDeploymentStep`, exported parents first) and **Maps and geocoding settings** (exports `GeoSettings` through the `Settings` step; provider keys stay on each site).

## Gotchas

| Symptom | Check |
|---|---|
| Travel times look like straight lines | No travel-time provider chosen, or it failed and the haversine fallback answered. Check the provider key and the logs. |
| Google travel times are slow or costly | Expected: they are never cached. Use Azure Maps or OSRM for heavy optimizer use. |
| Addresses never get coordinates | Choose a geocoding provider; requests wait without one. OSRM can't geocode. |
| Territory not found for a point | The shape must be valid GeoJSON with longitude first; otherwise the postal code is used. |
| Map tiles refused | Tiles are requested with the site's origin as referrer; use a tile service whose policy allows your site. |

Related skills: `orchardcore-addresses`, `orchardcore-dispatch`, `orchardcore-work-orders`, `orchardcore-assets`, `orchardcore-workforce`, `orchardcore-crestapps-resources`.

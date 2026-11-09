# Extending Geo

Reference `CrestApps.OrchardCore.Geo.Abstractions` from your module and add `CrestApps.OrchardCore.Geo` to your feature's dependencies. Namespaces: `CrestApps.OrchardCore.Geo.Models` (`GeoPoint`, `GeocodeQuery`, `GeocodeCandidate`, `TravelMatrix`, `TravelLeg`, `TravelOptions`, `GeoRoute`) and `CrestApps.OrchardCore.Geo.Services` (contracts, `GeoProviderException`).

## Geocode Your Own Records in the Background

1. Enqueue when a record with an address and no coordinates is saved.
2. Write the result back in an `IGeocodeTarget` with the same `TargetType`.

```csharp
public sealed class DepotGeocoding
{
    public const string TargetType = "DepotAddress";

    private readonly IGeocodeQueue _queue;

    public DepotGeocoding(IGeocodeQueue queue)
    {
        _queue = queue;
    }

    // Call after saving a depot whose address changed.
    public Task QueueAsync(Depot depot, CancellationToken cancellationToken = default)
        => depot.Latitude is null && !string.IsNullOrWhiteSpace(depot.Address?.AddressLine1)
            ? _queue.EnqueueAsync(TargetType, depot.Id, new GeocodeQuery { Address = depot.Address }, cancellationToken)
            : Task.CompletedTask;
}

public sealed class DepotGeocodeTarget : IGeocodeTarget
{
    private readonly DepotStore _depots; // your module's store

    public DepotGeocodeTarget(DepotStore depots)
    {
        _depots = depots;
    }

    public string TargetType => DepotGeocoding.TargetType;

    // Return false when the record is gone, so the request is given up.
    public async Task<bool> ApplyAsync(string targetId, GeocodeCandidate candidate, string source, CancellationToken cancellationToken = default)
    {
        var depot = await _depots.FindAsync(targetId, cancellationToken);

        if (depot is null)
        {
            return false;
        }

        depot.Latitude = candidate.Point.Latitude;
        depot.Longitude = candidate.Point.Longitude;
        depot.GeocodeSource = source;

        await _depots.SaveAsync(depot, cancellationToken);

        return true;
    }
}
```

```csharp
services.AddScoped<DepotGeocoding>();
services.TryAddEnumerable(ServiceDescriptor.Scoped<IGeocodeTarget, DepotGeocodeTarget>());
```

When the record keeps an `Address`, note that its properties are init-only: build a new `Address` with the old values plus `Latitude`, `Longitude`, `GeocodedUtc` and `GeocodeSource`, as the Work Orders target does. A changed address should clear its old coordinates so it is queued again.

## Add a Geocoding or Travel-Time Provider

A provider is chosen by `Name` in **Settings > Maps and Geocoding**. Throw `GeoProviderException` for a missing key, a refused key or no route, so `IGeoService` falls back to the built-in estimate (travel times, routes) or the queue retries (geocoding).

```csharp
public sealed class ContosoGeoProvider : IGeocodingProvider, ITravelTimeProvider
{
    public const string ProviderName = "Contoso";

    private readonly IHttpClientFactory _httpClientFactory;

    public ContosoGeoProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Name => ProviderName;

    public string DisplayName => "Contoso Maps";

    // Cap how long IGeoService may cache this provider's matrices; TimeSpan.Zero means never.
    public TimeSpan? MaxCacheDuration => TimeSpan.FromMinutes(30);

    public async Task<IReadOnlyList<GeocodeCandidate>> GeocodeAsync(GeocodeQuery query, CancellationToken cancellationToken = default)
    {
        var line = query.ToSingleLine();

        if (string.IsNullOrEmpty(line))
        {
            return [];
        }

        var client = _httpClientFactory.CreateClient(ProviderName);
        using var response = await client.GetAsync($"geocode?q={Uri.EscapeDataString(line)}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new GeoProviderException("The Contoso key was refused.");
        }

        response.EnsureSuccessStatusCode();

        // Read the body and map each result to a GeocodeCandidate (Point, Confidence 0..1, FormattedAddress, PostalCode, Country).
        return [];
    }

    public Task<IReadOnlyList<GeocodeCandidate>> ReverseGeocodeAsync(GeoPoint point, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GeocodeCandidate>>([]);

    public Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations, DateTime departureUtc, TravelOptions options, CancellationToken cancellationToken = default)
    {
        // Legs[origin][destination] must have one TravelLeg(Duration, DistanceMeters) per pair.
        throw new GeoProviderException("The Contoso travel matrix isn't set up.");
    }
}
```

```csharp
services.AddHttpClient(ContosoGeoProvider.ProviderName, client => client.BaseAddress = new Uri("https://maps.contoso.example/"))
    .AddStandardResilienceHandler();
services.TryAddScoped<ContosoGeoProvider>();
services.AddScoped<IGeocodingProvider>(sp => sp.GetRequiredService<ContosoGeoProvider>());
services.AddScoped<ITravelTimeProvider>(sp => sp.GetRequiredService<ContosoGeoProvider>());
```

Keep the provider's key in your own site settings section on the **Maps and Geocoding** group (`GeoConstants.SettingsGroupId`, `geo`), protected with data protection as the built-in providers do, and register `services.AddSiteSettingsPermission(GeoConstants.SettingsGroupId, GeoPermissions.ManageGeoSettings)`.

## List Territory Members From Your Module

```csharp
public sealed class CrewTerritoryMemberSource : ITerritoryMemberSource
{
    private readonly CrewStore _crews; // your module's store

    internal readonly IStringLocalizer S;

    public CrewTerritoryMemberSource(CrewStore crews, IStringLocalizer<CrewTerritoryMemberSource> stringLocalizer)
    {
        _crews = crews;
        S = stringLocalizer;
    }

    public string Kind => "Crew";

    public string DisplayName => S["Crews"];

    public async Task<IReadOnlyList<(string Id, string Name)>> ListAsync(CancellationToken cancellationToken = default)
        => [.. (await _crews.ListAsync(cancellationToken)).Select(crew => (crew.Id, crew.Name))];
}

// Startup: services.AddScoped<ITerritoryMemberSource, CrewTerritoryMemberSource>();
```

Read a territory's members of your kind from `ServiceTerritory.Members` (`Kind`, `Id`, `Role`).

## Resolve a Territory

```csharp
var match = await territoryResolver.ResolveAsync(GeoPoint.From(address.Latitude, address.Longitude), address.PostalCode, cancellationToken);

if (match is not null)
{
    var territoryId = match.Territory.ItemId;
    var byShape = match.MatchedBy == TerritoryMatchKind.Shape;
}
```

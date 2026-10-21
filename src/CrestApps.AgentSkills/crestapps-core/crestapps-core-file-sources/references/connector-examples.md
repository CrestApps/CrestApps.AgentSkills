# File Source Connector Examples

Companion to `crestapps-core-file-sources`.

## Contract

```csharp
namespace CrestApps.Core.AI.Indexing;

public interface IIngestionConnector
{
    string Name { get; }

    ValueTask ValidateAsync(IngestionSource source, ValidationResultDetails result, CancellationToken cancellationToken = default);

    Task<IngestionDiscoveryResult> DiscoverAsync(IngestionSource source, string continuationToken = null, CancellationToken cancellationToken = default);

    Task<IngestionItemContent> FetchAsync(IngestionSource source, string itemId, CancellationToken cancellationToken = default);
}

public sealed record IngestionDiscoveryResult(IReadOnlyList<IngestionItemRef> Items, bool IsComplete, string Message = null, string DiscoveryCursor = null);

public sealed record IngestionItemRef(string ItemId, string ChangeToken = null, long? SizeBytes = null, DateTimeOffset? LastModifiedUtc = null);
```

A connector is handed an `IngestionSource` (the base of `FileSource` and `WebCrawler`), so the same connector serves either record type. Read its settings with `source.GetOrCreate<TMetadata>()`.

Rules every connector must follow:

- Return `IsComplete = false` whenever the listing might not be the whole source (an error part-way, a page that failed, a limit reached, a resumed run). Only complete listings remove knowledge.
- Return a `DiscoveryCursor` when you stop early, and resume after it when `continuationToken` is set. Keep the listing order stable.
- Use a change token that changes whenever content changes (an ETag, or modified time plus size). Leave it `null` only when the source reports nothing; such items are re-read every run.
- Re-validate an item id in `FetchAsync`, because ids also come from stored state. Return `null` when the item no longer exists.

## Example: HTTP Manifest Connector

Reads a JSON manifest of documents from an internal service and fetches each one.

```csharp
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Indexing;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using Microsoft.Extensions.Logging;

public sealed class ManifestFileSourceMetadata
{
    public string ManifestUrl { get; set; }
}

public sealed class ManifestEntry
{
    public string Path { get; set; }

    public string ETag { get; set; }

    public string ContentType { get; set; }
}

public sealed class ManifestIngestionConnector : IIngestionConnector
{
    public const string ConnectorName = "Manifest";
    public const string HttpClientName = "ManifestConnector";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ManifestIngestionConnector> _logger;

    public ManifestIngestionConnector(
        IHttpClientFactory httpClientFactory,
        ILogger<ManifestIngestionConnector> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => ConnectorName;

    public ValueTask ValidateAsync(IngestionSource source, ValidationResultDetails result, CancellationToken cancellationToken = default)
    {
        var metadata = source.GetOrCreate<ManifestFileSourceMetadata>();

        if (!Uri.TryCreate(metadata.ManifestUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            result.Fail(new ValidationResult(
                "An absolute HTTPS manifest URL is required.",
                [nameof(ManifestFileSourceMetadata.ManifestUrl)]));
        }

        return ValueTask.CompletedTask;
    }

    public async Task<IngestionDiscoveryResult> DiscoverAsync(IngestionSource source, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        var metadata = source.GetOrCreate<ManifestFileSourceMetadata>();
        var client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            var entries = await client.GetFromJsonAsync<List<ManifestEntry>>(metadata.ManifestUrl, cancellationToken) ?? [];

            var items = entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                .OrderBy(entry => entry.Path, StringComparer.Ordinal)
                .Select(entry => new IngestionItemRef(entry.Path, entry.ETag))
                .ToList();

            // The manifest is the whole source in one response, so the listing is complete.
            return new IngestionDiscoveryResult(items, IsComplete: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "Failed to read the manifest '{ManifestUrl}'.", metadata.ManifestUrl);

            // Incomplete: nothing may be removed on the strength of a failed listing.
            return new IngestionDiscoveryResult([], IsComplete: false, "The manifest could not be read.");
        }
    }

    public async Task<IngestionItemContent> FetchAsync(IngestionSource source, string itemId, CancellationToken cancellationToken = default)
    {
        var metadata = source.GetOrCreate<ManifestFileSourceMetadata>();

        if (itemId.Contains("..", StringComparison.Ordinal) || Uri.TryCreate(itemId, UriKind.Absolute, out _))
        {
            return null;
        }

        var baseUri = new Uri(metadata.ManifestUrl);
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.GetAsync(new Uri(baseUri, itemId), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        // Buffer so the reader resolver can sniff the first bytes and rewind.
        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var fileName = Path.GetFileName(itemId);

        return new IngestionItemContent
        {
            Content = buffer,
            MediaType = response.Content.Headers.ContentType?.MediaType,
            FileName = fileName,
            Title = fileName,
        };
    }
}
```

## Registration

```csharp
using CrestApps.Core.AI.FileSources;
using Microsoft.Extensions.Localization;

builder.Services.AddHttpClient(ManifestIngestionConnector.HttpClientName);

builder.Services.AddCoreIngestionConnector<ManifestIngestionConnector>(
    ManifestIngestionConnector.ConnectorName,
    descriptor =>
    {
        descriptor.DisplayName = new LocalizedString("Manifest", "Document manifest");
        descriptor.Description = new LocalizedString("Manifest Description", "Reads the documents listed in an HTTPS manifest.");
    });
```

`AddCoreIngestionConnector<T>` calls `AddCoreFileSources()`, registers the connector scoped and keyed by name (resolved through `IIngestionConnectorResolver`), and adds an `IngestionConnectorDescriptor` to `IngestionConnectorOptions` so screens can list it. Create `FileSource` records with `Source = "Manifest"` and `Put(new ManifestFileSourceMetadata { ... })`.

## Remote File Servers

For another file-transfer protocol, derive from `RemoteFileIngestionConnector` (you implement `ValidateAsync`) and provide an `IRemoteFileClientFactory` (`ConnectorName`, `CreateAsync(source, ct)` returning an `IRemoteFileClient`), both in `CrestApps.Core.AI.FileSources.FileTransfer`, the way `FtpIngestionConnector` and `SftpIngestionConnector` do. The base class reads `RemoteFileSourceMetadata` (`RootPath`, `Recursive`, `MaxItems`), lists and fetches through the client, and reports a listing failure as incomplete.

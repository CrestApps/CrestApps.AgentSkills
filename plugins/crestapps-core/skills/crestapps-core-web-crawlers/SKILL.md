---
name: crestapps-core-web-crawlers
description: Skill for CrestApps.Core web crawlers that scrape public websites into a Web AI data source. Covers CrestApps.Core.AI.WebCrawlers with AddCoreWebCrawlers and AddCoreWebCrawlerStoresYesSql or AddCoreWebCrawlerStoresEntityCore, WebCrawler records with SitemapWebCrawlerMetadata, the Sitemap strategy for urlset, sitemapindex, gzip, text sitemaps, feeds and robots.txt, WebCrawlerOptions, the re-index planner and background service with lastmod-based incremental updates, PartiallyDiscovered results that never delete pages, page URL citations, HtmlIngestionDocumentReader from CrestApps.Core.DataIngestion, crawlers that feed a File data source, and custom IWebCrawlerStrategy implementations.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Web Crawlers

A **Web** AI data source (`AIDataSourceSourceTypes.Web`) is a target bucket with no site configuration and no field mapping. A **web crawler** (`WebCrawler` record) chooses a strategy (its `Source`, for example `Sitemap`), stores that strategy's settings, and points at the data source. Many crawlers can fill one knowledge base, each re-crawled on its own interval. Folders and file servers use file sources instead (`crestapps-core-file-sources`); the data source model is in `crestapps-core-data-sources`.

## Registration

```csharp
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.Data.YesSql;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai.AddOpenAI())
    .AddIndexingServices(indexing => indexing
        .AddYesSqlStores()
        .AddElasticsearch(
            builder.Configuration.GetSection("CrestApps:Elasticsearch"),
            elasticsearch => elasticsearch.AddAIDataSources())));

builder.Services.AddCoreWebCrawlers();
builder.Services.AddCoreWebCrawlerStoresYesSql(); // or AddCoreWebCrawlerStoresEntityCore()

builder.Services.Configure<WebCrawlerOptions>(options =>
{
    options.DefaultMaxPages = 500;
    options.DefaultMaxConcurrentRequests = 4;
    options.DefaultRequestTimeoutSeconds = 30;
    options.DefaultReindexIntervalMinutes = 1440;
    options.ReindexCheckIntervalMinutes = 15;
});
```

`AddCoreWebCrawlers()` registers the named `HttpClient` (`WebCrawlerConstants.HttpClientName`, gzip, deflate and brotli, redirects followed), `ISitemapCrawler`, the keyed `Sitemap` strategy and its descriptor in `WebCrawlerStrategyOptions`, `IWebCrawlerReindexPlanner`, `IWebCrawlerReindexService`, the `Web` source handler and keyed `IAIReferenceLinkResolver`, the crawler catalog handlers, `WebCrawlerReindexBackgroundService`, the shared ingestion runtime, and the **Web** entry in `AIDataSourceSourceOptions`.

`AddCoreWebCrawlerStoresYesSql()` registers `IWebCrawlerStore`, `IWebCrawlStateStore`, `IIngestionItemStateStore` and the knowledge object store. YesSql hosts create `CreateWebCrawlerIndexSchemaAsync`, `CreateWebCrawlStateIndexSchemaAsync` and `CreateIngestionItemStateIndexSchemaAsync` tables (the last one is required even without file sources).

## A Crawler Record

```csharp
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies.Sitemap;

var crawler = new WebCrawler
{
    DisplayText = "Product docs",
    Source = WebCrawlerConstants.Strategies.Sitemap,
    AIDataSourceId = webDataSource.ItemId,   // an AIDataSource with Source = AIDataSourceSourceTypes.Web
    ReindexIntervalMinutes = 720,
};

crawler.Put(new SitemapWebCrawlerMetadata
{
    BaseUrl = "https://docs.example.com",
    SitemapUrl = null,                     // optional; resolved from robots.txt and conventional paths
    MaxPages = 2000,
    MaxConcurrentRequests = 4,
    RequestTimeoutSeconds = 30,
    IncludeUrlPatterns = ["^https://docs\\.example\\.com/guides/"],
    ExcludeUrlPatterns = ["/changelog/"],
    UserAgent = "MyCompany-Crawler/1.0",
});

await webCrawlerStore.CreateAsync(crawler, cancellationToken);
```

A base URL or a sitemap URL is required. The Sitemap strategy follows flat `urlset`, nested `sitemapindex`, gzip and plain-text sitemaps, RSS and Atom feeds and `robots.txt` advertisements, and skips non-HTML responses. Pages are cleaned with `HtmlIngestionDocumentReader` (AngleSharp HTML5 parsing that removes `script`, `style` and other non-content nodes), then normalized, chunked and embedded by the data source pipeline. The page URL is each chunk's reference id and the citation link.

## Re-indexing

`WebCrawlerReindexBackgroundService` checks every `ReindexCheckIntervalMinutes` and calls `IWebCrawlerReindexService.ReindexDueAsync()`. For each due crawler, `IWebCrawlerReindexPlanner.PlanAndEnqueueAsync(crawler)` compares discovery with the stored `WebCrawlState`:

- new pages are queued for indexing,
- pages whose sitemap `<lastmod>` is newer are re-fetched and re-embedded,
- pages missing from a **complete** discovery are removed,
- pages without `<lastmod>` are refreshed by the nightly data source alignment.

The `WebCrawlerReindexResult` carries `NewCount`, `ChangedCount`, `RemovedCount`, `UnchangedCount`, `DiscoveredCount`, `Message` and a `Status` of `Completed`, `Skipped`, `NoPagesDiscovered`, `DiscoveryFailed` or `PartiallyDiscovered`. A walk that hit its page limit, failed to download a child sitemap, or nested too deep is `PartiallyDiscovered` and removes nothing.

Drive re-indexing from your own scheduler with `IWebCrawlerReindexService.ReindexDueAsync()` or `ReindexAllAsync()` inside a scope.

## Feeding a File Data Source

A crawl strategy is also an `IIngestionConnector` (through `WebIngestionConnector`). A crawler whose data source is `File` runs through the ingestion run service and `IFileSourceScheduler` (when file sources are registered) and is stored as typed knowledge objects; a crawler feeding a `Web` data source is driven only by the re-index service. Each record runs on exactly one path.

## Custom Strategy

```csharp
using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies;
using CrestApps.Core.DataIngestion;
using CrestApps.Core.Models;

public sealed class UrlListWebCrawlerMetadata
{
    public List<string> Urls { get; set; } = [];
}

public sealed class UrlListWebCrawlerStrategy : IWebCrawlerStrategy
{
    public const string StrategyName = "UrlList";

    private readonly IHttpClientFactory _httpClientFactory;

    public UrlListWebCrawlerStrategy(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Name => StrategyName;

    public ValueTask ValidateAsync(WebCrawler crawler, ValidationResultDetails result, CancellationToken cancellationToken = default)
    {
        var metadata = crawler.GetOrCreate<UrlListWebCrawlerMetadata>();

        if (metadata.Urls.Count == 0 || metadata.Urls.Any(url => !Uri.TryCreate(url, UriKind.Absolute, out _)))
        {
            result.Fail(new ValidationResult("Enter one or more absolute URLs.", [nameof(UrlListWebCrawlerMetadata.Urls)]));
        }

        return ValueTask.CompletedTask;
    }

    public Task<IReadOnlyList<CrawledPageRef>> DiscoverAsync(WebCrawler crawler, CancellationToken cancellationToken = default)
    {
        var metadata = crawler.GetOrCreate<UrlListWebCrawlerMetadata>();
        IReadOnlyList<CrawledPageRef> pages = metadata.Urls.Select(url => new CrawledPageRef(url)).ToList();

        return Task.FromResult(pages);
    }

    // The configured list is the whole site, so discovery is complete. Return IsComplete = false
    // whenever discovery may have missed pages, or the planner would delete them.
    public async Task<WebCrawlDiscovery> DiscoverDetailedAsync(WebCrawler crawler, CancellationToken cancellationToken = default)
        => new(await DiscoverAsync(crawler, cancellationToken), IsComplete: true);

    public async Task<CrawledPage> FetchAsync(WebCrawler crawler, string url, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(WebCrawlerConstants.HttpClientName);
        using var response = await client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "text/html")
        {
            return null;
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var text = HtmlIngestionDocumentReader.ExtractText(html);

        return string.IsNullOrWhiteSpace(text)
            ? null
            : new CrawledPage(HtmlIngestionDocumentReader.ExtractTitle(html), text);
    }
}
```

```csharp
using CrestApps.Core.AI.Indexing;
using CrestApps.Core.AI.WebCrawlers.Connectors;
using Microsoft.Extensions.Localization;

builder.Services.AddKeyedSingleton<IWebCrawlerStrategy, UrlListWebCrawlerStrategy>(UrlListWebCrawlerStrategy.StrategyName);
builder.Services.Configure<WebCrawlerStrategyOptions>(options => options.AddOrUpdate(
    UrlListWebCrawlerStrategy.StrategyName,
    new LocalizedString("UrlList", "URL list"),
    new LocalizedString("UrlList Description", "Scrapes a fixed list of page URLs.")));

// Optional: let the strategy feed File data sources too.
builder.Services.AddKeyedScoped<IIngestionConnector>(
    UrlListWebCrawlerStrategy.StrategyName,
    (sp, key) => new WebIngestionConnector(sp.GetRequiredKeyedService<IWebCrawlerStrategy>(key)));
```

Strategies are keyed singletons, so inject only singleton-safe services (`IHttpClientFactory`, options, loggers). The descriptor makes the strategy appear in crawler editors.

## Upgrading

- Web crawlers are new in 2.0; 1.x has no equivalent.
- `IWebCrawlerStrategy.DiscoverDetailedAsync` and `ISitemapCrawler.DiscoverDetailedAsync` are default-implemented, so older strategies still compile. Override them to report incomplete discovery, or a strategy whose `DiscoverAsync` returns any pages is treated as complete.
- File sources are separate `FileSource` records now; a crawler record is never also run as a file source.
- YesSql hosts with crawlers but no file sources must still create the `IngestionItemStateIndex` table.

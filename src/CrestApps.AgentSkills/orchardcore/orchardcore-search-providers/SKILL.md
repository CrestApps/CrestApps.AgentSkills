---
name: orchardcore-search-providers
description: Skill for implementing custom search providers in Orchard Core. Covers provider module structure, index profile handlers, provider-specific services, OrchardCore.Search integration, optional admin actions, and OpenSearch-style implementations that follow OrchardCore.Elasticsearch. Use this skill when requests mention Orchard Core Search Providers, Create a new search provider, Implement OpenSearch support, AddSearchService registration, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Indexing, OrchardCore.Search, AddSearchService, IndexProfileHandlerBase, ElasticsearchIndexProfileHandler, ElasticsearchService, AddElasticsearchServices. It also helps with OpenSearch provider examples, OrchardCore.Search integration, Optional admin actions, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Search Providers

## Implement a New Search Provider

You are an Orchard Core expert. Generate code and configuration for implementing a new search provider module such as OpenSearch by following the current `OrchardCore.Elasticsearch` pattern.

### Guidelines

- Use the current provider pattern based on `OrchardCore.Elasticsearch`, not legacy feature aliases.
- Create a canonical feature ID such as `OrchardCore.OpenSearch`, not `OrchardCore.Search.OpenSearch`.
- Split provider concerns into clear registrations:
  - core provider services and client factory
  - index profile UI and handlers
  - content indexing source registration under `OrchardCore.Contents`
  - `OrchardCore.Search` integration through `AddSearchService<TService>()`
- Register provider services in `Startup` and keep feature-specific registrations in separate `StartupBase` classes decorated with `[RequireFeatures(...)]`.
- Use `IndexProfileHandlerBase` to initialize and update provider-specific metadata and mappings.
- Register a `DisplayDriver<IndexProfile>` only when the provider exposes editable provider-specific metadata.
- Add an `AdminController` only when the provider needs extra actions such as index info, run query, or custom diagnostics.
- Do not add provider-specific deployment steps just to create, reset, or rebuild indexes. Orchard Core already provides `CreateOrUpdateIndexProfile`, `ResetIndex`, and `RebuildIndex` for any provider.
- Integrate with `OrchardCore.Search` by adding a scoped `ISearchService` and keyed registration through `services.AddSearchService<TService>(ProviderName)`.
- If the provider exposes query definitions, register the query source and query handler in the main startup.
- Keep examples focused on the latest Orchard Core implementation and omit backward-compatibility guidance.
- All C# classes must use the `sealed` modifier.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.

### Architecture Checklist

For a provider like OpenSearch, the usual pieces are:

- `Manifest.cs` feature definitions
- provider constants such as `OpenSearchConstants.ProviderName`
- connection options and client factory
- provider service extensions such as `AddOpenSearchServices()`
- `DisplayDriver<IndexProfile>` for provider metadata
- `IndexProfileHandlerBase` implementation for mappings and query defaults
- optional query services and query handlers
- optional `ContentsStartup` that registers `AddOpenSearchIndexingSource(...)`
- optional `SearchStartup` that integrates with `OrchardCore.Search`

### Feature and Dependency Pattern

Follow the current Elasticsearch feature structure, but without the obsolete compatibility feature:

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "OpenSearch",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion
)]

[assembly: Feature(
    Id = "OrchardCore.OpenSearch",
    Name = "OpenSearch",
    Description = "Creates OpenSearch indexes to support search scenarios.",
    Dependencies =
    [
        "OrchardCore.Queries.Core",
        "OrchardCore.Indexing",
        "OrchardCore.ContentTypes",
    ],
    Category = "Search"
)]
```

Add separate startup classes for optional integrations:

- `[RequireFeatures("OrchardCore.Contents")]` for content indexing registration
- `[RequireFeatures("OrchardCore.Search")]` for `ISearchService`
- other features only when the provider really needs them

### Provider Service Extensions

Create provider-specific service extensions just like Elasticsearch does:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing.Core;
using OrchardCore.OpenSearch.Core.Services;
using OrchardCore.Queries;

namespace OrchardCore.OpenSearch;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenSearchServices(this IServiceCollection services)
    {
        services.AddScoped<OpenSearchQueryService>();
        services.AddQuerySource<OpenSearchQuerySource>(OpenSearchQuerySource.SourceName);

        return services;
    }

    public static IServiceCollection AddOpenSearchIndexingSource(
        this IServiceCollection services,
        string implementationType,
        Action<IndexingOptionsEntry> action = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(implementationType);

        services.AddIndexingSource<
            OpenSearchIndexManager,
            OpenSearchDocumentIndexManager,
            OpenSearchIndexNameProvider>(
            OpenSearchConstants.ProviderName,
            implementationType,
            action);

        return services;
    }
}
```

### Main Provider Startup

Register connection options, client factory, provider services, query support, permissions, navigation, and profile UI in the main startup:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.OpenSearch.Core.Handlers;
using OrchardCore.OpenSearch.Core.Models;
using OrchardCore.OpenSearch.Core.Services;
using OrchardCore.OpenSearch.Drivers;
using OrchardCore.OpenSearch.Services;
using OrchardCore.Queries;
using OrchardCore.Queries.Core;
using OrchardCore.Security.Permissions;

namespace OrchardCore.OpenSearch;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<OpenSearchConnectionOptions>, OpenSearchConnectionOptionsConfigurations>();
        services.AddTransient<IOpenSearchClientFactory, OpenSearchClientFactory>();
        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IOpenSearchClientFactory>();
            var options = sp.GetRequiredService<IOptions<OpenSearchConnectionOptions>>().Value;

            return factory.Create(options);
        });

        services.Configure<OpenSearchOptions>(options =>
        {
            var configuration = _shellConfiguration.GetSection(OpenSearchConnectionOptionsConfigurations.ConfigSectionName);

            options.AddIndexPrefix(configuration);
            options.AddAnalyzers(configuration);
            options.AddTokenFilters(configuration);
        });

        services.AddOpenSearchServices();
        services.AddPermissionProvider<PermissionProvider>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddDisplayDriver<Query, OpenSearchQueryDisplayDriver>();
        services.AddScoped<IQueryHandler, OpenSearchQueryHandler>();

        services.AddDisplayDriver<IndexProfile, OpenSearchIndexProfileDisplayDriver>();
        services.AddIndexProfileHandler<OpenSearchIndexProfileHandler>();
        services.AddDataMigration<OpenSearchMigrations>();
    }
}
```

### OrchardCore.Search Integration

Register the search service only when the `OrchardCore.Search` feature is enabled:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Search;

namespace OrchardCore.OpenSearch;

[RequireFeatures("OrchardCore.Search")]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSearchService<OpenSearchService>(OpenSearchConstants.ProviderName);
    }
}
```

This is the current Orchard Core pattern used by Elasticsearch, Lucene, and Azure AI Search.

### Content Indexing Registration

Add a separate startup for content indexing support:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Data.Migration;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;
using OrchardCore.OpenSearch.Core.Handlers;

namespace OrchardCore.OpenSearch;

[RequireFeatures("OrchardCore.Contents")]
public sealed class ContentsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentsStartup(IStringLocalizer<ContentsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<IndexingMigrations>();

        services
            .AddIndexProfileHandler<OpenSearchContentIndexProfileHandler>()
            .AddOpenSearchIndexingSource(IndexingConstants.ContentsIndexSource, o =>
            {
                o.DisplayName = S["Content in OpenSearch"];
                o.Description = S["Create an OpenSearch index based on site contents."];
            });
    }
}
```

### Index Profile Handler Pattern

Provider-specific mappings belong in an `IndexProfileHandlerBase` implementation:

```csharp
using OpenSearch.Client;
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.OpenSearch.Core.Models;

namespace OrchardCore.OpenSearch.Core.Handlers;

public sealed class OpenSearchIndexProfileHandler : IndexProfileHandlerBase
{
    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => ApplyDefaultsAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => ApplyDefaultsAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => ApplyDefaultsAsync(context.Model);

    private static Task ApplyDefaultsAsync(IndexProfile indexProfile)
    {
        if (!string.Equals(indexProfile.ProviderName, OpenSearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (!indexProfile.TryGet<OpenSearchIndexMetadata>(out var metadata))
        {
            metadata = new OpenSearchIndexMetadata();
        }

        metadata.IndexMappings ??= new OpenSearchIndexMap();
        metadata.IndexMappings.Mapping ??= new TypeMapping();
        metadata.IndexMappings.Mapping.Properties ??= [];
        metadata.IndexMappings.KeyFieldName = "ContentItemId";

        indexProfile.Put(metadata);

        if (!indexProfile.TryGet<OpenSearchDefaultQueryMetadata>(out var queryMetadata))
        {
            queryMetadata = new OpenSearchDefaultQueryMetadata();
        }

        queryMetadata.DefaultSearchFields = ["Content.ContentItem.FullText"];
        indexProfile.Put(queryMetadata);

        return Task.CompletedTask;
    }
}
```

### AdminController Guidance

Do **not** add an `AdminController` by default.

Add one only when the provider needs provider-specific actions such as:

- viewing provider index info
- testing query DSL requests
- running diagnostics or custom actions that do not belong in the generic index profile UI

If the provider only needs normal index-profile editing, lifecycle operations, and search registration, the display driver and handler pattern is enough.

### Deployment Guidance

Do **not** add provider-specific deployment steps just to manage indexes.

Use the provider-agnostic steps that already exist:

- `CreateOrUpdateIndexProfile`
- `ResetIndex`
- `RebuildIndex`

### Index Profile Recipe Example

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "OpenSearchContent",
          "IndexName": "opensearch-content",
          "ProviderName": "OpenSearch",
          "Type": "Content",
          "Properties": {
            "ContentIndexMetadata": {
              "IndexLatest": false,
              "IndexedContentTypes": ["Article", "BlogPost"],
              "Culture": "any"
            },
            "OpenSearchIndexMetadata": {
              "AnalyzerName": "standard"
            },
            "OpenSearchDefaultQueryMetadata": {
              "DefaultSearchFields": [
                "Content.ContentItem.FullText"
              ]
            }
          }
        }
      ]
    }
  ]
}
```

## Security and Reliability Notes

- Keep provider credentials in configuration, not in recipes or index-profile properties.
- Register provider services with the same lifetimes Orchard Core uses for the current providers.
- Use feature-gated startups instead of runtime `if` blocks where possible.
- Prefer provider-specific wrapper methods like `AddOpenSearchIndexingSource()` over scattered direct calls to `AddIndexingSource(...)`.

---
name: orchardcore-indexing-sources
description: Skill for registering Orchard Core indexing sources for search providers. Covers AddIndexingSource wrappers, provider-specific source extensions such as AddElasticsearchIndexingSource, OrchardCore.Contents integration, source metadata, options-gated registration, and custom record indexing. Use this skill when requests mention Orchard Core Indexing Sources, AddElasticsearchIndexingSource, AddAzureAISearchIndexingSource, Register a new indexing source, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Indexing.Core, AddIndexingSource, IndexingOptionsEntry, IIndexManager, IDocumentIndexManager, IIndexNameProvider, and IndexProfileHandlerBase. It also helps with OpenSearch indexing source examples, OrchardCore.Contents registration, Options-gated source registration, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Indexing Sources

## Register a New Indexing Source

You are an Orchard Core expert. Generate code and configuration for registering new indexing sources for a provider such as Elasticsearch, Azure AI Search, or OpenSearch.

### Guidelines

- Use `services.AddIndexingSource<TManager, TDocumentManager, TNamingProvider>(providerName, implementationType, action)` as the underlying registration primitive.
- Prefer a provider-specific wrapper such as `AddOpenSearchIndexingSource(...)` instead of repeating raw `AddIndexingSource(...)` calls throughout feature startups.
- Register indexing sources in feature-specific startups such as `[RequireFeatures("OrchardCore.Contents")]`.
- Use `IndexingOptionsEntry.DisplayName` and `Description` so the source appears clearly in the admin UI.
- Pair the source registration with the appropriate `IIndexProfileHandler` so new profiles get provider-specific defaults.
- Use the overload with `TOptions : ISearchProviderOptions` only when the source should appear only if provider configuration exists.
- Use the canonical provider name such as `OpenSearch`, `Elasticsearch`, `Lucene`, or `AzureAISearch` consistently across the wrapper, `AddSearchService`, and index profiles.
- Do not add deployment steps just to support a new indexing source. `CreateOrUpdateIndexProfile`, `ResetIndex`, and `RebuildIndex` already work across providers.
- Do not add an admin controller just to register an indexing source. Only add controller actions when the provider exposes extra provider-specific operations.
- Keep examples on the latest Orchard Core indexing abstractions and skip legacy recipes and legacy feature IDs.
- All C# classes must use the `sealed` modifier.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.

### Core Registration Pattern

Orchard Core providers register sources by wrapping the generic indexing extension:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing.Core;

namespace OrchardCore.OpenSearch;

public static class ServiceCollectionExtensions
{
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

This matches the pattern used by:

- `AddElasticsearchIndexingSource(...)`
- `AddAzureAISearchIndexingSource(...)`
- `AddLuceneIndexingSource(...)`

### What the Generic Registration Adds

The generic `AddIndexingSource(...)` call wires up:

- keyed `IIndexManager`
- keyed `IDocumentIndexManager`
- keyed `IIndexNameProvider`
- `IndexingOptions` metadata so the source shows up in the index profile UI

That means your provider-specific wrapper should stay very small and only express the provider name plus the concrete types.

### Registering the Content Source

Follow the current Orchard Core `ContentsStartup` pattern:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Data.Migration;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

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

Use this exact shape when you want the provider to index Orchard content items.

### Registering a Custom Source Type

For arbitrary records, define a custom source type constant and register it through the provider wrapper:

```csharp
public static class ProductIndexingConstants
{
    public const string IndexSource = "Products";
}
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace OrchardCore.OpenSearch.Products;

[Feature("Contoso.OpenSearch.Products")]
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddIndexProfileHandler<ProductOpenSearchIndexProfileHandler>()
            .AddOpenSearchIndexingSource(ProductIndexingConstants.IndexSource, o =>
            {
                o.DisplayName = S["Products in OpenSearch"];
                o.Description = S["Create an OpenSearch index for product records."];
            });
    }
}
```

### Implementing the Required Services

An indexing source registration points to three provider-specific services:

#### `IIndexManager`

Tracks the append-only indexing task stream for the source.

#### `IDocumentIndexManager`

Writes provider-specific `DocumentIndex` documents to the external engine.

#### `IIndexNameProvider`

Returns the physical provider index name for the `IndexProfile`.

```csharp
using OrchardCore.Indexing;

namespace OrchardCore.OpenSearch.Products;

public sealed class ProductIndexNameProvider : IIndexNameProvider
{
    public Task<string> GetIndexNameAsync(IndexProfile indexProfile)
    {
        return Task.FromResult(indexProfile.IndexName);
    }
}
```

### Pair the Source with an Index Profile Handler

Register an `IndexProfileHandlerBase` implementation for source-specific defaults and mappings:

```csharp
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.OpenSearch.Core.Models;

namespace OrchardCore.OpenSearch.Products;

public sealed class ProductOpenSearchIndexProfileHandler : IndexProfileHandlerBase
{
    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => ConfigureAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => ConfigureAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => ConfigureAsync(context.Model);

    private static Task ConfigureAsync(IndexProfile indexProfile)
    {
        if (!string.Equals(indexProfile.ProviderName, OpenSearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(indexProfile.Type, ProductIndexingConstants.IndexSource, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (!indexProfile.TryGet<OpenSearchIndexMetadata>(out var metadata))
        {
            metadata = new OpenSearchIndexMetadata();
        }

        metadata.IndexMappings ??= new OpenSearchIndexMap();
        indexProfile.Put(metadata);

        return Task.CompletedTask;
    }
}
```

### Options-Gated Source Registration

Use the generic overload with `TOptions` only when the source should appear only if the provider has valid configuration:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing.Core;
using OrchardCore.OpenSearch.Core.Models;

namespace OrchardCore.OpenSearch;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguredOpenSearchIndexingSource(
        this IServiceCollection services,
        string implementationType,
        Action<IndexingOptionsEntry> action = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(implementationType);

        services.AddIndexingSource<
            OpenSearchIndexManager,
            OpenSearchDocumentIndexManager,
            OpenSearchIndexNameProvider,
            OpenSearchConnectionOptions>(
            OpenSearchConstants.ProviderName,
            implementationType,
            action);

        return services;
    }
}
```

Choose this only when the Orchard Core admin UI should hide the source until configuration exists.

### OrchardCore.Search Integration

Indexing source registration is separate from search registration, but provider modules commonly need both:

```csharp
[RequireFeatures("OrchardCore.Search")]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSearchService<OpenSearchService>(OpenSearchConstants.ProviderName);
    }
}
```

Use both pieces when the provider should:

- build and maintain indexes
- answer site search requests through `ISearchService`

### Generic Lifecycle Steps

Use the built-in provider-agnostic index steps instead of creating source-specific ones:

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "ProductsOpenSearch",
          "IndexName": "products",
          "ProviderName": "OpenSearch",
          "Type": "Products"
        }
      ]
    },
    {
      "name": "RebuildIndex",
      "indexNames": ["ProductsOpenSearch"]
    }
  ]
}
```

## Practical Recommendations

- Put provider wrappers in the provider module or core provider assembly.
- Keep source type constants stable because they are stored in `IndexProfile.Type`.
- Use localized display names and descriptions so the source is understandable in admin.
- If the provider mirrors Orchard content indexing, follow the exact `ContentsStartup` pattern shown above.

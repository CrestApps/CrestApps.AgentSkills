---
name: orchardcore-indexing-sources
description: Skill for registering Orchard Core indexing sources for search providers. Covers AddIndexingSource wrappers, AddElasticsearchIndexingSource, OrchardCore.Contents integration, source metadata, options-gated registration, current index manager contracts, indexing task categories, and custom record indexing. Use this skill when requests mention Orchard Core Indexing Sources, AddElasticsearchIndexingSource, AddAzureAISearchIndexingSource, Register a new indexing source, or closely related Orchard Core implementation, setup, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Indexing Sources

An indexing source registers provider-specific index, document-index, and
index-name managers together with source metadata for the index-profile UI.
Use the provider's real wrapper when one exists.

## Elasticsearch Content Source

Orchard Core's Elasticsearch provider exposes
`AddElasticsearchIndexingSource`. Its content startup registers the source
with `IndexingConstants.ContentsIndexSource`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Data.Migration;
using OrchardCore.Elasticsearch;
using OrchardCore.Elasticsearch.Core.Handlers;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures("OrchardCore.Contents")]
public sealed class ContentsStartup : StartupBase
{
    private readonly IStringLocalizer<ContentsStartup> _localizer;

    public ContentsStartup(IStringLocalizer<ContentsStartup> localizer)
    {
        _localizer = localizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<IndexingMigrations>();

        services
            .AddIndexProfileHandler<ElasticsearchContentIndexProfileHandler>()
            .AddElasticsearchIndexingSource(IndexingConstants.ContentsIndexSource, options =>
            {
                options.DisplayName = _localizer["Content in Elasticsearch"];
                options.Description = _localizer["Create an Elasticsearch index based on site contents."];
            });
    }
}
```

The generic primitive is
`AddIndexingSource<TIndexManager, TDocumentIndexManager, TIndexNameProvider>`.
The type parameters must implement `IIndexManager`,
`IDocumentIndexManager`, and `IIndexNameProvider`, respectively. The
registration uses keyed services by provider name, so a custom provider should
resolve its managers by that key.

Indexing tasks use a shared category string. Use
`IndexingConstants.ContentsIndexSource` for content records, or define a
stable category for another record source and use the same value when creating
tasks and reading them with `IIndexingTaskManager`. Categories are universal
across providers; they are not provider-specific index names.

```csharp
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

await indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(
    recordId,
    IndexingConstants.ContentsIndexSource,
    RecordIndexingTaskTypes.Update));
```

Content index field names are centralized in `ContentIndexingConstants` from
the `OrchardCore.Contents.Indexing` namespace. Use these constants instead of
duplicating names such as `Content.ContentItem.ContentType` or
`Content.ContentItem.FullText`.

Use its options-gated overload only when a source must be hidden until valid
provider configuration exists.

## Custom Providers

The following namespace and types are intentionally fictional placeholders
for a custom provider. They are not Orchard Core APIs and must be replaced by
the provider's own manager implementations:

```csharp
// Fictional Contoso provider example.
services.AddIndexingSource<
    ContosoIndexManager,
    ContosoDocumentIndexManager,
    ContosoIndexNameProvider>(
    "Contoso",
    implementationType: "Products",
    options =>
    {
        options.DisplayName = "Products in Contoso";
        options.Description = "Create a Contoso index for product records.";
    });
```

Do not treat `OrchardCore.OpenSearch` or `AddOpenSearchIndexingSource` as
built-in Orchard Core APIs; no such provider exists in Orchard Core 3.0.0.
Pair a custom source with an
`IndexProfileHandlerBase` implementation only when it owns source-specific
defaults or mappings.

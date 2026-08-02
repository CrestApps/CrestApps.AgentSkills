---
name: orchardcore-indexing-sources
description: Skill for registering Orchard Core indexing sources for search providers. Covers AddIndexingSource wrappers, AddElasticsearchIndexingSource, OrchardCore.Contents integration, source metadata, options-gated registration, and custom record indexing. Use this skill when requests mention Orchard Core Indexing Sources, AddElasticsearchIndexingSource, AddAzureAISearchIndexingSource, Register a new indexing source, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
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

Do not use `OrchardCore.OpenSearch` or `AddOpenSearchIndexingSource`: no such
provider exists in Orchard Core v3.0.1. Pair a custom source with an
`IndexProfileHandlerBase` implementation only when it owns source-specific
defaults or mappings.

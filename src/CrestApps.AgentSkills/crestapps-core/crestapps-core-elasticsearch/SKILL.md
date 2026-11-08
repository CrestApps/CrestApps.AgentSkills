---
name: crestapps-core-elasticsearch
description: Skill for using Elasticsearch as the CrestApps.Core indexing and vector backend. Covers CrestApps.Core.Elasticsearch and CrestApps.Core.AI.Elasticsearch, AddElasticsearch on the indexing builder with AddAIDocuments, AddAIDataSources and AddAIMemory, AddCoreElasticsearchServices with a configuration section or a host-supplied ElasticsearchClient factory, the lower-level source registrations, AddElasticsearchVectorSearchService, AddElasticsearchMemoryVectorSearchService and AddElasticsearchAIDataSourceSourceHandler with DocumentIndexColumnNames, ElasticsearchConnectionOptions and authentication types, AddElasticsearchODataFilterTranslator, the filters prefix and typed knowledge columns, in-place schema upgrades through TryAddFieldsAsync, external Elasticsearch source mappings with IAIDataSourceChangeNotifier, and troubleshooting connection, certificate and filter problems.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Elasticsearch

Use Elasticsearch as the knowledge-base backend for AI documents, AI data sources and AI memory. For the data source model itself see `crestapps-core-data-sources`.

## Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai.AddOpenAI())
    .AddIndexingServices(indexing => indexing
        .AddYesSqlStores()
        .AddElasticsearch(
            builder.Configuration.GetSection("CrestApps:Elasticsearch"),
            elasticsearch => elasticsearch
                .AddAIDocuments()
                .AddAIDataSources()
                .AddAIMemory())));
```

The configuration section name is the host's choice. The sample hosts use `CrestApps:Elasticsearch`. `AddElasticsearch(configure)` without a section registers the services with no bound options.

| Call | Package | Adds |
|---|---|---|
| `AddCoreElasticsearchServices(section)` | `CrestApps.Core.Elasticsearch` | `ElasticsearchConnectionOptions`, `IElasticsearchClientFactory`, a singleton `ElasticsearchClient` created by that factory, keyed `IDataSourceContentManager`, `IDataSourceDocumentReader`, `IODataFilterTranslator`, `ISearchIndexManager`, `ISearchDocumentManager` |
| `AddElasticsearchODataFilterTranslator()` | `CrestApps.Core.Elasticsearch` | only the keyed `IODataFilterTranslator`, for hosts that bring their own clients and managers |
| `AddAIDocuments()` / `AddCoreElasticsearchAIDocumentSource()` | `CrestApps.Core.AI.Elasticsearch` | `AIDocuments` index-profile source, profile handler, keyed `IVectorSearchService` |
| `AddAIDataSources()` / `AddCoreElasticsearchAIDataSource()` | `CrestApps.Core.AI.Elasticsearch` | `DataSource` index-profile source, keyed `ElasticsearchAIDataSourceSourceHandler`, and `AddCoreAIDataSourceRag()` |
| `AddAIMemory()` / `AddCoreElasticsearchAIMemorySource()` | `CrestApps.Core.AI.Elasticsearch` | `AIMemory` index-profile source, profile handler, keyed `IMemoryVectorSearchService` |

All keyed services use the provider name `"Elasticsearch"`. Resolve them with `[FromKeyedServices("Elasticsearch")]`, never unkeyed.

## Configuration

```json
{
  "CrestApps": {
    "Elasticsearch": {
      "Url": "https://localhost:9200",
      "AuthenticationType": "Basic",
      "Username": "elastic",
      "Password": "use-a-secret",
      "CertificateFingerprint": "",
      "IndexPrefix": "app_"
    }
  }
}
```

`ElasticsearchConnectionOptions`: `Url`, `CloudId`, `AuthenticationType` (`None`, `Basic`, `ApiKey`, `Base64ApiKey`, `KeyIdAndKey`), `Username`, `Password`, `ApiKey`, `Base64ApiKey`, `ApiKeyId`, `CertificateFingerprint` and `IndexPrefix` (prepended to framework-managed index names). Elastic Cloud (`CloudId`) requires an authenticated mode. Keep credentials in user secrets, environment variables or a key vault.

## Filters and Typed Columns

- The translator prefixes each field with `filters.` because source fields live in a `filters` object. `category eq 'support'` becomes a `term` query on `filters.category`. A field already starting with `filters.` is not prefixed again.
- Dynamically mapped strings are `text` with a `keyword` sub-field. Use `category.keyword eq 'Support Docs'` for exact, mixed-case or multi-word matches.
- The typed knowledge columns `contentType`, `rootId`, `parentId` and `page` are not prefixed and map to real fields. `contentType eq null` matches rows written before the column existed.
- `ElasticsearchSearchIndexManager.TryAddFieldsAsync` adds the typed columns to an existing index mapping on the next data source sync.

## External Elasticsearch Sources

An `AIDataSource` with `Source = AIDataSourceSourceTypes.Elasticsearch` reads a remote index whose settings live on the data source: `Url` or `CloudId`, `EnvironmentType` (`SelfManaged` or `CloudHosted`), `AuthenticationType`, `IndexName`, credentials (protected with `AIDataSourceProtectionConstants.SourceSecretPurpose`) and `CertificateFingerprint`. These are independent of the backend options above, which say where CrestApps writes its knowledge-base chunks. Push remote changes with `IAIDataSourceChangeNotifier`.

## Supplying Your Own Client

A host that already holds an `ElasticsearchClient`, such as a multi-tenant host with one client per tenant, passes a `Func<IServiceProvider, ElasticsearchClient>` instead of a configuration section:

```csharp
using CrestApps.Core.Elasticsearch;
using CrestApps.Core.AI.Elasticsearch;
using CrestApps.Core.Infrastructure;

// Every core service with the host's client. No IElasticsearchClientFactory or ElasticsearchClient is registered.
builder.Services.AddCoreElasticsearchServices(sp => sp.GetRequiredService<TenantSearchClients>().Elasticsearch);

// AI search services alone, without the index profile sources or handlers.
builder.Services
    .AddElasticsearchVectorSearchService(
        sp => sp.GetRequiredService<TenantSearchClients>().Elasticsearch,
        new DocumentIndexColumnNames { FileName = "file_name" })
    .AddElasticsearchMemoryVectorSearchService(sp => sp.GetRequiredService<TenantSearchClients>().Elasticsearch)
    .AddElasticsearchAIDataSourceSourceHandler();
```

| Method | Registers |
|---|---|
| `AddCoreElasticsearchServices(Func<IServiceProvider, ElasticsearchClient>)` | content manager, document reader, filter translator, index and document managers with the supplied client |
| `AddElasticsearchDataSourceContentManager(clientFactory)` / `AddElasticsearchDataSourceDocumentReader(clientFactory)` | one service each |
| `AddElasticsearchVectorSearchService(clientFactory, DocumentIndexColumnNames columnNames = null)` | keyed `IVectorSearchService` for AI documents |
| `AddElasticsearchMemoryVectorSearchService(clientFactory)` | keyed `IMemoryVectorSearchService` |
| `AddElasticsearchAIDataSourceSourceHandler()` or `(Func<IServiceProvider, IElasticsearchClientFactory>)` | the external-source handler, with the default or a supplied client factory |

- The factory runs each time a scoped service is created, so return a client the host already holds.
- Search services are added only when none is registered for the key, so a host can register its own first and still call `AddAIDocuments()` or `AddAIMemory()`. The source handler uses `AddKeyedScoped`, so the last registration wins. It connects with the settings stored on each data source, which is why it takes an `IElasticsearchClientFactory`.
- `DocumentIndexColumnNames` (in `CrestApps.Core.Infrastructure`) names the fields of the host's AI documents index; each defaults to `DocumentIndexConstants.ColumnNames`.

## Troubleshooting

| Symptom | Check |
|---|---|
| Index profile creation says the provider is not configured | `Url` or `CloudId` missing from the bound section; the message now names the provider |
| `401 Unauthorized` | `AuthenticationType` matches the supplied credential fields |
| TLS failure | set `CertificateFingerprint` for self-signed clusters |
| Filter returns nothing | use the `.keyword` sub-field for exact string matches |
| Filter ignored with a warning | no keyed translator; call `AddElasticsearchODataFilterTranslator()` |

## Upgrading Within 2.0 Previews

- The section-based registrations now call the client-factory overloads with the factory's client, so their behavior is unchanged. `Elastic.Clients.Elasticsearch` was held at its current version in the latest package update.

## Upgrading from 1.x

- Existing indexes keep working; rows without `contentType` are read as `text`, and the typed columns are added in place.
- Hosts that wire their own Elasticsearch clients (for example Orchard Core) can now register only the translator with `AddElasticsearchODataFilterTranslator()`.
- Remove any deployment `Purpose` usage from embedding configuration; embeddings resolve through the `embedding` deployment slot or the index profile's `EmbeddingDeploymentName`.

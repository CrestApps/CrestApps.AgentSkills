---
name: crestapps-core-azure-ai-search
description: Skill for using Azure AI Search as the CrestApps.Core indexing and vector backend. Covers CrestApps.Core.Azure.AISearch and CrestApps.Core.AI.Azure.AISearch, AddAzureAISearch on the indexing builder with AddAIDocuments, AddAIDataSources and AddAIMemory, AddCoreAzureAISearchServices with a configuration section or a host-supplied SearchIndexClient factory, the AddAzureAISearchVectorSearchService, AddAzureAISearchMemoryVectorSearchService and AddAzureAISearchAIDataSourceSourceHandler registrations with DocumentIndexColumnNames, escaping filter values with ODataFilter, AzureAISearchConnectionOptions with Default, ApiKey and ManagedIdentity authentication, AddAzureAISearchODataFilterTranslator, the filters complex field and typed knowledge columns, in-place schema upgrades through TryAddFieldsAsync, external Azure AI Search source mappings with IAIDataSourceChangeNotifier, and troubleshooting tier, authentication and filter errors.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Azure AI Search

Use Azure AI Search as the knowledge-base backend for AI documents, AI data sources and AI memory. For the data source model see `crestapps-core-data-sources`.

## Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai.AddAzureOpenAI())
    .AddIndexingServices(indexing => indexing
        .AddYesSqlStores()
        .AddAzureAISearch(
            builder.Configuration.GetSection("CrestApps:AzureAISearch"),
            azureAISearch => azureAISearch
                .AddAIDocuments()
                .AddAIDataSources()
                .AddAIMemory())));
```

| Call | Package | Adds |
|---|---|---|
| `AddCoreAzureAISearchServices(section)` | `CrestApps.Core.Azure.AISearch` | `AzureAISearchConnectionOptions`, `IAzureAISearchClientFactory`, a singleton `SearchIndexClient`, keyed `IDataSourceContentManager`, `IDataSourceDocumentReader`, `IODataFilterTranslator`, `ISearchIndexManager`, `ISearchDocumentManager` |
| `AddAzureAISearchODataFilterTranslator()` | `CrestApps.Core.Azure.AISearch` | only the keyed translator, for hosts that bring their own clients |
| `AddAIDocuments()` / `AddCoreAzureAISearchAIDocumentSource()` | `CrestApps.Core.AI.Azure.AISearch` | `AIDocuments` index-profile source, profile handler, keyed `IVectorSearchService` |
| `AddAIDataSources()` / `AddCoreAzureAISearchAIDataSource()` | `CrestApps.Core.AI.Azure.AISearch` | `DataSource` index-profile source, keyed `AzureAISearchAIDataSourceSourceHandler`, and `AddCoreAIDataSourceRag()` |
| `AddAIMemory()` / `AddCoreAzureAISearchAIMemorySource()` | `CrestApps.Core.AI.Azure.AISearch` | `AIMemory` index-profile source, profile handler, keyed `IMemoryVectorSearchService` |

Every keyed service uses `AISearchConstants.ProviderName` (`"AzureAISearch"`).

## Configuration

```json
{
  "CrestApps": {
    "AzureAISearch": {
      "Endpoint": "https://my-search.search.windows.net",
      "AuthenticationType": "Default",
      "IdentityClientId": "",
      "IndexPrefix": ""
    }
  }
}
```

`AzureAISearchConnectionOptions`: `Endpoint`, `AuthenticationType` (`Default` uses `DefaultAzureCredential` and is the fallback for empty or unknown values, `ApiKey`, `ManagedIdentity`), `ApiKey`, `IdentityClientId` (user-assigned identity) and `IndexPrefix`. The older `IndexesPrefix` is still honored when `IndexPrefix` is empty. Prefer managed identity in Azure, and keep any admin key out of source control. Vector search needs the **Basic** tier or higher.

## Filters and Typed Columns

- The translator prefixes source fields with `filters/` (`category eq 'support'` becomes `filters/category eq 'support'`). Azure AI Search only filters on fields the schema declares filterable, so the knowledge base index must declare the `filters` complex field and each sub-field you filter on.
- `contentType`, `rootId`, `parentId` and `page` map to top-level columns, and `contentType eq null` is a null test.
- `AzureAISearchIndexManager.TryAddFieldsAsync` adds the typed columns to an existing index on the next sync.

## External Azure AI Search Sources

An `AIDataSource` with `Source = AIDataSourceSourceTypes.AzureAISearch` reads a remote index configured on the data source itself: `Endpoint`, `AuthenticationType`, `IndexName`, `IdentityClientId`, and an `ApiKey` protected with `AIDataSourceProtectionConstants.SourceSecretPurpose`. This is separate from the backend options above. Notify the sync pipeline with `IAIDataSourceChangeNotifier` when the remote index changes.

## Supplying Your Own Client

A host that already holds a `SearchIndexClient`, such as a multi-tenant host with one client per tenant, passes a `Func<IServiceProvider, SearchIndexClient>` instead of a configuration section:

```csharp
using CrestApps.Core.Azure.AISearch;
using CrestApps.Core.AI.Azure.AISearch;
using CrestApps.Core.Infrastructure;

// Every core service with the host's client. No IAzureAISearchClientFactory or SearchIndexClient is registered.
builder.Services.AddCoreAzureAISearchServices(sp => sp.GetRequiredService<TenantSearchClients>().IndexClient);

// AI search services alone, without the index profile sources or handlers.
builder.Services
    .AddAzureAISearchVectorSearchService(
        sp => sp.GetRequiredService<TenantSearchClients>().IndexClient,
        new DocumentIndexColumnNames { FileName = "file_name" })
    .AddAzureAISearchMemoryVectorSearchService(sp => sp.GetRequiredService<TenantSearchClients>().IndexClient)
    .AddAzureAISearchAIDataSourceSourceHandler();
```

| Method | Registers |
|---|---|
| `AddCoreAzureAISearchServices(Func<IServiceProvider, SearchIndexClient>)` | content manager, document reader, filter translator, index and document managers with the supplied client |
| `AddAzureAISearchDataSourceContentManager(clientFactory)` / `AddAzureAISearchDataSourceDocumentReader(clientFactory)` | one service each |
| `AddAzureAISearchVectorSearchService(clientFactory, DocumentIndexColumnNames columnNames = null)` | keyed `IVectorSearchService` for AI documents |
| `AddAzureAISearchMemoryVectorSearchService(clientFactory)` | keyed `IMemoryVectorSearchService` |
| `AddAzureAISearchAIDataSourceSourceHandler()` or `(Func<IServiceProvider, IAzureAISearchClientFactory>)` | the external-source handler, with the default or a supplied client factory |

- The factory runs each time a scoped service is created, so return a client the host already holds.
- Search services are added only when none is registered for the key, so a host can register its own first and still call `AddAIDocuments()` or `AddAIMemory()`. The source handler uses `AddKeyedScoped`, so the last registration wins. It connects with the credentials stored on each data source, which is why it takes an `IAzureAISearchClientFactory`.
- `DocumentIndexColumnNames` (in `CrestApps.Core.Infrastructure`) names the fields of the host's AI documents index; each defaults to `DocumentIndexConstants.ColumnNames`.

## Escaping Filter Values

Build any filter from untrusted values with `ODataFilter` (`CrestApps.Core.Azure.AISearch`); every filter the provider builds goes through it:

```csharp
ODataFilter.Quote("O'Brien");                    // 'O''Brien'
ODataFilter.Equal("userId", userId);             // userId eq '...'
ODataFilter.AnyEqual("referenceId", ["a", "b"]); // referenceId eq 'a' or referenceId eq 'b'
ODataFilter.Escape("O'Brien");                   // O''Brien
```

`Quote(null)` returns the OData `null` literal. Field names are written as is, so pass only names your code controls.

## Troubleshooting

| Symptom | Check |
|---|---|
| Index profile creation reports the provider is not configured | `Endpoint` missing from the bound section |
| `403` or credential errors | `AuthenticationType`, role assignments for the managed identity, or the admin key |
| Filter request fails | declare `filters` and the filtered sub-fields as filterable |
| Filter ignored with a warning | no keyed translator; call `AddAzureAISearchODataFilterTranslator()` |

## Upgrading Within 2.0 Previews

- The section-based registrations now call the client-factory overloads with the factory's client, so their behavior is unchanged.
- **Security fix:** the AI document and AI memory searches did not escape the reference and user identifiers in their filters; a crafted identifier could widen the filter to other references or users. Both are escaped now. Escape your own filter values with `ODataFilter`.

## Upgrading from 1.x

- Existing indexes keep working; the typed columns are added in place and missing `contentType` reads as `text`.
- Hosts that create their own `SearchIndexClient` can register only `AddAzureAISearchODataFilterTranslator()`.
- Embeddings resolve through the `embedding` deployment slot or the index profile's `EmbeddingDeploymentName`; deployment `Purpose` is gone.

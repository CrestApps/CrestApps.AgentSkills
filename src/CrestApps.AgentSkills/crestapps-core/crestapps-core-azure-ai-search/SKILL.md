---
name: crestapps-core-azure-ai-search
description: Skill for using Azure AI Search as the CrestApps.Core indexing and vector backend. Covers CrestApps.Core.Azure.AISearch and CrestApps.Core.AI.Azure.AISearch, AddAzureAISearch on the indexing builder with AddAIDocuments, AddAIDataSources and AddAIMemory, AddCoreAzureAISearchServices, AzureAISearchConnectionOptions with Default, ApiKey and ManagedIdentity authentication, AddAzureAISearchODataFilterTranslator, the filters complex field and typed knowledge columns, in-place schema upgrades through TryAddFieldsAsync, external Azure AI Search source mappings with IAIDataSourceChangeNotifier, and troubleshooting tier, authentication and filter errors.
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

## Troubleshooting

| Symptom | Check |
|---|---|
| Index profile creation reports the provider is not configured | `Endpoint` missing from the bound section |
| `403` or credential errors | `AuthenticationType`, role assignments for the managed identity, or the admin key |
| Filter request fails | declare `filters` and the filtered sub-fields as filterable |
| Filter ignored with a warning | no keyed translator; call `AddAzureAISearchODataFilterTranslator()` |

## Upgrading from 1.x

- Existing indexes keep working; the typed columns are added in place and missing `contentType` reads as `text`.
- Hosts that create their own `SearchIndexClient` can register only `AddAzureAISearchODataFilterTranslator()`.
- Embeddings resolve through the `embedding` deployment slot or the index profile's `EmbeddingDeploymentName`; deployment `Purpose` is gone.

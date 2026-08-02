---
name: crestapps-core-azure-ai-search
description: Skill for integrating Azure AI Search indexing data sources documents vector retrieval and memory in CrestApps.Core.
---

# CrestApps.Core Azure AI Search - Prompt Templates

## Add Azure AI Search

You are a CrestApps.Core expert. Configure the `CrestApps.Core.Azure.AISearch` and `CrestApps.Core.AI.Azure.AISearch` packages through the indexing builder.

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddIndexingServices(indexing => indexing
        .AddAzureAISearch(
            builder.Configuration.GetSection("CrestApps:AzureAISearch"),
            search => search
                .AddAIDocuments()
                .AddAIDataSources()
                .AddAIMemory())
    )
);
```

`AddCoreAzureAISearchServices(...)` is the lower-level equivalent. It configures `AzureAISearchConnectionOptions`, `IAzureAISearchClientFactory`, a `SearchIndexClient`, and keyed services with provider key `AISearchConstants.ProviderName`.

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

`AzureAISearchConnectionOptions` supports `Endpoint`, `AuthenticationType`, `ApiKey`, `IdentityClientId`, and `IndexPrefix`. Use `Default` or `ManagedIdentity` in hosted environments; protect an `ApiKey` outside source control.

## What Each AI Option Adds

- `AddAIDocuments()` registers the `AIDocuments` profile source, its index-profile handler, and keyed `IVectorSearchService`.
- `AddAIDataSources()` registers the `DataSource` profile source, data-source RAG services, the `AzureAISearch` `IAIDataSourceSourceHandler`, and its index-profile handler.
- `AddAIMemory()` registers the `AIMemory` profile source, its index-profile handler, and keyed `IMemoryVectorSearchService`.

The provider primitives register keyed `IDataSourceContentManager`, `IDataSourceDocumentReader`, `IODataFilterTranslator`, `ISearchIndexManager`, and `ISearchDocumentManager`. Resolve them with the Azure AI Search provider key rather than as unkeyed services.

## External Sources and Indexing

An `AIDataSource` whose source type is `AzureAISearch` reads an externally managed Azure AI Search index. Keep that source configuration separate from the shared backend configuration used to write CrestApps knowledge-base chunks. Notify the synchronization pipeline through `IAIDataSourceChangeNotifier` when an external source changes.

`AddAIDataSources()` also composes the shared asynchronous RAG synchronization components, including `IAIDataSourceIndexingQueue`, `IAIDataSourceIndexingService`, `AIDataSourceCatalogIndexingHandler`, `AIDataSourceSearchDocumentHandler`, `AIDataSourceIndexingBackgroundService`, `AIDataSourceAlignmentBackgroundService`, and `DataSourceSearchIndexProfileHandler`.

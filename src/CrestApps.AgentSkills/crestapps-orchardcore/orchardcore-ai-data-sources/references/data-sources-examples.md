# AI Data Sources Examples

## Knowledge base index profile

The knowledge base is an index profile of type `DataSourceIndex`. Its embedding deployment is stored in `DataSourceIndexProfileMetadata.EmbeddingDeploymentName` and must name a deployment that declares `textEmbedding`. The index profile recipe step itself belongs to Orchard Core (see `orchardcore-indexing`).

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "AIRagKnowledgeBase",
          "IndexName": "ai-rag-knowledge-base",
          "ProviderName": "Elasticsearch",
          "Type": "DataSourceIndex",
          "Properties": {
            "DataSourceIndexProfileMetadata": {
              "EmbeddingDeploymentName": "embedding-default"
            }
          }
        }
      ]
    }
  ]
}
```

Use `"ProviderName": "AzureAISearch"` for an Azure AI Search knowledge base. The provider handler builds the managed fields when the profile is created:

| Field | Elasticsearch | Azure AI Search |
|---|---|---|
| `chunkId` | `keyword`, key | Text, key, filterable |
| `referenceId` | `keyword` | Text, filterable |
| `dataSourceId` | `keyword` | Text, filterable |
| `referenceType` | `keyword` | Text, filterable |
| `chunkIndex` | integer | Integer |
| `title` | `text` | Text, searchable |
| `content` | `text`, default search field | Text, searchable |
| `timestamp` | date | DateTime, filterable, sortable |
| `embedding` | `dense_vector`, cosine, dimensions from the deployment | Vector, dimensions from the deployment |
| `filters` | dynamic object | not declared, so filters are unsupported |

## A deployment that can embed

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Deployments": [
          {
            "Name": "embedding-default",
            "ClientName": "OpenAI",
            "ConnectionName": "my-connection",
            "ModelName": "text-embedding-3-small",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textEmbedding" ]
              }
            }
          }
        ]
      }
    }
  }
}
```

## Source type recipes

### External Elasticsearch index

```json
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "DisplayText": "Support articles",
          "Source": "Elasticsearch",
          "AIKnowledgeBaseIndexProfileName": "AIRagKnowledgeBase",
          "KeyFieldName": "id",
          "TitleFieldName": "title",
          "ContentFieldName": "body",
          "Properties": {
            "ElasticsearchSourceMetadata": {
              "EnvironmentType": "SelfManaged",
              "Url": "https://search.example.com:9200",
              "AuthenticationType": "ApiKey",
              "IndexName": "support-articles"
            }
          }
        }
      ]
    }
  ]
}
```

`ElasticsearchSourceMetadata` accepts `EnvironmentType` (`SelfManaged` or `CloudHosted`), `Url`, `CloudId`, `AuthenticationType` (`None`, `Basic`, `ApiKey`, `Base64ApiKey`, `KeyIdAndKey`), `IndexName`, `Username`, `Password`, `ApiKey`, `Base64ApiKey`, `ApiKeyId`, and `CertificateFingerprint`. Secrets entered in the editor are stored data-protected, so supply them through the admin editor or the global connection below rather than in a recipe.

### External Azure AI Search index

`AzureAISearchSourceMetadata` accepts `Endpoint`, `AuthenticationType` (`Default`, `ApiKey`, or `ManagedIdentity`), `IndexName`, `IdentityClientId`, and `ApiKey`. The editor stores the API key data-protected, so prefer managed identity or enter the key in the editor.

```json
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "DisplayText": "Product catalog",
          "Source": "AzureAISearch",
          "AIKnowledgeBaseIndexProfileName": "AIRagKnowledgeBase",
          "KeyFieldName": "id",
          "TitleFieldName": "name",
          "ContentFieldName": "description",
          "Properties": {
            "AzureAISearchSourceMetadata": {
              "Endpoint": "https://contoso.search.windows.net",
              "AuthenticationType": "ManagedIdentity",
              "IndexName": "products"
            }
          }
        }
      ]
    }
  ]
}
```

### PostgreSQL table

```json
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "DisplayText": "Knowledge articles",
          "Source": "PostgreSQL",
          "AIKnowledgeBaseIndexProfileName": "AIRagKnowledgeBase",
          "KeyFieldName": "id",
          "TitleFieldName": "title",
          "ContentFieldName": "body",
          "Properties": {
            "PostgreSQLSourceMetadata": {
              "TableName": "public.articles"
            }
          }
        }
      ]
    }
  ]
}
```

- `TableName` may be schema-qualified. Each part is quoted as an identifier.
- Rows are read `SELECT *` in batches ordered by the key column; the key falls back to the `id` column.
- When the content column of a row is empty, the whole row is serialized as JSON content; a missing title is taken from the first line of the content (up to 200 characters).
- Every column is kept on the chunk, so it can be used in a filter.
- Leave `ConnectionString` out of recipes. A connection string saved in the editor is protected with data protection; a data source without one uses the global connection string.

### Web and File targets

```json
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "DisplayText": "Public website",
          "Source": "Web",
          "AIKnowledgeBaseIndexProfileName": "AIRagKnowledgeBase"
        }
      ]
    }
  ]
}
```

A `Web` data source has no connection settings and needs no field mapping; web crawlers point at it (see `orchardcore-ai-web-crawlers`). A `File` data source is the same kind of target for file sources (see `orchardcore-ai-file-sources`).

## Global connections

Configured values are read through `IShellConfiguration` under the `OrchardCore` section. When one exists, the source editor offers **Use the globally configured connection** (Elasticsearch) or **Use the globally configured connection string** (PostgreSQL). A value stored on the data source always wins.

### Elasticsearch

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Elasticsearch": {
        "Url": "http://localhost:9200",
        "AuthenticationType": "Basic",
        "Username": "elastic",
        "Password": "elasticsearch"
      },
      "AI": {
        "DataSources": {
          "Elasticsearch": {
            "Url": "http://reporting:9200"
          }
        }
      }
    }
  }
}
```

`CrestApps:Elasticsearch` is shared by every CrestApps Elasticsearch feature and is bound first; `CrestApps:AI:DataSources:Elasticsearch` overrides it for data sources only. Keys: `Url`, `CloudId`, `AuthenticationType`, `Username`, `Password`, `ApiKey`, `Base64ApiKey`, `ApiKeyId`, `CertificateFingerprint`. This is not the Orchard Core Elasticsearch section, which configures the Orchard-managed indexes (see `orchardcore-elasticsearch`).

### PostgreSQL

```json
{
  "OrchardCore": {
    "CrestApps": {
      "PostgreSQL": {
        "ConnectionString": "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=vectordb"
      },
      "AI": {
        "DataSources": {
          "PostgreSQL": {
            "ConnectionString": "Host=reporting;Port=5432;Username=postgres;Password=postgres;Database=reporting"
          }
        }
      }
    }
  }
}
```

Environment variables follow the usual pattern, for example `OrchardCore__CrestApps__PostgreSQL__ConnectionString` or `OrchardCore__CrestApps__AI__DataSources__PostgreSQL__ConnectionString`. Validation fails with *PostgreSQL connection string is required* when neither the data source nor configuration supplies one.

## Site settings recipe

```json
{
  "steps": [
    {
      "name": "Settings",
      "AIDataSourceSettings": {
        "DefaultStrictness": 3,
        "DefaultTopNDocuments": 5
      }
    }
  ]
}
```

The editor clamps strictness to 1 through 5 and the document count to 3 through 20.

## Reading the options in code

```csharp
public sealed class RetrievalDefaultsService
{
    private readonly IOptionsMonitor<AIDataSourceOptions> _options;

    public RetrievalDefaultsService(IOptionsMonitor<AIDataSourceOptions> options)
    {
        _options = options;
    }

    public int GetTopN(AIDataSourceRagMetadata metadata)
        => _options.CurrentValue.GetTopNDocuments(metadata?.TopNDocuments);
}
```

`GetTopNDocuments` and `GetStrictness` return the given value when it is in range, and the site default otherwise.

## Queue a sync from code

```csharp
public sealed class ProductImportCompletedHandler
{
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IAIDataSourceIndexingQueue _indexingQueue;

    public ProductImportCompletedHandler(
        IAIDataSourceStore dataSourceStore,
        IAIDataSourceIndexingQueue indexingQueue)
    {
        _dataSourceStore = dataSourceStore;
        _indexingQueue = indexingQueue;
    }

    public async Task ImportedAsync(string dataSourceId, CancellationToken cancellationToken)
    {
        var dataSource = await _dataSourceStore.FindByIdAsync(dataSourceId, cancellationToken);

        if (dataSource is not null)
        {
            await _indexingQueue.QueueSyncDataSourceAsync(dataSource, cancellationToken);
        }
    }
}
```

The Orchard queue defers the work until the request ends and runs it in a background job.

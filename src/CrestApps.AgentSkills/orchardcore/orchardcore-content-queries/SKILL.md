---
name: orchardcore-content-queries
description: Skill for querying content items in Orchard Core using YesSql and saved queries. Covers ContentItemIndex queries and its indexes (including IDX_ContentItemIndex_ContentItemId), custom YesSql indexes, ISession and IContentManager queries, saved SQL queries of the Queries module (Cyqwel parser, SELECT-only validation, parameters, paging), IQueryManager for creating and running saved queries from code, how SQL templates execute, custom query sources with IQuerySource, AddQuerySource and IQueryHandler, the Queries recipe step, the api/queries endpoint (ApiExecuteQueryGet and ApiExecuteQueryPost), query permissions, and the Liquid query filter. Use this skill when requests mention Orchard Core Content Queries, Query Content Items, ContentItemIndex, SQL queries, saved queries, or related troubleshooting. Strong matches include OrchardCore.Queries, ISession, IContentManager, MapIndex, IQueryManager, IQuerySource, QueryHandlerBase, ManageSqlQueries.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Queries - Prompt Templates

## Query Content Items

You are an Orchard Core expert. Generate code for querying content items using YesSql indexes and IContentManager.

### Guidelines

- Orchard Core uses YesSql as its document database abstraction over SQL.
- `ISession` is the primary interface for querying YesSql indexes.
- `ContentItemIndex` is the built-in index for all content items.
- Custom indexes can be created for frequently queried fields.
- `IContentManager` provides higher-level content operations (Get, New, Create, Publish).
- Always use `async/await` patterns for database queries.
- Use `.With<IndexType>()` to join against specific indexes.
- Keep YesSql predicates translatable: use comparisons, null checks, and boolean `&&` / `||`, but avoid ternaries and other conditional expressions inside query lambdas.
- If null and non-null rows need different query logic, split the query into multiple supported YesSql expressions and combine the results in memory.
- Use literal column names in `CreateMapIndexTableAsync()` and `AlterIndexTableAsync()` for custom YesSql migrations; do not use `nameof(...)`.
- `MapIndex` tables already include the `DocumentId` column automatically, so do not add it manually in the migration.
- Always seal classes.

### Choosing the Right Mechanism

"Query" and "index" mean three different things in Orchard Core. Pick the one that matches the goal.

| Goal | Mechanism | Skill |
|------|-----------|-------|
| Fast relational lookup from C# by a column | YesSql `MapIndex` table + `ISession.Query<T, TIndex>()` | this skill, `orchardcore-yessql-indexes` |
| Named, stored query run from Liquid, Razor, GraphQL or `api/queries` | Queries module (`IQueryManager`, `Sql`, `Lucene`, `Elasticsearch` sources) | this skill |
| Full-text search over content | Index profiles fed by index handlers | `orchardcore-indexing`, `orchardcore-search-indexing` |

### Querying with ContentItemIndex

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

public sealed class ContentQueryService
{
    private readonly ISession _session;

    public ContentQueryService(ISession session)
    {
        _session = session;
    }

    // Query by content type
    public async Task<IEnumerable<ContentItem>> GetByTypeAsync(string contentType)
    {
        return await _session
            .Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentType && x.Published)
            .ListAsync();
    }

    // Query by content type with paging
    public async Task<IEnumerable<ContentItem>> GetPagedAsync(string contentType, int page, int pageSize)
    {
        return await _session
            .Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentType && x.Published)
            .OrderByDescending(x => x.CreatedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync();
    }

    // Query by display text
    public async Task<ContentItem> GetByDisplayTextAsync(string displayText)
    {
        return await _session
            .Query<ContentItem, ContentItemIndex>(x => x.DisplayText == displayText && x.Published)
            .FirstOrDefaultAsync();
    }

    // Count content items
    public async Task<int> CountByTypeAsync(string contentType)
    {
        return await _session
            .Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentType && x.Published)
            .CountAsync();
    }

    // Query latest versions (including drafts)
    public async Task<IEnumerable<ContentItem>> GetLatestAsync(string contentType)
    {
        return await _session
            .Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentType && x.Latest)
            .ListAsync();
    }
}
```

### ContentItemIndex Fields Reference

The built-in `ContentItemIndex` has these fields:

- `ContentItemId` — Unique content item identifier.
- `ContentItemVersionId` — Version-specific identifier.
- `ContentType` — The content type name.
- `DisplayText` — The display text (usually from TitlePart).
- `Published` — Whether this version is the published one.
- `Latest` — Whether this version is the latest one.
- `CreatedUtc` — When the content item was created.
- `ModifiedUtc` — When the content item was last modified.
- `PublishedUtc` — When the content item was published.
- `Owner` — The owner user ID.
- `Author` — The author user name.

Indexes on the table include `IDX_ContentItemIndex_DocumentId` (and the other `DocumentId`-led indexes) plus, since Orchard Core 4.0, `IDX_ContentItemIndex_ContentItemId` (`ContentItemId`, `DocumentId`, `Published`, `Latest`). Reads by content item id, such as `IContentManager.GetAsync(id)` or `GetAsync(ids)`, now seek this index instead of scanning. Existing tenants get it through a migration step, which can take a moment on a large table during the upgrade. Prefer filtering on `ContentItemId` together with `Published` or `Latest` to benefit from it.

### Creating a Custom YesSql Index

```csharp
using OrchardCore.ContentManagement;
using YesSql.Indexes;

public sealed class {{PartName}}Index : MapIndex
{
    public string ContentItemId { get; set; }
    public string {{PropertyName}} { get; set; }
    public bool Published { get; set; }
}

public sealed class {{PartName}}IndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<{{PartName}}Index>()
            .When(contentItem => contentItem.Has<{{PartName}}>())
            .Map(contentItem =>
            {
                if (!contentItem.TryGet<{{PartName}}>(out var part))
                {
                    return null;
                }

                return new {{PartName}}Index
                {
                    ContentItemId = contentItem.ContentItemId,
                    {{PropertyName}} = part.{{PropertyName}},
                    Published = contentItem.Published
                };
            });
    }
}
```

### Registering Custom Index and Migration

```csharp
using OrchardCore.Data.Migration;
using YesSql.Sql;

public sealed class Migrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<{{PartName}}Index>(table => table
            .Column<string>("ContentItemId", col => col.WithLength(26))
            .Column<string>("{{PropertyName}}", col => col.WithLength(256))
            .Column<bool>("Published")
        );

        await SchemaBuilder.AlterIndexTableAsync<{{PartName}}Index>(table => table
            .CreateIndex("IDX_{{PartName}}Index_{{PropertyName}}",
                "{{PropertyName}}",
                "Published"));

        return 1;
    }
}
```

### Registering Index Provider in Startup

```csharp
using OrchardCore.Data.Migration;
using YesSql.Indexes;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProvider<{{PartName}}IndexProvider>();
        services.AddDataMigration<Migrations>();
    }
}
```

### Querying with Custom Index

```csharp
public sealed class {{PartName}}QueryService
{
    private readonly ISession _session;

    public {{PartName}}QueryService(ISession session)
    {
        _session = session;
    }

    public async Task<IEnumerable<ContentItem>> GetByPropertyAsync(string value)
    {
        return await _session
            .Query<ContentItem, {{PartName}}Index>(x => x.{{PropertyName}} == value && x.Published)
            .ListAsync();
    }
}
```

### Using IContentManager

```csharp
using OrchardCore.ContentManagement;

public sealed class ContentService
{
    private readonly IContentManager _contentManager;

    public ContentService(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    // Get by ID
    public async Task<ContentItem> GetByIdAsync(string contentItemId)
    {
        return await _contentManager.GetAsync(contentItemId);
    }

    // Get specific version
    public async Task<ContentItem> GetVersionAsync(string versionId)
    {
        return await _contentManager.GetVersionAsync(versionId);
    }

    // Create new content item
    public async Task<ContentItem> CreateAsync(string contentType)
    {
        var contentItem = await _contentManager.NewAsync(contentType);
        await _contentManager.CreateAsync(contentItem, VersionOptions.Draft);
        return contentItem;
    }

    // Publish a content item
    public async Task PublishAsync(ContentItem contentItem)
    {
        await _contentManager.PublishAsync(contentItem);
    }

    // Update content item
    public async Task UpdateAsync(ContentItem contentItem)
    {
        await _contentManager.UpdateAsync(contentItem);
    }

    // Remove content item
    public async Task RemoveAsync(ContentItem contentItem)
    {
        await _contentManager.RemoveAsync(contentItem);
    }
}
```

### Combining Predicates and Ordering

```csharp
using YesSql;

public sealed class AdvancedQueryService
{
    private readonly ISession _session;

    public AdvancedQueryService(ISession session)
    {
        _session = session;
    }

    public async Task<IEnumerable<ContentItem>> QueryWithSqlAsync()
    {
        return await _session
            .Query<ContentItem, ContentItemIndex>()
            .Where(x => x.Published && x.ContentType == "Article")
            .OrderByDescending(x => x.PublishedUtc)
            .Take(10)
            .ListAsync();
    }
}
```

## Saved SQL Queries (Queries Module)

The **SQL Queries** feature (`OrchardCore.Queries.Sql`, depends on `OrchardCore.Queries`) stores named SQL queries that run against the index tables. They are used from Liquid, Razor, scripting, GraphQL, the `api/queries/{name}` endpoint, data sources of data pipelines (see `orchardcore-data-sources`), and widgets.

### SQL Syntax and Validation

- Queries are parsed by the **Cyqwel** SQL parser and translated to the dialect of the tenant database (SQL Server, SQLite, PostgreSQL, MySQL), so they can be exported to sites using another database. Table names get the tenant table prefix and schema automatically.
- **Only `SELECT` statements are supported.** `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `TRUNCATE`, `CREATE`, `ALTER`, `DROP`, `CALL`, multiple statements (`select ...; delete ...`) and mutations nested in CTEs or sub-queries are rejected with "Only SELECT statements are supported."
- The admin editor validates the SQL when saving a query that contains no Liquid syntax and shows parse errors with their line and column. Queries containing Liquid are validated after rendering, when executed.
- Supported constructs include `DISTINCT`, aggregates such as `count(distinct a)`, `GROUP BY`, `UNION`, joins, sub-queries, and `LIMIT n OFFSET m` (translated per dialect, e.g. `OFFSET ... FETCH NEXT` on SQL Server).
- Helper functions translated per dialect: `second()`, `minute()`, `hour()`, `day()`, `month()`, `year()`, `now()` (UTC), and `random()` in `ORDER BY`.
- Return `DocumentId` and check **Return Documents** (`ReturnContentItems`) to load content items.

```sql
select DocumentId
from ContentItemIndex
where Published = true and ContentType = @contenttype:'BlogPost'
order by CreatedUtc desc
limit @limit:10
```

### Parameters and Templates

- Parameters use `@name` or `@name:default_value`; names are case-sensitive. Always pass user input as parameters.
- A SQL query is a Liquid template, so Liquid may change the shape of the query, but never output user input with `{{ ... }}` (SQL injection).

```liquid
{% if limit > 0 %}
    select DocumentId from ContentItemIndex where Published = true limit @limit
{% else %}
    select DocumentId from ContentItemIndex where Published = true
{% endif %}
```

### Recipe Step

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [ "OrchardCore.Queries", "OrchardCore.Queries.Sql" ]
    },
    {
      "name": "Queries",
      "Queries": [
        {
          "Source": "Sql",
          "Name": "RecentBlogPosts",
          "Template": "select DocumentId from ContentItemIndex where ContentType = 'BlogPost' and Published = true order by CreatedUtc desc limit @limit:3",
          "Schema": "{ \"type\": \"ContentItem/BlogPost\" }",
          "ReturnContentItems": true
        }
      ]
    }
  ]
}
```

### Using Saved Queries

```liquid
{% assign posts = Queries.RecentBlogPosts | query: limit: 5 %}
{% for item in posts %}
    {{ item | display_text }}
{% endfor %}
```

```cshtml
@foreach (var contentItem in await Orchard.ContentQueryAsync("RecentBlogPosts"))
{
    @await Orchard.DisplayAsync(contentItem)
}
```

The `executeQuery(name, parameters)` scripting function runs a query from JavaScript (workflows, recipes).

### Web API

`api/queries/{name}` runs a query with `GET` or `POST`; `parameters` is a JSON object, passed in the query string or as the POST body. The response holds `items` and `count`. The caller needs `ExecuteApi_{QueryName}` (implied by `ExecuteApiAll` and `ManageQueries`); a query the caller may not execute returns `404`.

```http
POST /api/queries/RecentBlogPosts
Content-Type: application/json

{ "limit": 5 }
```

In Orchard Core 4.0, the endpoint is split into two actions with endpoint names `ApiExecuteQueryGet` and `ApiExecuteQueryPost` (route, verbs and parameters unchanged), so OpenAPI documents are valid (see `orchardcore-openapi`).

### Permissions

| Permission | Member |
|------------|--------|
| Manage SQL Queries (security critical) | `OrchardCore.Queries.QueriesPermissions.ManageSqlQueries` |
| Manage queries | `OrchardCore.Queries.QueryPermissions.ManageQueries` |
| Execute Api - All queries | `OrchardCore.Queries.QueryPermissions.ExecuteApiAll` |
| Execute Api - {name} | `QueryPermissions.CreatePermissionForQuery(name)` |

Since Orchard Core 4.0, the GraphQL fields that expose named SQL, Lucene and Elasticsearch queries also require `ExecuteApi_{QueryName}`, like the REST endpoint.

### Running Queries from Code

`IQueryManager` (namespace `OrchardCore.Queries`) creates, stores and runs named queries. `ExecuteQueryAsync` resolves the `IQuerySource` keyed by `Query.Source`.

```csharp
using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;
using OrchardCore.Queries;

public sealed class RecentPostsService
{
    private readonly IQueryManager _queryManager;

    public RecentPostsService(IQueryManager queryManager)
    {
        _queryManager = queryManager;
    }

    public async Task EnsureQueryAsync()
    {
        if (await _queryManager.GetQueryAsync("RecentBlogPosts") is not null)
        {
            return;
        }

        // The data keys match the recipe step: Name, Template, Schema, ReturnContentItems.
        var query = await _queryManager.NewAsync("Sql", new JsonObject
        {
            ["Name"] = "RecentBlogPosts",
            ["Template"] = "select DocumentId from ContentItemIndex where ContentType = 'BlogPost' and Published = true order by CreatedUtc desc limit @limit:10",
            ["ReturnContentItems"] = true,
        });

        await _queryManager.SaveAsync(query);
    }

    public async Task<IEnumerable<ContentItem>> GetRecentAsync(int limit)
    {
        var query = await _queryManager.GetQueryAsync("RecentBlogPosts");

        var results = await _queryManager.ExecuteQueryAsync(query, new Dictionary<string, object>
        {
            ["limit"] = limit,
        });

        return results.Items.OfType<ContentItem>();
    }
}
```

| Member | Purpose |
|--------|---------|
| `NewAsync(source, data)` | Build a query for a registered source; returns `null` when the source is unknown. |
| `UpdateAsync(query, data)` / `SaveAsync(params Query[])` | Persist changes. |
| `GetQueryAsync(name)` / `DeleteQueryAsync(params string[])` | Load or remove by name. |
| `ListQueriesAsync(QueryContext)` / `PageQueriesAsync(page, pageSize, context)` | List queries; the `ListQueriesBySourceAsync(source)` extension filters by source. |
| `ExecuteQueryAsync(query, parameters)` | Run the query and return `IQueryResults.Items`. |

How a SQL query executes:

1. The `Template` (stored in `SqlQueryMetadata`) is rendered as Liquid with the parameters as Liquid values.
2. The result is parsed by Cyqwel and translated to the tenant dialect, schema and table prefix. A parse failure is logged as an error and returns an empty result instead of throwing.
3. The SQL runs through Dapper with the parameters bound as SQL parameters.
4. With `ReturnContentItems` the `DocumentId` column (or the first `long` column) is used to load `ContentItem` documents; otherwise each row is returned as a `JsonObject`.

### Custom Query Sources

Implement `IQuerySource` and register it with `AddQuerySource<T>(name)`, which registers the source as a keyed service under its name. Pair it with a `DisplayDriver<Query>` for the admin editor and an `IQueryHandler` (or `QueryHandlerBase`) that copies source-specific data from the recipe or editor JSON into the query.

```csharp
using System.Text.Json.Nodes;
using OrchardCore.Entities;
using OrchardCore.Queries;
using OrchardCore.Queries.Core;

public sealed class ProductQueryMetadata
{
    public string Filter { get; set; }
}

public sealed class ProductQueryResults : IQueryResults
{
    public IEnumerable<object> Items { get; set; } = [];
}

public sealed class ProductQuerySource : IQuerySource
{
    public const string SourceName = "Products";

    public string Name => SourceName;

    public Task<IQueryResults> ExecuteQueryAsync(Query query, IDictionary<string, object> parameters)
    {
        var metadata = query.GetOrCreate<ProductQueryMetadata>();

        // Run the query against your store using metadata.Filter and the parameters.
        IQueryResults results = new ProductQueryResults();

        return Task.FromResult(results);
    }
}

public sealed class ProductQueryHandler : QueryHandlerBase
{
    public override Task InitializingAsync(InitializingQueryContext context)
        => CopyAsync(context);

    public override Task UpdatingAsync(UpdatingQueryContext context)
        => CopyAsync(context);

    private static Task CopyAsync(DataQueryContextBase context)
    {
        if (context.Query.Source != ProductQuerySource.SourceName)
        {
            return Task.CompletedTask;
        }

        var filter = context.Data?[nameof(ProductQueryMetadata.Filter)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(filter))
        {
            context.Query.Put(new ProductQueryMetadata { Filter = filter });
        }

        return Task.CompletedTask;
    }
}
```

```csharp
[RequireFeatures("OrchardCore.Queries")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddQuerySource<ProductQuerySource>(ProductQuerySource.SourceName);
        services.AddScoped<IQueryHandler, ProductQueryHandler>();
        // Your DisplayDriver<Query> that renders the Filter editor.
        services.AddDisplayDriver<Query, ProductQueryDisplayDriver>();
    }
}
```

`IQueryHandler` exposes `InitializingAsync`, `InitializedAsync`, `LoadedAsync`, `UpdatingAsync`, `UpdatedAsync`, `DeletingAsync` and `DeletedAsync`. Every handler sees every query, so check `context.Query.Source` first.

### Upgrading from 3.x

- `OrchardCore.Queries.Sql.Permissions.ManageSqlQueries` was removed; use `OrchardCore.Queries.QueriesPermissions.ManageSqlQueries` (stored permission names are unchanged).
- The Parlot-based SQL parser (`SqlAst`, `SqlTranslator`, `ParlotSqlParser`, `SqlParserException`) was replaced by Cyqwel. Non-`SELECT` statements, which were never intended, now fail to parse; review queries that relied on lenient parsing.
- Generated API clients for `api/queries/{name}` get new method names (`ApiExecuteQueryGet`, `ApiExecuteQueryPost`, previously `ApiExecuteQuery`) on regeneration.

---
name: orchardcore-graphql
description: Skill for configuring and using the GraphQL API in Orchard Core. Covers GraphQL queries, custom graph types, content querying, IIndexAliasProvider, AddWhereInputIndexPropertyProvider, where filters, GraphQLPermissions, named query permissions, RequirePermission on fields, the OrchardCore Apis GraphQL configuration section, GraphQLContentOptions, and rate limiting. Use this skill when requests mention Orchard Core GraphQL, Configure and Use GraphQL API, Enabling GraphQL Features, Basic Content Query, Query with Filtering, Query with Pagination, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core GraphQL

Enable `OrchardCore.Apis.GraphQL` for `/api/graphql` (GET with `query`/`variables`/`operationName`, or POST with `application/json` or `application/graphql`). Use **GraphiQL** in the admin to inspect the generated schema and field names. Do not invent a Liquid `graphql` tag; execute GraphQL through the HTTP endpoint or application code.

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.Apis.GraphQL"
      ]
    }
  ]
}
```

## Permissions

Permissions are on `OrchardCore.Apis.GraphQL.GraphQLPermissions`:

| Permission | Grants |
|---|---|
| `ExecuteGraphQL` | Run queries. Implied by `ExecuteGraphQLMutations`. Anonymous users do not have it by default. |
| `ExecuteGraphQLMutations` | Run mutations. |
| `ApiViewContent` | Access view content endpoints. |

- Content queries are also filtered by `ViewContent` / `ViewOwnContent`, per content type and per returned item, so a caller only sees items it may view.
- Named queries (SQL, Lucene, Elasticsearch) exposed as GraphQL fields require `ExecuteApi_{QueryName}`, `ExecuteApiAll`, or `ManageQueries` (`OrchardCore.Queries.QueryPermissions.CreatePermissionForQuery(name)`), the same as `api/queries/{name}`.
- The endpoint accepts cookie sessions and bearer tokens from `OrchardCore.OpenId.Validation`. A token must carry roles (the `roles` scope or client `RoleEntries`) that grant these permissions.

Protect a custom field with `RequirePermission`:

```csharp
using GraphQL.Types;
using OrchardCore.Apis.GraphQL;

namespace MyModule.GraphQL;

public sealed class InvoiceQueryObjectType : ObjectGraphType<Invoice>
{
    public InvoiceQueryObjectType()
    {
        Name = "Invoice";

        Field(x => x.Number);
        Field(x => x.Total)
            .RequirePermission(InvoicePermissions.ViewInvoiceTotals);
    }
}
```

## Configuration

Settings come from the tenant-aware `OrchardCore:Apis:GraphQL` section (formerly `OrchardCore_Apis_GraphQL`):

```json
{
  "OrchardCore": {
    "Apis": {
      "GraphQL": {
        "ExposeExceptions": false,
        "MaxDepth": 100,
        "MaxComplexity": 1000,
        "FieldImpact": 2.0,
        "DefaultNumberOfResults": 100,
        "MaxNumberOfResults": 1000,
        "MaxNumberOfResultsValidationMode": "Enabled"
      }
    }
  }
}
```

| Setting | Default |
|---|---|
| `ExposeExceptions` | `true` in Development, otherwise `false`. Keep it off in production. |
| `MaxDepth` | `100` |
| `MaxComplexity`, `FieldImpact` | Not set |
| `DefaultNumberOfResults` / `MaxNumberOfResults` | `100` / `1000` |
| `MaxNumberOfResultsValidationMode` | `Default` (error in Development, truncate and log otherwise), `Enabled`, or `Disabled` |

Control which content types, parts, and fields are exposed:

```csharp
services.Configure<GraphQLContentOptions>(options =>
{
    options.DiscoverableSterotypes.Add("ExampleStereotype");
    options.IgnoreField<ContentItemType>(nameof(ContentItem.Owner));
    options.ConfigureContentType("SiteLayers", type => type.Hidden = true);
});
```

Content types with a stereotype are hidden unless their stereotype is listed in `DiscoverableSterotypes` (the property name keeps this spelling).

## Rate Limiting and Rendered HTML

- The GraphQL path is mapped through endpoint routing, so an `Endpoint` policy on `/api/graphql` from `orchardcore-rate-limits` throttles it.
- Rendered `html` fields of `HtmlBodyPart`, `HtmlField`, `MarkdownBodyPart`, and `MarkdownField` no longer run Liquid in 4.0; when `SanitizeHtml` is enabled, sanitization runs on the final rendered HTML. Use `LiquidPart` or `LiquidField` for Liquid content.

## Graph Types and Where Filters

`InputObjectGraphType<T>` models an ordinary input object. It does not
automatically create predicate suffixes such as `_contains`. For indexed
content filtering, derive from `WhereInputObjectGraphType<T>` and use its
filter-field helpers.

```csharp
using GraphQL.Types;
using Microsoft.Extensions.Localization;
using OrchardCore.Apis.GraphQL.Queries;

namespace MyModule;

public sealed class ProductWhereInput : WhereInputObjectGraphType<ProductIndex>
{
    public ProductWhereInput(IStringLocalizer<ProductWhereInput> localizer)
        : base(localizer)
    {
        AddScalarFilterFields<StringGraphType>(
            fieldName: nameof(ProductIndex.Sku),
            description: "Product SKU",
            aliasName: "Product",
            contentPart: "ProductPart",
            contentField: "Sku");
    }
}
```

Register a map index with the dedicated extension:

```csharp
services.AddWhereInputIndexPropertyProvider<ProductIndex>();
```

## Index Aliases

`IIndexAliasProvider` is asynchronous in v3:

```csharp
using OrchardCore.ContentManagement.GraphQL.Queries;

namespace MyModule;

public sealed class ProductIndexAliasProvider : IIndexAliasProvider
{
    public ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync()
    {
        IEnumerable<IndexAlias> aliases =
        [
            new IndexAlias
            {
                Alias = "Product",
                Index = nameof(ProductIndex),
                IndexType = typeof(ProductIndex),
            },
        ];

        return ValueTask.FromResult(aliases);
    }
}
```

Register it with its intended lifetime:

```csharp
services.AddScoped<IIndexAliasProvider, ProductIndexAliasProvider>();
```

## Upgrading from 3.x

- `OrchardCore.Apis.GraphQL.CommonPermissions` was removed; use `OrchardCore.Apis.GraphQL.GraphQLPermissions.ExecuteGraphQL` and `ExecuteGraphQLMutations`.
- `OrchardCore.ContentManagement.GraphQL.Permissions.ApiViewContent` was removed; use `OrchardCore.Apis.GraphQL.GraphQLPermissions.ApiViewContent`.
- Rename the `OrchardCore_Apis_GraphQL` section to `OrchardCore:Apis:GraphQL` (environment variables `OrchardCore__Apis__GraphQL__MaxDepth`, ...). The old name still works but is deprecated.
- Grant `ExecuteApi_{QueryName}` (or `ExecuteApiAll`) to roles that run named queries through GraphQL; `ExecuteGraphQL` alone is no longer enough.

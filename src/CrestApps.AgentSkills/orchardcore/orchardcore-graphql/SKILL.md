---
name: orchardcore-graphql
description: Skill for configuring and using the GraphQL API in Orchard Core. Covers GraphQL queries, custom graph types, content querying, IIndexAliasProvider, AddWhereInputIndexPropertyProvider, where filters, and GraphQL permissions. Use this skill when requests mention Orchard Core GraphQL, Configure and Use GraphQL API, Enabling GraphQL Features, Basic Content Query, Query with Filtering, Query with Pagination, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core GraphQL

Enable `OrchardCore.Apis.GraphQL` for `/api/graphql`. The endpoint requires
`ExecuteGraphQL`; mutations additionally require
`ExecuteGraphQLMutations`. Content access is also limited by the caller's
content-view permissions. A JWT bearer principal must therefore receive the
same Orchard permissions through its user or role before it can query content.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Apis.GraphQL"
      ]
    }
  ]
}
```

Use GraphiQL in the admin UI to inspect the schema and generated field names.
Do not embed a fictional Liquid `graphql` tag; execute GraphQL through its
HTTP endpoint or application code.

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

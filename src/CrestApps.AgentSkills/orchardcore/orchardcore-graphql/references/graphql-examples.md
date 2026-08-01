# GraphQL Examples

## Content Query

```graphql
{
  blogPost(status: PUBLISHED, first: 10) {
    contentItemId
    displayText
    publishedUtc
  }
}
```

Use the schema explorer to determine which generated where fields are
available for a particular index and content definition.

## Custom Where Input

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
            nameof(ProductIndex.Sku),
            "Product SKU",
            "Product",
            "ProductPart",
            "Sku");
    }
}
```

```csharp
services.AddWhereInputIndexPropertyProvider<ProductIndex>();
```

For a generic mutation or command input with no generated filter operators,
use `InputObjectGraphType<T>` instead.

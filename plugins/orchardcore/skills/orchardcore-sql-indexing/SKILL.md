---
name: orchardcore-sql-indexing
description: Skill for using SQL-based content indexing in Orchard Core. Covers content item indexing tables, field indexing tables, database schema reference, querying indexed data with C#, Razor views, Liquid templates, and GraphQL, and SQL indexing performance patterns. Use this skill when requests mention Orchard Core SQL Indexing, Query SQL-Indexed Content, Enabling SQL Field Indexing, ContentItemIndex Schema, LocalizedContentItemIndex Schema, Content Field Index Tables, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.ContentFields.Indexing.SQL, OrchardCore.ContentManagement, ContentItemIndex, ISession, LocalizedContentItemIndex, ContentPart, ContentField, BooleanFieldIndex, ContentPickerFieldIndex. It also helps with LocalizedContentItemIndex Schema, Content Field Index Tables, Querying from C#, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core SQL Indexing - Prompt Templates

## Query SQL-Indexed Content

You are an Orchard Core expert. Generate code for querying SQL-indexed content in Orchard Core using YesSql sessions, Razor views, Liquid templates, and GraphQL.

### Guidelines

- The `ContentItemIndex` table is always available and indexes core content item fields.
- Enable `OrchardCore.ContentFields.Indexing.SQL` to create SQL index tables for content fields (Boolean, Text, Numeric, Date, etc.).
- Use `ISession.Query<ContentItem, TIndex>()` in C# to query indexed data.
- Use `@inject ISession Session` in Razor views to access query capabilities.
- In Liquid, create a SQL Query in Orchard Core admin and use `Queries.QueryName | query` to retrieve records.
- Dates are stored as UTC — convert to local time when displaying.
- Column types listed are SQL Server types; SQLite has no length limits on text fields.
- GraphQL filters are automatically generated from SQL index tables when `OrchardCore.ContentFields.Indexing.SQL` is enabled.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling SQL Field Indexing

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.ContentFields.Indexing.SQL"
      ],
      "disable": []
    }
  ]
}
```

### ContentItemIndex Schema

The `ContentItemIndex` table is always available for querying content items.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `int` | Primary key. |
| `DocumentId` | `int` | YesSql document ID. |
| `ContentItemId` | `nvarchar(26)` | Unique content item identifier. |
| `Published` | `bit` | Whether the item is published. |
| `Latest` | `bit` | Whether this is the latest version. |
| `ModifiedUtc` | `datetime` | Last modified date (UTC). |
| `PublishedUtc` | `datetime` | Publish date (UTC). |
| `CreatedUtc` | `datetime` | Creation date (UTC). |
| `Owner` | `nvarchar(255)` | Owner of the content item. |
| `Author` | `nvarchar(255)` | Author of the content item. |
| `DisplayText` | `nvarchar(255)` | Display text of the content item. |

### LocalizedContentItemIndex Schema

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `int` | Primary key. |
| `DocumentId` | `int` | YesSql document ID. |
| `ContentItemId` | `nvarchar(26)` | Unique content item identifier. |
| `Published` | `bit` | Whether the item is published. |
| `Latest` | `bit` | Whether this is the latest version. |
| `LocalizationSet` | `nvarchar` | Localization set identifier. |
| `Culture` | `nvarchar` | Culture code for the content item. |

### Content Field Index Tables

Each field type has a corresponding index table. All share these common columns:

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `int` | Primary key. |
| `DocumentId` | `int` | YesSql document ID. |
| `ContentItemId` | `nvarchar(26)` | Content item identifier. |
| `ContentItemVersionId` | `nvarchar(26)` | Content item version identifier. |
| `ContentType` | `nvarchar(255)` | Content type name. |
| `ContentPart` | `nvarchar(255)` | Content part name. |
| `ContentField` | `nvarchar(255)` | Content field name. |
| `Published` | `bit` | Whether the item is published. |
| `Latest` | `bit` | Whether this is the latest version. |

**Field-specific columns by table:**

| Table | Value Column(s) | Type |
|-------|-----------------|------|
| `BooleanFieldIndex` | `Boolean` | `bit` |
| `ContentPickerFieldIndex` | `SelectedContentItemId` | `nvarchar(26)` |
| `DateFieldIndex` | `Date` | `datetime` |
| `DateTimeFieldIndex` | `DateTime` | `datetime` |
| `HtmlFieldIndex` | `Html` | `nvarchar(max)` |
| `LinkFieldIndex` | `Url`, `BigUrl`, `Text`, `BigText` | `nvarchar(766)` / `nvarchar(max)` |
| `MultiTextFieldIndex` | `Value`, `BigValue` | `nvarchar(766)` / `nvarchar(max)` |
| `NumericFieldIndex` | `Numeric` | `decimal(19,5)` |
| `TextFieldIndex` | `Text`, `BigText` | `nvarchar(766)` / `nvarchar(max)` |
| `TimeFieldIndex` | `Time` | `datetime` |
| `UserPickerFieldIndex` | `SelectedUserId` | `string` |

### Querying from C#

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentFields.Indexing.SQL;
using YesSql;

namespace MyModule;

public sealed class ProductService
{
    private readonly ISession _session;

    public ProductService(ISession session)
    {
        _session = session;
    }

    public async Task<IEnumerable<ContentItem>> GetProductsByTextField(
        string contentType,
        string contentField)
    {
        return await _session
            .Query<ContentItem, TextFieldIndex>(x =>
                x.ContentType == contentType &&
                x.ContentField == contentField)
            .ListAsync();
    }

    public async Task<IEnumerable<ContentItem>> GetExpensiveProducts(decimal minPrice)
    {
        return await _session
            .Query<ContentItem, NumericFieldIndex>(x =>
                x.ContentType == "Product" &&
                x.ContentField == "Price" &&
                x.Numeric > minPrice)
            .ListAsync();
    }

    public async Task<IEnumerable<ContentItem>> GetRecentArticles(DateTime since)
    {
        return await _session
            .Query<ContentItem, ContentItemIndex>(x =>
                x.ContentType == "Article" &&
                x.Published &&
                x.CreatedUtc >= since)
            .ListAsync();
    }
}
```

### Querying from Razor

```html
@using OrchardCore.ContentManagement
@using OrchardCore.ContentFields.Indexing.SQL
@using YesSql
@inject ISession Session

@{
    var contentItems = await Session
        .Query<ContentItem, TextFieldIndex>(x =>
            x.ContentType == "Acme" &&
            x.ContentField == "Country")
        .ListAsync();
}

<ul>
    @foreach (var item in contentItems)
    {
        <li>@item.DisplayText</li>
    }
</ul>
```

### Querying from Liquid

First, create a SQL Query in the Orchard Core admin (Queries section). Name it `AllCountries` and do **not** select "Return Documents":

```sql
SELECT * FROM TextFieldIndex
WHERE ContentType = 'Acme' AND ContentField = 'Country'
```

Then use the query in a Liquid template:

```liquid
{% assign allCountries = Queries.AllCountries | query %}
{% for country in allCountries %}
  {{ country.Text }}
{% endfor %}
```

### GraphQL Usage

Enable `OrchardCore.ContentFields.Indexing.SQL` to build filters based on dynamic content fields in GraphQL queries.

**Standard usage** — filtering products by a `NumericField` named `Amount` on `PricePart`:

```graphql
product(where: {price: {amount_gt: 10}}) {
    contentItemId
    displayText
    price {
        amount
    }
}
```

**Collapsed part** — when `PricePart` is collapsed:

```graphql
product(where: {amount_gt: 10}) {
    contentItemId
    displayText
    amount
}
```

**Collapsed with field name collision prevention:**

```graphql
product(where: {priceAmount_gt: 10}) {
    contentItemId
    displayText
    priceAmount
}
```

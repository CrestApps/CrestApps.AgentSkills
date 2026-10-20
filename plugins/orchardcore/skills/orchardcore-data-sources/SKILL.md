---
name: orchardcore-data-sources
description: Skill for the Orchard Core Data Pipelines Core feature (OrchardCore.DataPipelines.Core), the shared data layer of data pipelines and reports. Covers the built-in Contents, Users and Queries data sources, ContentDataSourceOptions, data sets, schemas and field types, filter operators, the CSV, Excel, JSON and JSON Lines file formats, IDataFileFormat, IDataWorkbookFormat, the formula language, ExpressionCompiler, AddExpressionFunction, IExpressionFunctionProvider, IExpressionFunctionCatalog, IDataTimeZoneResolver, the DataSorter, DataDistinct, DataAggregator and DataJoin data operations, custom data sources with AddDataSource, RecordDataSource, RecordDataSet, DataConditions, IDataAggregateSource, OnSucceeded, AddDataFileFormat, and shared links with ISharedLinkHandler and ISharedLinkManager. Use this skill when requests mention Orchard Core data sources, exposing module data to data pipelines or reports, formula functions, tabular exports, or closely related Orchard Core implementation or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Data Sources (Data Pipelines Core)

The **Data Pipelines Core** feature (`OrchardCore.DataPipelines.Core`, also `DataPipelinesConstants.CoreFeatureId`) ships with Orchard Core 4.0 in the `OrchardCore.DataPipelines` module. It is the data layer shared by data pipelines (see `orchardcore-data-pipelines`) and other features such as reports:

- **data sources** that expose site data as typed, tabular **data sets**, read in batches with the access of a user;
- **file formats**: CSV, Excel, JSON and JSON Lines;
- a **formula language** whose functions modules can add or replace;
- bounded-memory **data operations** (sort, distinct, group, join) that spill to temporary files;
- **shared links**: expiring, revocable, token-secured links to a resource, listed under **Tools > Shared Links**.

The feature is `EnabledByDependencyOnly`: it has no admin screen of its own and is enabled by the features that depend on it, such as `OrchardCore.DataPipelines`.

## Packages

Install the module package in the web/startup project. Module projects that extend data sources reference the abstractions and core packages:

```xml
<!-- Web/startup project -->
<PackageReference Include="OrchardCore.DataPipelines" Version="4.*" />

<!-- Module project -->
<PackageReference Include="OrchardCore.DataSources.Abstractions" Version="4.*" />
<PackageReference Include="OrchardCore.DataSources.Core" Version="4.*" />
<!-- Only for shared links, IDataTimeZoneResolver or step types -->
<PackageReference Include="OrchardCore.DataPipelines.Abstractions" Version="4.*" />
<PackageReference Include="OrchardCore.DataPipelines.Core" Version="4.*" />
```

A module that reads or adds data sources depends on the feature in its manifest (enabling the module then enables the feature), or registers services in a `[RequireFeatures("OrchardCore.DataPipelines.Core")]` startup (registered only once another feature enabled it).

```csharp
[assembly: Module(
    Name = "Support Reports",
    Version = "1.0.0",
    Dependencies = ["OrchardCore.DataPipelines.Core"]
)]
```

## Concepts

| Concept | Description |
|---------|-------------|
| Data source | An `IDataSource` scoped service with a stable static `Name` (e.g. `Contents`). |
| Data set | One table of a data source (`DataSetDescriptor`: `Name`, `DisplayName`, `Description`, `Group`, `References`). |
| Schema | Ordered `DataField`s (`Name`, may contain dots such as `TitlePart.Title`, `DisplayName`, `Type`, `Group`, `IsIdentifier`, `IsKeyFilterable`, `References`). |
| Field type | `DataFieldType`: `Text` (`string`), `Integer` (`long`), `Decimal` (`decimal`), `Boolean` (`bool`), `Date` and `DateTime` (`DateTime`). Missing value is `null`. |
| Batch | `DataBatch` of rows (`object[]` in field order), 500 rows by default (`DataSourceQuery.DefaultBatchSize`). |

Data is always read for `DataSourceContext.User`. A data source lists only readable data sets, returns `null` from `GetSchemaAsync` for an unreadable one (the security boundary) and checks again in `ReadAsync`. In a pipeline preview the user is the designer; in a background run it is the user who published the pipeline.

## Built-in data sources

| Name | Requires | Data sets |
|------|----------|-----------|
| `Contents` | `OrchardCore.Contents` | One per content type, grouped by stereotype, visible with `ViewContent`. Users with `EditContent` read the latest version (drafts), others the published version. |
| `Users` | `OrchardCore.Users` | `Users` (with `Roles` as comma-separated text) and `UserRoles`, only for `ViewUsers`. Passwords and security stamps are never exposed. |
| `Queries` | `OrchardCore.Queries` | One per saved query, grouped by source (`Sql`, `Lucene`, ...), visible with `ExecuteApi_{QueryName}` (implied by `ExecuteApiAll`, `ManageQueries`). Parameters via `DataSourceQuery.Parameters`. A query runs as a whole, then is split into batches, so limit its results in the query itself. |

Every content type data set has `ContentItemId`, `ContentItemVersionId`, `ContentType`, `DisplayText`, `Owner` (references `Users.UserId`), `Author`, `Published`, `Latest`, `CreatedUtc`, `ModifiedUtc`, `PublishedUtc`, then part and field values:

- part value: `{PartName}.{Property}`, e.g. `TitlePart.Title` (nested objects up to three levels: `{PartName}.{Object}.{Property}`);
- single-value field: `{PartName}.{FieldName}`, e.g. `Product.Price`;
- multi-value field: `{PartName}.{FieldName}.{Property}`, e.g. `Product.Website.Url`;
- lists (content picker ids, media paths, ...) are read as comma-separated text.

Values come from `ContentDataSourceOptions` first, then the public properties of part and field classes registered in code, then the fields added to part definitions in the admin. Secrets (`Password`, `Secret`, `ApiKey`, `ConnectionString`, names ending with `Token`), `[JsonIgnore]` properties, dictionaries and raw JSON are skipped.

### Describing more parts and fields

`ContentDataSourceOptions` (namespace `OrchardCore.DataSources.Contents`, defined in the `OrchardCore.DataPipelines` module package) maps part and field type names to `ContentDataValue[]`. Described values are also writable by the **Save content items** pipeline step unless `IsReadOnly`.

```csharp
using OrchardCore.DataSources;
using OrchardCore.DataSources.Contents;

services.Configure<ContentDataSourceOptions>(options =>
{
    options.Fields["ColorField"] = [new("Value", DataFieldType.Text)];
    options.Fields["TagsField"] = [new("Tags", DataFieldType.Text) { IsList = true }];

    // Nested JSON paths are allowed.
    options.Parts["RatingPart"] =
    [
        new("Score", DataFieldType.Decimal),
        new("Votes.Count", DataFieldType.Integer) { DisplayName = "Votes", IsReadOnly = true },
    ];
});
```

Built-in descriptions include `TitlePart.Title`, `AutoroutePart.Path`, `AliasPart.Alias`, `HtmlBodyPart.Html`, `MarkdownBodyPart.Markdown`, `ContainedPart`, `LocalizationPart`, `PublishLaterPart`, `ArchiveLaterPart` and the common fields (`TextField.Text`, `NumericField.Value`, `LinkField.Url/Text`, `ContentPickerField.ContentItemIds`, `MediaField.Paths`, ...). `ContentDataValueDiscovery.Describe(type)` lists how a class is read.

## Reading a data source from code

```csharp
using System.Security.Claims;
using OrchardCore.DataSources;

public sealed class OrdersExporter
{
    private readonly IDataSourceManager _dataSourceManager;

    public OrdersExporter(IDataSourceManager dataSourceManager)
    {
        _dataSourceManager = dataSourceManager;
    }

    public async Task ExportAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var dataSource = _dataSourceManager.GetDataSource("Contents");
        var context = new DataSourceContext { User = user };

        // Null when the type doesn't exist or the user may not read it.
        var schema = await dataSource.GetSchemaAsync("Order", context, cancellationToken);
        if (schema is null)
        {
            return;
        }

        var query = new DataSourceQuery
        {
            DataSet = "Order",
            Fields = new HashSet<string> { "ContentItemId", "DisplayText", "ModifiedUtc" },
            Conditions =
            [
                new DataCondition
                {
                    Field = "ModifiedUtc",
                    Operator = DataFilterOperator.GreaterThanOrEqual,
                    Values = [DateTime.UtcNow.AddDays(-30)],
                },
            ],
            Context = context,
        };

        await foreach (var batch in dataSource.ReadAsync(query, cancellationToken))
        {
            foreach (var row in batch.Rows)
            {
                // row[i] matches batch.Fields[i].
            }
        }
    }
}
```

Applying conditions is optional for a data source; consumers re-apply every condition to the rows. `dataSource.ReadRowsAsync(query, maxRows)` reads the first rows into memory (previews), and `DataValues.Coerce(value, fieldType)` converts a raw value to a field type's CLR type.

### Filter operators

`DataFilterOperator`: `Equals`, `NotEquals`, `In`, `NotIn`, `IsEmpty`, `IsNotEmpty` (every type); `Contains`, `NotContains`, `StartsWith`, `EndsWith` (text, ignore case); `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Between` (numbers and dates; empty bound is open); `InLastDays`, `InNextDays` (dates, counting today). `DataFilterOperatorExtensions.AppliesTo(type)` tells whether an operator applies to a field type.

## File formats

`IDataFileFormatManager` lists `DataFileFormatDescriptor`s without creating formats, finds one by extension (`GetDescriptorByFileName`) and returns it with `GetFormat(name)`.

| Name | Extension | Notes |
|------|-----------|-------|
| `csv` | `.csv` | RFC 4180 quoting, UTF-8 with BOM, comma delimiter by default (`tab` or `\t` for tab). Every column read is text. |
| `xlsx` | `.xlsx` | Streamed (about 16 MB of memory whatever the row count), one data set per worksheet via `IDataWorkbookFormat`, max 1,048,576 rows per sheet, types preserved. |
| `json` | `.json` | Array of objects; fields inferred from the first batch. |
| `jsonl` | `.jsonl` | One object per line, for large exports. |

`DataFileOptions`: `Delimiter`, `HasHeaderRow`, `SheetName`, `Indented`, `UseDisplayNames`, `BatchSize`.

```csharp
using OrchardCore.DataSources.Files;

if (dataFileFormatManager.GetFormat("xlsx") is IDataWorkbookFormat workbookFormat)
{
    IReadOnlyList<long> rowCounts = await workbookFormat.WriteSheetsAsync(
        stream,
        [
            new DataSheet("Orders", orderFields, orderBatches),
            new DataSheet("Customers", customerFields, customerBatches),
        ],
        new DataFileOptions { UseDisplayNames = true });
}
```

Sheets are written in order, each read completely before the next starts; sheet names are sanitized for Excel (invalid characters replaced, 31 characters max, duplicates numbered).

## Formulas

Formulas compute a value from a row, e.g. `[Price] * [Quantity]`, `UPPER(TRIM([Country]))`, `[CreatedUtc] >= '2025-01-01'`.

- Fields in brackets (`[TitlePart.Title]`, escape `]` as `]]`), text in single or double quotes (double a quote to escape it), numbers with a dot, `TRUE`, `FALSE`, `NULL`.
- Operators by precedence: `OR`/`||`, `AND`/`&&`, `NOT`/`!`, comparisons (`=`, `==`, `!=`, `<>`, `<`, `<=`, `>`, `>=`), `&` (text join), `+ -`, `* / %`, unary `-`.
- Missing values propagate in arithmetic; division by zero gives a missing value; date plus number adds days; functions return missing rather than failing on bad arguments.
- Function categories: Text (`UPPER`, `LOWER`, `TRIM`, `LEFT`, `MID`, `REPLACE`, `CONCAT`, `SPLIT`, `TEXT`, ...), Number (`ROUND`, `FLOOR`, `POWER`, `NUMBER`, `INTEGER`, `LEAST`, `GREATEST`, ...), Date (`NOW`, `TODAY`, `YEAR`, `WEEK`, `DATEADD`, `DATEDIFF`, `DATETRUNC`, ...), Logical (`IF`, `IFS`, `SWITCH`, `COALESCE`, `IFNULL`, `ISBLANK`, `IN`, ...), Aggregate (`SUM`, `AVG`, `MEDIAN`, `MIN`, `MAX`, `COUNTD`, `COUNT`).
- `NOW()`/`TODAY()` use the site time zone; in a pipeline run they return the run start time. Pipeline step formulas are per row and cannot use aggregate functions.

Compile and evaluate from code with `ExpressionCompiler` and the `IExpressionFunctionCatalog` singleton:

```csharp
using OrchardCore.DataSources.Expressions;

var scope = new FieldListExpressionScope(batch.Fields);

// Throws ExpressionException (localized message) when invalid.
var expression = ExpressionCompiler.Compile("[Price] * [Quantity]", scope, _functionCatalog, S);

foreach (var row in batch.Rows)
{
    var total = expression.Evaluate(new ExpressionContext { Row = row, Now = now });
}
```

`CompiledExpression.DataType`, `References` and `Aggregates` (aggregate calls, computable with `DataAggregator` as rows arrive, then evaluated with `ExpressionContext.AggregateValues`). `IExpressionFunctionCatalog.Functions` lists every function with signature and description; `Find(name)` is case-insensitive.

### Adding formula functions

```csharp
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.Modules;

[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class FormulasStartup : StartupBase
{
    private const decimal TaxRate = 0.15m;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddExpressionFunction(ExpressionFunction.CreateScalar(
            name: "TAX",
            category: ExpressionFunctions.NumberCategory,
            signature: "TAX(amount)",
            description: "Returns the sales tax of an amount.",
            minArguments: 1,
            maxArguments: 1,
            returnType: _ => DataFieldType.Decimal,
            invoke: args => args[0] switch
            {
                long amount => Math.Round(amount * TaxRate, 2),
                decimal amount => Math.Round(amount * TaxRate, 2),
                _ => null,
            }));
    }
}
```

- `ExpressionFunction.CreateAggregate(...)` creates group functions (pass a `DataAggregate` when it computes one, so it can be computed as rows arrive).
- A registered function replaces a built-in of the same name, regardless of startup order; the description stays in English and is localized for display.
- For several functions, implement `IExpressionFunctionProvider.GetFunctions()` and register it with `services.AddExpressionFunctionProvider<TProvider>()` (singleton).

## Time zones

`IDataTimeZoneResolver` (namespace `OrchardCore.DataPipelines.Services`) resolves the same `ITimeZone` in a request and in a background run: `GetSiteTimeZoneAsync()`, `GetUserTimeZoneAsync(user)` and `GetTimeZone(timeZoneId)`.

## Data operations

`OrchardCore.DataSources.Operations` (in `OrchardCore.DataSources.Core`) processes any number of rows with bounded memory, spilling to temporary files described by `DataSpillOptions` (32 MB by default, `DataSpillOptions.DefaultMemoryLimit`).

| Operation | Description |
|-----------|-------------|
| `DataSorter.SortAsync` | Stable external merge sort; `DataSorter.CreateComparer` builds the comparer (text ignores case, missing values first). |
| `DataDistinct.DistinctAsync` | Keeps the first row of each key, preserving order. |
| `DataAggregator.AggregateAsync` | Groups by fields and computes `DataMeasure`s, e.g. `new DataMeasure(DataAggregate.Sum, amountIndex)` or `new DataMeasure(DataAggregate.Count, -1)`. |
| `DataJoin.JoinAsync` | `Inner`, `Left`, `Right` or `Full` join (`DataJoinType`); hash join when the right input fits, otherwise sort-merge. |

```csharp
using OrchardCore.DataSources.Operations;

var options = new DataSpillOptions(
    _tempDirectoryProvider.CreateTempSubdirectory("orders"),
    memoryLimit: 64L * 1024 * 1024);

var comparer = DataSorter.CreateComparer([(fields.IndexOf("Amount"), Descending: true)]);

await foreach (var row in DataSorter.SortAsync(rows, comparer, options, cancellationToken))
{
}
```

Helpers: `DataRowBuffer`, `DataRowQueue`, `DataKeys`, `DataFieldTransforms.Apply` (`Year`, `Quarter`, `Month`, `Day`, `Upper`, `Round`, ...). See `orchardcore-temp-file-storage` for `ITempDirectoryProvider`.

## Custom data sources

- Derive from `RecordDataSource` and declare each data set as a `RecordDataSet<TRecord>` over a .NET type, or implement `IDataSource` directly for dynamic data sets or hand-built batches.
- `Name`, `DisplayName` and `Description` are **static** members (`LocalizationSource` labels); `RecordDataSource` is a base class, so the data source also declares `IDataSource`.
- Register with `services.AddDataSource<TDataSource>()` (scoped). Two data sources cannot share a name. Keep data set and field names stable; saved pipelines store them.
- `RecordDataSet<TRecord>.AddField(name, displayName, type, read, group)` returns the `DataField`; set `IsIdentifier`, `IsKeyFilterable` (then `LoadAsync` must honor `DataConditions.Keys` exactly) and `References` (e.g. `new DataFieldReference("Users", "Users", "UserId")`).
- Override `CanReadAsync` to authorize (everyone may by default). `LoadAsync` streams records in a stable order, a page at a time; the base class enforces fields, `MaxRows` and batching, converts enums to names and dates to UTC.
- `DataConditions.Keys(conditions, field)` and `DataConditions.DateRange(conditions, field)` extract the conditions a store can apply.
- Implement `IAggregateRecordDataSet` (or `IDataAggregateSource` on an `IDataSource`) to answer `DataAggregateQuery` with a `GROUP BY`; return `null` when the query cannot be answered exactly.
- For queue-based change feeds, use `query.Context.ConsumerId` and `query.Context.OnSucceeded((services, token) => ...)` to remove entries only after a successful run.

See [references/data-sources-examples.md](references/data-sources-examples.md) for a complete `RecordDataSource`, an `IDataSource` implementation, aggregation, and a custom file format.

### Custom file formats

Implement `IDataFileFormat` (static `Name`, `DisplayName`, `Extension`, `ContentType`; `WriteAsync` and `ReadAsync`) and register it as a singleton with `services.AddDataFileFormat<TFormat>()`. Implement `IDataWorkbookFormat` too for multi-sheet files. The format is then offered by the **Create a file** and **Read a media file** pipeline steps.

## Shared links

Shared links (namespace `OrchardCore.DataPipelines.SharedLinks`) give signed-in recipients access to a resource with a 256-bit token (only a SHA-256 hash is stored), an expiry, an optional maximum number of uses (counted atomically) and revocation. They are emailed to recipients (Email feature) and listed under **Tools > Shared Links** (`ManageSharedLinks` permission). A link opens at `/SharedLinks/{linkId}?token=...`.

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DataPipelines.SharedLinks;
using OrchardCore.Localization;

public sealed class ReportLinkHandler : ISharedLinkHandler
{
    public static string Kind => "Reports.Report";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ReportLinkHandler>("Report");

    // Called once the user is signed in, a recipient, and has the token.
    public Task<IActionResult> OpenAsync(SharedLink link, HttpContext httpContext)
        => Task.FromResult<IActionResult>(new ViewResult
        {
            ViewName = "SharedReport",
            ViewData = { ["ReportId"] = link.ResourceId },
        });
}
```

```csharp
services.AddSharedLinkHandler<ReportLinkHandler>();
```

```csharp
var result = await _sharedLinkManager.CreateAsync(new SharedLinkRequest
{
    Kind = ReportLinkHandler.Kind,
    ResourceId = report.Id,
    Title = report.Name,
    SourceName = report.Name,
    Recipients = ["alice", "bob@example.com"],
    Lifetime = TimeSpan.FromDays(30),
    MaxUses = 10,
});

// List (and clean up) the links of a resource, most recent first.
foreach (var link in await _sharedLinkManager.ListAsync(ReportLinkHandler.Kind, report.Id))
{
    await _sharedLinkManager.DeleteAsync(link);
}
```

- With `NotifyByEmail = false`, deliver `result.Url` (it holds the token) yourself.
- Optional handler members: `GetSourceUrl(link, urlHelper)` (admin page shown in the shared links list) and `DeletedAsync(link)`.
- `ISharedLinkManager` also has `GetAsync`, `UseAsync`, `RevokeAsync`, `DeleteAsync` and a paged `ListAsync(search, skip, take)`.

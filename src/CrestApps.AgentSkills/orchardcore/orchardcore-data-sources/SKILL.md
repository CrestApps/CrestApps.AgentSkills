---
name: orchardcore-data-sources
description: Skill for the data layer of the Orchard Core Data Pipelines Core feature (OrchardCore.DataPipelines.Core). Covers data sources, data sets, schemas and field types, reading with the access of a user, the built-in Contents, Users and Queries data sources, ContentDataSourceOptions for reading more parts and fields, reading from code with IDataSourceManager, the CSV, Excel, JSON and JSON Lines file formats and IDataWorkbookFormat, the DataSorter, DataDistinct, DataAggregator and DataJoin data operations, IDataTimeZoneResolver, custom data sources with RecordDataSource, RecordDataSet, IDataSource and AddDataSource, confirming what was read with ConsumerId and OnSucceeded, aggregating in the data source with IDataAggregateSource and IAggregateRecordDataSet, and custom file formats with AddDataFileFormat. Use this skill for Orchard Core data sources, exposing module data to data pipelines or reports, a custom data source or file format, or change feeds.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Data Sources (Data Pipelines Core)

The **Data Pipelines Core** feature (`OrchardCore.DataPipelines.Core`, also `DataPipelinesConstants.CoreFeatureId`) ships with Orchard Core 4.0 in the Data Pipelines module. It is the data layer that data pipelines and other features, such as reports, share:

- **data sources** that expose site data as typed, tabular **data sets**, read in batches with the access of a user;
- **file formats** rows are written to and read from: CSV, Excel, JSON and JSON Lines;
- the **formula language** and filter operators (see `orchardcore-data-formulas`);
- bounded-memory **data operations** (sort, distinct, group, join) that spill to temporary files;
- **shared links** (see `orchardcore-data-pipeline-steps`).

The feature has no admin screen and is `EnabledByDependencyOnly`: features such as `OrchardCore.DataPipelines` enable it. Using pipelines: `orchardcore-data-pipelines`.

## Packages and dependencies

```xml
<!-- Web/startup project -->
<PackageReference Include="OrchardCore.DataPipelines" Version="4.*" />

<!-- Module project that reads or adds data sources -->
<PackageReference Include="OrchardCore.DataSources.Abstractions" Version="4.*" />
<PackageReference Include="OrchardCore.DataSources.Core" Version="4.*" />
```

`ContentDataSourceOptions` lives in the `OrchardCore.DataPipelines` package; `IDataTimeZoneResolver` in `OrchardCore.DataPipelines.Core`.

A module that reads or adds data sources either depends on the feature in its manifest (enabling the module enables the feature), or registers its services in a `[RequireFeatures("OrchardCore.DataPipelines.Core")]` startup (registered once another feature, such as Data Pipelines, enabled it):

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
| Data source | An `IDataSource` scoped service with a stable static `Name` (such as `Contents`). |
| Data set | One table of a data source. `DataSetDescriptor`: `Name` (stable), `DisplayName`, `Description`, `Group`, `DefaultDateField` (its main date field, if any), `References` (data sets its fields link to). |
| Schema | `DataSetSchema`: the descriptor and its ordered `DataField`s (`Name`, may contain dots such as `TitlePart.Title`; `DisplayName`, `Type`, `Group`, `Description`, `IsIdentifier`, `IsKeyFilterable`, `References`). `FindField(name)`. |
| Field type | `DataFieldType`: `Text` (`string`), `Integer` (`long`), `Decimal` (`decimal`), `Boolean` (`bool`), `Date` (a calendar date, `DateTime`) and `DateTime` (UTC, or a local time without zone after a conversion or from a file). A missing value is `null`. |
| Batch | `DataBatch` (`Fields`, `Rows` of `object[]` in field order, `Count`, `IndexOf(name)`), 500 rows by default (`DataSourceQuery.DefaultBatchSize`). |

A data source can tell which fields identify a record (`IsIdentifier`) and which data sets a field refers to (`new DataFieldReference(source, dataSet, field)`, such as the `Owner` of a content item referring to `Users.UserId`), so consumers suggest related data sets and joins.

### Reading with the access of a user

Data is always read for `DataSourceContext.User`. A data source lists only the data sets this user may read, returns **no schema** (`null`) for a data set the user may not read (the security boundary), and checks the user's access again when it reads rows. While a pipeline is designed or previewed, data is read for the designer; when a published pipeline runs in the background, for the user who published it.

## Built-in data sources

Each is part of Data Pipelines Core and available when the feature owning its data is enabled.

| Name | Requires | Data sets and access |
|------|----------|----------------------|
| `Contents` | `OrchardCore.Contents` | One per content type, grouped by stereotype, for users with `ViewContent` on the type. With `EditContent` the latest version (drafts included) is read, otherwise the published version. |
| `Users` | `OrchardCore.Users` | `Users` (`UserId`, `UserName`, `Email`, `EmailConfirmed`, `PhoneNumber`, `PhoneNumberConfirmed`, `IsEnabled`, `TwoFactorEnabled`, `IsLockoutEnabled`, `LockoutEndUtc`, `AccessFailedCount`, `Roles` as comma-separated text) and `UserRoles` (`UserId`, `UserName`, `RoleName`), for users with `ViewUsers`. Passwords, security stamps and other secrets are never exposed. |
| `Queries` | `OrchardCore.Queries` | One per saved query, grouped by source (`Sql`, `Lucene`, ...), for users with `ExecuteApi_{QueryName}` (implied by `ExecuteApiAll` and `ManageQueries`). Parameters via `DataSourceQuery.Parameters` (the **Parameters** of the pipeline step). |

- A query returning content items has the common content fields below, without part and field values; other queries' fields are inferred from a sample executed without parameters.
- A query runs as a whole, then its results are split into batches: limit the results in the query itself.

### Content items

Every content type data set has `ContentItemId`, `ContentItemVersionId`, `ContentType`, `DisplayText`, `Owner` (refers to `Users.UserId`), `Author`, `Published`, `Latest`, `CreatedUtc`, `ModifiedUtc`, `PublishedUtc`, then, part by part:

- a part value: `{PartName}.{Property}` (such as `TitlePart.Title`), objects held by the part as `{PartName}.{Object}.{Property}`;
- a single-value content field: `{PartName}.{FieldName}` (such as `Product.Price`);
- a multi-value field: `{PartName}.{FieldName}.{Property}` (such as `Product.Website.Url`, `Product.Website.Text`);
- lists (content picker ids, media paths, ...) as one comma-separated text.

Values come from, in order: the values described in `ContentDataSourceOptions` (with their described types); the other public properties of part and field classes registered with `AddContentPart<TPart>()` / `AddContentField<TField>()` (numbers, booleans, dates, text, enums, lists of them; nested objects up to three levels; a content field declared as a part property, such as the images of `SeoMetaPart`, is read like a definition field); and the content fields added to part definitions in the admin. Lists of objects, dictionaries, raw JSON, `[JsonIgnore]` properties, and properties named like secrets (`Password`, `Secret`, `ApiKey`, `ConnectionString`, or ending with `Token`) are skipped. A field type described in the options is read only as described.

The data source applies some conditions in the database (content item id and owner lists, `CreatedUtc`, `ModifiedUtc` and `PublishedUtc` ranges), reads in pages, and releases each page from the session before the next.

#### Reading more parts and fields

Values described by default:

| Part or field | Values |
|---------------|--------|
| `TitlePart` / `AutoroutePart` / `AliasPart` | `Title` / `Path` / `Alias` |
| `HtmlBodyPart` / `MarkdownBodyPart` | `Html` / `Markdown` |
| `ContainedPart` | `ListContentItemId`, `Order` |
| `LocalizationPart` | `Culture`, `LocalizationSet` |
| `PublishLaterPart` / `ArchiveLaterPart` | `ScheduledPublishUtc` / `ScheduledArchiveUtc` |
| `TextField`, `HtmlField`, `MarkdownField`, `YoutubeField` | `Text`, `Html`, `Markdown`, `RawAddress` |
| `NumericField` / `BooleanField` / `DateField` / `DateTimeField` / `TimeField` | `Value` (decimal / boolean / date / date and time / text) |
| `LinkField` | `Url`, `Text` |
| `ContentPickerField`, `UserPickerField`, `TaxonomyField`, `MediaField`, `MultiTextField`, `LocalizationSetContentPickerField` | `ContentItemIds`, `UserIds`, `TermContentItemIds`, `Paths`, `Values`, `LocalizationSets` (lists) |

Describe other values, or read one differently (another type, a list, a part without a class), with `ContentDataSourceOptions` (namespace `OrchardCore.DataSources.Contents`). Each `ContentDataValue(property, type)` reads the JSON property of that name or a nested path such as `Settings.Mode`, with `IsList`, `IsReadOnly` (not writable by **Save content items**) and `DisplayName`:

```csharp
using OrchardCore.DataSources;
using OrchardCore.DataSources.Contents;

services.Configure<ContentDataSourceOptions>(options =>
{
    // One value: the field {PartName}.{FieldName}.
    options.Fields["ColorField"] = [new("Value", DataFieldType.Text)];

    // Several values: one {PartName}.{FieldName}.{Property} field each.
    options.Fields["AddressField"] =
    [
        new("Street", DataFieldType.Text),
        new("City", DataFieldType.Text),
    ];

    // A list, read as comma-separated text.
    options.Fields["TagsField"] = [new("Tags", DataFieldType.Text) { IsList = true }];

    // Part values: {PartName}.{Property}.
    options.Parts["RatingPart"] =
    [
        new("Score", DataFieldType.Decimal),
        new("Votes.Count", DataFieldType.Integer) { DisplayName = "Votes", IsReadOnly = true },
    ];
});
```

`ContentDataValueDiscovery.Describe(type)` describes how a class is read, for modules that list the values of a part or field type.

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
        // Resolved from the services of the scope; null when no data source has that name.
        var dataSource = _dataSourceManager.GetDataSource("Contents");
        var context = new DataSourceContext { User = user };

        // Null when the data set doesn't exist or the user may not read it.
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

- A data source **may ignore** conditions it can't translate; a consumer applies every condition again to the rows it reads. Conditions, operators and how pipelines pass them down: `orchardcore-data-formulas`.
- `DataSourceQuery`: `DataSet`, `Fields` (empty = all), `Conditions`, `MaxRows` (0 = all), `BatchSize`, `Parameters`, `Context` (`User`, `ConsumerId`, `Properties`, `OnSucceeded`).
- `IDataSourceManager.GetDataSources()` / `GetDescriptor(name)` list `DataSourceDescriptor`s without creating data sources.
- `dataSource.ReadRowsAsync(query, maxRows)` reads the first rows into a `DataRowSet` (`Fields`, `Rows`, `Truncated`), such as for a preview. `DataValues.Coerce(value, fieldType)` converts a raw value to a field type's CLR type.

## File formats

`IDataFileFormat` services, listed by `IDataFileFormatManager` (`GetFormats()`, `GetDescriptor(name)`, `GetDescriptorByFileName(fileName)`, `GetFormat(name)`). Name, label, extension and media type are static, so formats are listed with their `DataFileFormatDescriptor` without being created.

| Name | Extension | Notes |
|------|-----------|-------|
| `csv` | `.csv` | RFC 4180 quoting, UTF-8 with BOM, comma by default (`tab` or `\t` for tab). Every column read is text. |
| `xlsx` | `.xlsx` | Streamed one row at a time (about 16 MB whatever the row count). Several data sets via `IDataWorkbookFormat`. At most 1,048,576 rows per sheet including the header (writing more fails). Numbers, booleans, dates keep their types; read types are inferred from the first batch. Sheet `Sheet1` unless named; reads the first sheet unless named. |
| `json` | `.json` | An array of objects. Fields and types inferred from the first batch; later new properties are ignored; nested objects and arrays read as JSON text. |
| `jsonl` | `.jsonl` | One object per line, for large exports. Inferred as JSON. |

Text output writes dates as `yyyy-MM-dd`, date-times in ISO 8601 (ending with `Z` when UTC), numbers without grouping, booleans as `true`/`false`. `DataFileOptions`: `Delimiter`, `HasHeaderRow`, `SheetName`, `Indented`, `UseDisplayNames`, `BatchSize`.

`IDataWorkbookFormat.WriteSheetsAsync` (namespace `OrchardCore.DataSources.Files`) writes several `DataSheet`s (name, fields, batches) to one workbook and returns the row count of each; `SheetName` is ignored. Sheets are written in order, each read completely before the next starts, so later sheets' batches must be able to wait. Names are made valid (`: \ / ? * [ ]` become `_`, 31 characters max, empty becomes `SheetN`, duplicates get ` (2)`).

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

## Data operations

The static classes of `OrchardCore.DataSources.Operations` (package `OrchardCore.DataSources.Core`) process any number of rows with bounded memory: rows stay in memory up to a limit and spill to temporary files deleted when done. `DataSpillOptions(temporaryDirectory, memoryLimit)` sets the folder (created when needed) and limit (32 MB, `DataSpillOptions.DefaultMemoryLimit`). Inputs and outputs are `IAsyncEnumerable<object[]>`.

| Operation | Description |
|-----------|-------------|
| `DataSorter.SortAsync(rows, comparer, options)` | Stable external merge sort. `DataSorter.CreateComparer([(index, Descending: true)])`: numbers by value, text ignoring case, missing values first. |
| `DataDistinct.DistinctAsync` | First row of each key, in order. |
| `DataAggregator.AggregateAsync(rows, groupIndexes, measures, options)` | Groups and computes `DataMeasure(DataAggregate function, int fieldIndex)` per group, such as `new DataMeasure(DataAggregate.Sum, amountIndex)` or `new DataMeasure(DataAggregate.Count, -1)` (rows). Groups in first-seen order; no group fields = one group (even for an empty input). Values come from `DataAggregations.Compute`, including over `DataPartialAggregate`s a data source computed per group. Medians and distinct counts stay in memory. |
| `DataJoin.JoinAsync` | `DataJoinType` `Inner`, `Left`, `Right`, `Full`. Right input read first: a hash join when it fits, otherwise sort-merge in temporary files; same output order either way. A key with a missing value never matches. |

Grouping, deduplicating and joining build keys with `DataKeys`: numbers by value (`2` equals `2.0`), text exactly unless ignoring case, two missing values equal.

```csharp
using OrchardCore.DataSources.Operations;

// _tempDirectoryProvider is the ITempDirectoryProvider (see orchardcore-temp-file-storage).
var options = new DataSpillOptions(_tempDirectoryProvider.CreateTempSubdirectory("orders"), memoryLimit: 64L * 1024 * 1024);

var comparer = DataSorter.CreateComparer([(fields.IndexOf("Amount"), Descending: true)]);

await foreach (var row in DataSorter.SortAsync(rows, comparer, options, cancellationToken))
{
    // The first row is available once every row was read.
}
```

Helpers: `DataRowBuffer` (re-readable rows that spill to one file), `DataRowQueue` (FIFO of batches in a temporary file, for a producer that must not wait for a slower consumer), `DataFieldTransforms.Apply` with a `DataFieldTransform` (`Year`, `Quarter`, `Month`, `Day`, `Upper`, `Round`, ...; `Supports` and `GetResultType`). Pipeline steps get spill options from `DataPipelineRowReader.CreateSpillOptions(context)` (see `orchardcore-data-pipeline-steps`).

## Time zones

`IDataTimeZoneResolver` (namespace `OrchardCore.DataPipelines.Services`) resolves the same `ITimeZone` in a request and in a background run: `GetSiteTimeZoneAsync()`, `GetUserTimeZoneAsync(user)`, `GetTimeZone(timeZoneId)`.

## Extending data sources

### Define your own data source

Derive from `RecordDataSource` (package `OrchardCore.DataSources.Abstractions`) and describe each data set as a `RecordDataSet<TRecord>` over a .NET type (a document, an index row):

- `AddField(name, displayName, type, read, group)` declares a field, in order, with the function reading it from a record, and returns the `DataField` to set `IsIdentifier`, `References` or `IsKeyFilterable` (the data set can load only the records of a list of keys; it must then honor `DataConditions.Keys` exactly).
- `CanReadAsync(context)` authorizes the user (everyone by default); the data source lists, describes and reads only readable data sets.
- The data sets fields reference are listed in `DataSetDescriptor.References`, so consumers know related data sets before reading fields.
- `LoadAsync(query, cancellationToken)` streams records in a **stable order**, a page at a time, and stops when the enumeration stops: the base class reads only the requested fields and `MaxRows`, and splits rows into batches. Values are converted to their field type (enums become names, `DateTimeOffset`, local and unspecified dates become UTC).
- `Name`, `DisplayName`, `Description` are static; `RecordDataSource` is a base class, so the data source declares `IDataSource` itself.

```csharp
public sealed class SupportDataSource : RecordDataSource, IDataSource
{
    public SupportDataSource(ITicketStore ticketStore, IAuthorizationService authorizationService, IStringLocalizer<SupportDataSource> localizer)
    {
        DataSets = [new TicketsDataSet(ticketStore, authorizationService, localizer)];
    }

    public static string Name => "Support";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SupportDataSource>("Support");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SupportDataSource>("The tickets of the help desk.");

    protected override IEnumerable<IRecordDataSet> DataSets { get; }
}
```

```csharp
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<SupportDataSource>();
    }
}
```

The full `TicketsDataSet` (fields, references, `CanReadAsync`, `LoadAsync` with `DataConditions`) is in [references/data-sources-examples.md](references/data-sources-examples.md). `DataField.WithName(name, displayName)` copies a field under another name (type, group, description, identifier and references kept), such as when a consumer prefixes joined fields.

### Confirming what was read

A data source reading changes from a queue removes them only once delivered. `DataSourceQuery.Context`:

- `ConsumerId`: a stable id of the reader, `{pipelineId}/{stepId}` for a pipeline step, `null` for a preview. Keep the changes each consumer still has to read.
- `OnSucceeded((services, token) => ...)`: runs once the consumer delivered what was read. A pipeline run runs it only when the run succeeds **and** its step read every row; a preview never runs it.

```csharp
protected override async IAsyncEnumerable<ChangedOrder> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
{
    var changes = await _queue.ListAsync(query.Context.ConsumerId, cancellationToken);

    foreach (var change in changes)
    {
        yield return await LoadOrderAsync(change, cancellationToken);
    }

    var ids = changes.Select(change => change.Id).ToArray();

    // Removed once the run delivered them.
    query.Context.OnSucceeded((services, token) => services.GetRequiredService<OrderChangeQueue>().RemoveAsync(ids, token));
}
```

### Aggregating in the data source

A consumer that only counts, sums or takes min/max (such as a report of orders per month) asks an `IDataAggregateSource` first and reads rows when it answers `null`. `AggregateAsync(DataAggregateQuery)`:

- `Groups`: `DataAggregateGroup` (`Field`, optional `Boundaries` that bucket a date field, such as months; the group value is the bucket index).
- `Measures`: `DataAggregateMeasure` (`Function` `Count`, `Sum`, `Min` or `Max`, and `Field`; `Count` without a field counts rows). Consumers combine these across groups and derive others (an average from a sum and a count).
- `Conditions` must be applied **exactly** (text ignoring case, dates in UTC, `NotEquals`/`NotIn` keep rows without a value), unlike row queries.
- `MaxGroups` (10,000 by default), `DataSet`, `Context`.

Answer one row per group (group values then measure values), only with rows the context user may read. Answer `null` for anything you can't answer exactly: an unsupported field, condition or function, or more than `MaxGroups` groups. `RecordDataSource` implements `IDataAggregateSource` and asks data sets implementing `IAggregateRecordDataSet`, after checking the user may read them; other data sets answer `null`.

### Adding a data source

Implement `IDataSource` directly for data sets listed dynamically or batches built by hand: `GetDataSetsAsync(context)`, `GetSchemaAsync(dataSet, context)` (return `null` for unreadable data sets) and `ReadAsync(query)` (check access again; one value per field in field order with the field type's CLR type; honor `MaxRows` and `BatchSize`). Register with `AddDataSource<T>()`; data sources are scoped, names must be unique, and data set and field names must stay stable (saved pipelines store them). Example in [references/data-sources-examples.md](references/data-sources-examples.md).

### Adding a file format

Implement `IDataFileFormat` (static `Name`, `DisplayName`, `Extension`, `ContentType`; `WriteAsync(stream, fields, batches, options)` returning the row count; `ReadAsync(stream, options)` yielding batches, one empty batch with the fields for a file without rows), and `IDataWorkbookFormat` too for several sheets. Register with `services.AddDataFileFormat<T>()` (singleton). It is then offered by the **Create a file** and **Read a media file** steps.

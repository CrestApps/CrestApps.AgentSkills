# Add your own Report Builder data source

The Report Builder reads Orchard Core 4.0 data sources (`OrchardCore.DataPipelines.Core`), so a source you add serves designed reports and Orchard Core data pipelines alike. A data source answers three questions: which data sets the principal may read, which typed fields each has, and which rows it holds. The engine does the rest: joins, formulas, transforms, filters, grouping, sorting, limits, time zones, viewer filters, visuals, export, sharing and authorization. The contract itself is covered by the `orchardcore-data-sources` skill.

## Choose an approach

| You report on | Build | Packages |
| --- | --- | --- |
| Records your module stores (YesSql documents, catalog entries) | `RecordDataSource` with one `RecordDataSet<T>` per record type | `OrchardCore.DataSources.Abstractions`, `CrestApps.OrchardCore.Reports.Abstractions` |
| The same, grouped in the database | Add `IAggregateRecordDataSet` and `ReportIndexAggregator` | Also `CrestApps.OrchardCore.Reports.Core` |
| An external system whose data sets or fields are found at run time | `IDataSource` | `OrchardCore.DataSources.Abstractions` |
| A content part or field the Content items source reads the wrong way | `ContentDataSourceOptions` | Orchard Core Data Pipelines |
| Data people can prepare in the browser | Nothing, use a reusable view | |

Namespaces: the contract (`IDataSource`, `RecordDataSource`, `RecordDataSet<T>`, `DataField`, `DataFieldType`, `DataCondition`, `DataConditions`, `DataSetDescriptor`, `DataFieldReference`) is in `OrchardCore.DataSources`. The CrestApps helpers are in `CrestApps.OrchardCore.Reports.DataSources` (Abstractions) and `CrestApps.OrchardCore.Reports.Designer.DataSources` (Core). `ReportsConstants` is in `CrestApps.OrchardCore.Reports`.

## Where the code goes

- A module exposing its own records keeps the source in that module, in a startup marked `[RequireFeatures("OrchardCore.DataPipelines.Core")]` (and the module's feature). Reports never depends on it; the source is registered whenever Data Pipelines Core is on, whether through the Report Builder or Orchard Core's Data Pipelines. Omnichannel, Contact Center, AI Chat and Messaging do this.
- A source for an external system goes in its own module whose feature depends on `OrchardCore.DataPipelines.Core`. Put connection settings in configuration (for example `CrestApps:Reports:{Source}`), never in a design.
- Register with `services.AddDataSource<TDataSource>()` (from `OrchardCore.DataSources.Core`). Sources are scoped and resolved by name through `IDataSourceManager` (`GetDataSource(name)`). `IDataSource` declares `Name`, `DisplayName` and `Description` as static abstract members, so never resolve sources with `GetServices<IDataSource>()`. Two sources cannot share a name.

## A record-backed data set

`RecordDataSet<TRecord>` checks access on every call (`CanReadAsync`), reads only the fields and rows asked for, batches rows, and converts values to the field types (enums to names, `DateTimeOffset`, local and unspecified date-times to UTC).

```csharp
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace MyModule.Reports;

public sealed class InvoicesReportDataSet : RecordDataSet<Invoice>
{
    public const string Name = "Invoices";

    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;

    public InvoicesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<InvoicesReportDataSet> S)
        : base(new DataSetDescriptor(Name, S["Invoices"], S["One row per invoice."]) { DefaultDateField = "IssuedUtc" })
    {
        _session = session;
        _authorizationService = authorizationService;

        var invoice = S["Invoice"].Value;
        var people = S["People"].Value;

        AddField("InvoiceId", S["Invoice ID"], DataFieldType.Text, record => record.InvoiceId, invoice).Identifier();
        AddField("Number", S["Number"], DataFieldType.Text, record => record.Number, invoice);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status, invoice);
        AddField("Amount", S["Amount"], DataFieldType.Decimal, record => record.Amount, invoice);
        AddField("IssuedUtc", S["Issued"], DataFieldType.DateTime, record => record.IssuedUtc, invoice);

        // The customer is a content item of the Customer type; the account manager is a user.
        AddField("CustomerId", S["Customer ID"], DataFieldType.Text, record => record.CustomerContentItemId, people)
            .Identifier(new DataFieldReference("Contents", "Customer", "ContentItemId"));
        AddField("AccountManagerId", S["Account manager (user ID)"], DataFieldType.Text, record => record.AccountManagerUserId, people)
            .Identifier(new DataFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));

        // Fields LoadAsync filters exactly when a join sends keys.
        Fields.MarkKeyFilterable(["InvoiceId", "CustomerId"]);
    }

    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, BillingPermissions.ViewInvoiceReports);
    }

    protected override async IAsyncEnumerable<Invoice> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Join keys: null means no key condition; an empty list means nothing can match.
        var invoiceIds = DataConditions.Keys(query.Conditions, "InvoiceId")?.Select(key => key?.ToString()).ToArray();
        var customerIds = DataConditions.Keys(query.Conditions, "CustomerId")?.Select(key => key?.ToString()).ToArray();

        // A viewer filter limiting Status to a list of values, applied in the database without regard to case.
        var columns = new ReportIndexConditions<InvoiceIndex>(query.Conditions)
            .Text("Status", nameof(InvoiceIndex.Status));

        if (invoiceIds is { Length: 0 } || customerIds is { Length: 0 } || columns.MatchesNothing)
        {
            yield break;
        }

        // The period the filters put on the main date, never narrower than the filters.
        var (from, to) = DataConditions.DateRange(query.Conditions, "IssuedUtc");

        IQueryIndex<InvoiceIndex> Filter(IQueryIndex<InvoiceIndex> invoices)
        {
            invoices = columns.Apply(invoices);

            if (invoiceIds is not null)
            {
                invoices = invoices.Where(index => index.InvoiceId.IsIn(invoiceIds));
            }

            if (customerIds is not null)
            {
                invoices = invoices.Where(index => index.CustomerContentItemId.IsIn(customerIds));
            }

            if (from.HasValue)
            {
                var earliest = from.Value;
                invoices = invoices.Where(index => index.IssuedUtc >= earliest);
            }

            if (to.HasValue)
            {
                var latest = to.Value;
                invoices = invoices.Where(index => index.IssuedUtc <= latest);
            }

            return invoices;
        }

        // Newest first, a page at a time; the base class stops at the row limit.
        await foreach (var invoice in ReportIndexPages.ReadAsync<Invoice, InvoiceIndex>(_session, Filter, index => index.IssuedUtc, cancellationToken: cancellationToken))
        {
            yield return invoice;
        }
    }
}
```

The source lists its data sets. It declares `IDataSource` itself, since `RecordDataSource` is a base class:

```csharp
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace MyModule.Reports;

public sealed class BillingReportDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    public BillingReportDataSource(InvoicesReportDataSet invoices)
    {
        _dataSets = [invoices];
    }

    public static string Name => "Billing";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<BillingReportDataSource>("Billing");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<BillingReportDataSource>("Invoices of the billing module.");

    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<InvoicesReportDataSet>()
            .AddDataSource<BillingReportDataSource>();
    }
}
```

`AddField(name, displayName, type, read, group)` returns the `DataField`, so set its `Description` (a hint in the builder) or chain helpers. A source whose data sets depend on several features (like Contact Center) injects them as an interface each feature registers, so a data set whose feature is off is never listed.

### CrestApps helpers

| Helper | Use |
| --- | --- |
| `field.Identifier(params DataFieldReference[] references)` | Sets `IsIdentifier` and adds the data sets the values point to (`References`) |
| `Fields.MarkKeyFilterable(names)` | Sets `IsKeyFilterable` on the named fields |
| `ReportIndexPages.ReadAsync<TRecord, TIndex>(session, filter, date, collection, cancellationToken)` | Reads a map index 500 at a time, newest first by a date column then index row, with keyset paging (no offsets), detaching each page from the session. A nullable date reads undated records last; `ReadPagesAsync` yields a page at a time |
| `ReportIndexConditions<TIndex>` | `.Text(field, column)` (upper-case comparison, non-ASCII left to the engine), `.Enum(field, index => index.Column)`, `MatchesNothing`, `Apply(query)` |
| `ReportIndexAggregator` | Answers an aggregate query with one `GROUP BY` over a map index |

### Index the date you read by

`ReportIndexPages` seeks through the date column and the index row id. Add a database index in the migration that creates the index table, with `IndexTableIndexCreation.CreateIfMissingAsync` (namespace `CrestApps.OrchardCore.YesSql.Core.Migrations`) so the step can run again safely:

```csharp
public async Task<int> UpdateFrom3Async()
{
    await IndexTableIndexCreation.CreateIfMissingAsync<InvoiceIndex>(SchemaBuilder, "IDX_InvoiceIndex_Report", null, "IssuedUtc", "Id");

    return 4;
}
```

Without it the database sorts the whole index table for every page.

## A source for an external system

Implement `IDataSource` when data sets or fields are not known in advance:

```csharp
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace MyModule.Reports;

public sealed class HelpDeskDataSource : IDataSource
{
    private const string TicketsDataSet = "Tickets";

    private readonly IHelpDeskClient _client;
    private readonly IAuthorizationService _authorizationService;
    private readonly IStringLocalizer S;

    public HelpDeskDataSource(
        IHelpDeskClient client,
        IAuthorizationService authorizationService,
        IStringLocalizer<HelpDeskDataSource> stringLocalizer)
    {
        _client = client;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public static string Name => "HelpDesk";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<HelpDeskDataSource>("Help desk");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<HelpDeskDataSource>("Tickets of the help desk.");

    public async Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => await CanReadAsync(context) ? [Describe()] : [];

    public async Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (dataSet != TicketsDataSet || !await CanReadAsync(context))
        {
            return null; // The engine refuses the data set.
        }

        return new DataSetSchema
        {
            DataSet = Describe(),
            Fields = [.. GetFields()],
        };
    }

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Check again: reading is the last line of defense.
        if (query.DataSet != TicketsDataSet || !await CanReadAsync(query.Context))
        {
            yield break;
        }

        // Only the fields the report uses (all when none is named).
        var fields = GetFields()
            .Where(field => query.Fields is null || query.Fields.Count == 0 || query.Fields.Contains(field.Name))
            .ToArray();

        var (from, to) = DataConditions.DateRange(query.Conditions, "CreatedUtc");
        var remaining = query.MaxRows > 0 ? query.MaxRows : int.MaxValue;
        var skip = 0;

        while (remaining > 0)
        {
            var take = Math.Min(Math.Max(1, query.BatchSize), remaining);
            var tickets = await _client.GetTicketsAsync(from, to, skip, take, cancellationToken);

            if (tickets.Count == 0)
            {
                yield break;
            }

            var rows = tickets
                .Select(ticket => fields.Select(field => DataValues.Coerce(Read(ticket, field.Name), field.Type)).ToArray())
                .ToList();

            yield return new DataBatch(fields, rows);

            skip += tickets.Count;
            remaining -= tickets.Count;
        }
    }

    private DataSetDescriptor Describe()
        => new(TicketsDataSet, S["Tickets"], S["One row per help desk ticket."]) { DefaultDateField = "CreatedUtc" };

    private DataField[] GetFields() =>
    [
        new("TicketId", S["Ticket ID"], DataFieldType.Text) { IsIdentifier = true },
        new("Subject", S["Subject"], DataFieldType.Text),
        new("Priority", S["Priority"], DataFieldType.Text),
        new("CreatedUtc", S["Created"], DataFieldType.DateTime),
        new("ResolvedUtc", S["Resolved"], DataFieldType.DateTime),
        new("ReopenCount", S["Times reopened"], DataFieldType.Integer),
    ];

    // Date-times must be UTC; the engine shows them in the tenant time zone.
    private static object Read(HelpDeskTicket ticket, string field) => field switch
    {
        "TicketId" => ticket.Id,
        "Subject" => ticket.Subject,
        "Priority" => ticket.Priority,
        "CreatedUtc" => ticket.CreatedAt.UtcDateTime,
        "ResolvedUtc" => ticket.ResolvedAt?.UtcDateTime,
        "ReopenCount" => ticket.ReopenCount,
        _ => null,
    };

    private async Task<bool> CanReadAsync(DataSourceContext context)
        => context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, HelpDeskPermissions.ViewHelpDeskReports);
}
```

A source that must read data to learn its fields can sample rows and infer types with `DataValues.InferType`. A source that cannot describe a data set may throw `ReportQueryException` (`CrestApps.OrchardCore.Reports.Designer`, in Reports.Core) with a message the builder shows; a source meant for data pipelines too should throw an exception whose message is safe to show.

## The contract

| Member | Rule |
| --- | --- |
| `static Name` | Stable; stored in designs (`ReportDataSetReference.Source`) |
| `static DisplayName`, `static Description` | `LocalizationSource`s shown in the data source picker |
| `GetDataSetsAsync(context, ct)` | Only the data sets `context.User` may read; nothing when `context.User` is `null` |
| `GetSchemaAsync(dataSet, context, ct)` | The fields, or `null` when missing or not allowed; the security boundary |
| `ReadAsync(query, ct)` | Batches of `query.Fields` (all when empty), at most `query.MaxRows`, about `query.BatchSize` per batch; check access again and honor cancellation. The engine asks for one row more than its limit to detect overflow |
| `RecordDataSet<T>.LoadAsync(query, ct)` | Stream records in a stable order, a page at a time; never load everything |
| Data set and field names | Stable; field names may contain dots (`Part.Field.Property`) and are referenced as `alias.FieldName` |
| `DataSetDescriptor.DefaultDateField` | Main date; new reports start filtered on its last 30 days |
| `DataSetDescriptor.Group`, `DataField.Group` | Grouping in the builder lists |
| `DataField.IsIdentifier` | Keys and foreign keys: suggested for joins, never summed by default |
| `DataField.IsKeyFilterable` | The source filters join keys on this field exactly |
| `References` | On a field, the data sets its values point to; on a data set, all those of its fields |
| `DataSourceContext.Properties` | Bag shared by one run (for example recursion guards) |

### Values

Each row is an `object[]` aligned with its `DataBatch` fields. `Text` is `string`, `Integer` is `long`, `Decimal` is `decimal`, `Boolean` is `bool`, `Date` is a date-only `DateTime` (never shifted), `DateTime` is a UTC `DateTime`. Missing is `null`. Normalize with `DataValues.Coerce(value, fieldType)`; `RecordDataSet<T>` does it for you.

### Conditions you may push down

`query.Conditions` holds row filters as `DataCondition`s (field, `DataFilterOperator`, typed values, `DateTime` values in UTC). Relative date filters arrive as a range. Applying a condition is optional and must never drop a row the engine would keep, because the engine re-applies every filter:

- Ignoring every condition is always correct.
- `DataConditions.DateRange(conditions, field)` returns the narrowest inclusive bounds (`null` when open); apply them to an indexed date column.
- Text compares ignoring case in the engine; never push text equality to a case-sensitive collation (`ReportIndexConditions` compares in upper case).
- Push a condition only to a column that holds exactly the field's value.
- Conditions are offered only for data sets no outer join can fill with empty values.

## Join keys

For inner and left joins the engine can send the distinct keys of the rows before the join, so the joined data set reads only records that can match:

1. Mark the field `IsKeyFilterable` (`Fields.MarkKeyFilterable(names)`). Only `Text` and `Integer` fields joined to the same type are narrowed.
2. Read keys with `DataConditions.Keys(query.Conditions, field)`: `null` (no key condition), empty (nothing matches), or the values.
3. Apply them exactly: every record whose value is one of the keys, and no other. A source that cannot must not set the flag.

Keys arrive as an `In` condition with `IsJoinKey`, in batches of `JoinKeyBatchSize`, up to `MaxJoinKeys`; beyond that the joined data set is read in full.

## Relationships

A `DataFieldReference(source, dataSet, field)` says a field's values identify records of another data set. The builder then joins on it automatically, lists the referenced data sets under **Related** in **Add data set**, and suggests the join. `RecordDataSource` copies field references to `DataSetDescriptor.References`; an `IDataSource` written by hand sets them on the descriptors it returns.

- Reference other modules by stable names, such as `new DataFieldReference("ContactCenter", "DialerProfiles", "ItemId")`, users with `ReportsConstants.UsersDataSource`, `UsersDataSet` and `UserIdField`, and a content type with `new DataFieldReference("Contents", "{ContentType}", "ContentItemId")`.
- A reference to a disabled source is ignored.
- Declare references only when known without reading data, for example from definitions.

## Grouping in the data source

Implement `IAggregateRecordDataSet` on a YesSql data set and hand the query to `ReportIndexAggregator`:

```csharp
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using OrchardCore.DataSources;

public sealed class InvoicesReportDataSet : RecordDataSet<Invoice>, IAggregateRecordDataSet
{
    // Report fields whose values the index copies unchanged, mapped to their index column.
    private IReadOnlyDictionary<string, string> _aggregateColumns;

    // ...constructor, fields, CanReadAsync and LoadAsync as above...

    public Task<IReadOnlyList<object[]>> AggregateAsync(DataAggregateQuery query, CancellationToken cancellationToken = default)
    {
        _aggregateColumns ??= ReportIndexAggregator.MapByName<InvoiceIndex>(Fields.Select(field => field.Name));

        return ReportIndexAggregator.AggregateAsync<InvoiceIndex>(_session, null, query, _aggregateColumns, cancellationToken);
    }
}
```

- `MapByName` maps each field to the index column of the same name; pass your own dictionary when names differ, and map only columns that hold exactly the field's value. Pass the index collection as the second argument when records are stored in one.
- Return `null` instead when the data set limits records per principal beyond its permission, since the statement reads the whole index.
- `ReportIndexAggregator` declines (`null`) whatever it cannot answer exactly: an unmapped field, a text comparison other than equality, equality on non-ASCII text, an empty-text test on a text column, or more groups than `MaxGroups`. Enums are compared and grouped by name; date buckets become a `CASE` over UTC boundaries.

To answer `AggregateAsync(DataAggregateQuery, CancellationToken)` yourself: `query.Groups` lists group fields (a group with `Boundaries` is a date bucketed by ascending UTC boundaries; its value is the zero-based bucket index as `long`, out-of-range or empty values fall in a `null` group); `query.Measures` lists `Count` (of a field, or all records when `Field` is `null`), `Sum`, `Min`, `Max`; each row holds group values then measures; apply `query.Conditions` exactly (`NotEquals` and `NotIn` keep empty values, text ignores case); answer only with records `query.Context.User` may read; return `null` for anything inexact or more than `query.MaxGroups` groups.

## Content parts and fields

The Content items source (Orchard Core's) reads parts and fields registered in code with `AddContentPart<TPart>()` and `AddContentField<TField>()` from their public properties (nested up to 3 levels) and fields added in the admin. Lists of ids (picker `ContentItemIds`, `UserIds`) become comma-separated text. Lists of objects, dictionaries, raw JSON, `[JsonIgnore]` properties and names that look like secrets (`Password`, `Secret`, `ApiKey`, `ConnectionString`, `...Token`) are skipped. Describe a part or field type differently with `ContentDataSourceOptions` (namespace `OrchardCore.DataSources.Contents`):

```csharp
using OrchardCore.DataSources;
using OrchardCore.DataSources.Contents;

services.Configure<ContentDataSourceOptions>(options =>
{
    // A field type with several values becomes {PartName}.{FieldName}.{Property}.
    options.Fields["AddressField"] =
    [
        new("City", DataFieldType.Text),
        new("PostalCode", DataFieldType.Text),
        new("CountryCode", DataFieldType.Text),
    ];

    // A part value becomes {PartName}.{Property}; a path reads a nested property.
    options.Parts["RatingPart"] =
    [
        new("Score", DataFieldType.Decimal),
        new("Votes.Count", DataFieldType.Integer) { DisplayName = "Votes", IsReadOnly = true },
    ];
});
```

A described type is read only as described, so its field names stay stable.

## Security rules

- While a saved report runs, `context.User` is the report **owner's** principal; while designing, the designer's; in a data pipeline, the publisher's.
- Never fall back to everything for a `null` or unauthorized principal: no data sets, `null` schema, no rows.
- Check access in `GetDataSetsAsync`, `GetSchemaAsync` and again in `ReadAsync` (`RecordDataSource` does it through `CanReadAsync`).
- Never expose secrets, password hashes, tokens, message bodies, IP addresses or storage locations.

## Checklist

- Stable, unique names for the source, its data sets and fields.
- Hidden from principals that may not read it; `null` principal sees nothing.
- CLR types per field, UTC date-times, `null` for missing.
- Stable order, a page at a time, newest first when there is a main date.
- Pushed-down conditions never drop rows; join keys and aggregates are exact or declined.
- Identifiers marked, references declared where known, main date set.
- Registered with `AddDataSource<T>()` in a startup requiring `OrchardCore.DataPipelines.Core`.

# Data Sources Examples

All examples target Orchard Core 4.0. The module project references `OrchardCore.DataSources.Abstractions` and `OrchardCore.DataSources.Core`; the web/startup project references `OrchardCore.DataPipelines`.

## Complete record data source

A help desk module exposes its tickets as a `Support` data source with one `Tickets` data set. The ticket store, the permission and the record are the module's own types.

```csharp
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace HelpDesk.DataSources;

public enum TicketState
{
    Open,
    Pending,
    Closed,
}

public sealed record Ticket(string TicketId, TicketState State, DateTimeOffset OpenedUtc, int Priority, string OwnerId);

public sealed record TicketStateCount(TicketState State, long Count);

public interface ITicketStore
{
    // Pages of tickets, in a stable order, optionally limited to ids and an opened date range.
    Task<IReadOnlyList<Ticket>> GetTicketsAsync(
        IReadOnlyList<object> ticketIds,
        DateTime? openedFrom,
        DateTime? openedTo,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TicketStateCount>> CountByStateAsync(CancellationToken cancellationToken);
}

public static class SupportPermissions
{
    public static readonly Permission ViewTickets = new(
        "ViewTickets",
        LocalizationSource.Create("View support tickets", typeof(SupportPermissions)));
}

public sealed class SupportDataSource : RecordDataSource, IDataSource
{
    public SupportDataSource(
        ITicketStore ticketStore,
        IAuthorizationService authorizationService,
        IStringLocalizer<SupportDataSource> localizer)
    {
        DataSets = [new TicketsDataSet(ticketStore, authorizationService, localizer)];
    }

    // Static descriptors: the data source is listed without being created.
    public static string Name => "Support";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SupportDataSource>("Support");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SupportDataSource>("The tickets of the help desk.");

    protected override IEnumerable<IRecordDataSet> DataSets { get; }
}

public sealed class TicketsDataSet : RecordDataSet<Ticket>, IAggregateRecordDataSet
{
    private const int PageSize = 500;

    private readonly ITicketStore _ticketStore;
    private readonly IAuthorizationService _authorizationService;

    public TicketsDataSet(ITicketStore ticketStore, IAuthorizationService authorizationService, IStringLocalizer localizer)
        : base(new DataSetDescriptor("Tickets", localizer["Tickets"], localizer["One row per ticket."]))
    {
        _ticketStore = ticketStore;
        _authorizationService = authorizationService;

        // LoadAsync reads only the requested ids when a consumer (such as a join) asks for them.
        var ticketId = AddField("TicketId", localizer["Ticket"], DataFieldType.Text, ticket => ticket.TicketId);
        ticketId.IsIdentifier = true;
        ticketId.IsKeyFilterable = true;

        // Enum values are read as names, DateTimeOffset values as UTC DateTime.
        AddField("State", localizer["State"], DataFieldType.Text, ticket => ticket.State);
        AddField("OpenedUtc", localizer["Opened"], DataFieldType.DateTime, ticket => ticket.OpenedUtc);
        AddField("Priority", localizer["Priority"], DataFieldType.Integer, ticket => ticket.Priority);

        // A reference lets consumers suggest joining the users data set.
        var owner = AddField("OwnerId", localizer["Owner"], DataFieldType.Text, ticket => ticket.OwnerId);
        owner.IsIdentifier = true;
        owner.References.Add(new DataFieldReference("Users", "Users", "UserId"));
    }

    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => context.User is not null && await _authorizationService.AuthorizeAsync(context.User, SupportPermissions.ViewTickets);

    protected override async IAsyncEnumerable<Ticket> LoadAsync(
        DataSourceQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Apply what the store understands; consumers re-apply every condition.
        var ticketIds = DataConditions.Keys(query.Conditions, "TicketId");
        var (openedFrom, openedTo) = DataConditions.DateRange(query.Conditions, "OpenedUtc");
        var skip = 0;

        while (true)
        {
            var tickets = await _ticketStore.GetTicketsAsync(ticketIds, openedFrom, openedTo, skip, PageSize, cancellationToken);

            if (tickets.Count == 0)
            {
                yield break;
            }

            foreach (var ticket in tickets)
            {
                yield return ticket;
            }

            skip += tickets.Count;
        }
    }

    // Optional: answer "count per state" with a GROUP BY instead of streaming every row.
    public async Task<IReadOnlyList<object[]>> AggregateAsync(DataAggregateQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Conditions.Count > 0 ||
            query.Groups is not [{ Field: "State", Boundaries: null }] ||
            query.Measures is not [{ Function: DataAggregate.Count, Field: null }])
        {
            // Null means "read the rows instead".
            return null;
        }

        var counts = await _ticketStore.CountByStateAsync(cancellationToken);

        return counts.Count > query.MaxGroups
            ? null
            : counts.Select(count => new object[] { count.State.ToString(), count.Count }).ToList();
    }
}
```

Register it, together with the module's own services and permission provider, in a startup that requires the feature:

```csharp
using OrchardCore.Modules;

namespace HelpDesk;

[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<SupportDataSource>();
    }
}
```

The `Tickets` data set is now offered by the **Add a data source** dialog of the pipeline designer, can be filtered on its fields, and joined to `Users` on `OwnerId`. Because `TicketId` is `IsKeyFilterable`, a consumer that joins data sets (such as a report) may send the ids it needs as an `In` condition marked `IsJoinKey`; `LoadAsync` must then return exactly the tickets of those ids.

## Implementing `IDataSource` directly

Use this when data sets are discovered at run time or batches are built by hand.

```csharp
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;

public sealed class WarehouseDataSource : IDataSource
{
    private const string StockDataSet = "Stock";

    private readonly IWarehouseService _warehouseService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IStringLocalizer S;

    public WarehouseDataSource(
        IWarehouseService warehouseService,
        IAuthorizationService authorizationService,
        IStringLocalizer<WarehouseDataSource> localizer)
    {
        _warehouseService = warehouseService;
        _authorizationService = authorizationService;
        S = localizer;
    }

    public static string Name => "Warehouse";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<WarehouseDataSource>("Warehouse");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<WarehouseDataSource>("The stock of the warehouse.");

    public async Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => await CanReadAsync(context) ? [Describe()] : [];

    public async Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
    {
        // The schema is the security boundary.
        if (dataSet != StockDataSet || !await CanReadAsync(context))
        {
            return null;
        }

        return new DataSetSchema
        {
            DataSet = Describe(),
            Fields = [.. GetFields()],
        };
    }

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Check access again when reading.
        if (query.DataSet != StockDataSet || !await CanReadAsync(query.Context))
        {
            yield break;
        }

        var fields = GetFields();
        var remaining = query.MaxRows > 0 ? query.MaxRows : int.MaxValue;
        var skip = 0;

        while (remaining > 0)
        {
            var take = Math.Min(Math.Max(1, query.BatchSize), remaining);
            var items = await _warehouseService.GetStockAsync(skip, take, cancellationToken);

            if (items.Count == 0)
            {
                yield break;
            }

            // One value per field, in field order, with the CLR type of the field type.
            var rows = items
                .Select(item => new object[] { item.Sku, item.Name, (long)item.Quantity, item.UnitPrice })
                .ToList();

            yield return new DataBatch(fields, rows);

            skip += items.Count;
            remaining -= items.Count;
        }
    }

    private async Task<bool> CanReadAsync(DataSourceContext context)
        => context?.User is not null && await _authorizationService.AuthorizeAsync(context.User, WarehousePermissions.ViewStock);

    private DataSetDescriptor Describe()
        => new(StockDataSet, S["Stock"], S["One row per product."]);

    private DataField[] GetFields() =>
    [
        new("Sku", S["SKU"], DataFieldType.Text) { IsIdentifier = true },
        new("Name", S["Name"], DataFieldType.Text),
        new("Quantity", S["Quantity"], DataFieldType.Integer),
        new("UnitPrice", S["Unit price"], DataFieldType.Decimal),
    ];
}
```

```csharp
services.AddDataSource<WarehouseDataSource>();
```

## Queue-based change feed confirmed on success

```csharp
protected override async IAsyncEnumerable<ChangedOrder> LoadAsync(
    DataSourceQuery query,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    // ConsumerId is "{pipelineId}/{stepId}" for a pipeline step, null for a preview.
    var changes = await _queue.ListAsync(query.Context.ConsumerId, cancellationToken);

    foreach (var change in changes)
    {
        yield return await LoadOrderAsync(change, cancellationToken);
    }

    var ids = changes.Select(change => change.Id).ToArray();

    // Runs only when the run succeeds and the step read every row; never for previews.
    query.Context.OnSucceeded((services, token) =>
        services.GetRequiredService<OrderChangeQueue>().RemoveAsync(ids, token));
}
```

## Custom file format

```csharp
using OrchardCore.DataSources;
using OrchardCore.DataSources.Files;
using OrchardCore.Localization;

public sealed class XmlDataFileFormat : IDataFileFormat
{
    public static string Name => "xml";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<XmlDataFileFormat>("XML");

    public static string Extension => ".xml";

    public static string ContentType => "application/xml";

    public Task<long> WriteAsync(
        Stream stream,
        IReadOnlyList<DataField> fields,
        IAsyncEnumerable<DataBatch> batches,
        DataFileOptions options,
        CancellationToken cancellationToken = default)
    {
        // Write rows as they arrive and return the number of rows written.
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<DataBatch> ReadAsync(
        Stream stream,
        DataFileOptions options,
        CancellationToken cancellationToken = default)
    {
        // Yield one batch at a time; an empty file yields one empty batch carrying its fields.
        throw new NotImplementedException();
    }
}
```

```csharp
services.AddDataFileFormat<XmlDataFileFormat>();
```

Formula functions, filter conditions and a data source that translates every condition it can into its store query are in `orchardcore-data-formulas`.

# Data Connectors Examples

## Write a connector

A connector is a class in any module. Register it with `services.AddDataConnector<T>()` and add a `DisplayDriver<DataConnection>` (and optionally `DisplayDriver<ConnectedDataSet>`) that edits its settings when `connection.Connector` matches.

```csharp
public sealed class InventoryApiConnector : IDataConnector
{
    public static string Name => "InventoryApi";

    public static LocalizationSource DisplayName { get; } =
        LocalizationSource.Create<InventoryApiConnector>("Inventory API");

    public static LocalizationSource Description { get; } =
        LocalizationSource.Create<InventoryApiConnector>("Reads the stock levels of the inventory system.");

    public static DataConnectorCapabilities Capabilities =>
        DataConnectorCapabilities.Live | DataConnectorCapabilities.Import;

    public Task<DataConnectorTestResult> TestAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
    {
        // Check the connection's settings reach the system and return a message safe to show.
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<DataConnectorResource>> DiscoverAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DataConnectorResource>>([]);

    public Task<IReadOnlyList<DataField>> DescribeAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default)
    {
        // Return the source fields, named as ReadAsync returns them.
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<DataBatch> ReadAsync(
        DataConnectorReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Stream batches of context.BatchSize rows keyed by your own field names,
        // and stop at context.MaxRows when it is set.
        await Task.CompletedTask;
        yield break;
    }
}
```

```csharp
[Feature("Contoso.InventoryConnector")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataConnector<InventoryApiConnector>();
        services.AddDisplayDriver<DataConnection, InventoryConnectionDisplayDriver>();
    }
}
```

Rules:

- Read settings with `context.Connection.GetOrCreate<TSettings>()` and `context.DataSet.GetOrCreate<TSettings>()`. Store secrets with `IDataConnectionSecretManager.Set` and read them with `context.Secrets.Get`.
- The infrastructure maps rows to the data set's fields, coerces values, keeps requested fields and enforces the row limit.
- Treat `context.Conditions` as hints; apply one only when it cannot drop a matching row, and declare `ConditionPushdown` only if you apply some.
- With `IncrementalImport`, always apply `context.WatermarkField` and `context.Watermark` (rows after the value).
- Throw `DataConnectorException` with a safe message; set `IsTransient` when a later try may succeed.
- Send HTTP requests through `DataConnectorHttpClient` to inherit the network rule, authentication kinds, retries and redirect checks. A relational database is a dialect on `RelationalDataConnector`.

## Reuse a connection from another module

`IDataConnectionResolver` (in `CrestApps.OrchardCore.DataConnectors.Abstractions`) is registered only while Data Connectors is on, so take it as an optional constructor parameter.

```csharp
public sealed class MySearchClientFactory
{
    private readonly IDataConnectionResolver _resolver;

    public MySearchClientFactory(IDataConnectionResolver resolver = null)
    {
        _resolver = resolver;
    }

    public async Task<IReadOnlyList<DataConnection>> ListConnectionsAsync()
        => _resolver is null
            ? []
            : await _resolver.ListAsync([DataConnectorConstants.Connectors.Elasticsearch]);
}
```

- `ResolveAsync(connectionId)` returns a `DataConnectorContext` (connection plus secret reader) or `null`.
- The `ResolveForAsync(connectionId, connectorNames)` extension also checks the connection belongs to one of the given connectors and returns a `DataConnectionResolution` that says why it cannot be used.
- Store `DataConnectionReference` (`ConnectionId`) in your record's properties; a record without it keeps its own settings. `HttpConnectionSettings`, `HttpAuthenticationSettings` and `HttpAuthenticationSecrets` are in the abstractions too.
- The Elasticsearch AI data source uses Elasticsearch connections and the Azure AI Search AI data source uses HTTP API connections this way.

## Refresh from code

```csharp
var dataSet = await dataSetStore.FindByNameAsync("Customers");
var run = await refreshService.RefreshAsync(dataSet, DataSetRefreshTrigger.Manual, User.Identity?.Name);
```

`IDataSetRefreshService.RefreshDueAsync` refreshes data sets that are due or were requested, as the background task does.

## Connector rules

### HTTP API

- Authentication: None, API key (header with optional prefix, or query parameter), Basic, Bearer token, OAuth 2.0 (client credentials or password grant; `client_secret_basic` or `client_secret_post`), OpenID Connect (token endpoint from `/.well-known/openid-configuration`).
- Paging: one response, page number, offset, cursor at a response path, next link at a response path, or the `Link` header `rel="next"`.
- Records at a dotted path (`data.items`); nested objects flatten to `address.city`; arrays stay JSON text.
- `POST` bodies replace `{page}`, `{offset}`, `{limit}` and `{cursor}`; `{param:Name}` takes a consumer parameter such as one from a **Read a data source** step.
- 408, 429, 500, 502, 503, 504 and connection failures retry up to four times honoring `Retry-After`.

### Elasticsearch

Cluster URL with none, Basic, API key or bearer token. Resource is an index, alias or pattern (`logs-*`) with an optional query DSL clause. Fields come from `_mapping`; `_id` is the identifier; reads use the scroll API.

### Media files

The connection names a media folder; the resource is a file path inside it. Any registered data source file format reads (CSV, JSON, JSON Lines, Excel). Numbers with leading zeros stay text.

### SQL Server

Server as `host`, `host,port` or `host\instance`. Tables and views from `INFORMATION_SCHEMA.TABLES`, primary key as identifier. Custom queries must be a single `SELECT` without `;` or `--` outside quotes; every read runs in a rolled-back transaction with bound parameters.

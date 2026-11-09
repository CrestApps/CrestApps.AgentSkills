---
name: orchardcore-data-connectors
description: Skill for the CrestApps Orchard Core Data Connectors modules (CrestApps.OrchardCore.DataConnectors and CrestApps.OrchardCore.DataConnectors.SqlServer). Covers connecting to SQL Server, Elasticsearch, OpenSearch, HTTP APIs and media files, live and imported connected data sets exposed through Orchard Core data sources (ConnectedData and ImportedData) to the Report Builder and data pipelines, full and incremental refreshes with versioned rows, schema change policies, the DataConnectorsOptions and Network configuration, per data set read permissions, encrypted secrets and the outbound network rule, the DataConnection and ConnectedDataSet recipe and deployment steps, IDataConnectionResolver for reusing a connection from another module, and writing a connector with IDataConnector and AddDataConnector. Use this skill when requests mention Data Connectors, external data in reports, a connected or imported data set, a SQL Server, Elasticsearch, REST API or file data source, or a custom data connector.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Data Connectors

Data Connectors connect a tenant to other systems and expose their data sets to Orchard Core's data sources (`OrchardCore.DataPipelines.Core`). A connected data set is listed by `IDataSourceManager` like any other, so the [Report Builder](../orchardcore-report-builder/SKILL.md), the **Read a data source** step of data pipelines and every other data source consumer read it with no code of their own.

## Guidelines

- Install `CrestApps.OrchardCore.DataConnectors` (and `CrestApps.OrchardCore.DataConnectors.SqlServer` for SQL Server) in the web/startup project.
- A data set is read **live** (every read goes to the external system) or **imported** (a refresh copies the rows into the tenant's database and reads are served from the last refresh that succeeded).
- Grant `ReadConnectedDataSet_{Name}` (or `ReadAllConnectedDataSets`) to whoever reads the data set; the same rule covers live and imported reads, and a `DataSourceContext` with no user reads nothing.
- Give SQL connections a login that can only read what the data sets need: anyone who can edit a data set can query anything that login can read.
- Never put real secrets in a recipe under source control; use `[js: configuration('...')]`.
- Never invent connector settings shapes; export a configured site to get the exact `Properties`.

## Features

| Feature | ID | Adds |
| --- | --- | --- |
| Data Connectors | `CrestApps.OrchardCore.DataConnectors` | Connections, data sets, the live (`ConnectedData`) and imported (`ImportedData`) data sources, the refresh background task, and **Tools > Data Connectors** (**Connections**, **Data Sets**). Depends on `OrchardCore.DataPipelines.Core`. |
| HTTP API Data Connector | `CrestApps.OrchardCore.DataConnectors.Http` | Any HTTP or REST API answering JSON, CSV or JSON Lines. |
| Elasticsearch Data Connector | `CrestApps.OrchardCore.DataConnectors.Elasticsearch` | Elasticsearch 7 to 9 and OpenSearch indices, aliases and patterns. |
| Media Files Data Connector | `CrestApps.OrchardCore.DataConnectors.Files` | CSV, JSON, JSON Lines and Excel files in the media library. Depends on `OrchardCore.Media`. |
| SQL Server Data Connector | `CrestApps.OrchardCore.DataConnectors.SqlServer` | Tables, views and single-`SELECT` read-only queries of SQL Server and Azure SQL; SQL login, Windows, Microsoft Entra service principal, managed identity or default Entra credential; optional `ApplicationIntent=ReadOnly`. Its own module (the only one that loads `Microsoft.Data.SqlClient`). |

Connector technical names (`DataConnectorConstants.Connectors`) are `HttpApi`, `Elasticsearch`, `MediaFiles` and `SqlServer`.

## Building blocks

| Piece | Role |
| --- | --- |
| `IDataConnector` | Talks to one kind of system: `TestAsync`, `DiscoverAsync`, `DescribeAsync`, `ReadAsync` (batches). Never stores rows, schedules or checks permissions. |
| `DataConnection` | One system a tenant reaches: `Connector`, settings in `Properties`, protected secrets. |
| `ConnectedDataSet` | One resource of a connection: stable technical `Name`, fields, `AccessMode`, `Refresh` settings and import state. |
| `IDataSetRowStore` | Rows of imported data sets in `{prefix}DataConnectors_Rows`, versioned, stored as JSON. |
| `IDataSetRefreshService` | `RefreshAsync` and `RefreshDueAsync`; the **Connected Data Set Refresh** background task calls it every minute. |
| `IDataConnectionResolver` | Lets other modules reuse a connection instead of storing their own endpoint and credentials. |

Connections, data sets and refresh runs are YesSql documents in the `DataConnectors` collection.

## Refreshes

- A refresh takes a distributed lock on the data set, writes rows as a **new version**, publishes it only when every row was written, and on failure, cancel or timeout keeps the published version and records the error in the data set history.
- **Full** refreshes read every row. **Incremental** refreshes need a connector with `IncrementalImport` (SQL Server, Elasticsearch), at least one identifier field and a growing date or number field; rows deleted at the source stay until the next full refresh.
- Schema change policy **Accept** adds new fields and keeps removed fields with empty values; **Fail** fails the refresh and keeps the published rows.
- Live reads push conditions to the source only for connectors declaring `ConditionPushdown` (SQL Server ranges, number `Equals`/`In`, `TOP`; Elasticsearch number and date ranges). Text conditions are never pushed down.

## Security

| Permission | Allows |
| --- | --- |
| `ManageDataConnections` | Create, edit, check and delete connections (security critical). |
| `ManageConnectedDataSets` | Create, edit, preview and delete data sets. |
| `RefreshConnectedDataSets` | Refresh on demand; implied by `ManageConnectedDataSets`. |
| `ReadAllConnectedDataSets` | Read every data set. |
| `ReadConnectedDataSet_{Name}` | Read one data set (`DataConnectorPermissions.CreateReadPermission(name)`). |

- Secrets (`Password`, `ApiKey`, `BearerToken`, `ClientSecret`) are protected with the purpose `CrestApps.DataConnectors.Secrets`, write-only in editors, and never logged.
- Connectors refuse loopback, private, link-local, shared, multicast and unspecified addresses unless `Network:AllowedHosts` or `Network:AllowPrivateNetworks` allows them; redirects stay on the same origin (max three); token endpoints and OIDC issuers must be HTTPS.

## Configuration

Section `OrchardCore:CrestApps:DataConnectors` (`DataConnectorsOptions`) and `...:Network` (`DataConnectorNetworkOptions`):

| Setting | Default |
| --- | --- |
| `RefreshTimeout` | `00:30:00` |
| `LiveReadTimeout` | `00:05:00` |
| `ImportBatchSize` | `1000` |
| `RefreshRunsKept` | `25` |
| `MaxRefreshesPerRun` | `10` |
| `PreviewRows` | `20` |
| `Network:AllowPrivateNetworks` | `false` |
| `Network:AllowedHosts` | none |
| `Network:MaxResponseBytes` | `104857600` |

```json
{
  "OrchardCore": {
    "CrestApps": {
      "DataConnectors": {
        "RefreshTimeout": "01:00:00",
        "Network": {
          "AllowedHosts": [ "sql01.corp.example.com" ]
        }
      }
    }
  }
}
```

## Recipes and deployment

| Recipe step | Deployment step | Matches by |
| --- | --- | --- |
| `DataConnection` (array `Connections`) | **Data Connections** | `ItemId`, then `Name` |
| `ConnectedDataSet` (array `DataSets`) | **Connected Data Sets** | `ItemId`, then `Name` |

Exports leave out secrets, rows and refresh history. Pass secrets in clear text under `Secrets`; an imported data set with no rows is queued and refreshed within a minute. With **CrestApps Recipes** (`CrestApps.OrchardCore.Recipes`) enabled, both steps have JSON schemas.

```json
{
  "steps": [
    {
      "name": "DataConnection",
      "Connections": [
        {
          "ItemId": "warehouse-db",
          "Name": "Warehouse",
          "DisplayText": "Warehouse database",
          "Connector": "SqlServer",
          "Properties": {
            "SqlServerConnectionSettings": {
              "Server": "sql01.example.com",
              "Database": "Warehouse",
              "Authentication": "SqlLogin",
              "UserName": "reports_reader",
              "Encrypt": true
            }
          },
          "Secrets": { "Password": "[js: configuration('Warehouse:Password')]" }
        }
      ]
    },
    {
      "name": "ConnectedDataSet",
      "DataSets": [
        {
          "Name": "Orders",
          "DisplayText": "Orders",
          "ConnectionName": "Warehouse",
          "Resource": "sales.Orders",
          "AccessMode": "Imported",
          "Refresh": { "Mode": "Incremental", "IncrementalField": "ModifiedUtc", "IntervalMinutes": 60, "SchemaChangePolicy": "Accept" },
          "Fields": [
            { "Name": "OrderId", "SourceName": "OrderId", "Type": "Integer", "IsIdentifier": true },
            { "Name": "Total", "SourceName": "Total", "Type": "Decimal" },
            { "Name": "ModifiedUtc", "SourceName": "ModifiedUtc", "Type": "DateTime" }
          ],
          "DefaultDateField": "ModifiedUtc"
        }
      ]
    }
  ]
}
```

A data set `Name` cannot change once it exists (reports, pipelines and its read permission store it); an existing connection's `Connector` cannot change.

## Extending

See [Connector and resolver examples](references/data-connectors-examples.md) for writing a connector, reusing a connection from another module, refreshing from code, and the HTTP API, Elasticsearch and media file connector rules.

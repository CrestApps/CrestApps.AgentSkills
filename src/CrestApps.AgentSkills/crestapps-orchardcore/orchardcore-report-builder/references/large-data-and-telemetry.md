# Large data and telemetry

## How a run stays bounded

- **Streaming.** Rows stream from the data sources through Orchard Core's `DataJoin`, `DataAggregator` and `DataSorter` a batch at a time. Each join, grouping or sort keeps at most `MemoryLimitMegabytes` of rows in memory and writes the rest to temporary files under `TemporaryDirectory` (one folder per run, deleted when it ends).
- **Concurrency gate.** `ReportQueryGate` (singleton) lets a tenant run at most `MaxConcurrentRuns` reports at once; the others wait. A report that reads a live view runs the view inside its own run.
- **Grouped results.** The rows behind a grouped result are not kept, so a runner asks for the groupings its visuals need before the query runs (`ReportQueryExecutionContext.Groupings`, from `ReportDesignDocumentBuilder.GetGroupings`) and then calls `Regroup` or `GetTotals`. `ReportQueryEngine.ReadAsync` streams the result rows of a query; a live view streams its rows to the reports that read it.
- **Aggregates.** A calculated field that calls an aggregate a report cannot compute as the rows arrive (a module aggregate made with the `ExpressionFunction.CreateAggregate` overload that needs every value) is refused.

## Push the work to the source

- **Date filter from the start.** A data set's main date (`DataSetDescriptor.DefaultDateField`) gives new reports a recent-period filter on the last 30 days. Relative date filters reach sources as a date range (`DataConditions.DateRange`).
- **Viewer filters in the database.** Row filters on data sets no outer join can fill with empty values are offered to the source as `DataCondition`s. CrestApps sources apply date ranges and list-of-values filters on indexed columns (`ReportIndexConditions`). The engine always applies every filter again.
- **Join key narrowing.** For inner and left joins on a field marked `IsKeyFilterable`, the engine sends the distinct keys as an `In` condition with `IsJoinKey`, `JoinKeyBatchSize` keys per read, up to `MaxJoinKeys` (more reads the joined data set in full). Only text and whole-number keys of the same type are narrowed.
- **Grouping in the source.** A single-data-set query with plain dimensions, date buckets (Hour of day, Day, Week, Day of week, Month, Month of year, Quarter, Year) and Count, Sum, Average, Min or Max can be answered by Orchard Core's `IDataAggregateSource`. `RecordDataSource` forwards it to data sets that implement `IAggregateRecordDataSet`; `ReportIndexAggregator` answers with one `GROUP BY` over a YesSql map index. A query that uses a row-level calculated field, joins or several data sets is not pushed down (`crestapps.report.aggregate.pushdown` says why).
- **Keyset paging.** `ReportIndexPages` reads a map index 500 records at a time, newest first, seeking by the date column and the index row id without offsets. It selects only the row id and the date for each page (then loads the records), so a decimal column SQLite stores as integer in one row and real in another cannot fail the read; when YesSql's query shape is not the expected one it falls back to reading whole index rows.

## Share work inside a run

- **Only the calculated fields a query uses.** A query computes only the calculated fields its columns and filters use (and the fields those read). Every calculated field is still checked in the builder.
- **Shared reads.** The queries of one run plan before any runs. Queries that read the same data set share one read: the first reads every field any of them needs and keeps up to `MaxSharedReadRows` rows; a query whose row filters include all of the kept read's filters (such as a click-filtered copy) reads the kept rows. `0` turns sharing off. Queries run one after another, since data sources may share the request's database session.
- **Preview.** The builder preview cancels a preview still running when it starts another, shows how long it has run, and offers **Try again** on failure.

## Scheduled views

- `ReportView.RefreshIntervalMinutes`: `0` live; the builder offers 15, 60, 360 and 1440 (`ReportViewRefreshIntervals`); other values are raised to at least 15 on save and import.
- The **Report view refresh** background task (`ReportViewSnapshotBackgroundTask`, every 5 minutes, distributed lock) refreshes due views with the owner's access and keeps the previous rows on failure. Each refresh also takes a per-view distributed lock, so a view already refreshing on another node reports `busy` instead of running twice.
- `POST /Admin/reports/views/{id}/refresh` (**Refresh now** on the view's **Settings** tab) refreshes at once.
- The stored rows (`ReportViewSnapshot`, index `ReportViewSnapshotIndex`) are dropped when the query changes, the view turns live, or an import replaces it with a changed or live view. At most `MaxRowsPerDataSet` rows are stored.
- A view that other reports or views read cannot be deleted; the message names them.

## Checklist for a large data set

1. Give the data set a main date and keep a date filter on new reports.
2. Index the main date and join keys; mark keys `IsKeyFilterable`.
3. Implement grouping in the source (`IAggregateRecordDataSet` or `IndexedReportDataSet`).
4. Read pages with `ReportIndexPages` and add the date + id database index.
5. Schedule heavy shared views.
6. Raise `CrestApps:Reports:Builder:Limits` only after that.

## Telemetry

Everything is emitted through the `ActivitySource` and `Meter` named `CrestApps.OrchardCore.Reports` (`ReportTelemetry.SourceName` and `ReportTelemetry.MeterName`, namespace `CrestApps.OrchardCore.Reports.Designer`, package `CrestApps.OrchardCore.Reports.Core`): designed runs, coded reports, exports, views, subscriptions, the designer and the widgets. The PDF export adds `crestapps.report.pdf.font_fallbacks` on a second meter of the same name. Nothing is recorded while nobody listens.

Collect it with the `CrestApps.OrchardCore.*` wildcard (`CrestAppsOrchardCoreTelemetry.SourceNameWildcard`), or name it explicitly, in the web host's `Program.cs` (see `orchardcore-opentelemetry`):

```csharp
builder.Services
    .AddOrchardCms(orchardCore => orchardCore
        .AddOpenTelemetry(openTelemetry => openTelemetry
            .WithTracing(tracing => tracing.AddSource("CrestApps.OrchardCore.*"))
            .WithMetrics(metrics => metrics.AddMeter("CrestApps.OrchardCore.*"))));
```

### Metric tags

Metrics carry bounded tags only:

| Tag | Values |
| --- | --- |
| `crestapps.report.source` | `ReportRunSources`: `Admin`, `Shared`, `Link`, `Preview`, `Widget`, `Workflow`, `Subscription`, `ViewRefresh`, `ViewRead`, `Other` (any unknown value becomes `Other`) |
| `crestapps.report.data_source` | Technical name of the data source, such as `Contents` |
| `outcome` | `ok`, `error`, `cancelled`, `timeout`, plus instrument-specific values |
| `crestapps.report.level` | On `crestapps.report.run.failures` only: `run` or `query` |
| `crestapps.report.name` | Technical name of a coded report |
| `format` | A registered export format; an unknown format in an address is `other` |

`crestapps.report.id`, `crestapps.report.query.id`, `crestapps.report.data_set`, `crestapps.report.title` and the view and subscription ids are **span tags only**; metrics dropped them so series do not multiply by the number of reports. The old `crestapps.report.outcome` tag (`success`, `failure`, `canceled`) is removed: move dashboards to `outcome`. A failing query is counted once at `level=query` and its run once at `level=run`; a run whose secondary query fails is `ok` and counts only that query.

### Spans

| Span | One per |
| --- | --- |
| `report.run` | Designed run, including preview and click refreshes (`crestapps.report.click_filtered`, `.refreshed_visuals`, `.rows.returned`, `.warnings`, `.queries`, `.queries.failed`, `outcome`) |
| `report.plan` | Query plan (`crestapps.report.calculated_fields`, `.row_calculations`, `.errors`) |
| `report.schema` | Data set schema a plan reads (`outcome` `ok`, `not_found`, `error`) |
| `report.query` | Query run (`crestapps.report.gate_wait_ms`, `.rows.source`, `.rows.filtered`, `.rows.returned`, `.calculate_ms`, `.calc_errors`, `.grouped`, `.grouped_by_source`, `.pushdown_fallback`, `.sorted`, `.spills`) |
| `report.stream` | Query streamed to another report, such as a live view |
| `report.read` | Data set read (`crestapps.report.fields`, `.rows.read`, `.read_shared`, `.truncated`, `.join_key_batches`, `.view.mode`) |
| `report.join` | Join (`crestapps.report.join_type`, `.join_keys`, `.rows.joined`) |
| `report.result`, `report.render` | Building a query's result and groupings; building its visuals' sections |
| `report.present` | Report page, shared page or share link around its run |
| `report.events` | Fan-out to `IReportEventHandler`s (`crestapps.report.event` `ran`, `exported`) |
| `report.coded.run` | Coded report run (`crestapps.report.name`, `.mode`) |
| `report.export` | File an export format writes (`format`, `crestapps.report.export.size`, `.export.rows`) |
| `report.view.refresh`, `report.view.sweep` | View refresh; the **Report view refresh** task |
| `report.subscription.send`, `report.subscription.sweep` | Subscription run (`reason`, `trigger`); the **Report subscriptions** task (`.due`, `.sent`, `.not_sent`, `.failed`) |

Designed run spans carry `crestapps.report.id`, `crestapps.report.title`, `crestapps.report.source` and `crestapps.report.query.id` (empty for the main query).

### Metrics

| Instrument | Kind | Tags / meaning |
| --- | --- | --- |
| `crestapps.report.run.duration`, `crestapps.report.query.duration`, `crestapps.report.stream.duration` | Histogram (s) | source, `outcome` |
| `crestapps.report.run.failures` | Counter | source, `crestapps.report.level`; cancellations are not failures |
| `crestapps.report.run.queries` / `crestapps.report.run.queries.skipped` | Histogram / Counter | Queries per run; skipped with `reason` `invalid` or `failed` |
| `crestapps.report.run.rejected` | Counter | `reason` `owner_missing` |
| `crestapps.report.plan.duration`, `crestapps.report.schema.duration` | Histogram (s) | `outcome` |
| `crestapps.report.rows.read`, `crestapps.report.rows.returned` | Counter | source (+ data source for reads) |
| `crestapps.report.reads.shared`, `crestapps.report.reads.shared.miss` | Counter | Miss `reason`: `disabled`, `join_key`, `conditions`, `too_many_rows` |
| `crestapps.report.limit.hits` | Counter | `crestapps.report.limit`: `rows_per_dataset`, `joined_rows`, `result_rows`, `join_keys`, `shared_read_rows` |
| `crestapps.report.aggregate.pushdown` | Counter | `outcome` `used`/`fallback`, `reason` (`multiple_data_sets`, `joins`, `row_calculations`, `not_supported`, `filter`, `dimension`, `measure`, `too_many_buckets`, `declined`) |
| `crestapps.report.aggregate.index` | Counter | `result` `built`, `unbuildable`, `too_many_groups`; `crestapps.report.index` |
| `crestapps.report.calc.errors`, `crestapps.report.spills` | Counter | source |
| `crestapps.report.gate.wait` / `.gate.active` / `.gate.waiting` | Histogram / UpDownCounter | Waiting for and holding `MaxConcurrentRuns` |
| `crestapps.report.coded.duration` | Histogram (s) | `crestapps.report.name`, `crestapps.report.mode` (`display`, `export`), `outcome` |
| `crestapps.report.coded.index_rows` | Histogram | Index rows a coded report read at once; warning logged from 50,000 |
| `crestapps.report.export.duration`, `.export.size`, `.export.rows` | Histogram | `format` |
| `crestapps.report.exports` | Counter | source, `format`, `outcome` (`ok`, `error`, `not_found`, `forbidden`) |
| `crestapps.report.pdf.font_fallbacks` | Counter | PDFs written in Helvetica (no TrueType font) |
| `crestapps.report.view.refresh.duration`, `crestapps.report.view.refreshes` | Histogram / Counter | `trigger` (`schedule`, `manual`), `outcome` (`refreshed`, `failed`, `busy`, `not_due`, `not_scheduled`, `not_found`) |
| `crestapps.report.view.snapshot.rows`, `crestapps.report.view.snapshot.age` | Histogram / Gauge | Rows stored; age of the oldest stored rows (alert when it grows past the longest interval) |
| `crestapps.report.view.reads`, `crestapps.report.view.rejected` | Counter | `crestapps.report.view.mode` (`snapshot`, `live`, `stale_snapshot`, `first_run`); rejected `reason` `loop`, `depth` |
| `crestapps.report.subscription.sends`, `crestapps.report.subscription.duration` | Counter / Histogram | `trigger`, `outcome` (`ok`, `skipped`, `error`), `reason` (`no_email`, `no_recipients`, `report_unavailable`, `view_denied`, `export_denied`, `send_failed`, `exception`) |
| `crestapps.report.design.publishes`, `crestapps.report.design.conflicts`, `crestapps.report.design.validation_errors` | Counter | Publish `outcome` (`saved`, `invalid`, `conflict`, `busy`, `not_found`); 409 `reason` |
| `crestapps.report.shared_link.requests` | Counter | `result` (`opened`, `exported`, `not_found`, `export_denied`) |
| `crestapps.report.workflow.tasks` | Counter | `crestapps.report.workflow_task` (`run`, `export`, `email`, `share_link`), `outcome` |
| `crestapps.report.widget.requests` | Counter | `crestapps.report.kind` (`design`, `coded`), `crestapps.report.cache` (`hit`, `miss`, `off`), `outcome` |
| `crestapps.report.hub.denied` | Counter | `operation` |

Both background tasks also report `crestapps.background_task.items`; builder notifications run as the deferred task `report_design_notify`.

A coded report that reads an index in one go can record how many rows it read, which warns at 50,000:

```csharp
ReportTelemetry.RecordCodedIndexRows(Name, activities.Count, _logger);
```

### Logs

Event ids 14000 to 14999: 14001 (coded report read 50,000+ index rows), 14002 (temporary files not deleted), 14101 (designed run ended with errors), 14102 (run over 10 seconds, `ReportTelemetry.SlowRunThreshold`), 14107 (presence hub refused a call), 14110 (builder found problems), 14111 and 14112 (recipe and deployment step summaries), 14400 (PDF export has no TrueType font, logged once).

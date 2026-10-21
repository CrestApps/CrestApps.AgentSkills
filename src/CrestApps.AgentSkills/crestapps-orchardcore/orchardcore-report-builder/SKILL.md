---
name: orchardcore-report-builder
description: Skill for the CrestApps Orchard Core Report Builder (CrestApps.OrchardCore.Reports.Builder), the drag-and-drop report designer built on Orchard Core Data Pipelines Core (OrchardCore.DataPipelines.Core). Covers ReportDesign and ReportView, data sets from Orchard Core data sources (IDataSource, RecordDataSource, RecordDataSet, AddDataSource) such as Contents, Users, Queries, ReportViews, Omnichannel, ContactCenter, AIChat and Messaging, joins, calculated fields and formulas, exposed filters, charts and pivots, ReportDesignerPermissions and the owner's-access model, share links with ISharedLinkManager, drafts, versions and SignalR presence, ReportIndexPages, ReportIndexConditions and ReportIndexAggregator for large data, scheduled views, ReportQueryLimits, the ReportDesigns recipe and deployment steps, workflow tasks, and IReportEventHandler. Use this skill when requests mention the Report Builder, designed or custom reports, report views, a report data source, report formulas, or sharing reports.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Report Builder

The **Report Builder** feature of the Reports module lets people design reports in the browser: add data sets from any data source, join them (inner, left, right, full), add calculated fields, filter rows or results (and let viewers change chosen filters), group and aggregate, and show tables, charts, headline metrics and pivot tables. Designed reports render through the same renderer and export formats as coded reports (see `orchardcore-reports`), can be pinned to the admin menu, shared with users and roles, and opened through expiring share links.

New in CrestApps.OrchardCore 3.0. The data source contract, the formula language and share links come from Orchard Core 4.0's **Data Pipelines Core** feature (see the `orchardcore-data-sources` and `orchardcore-data-pipelines` skills).

## Feature

| | |
| --- | --- |
| **Feature ID** | `CrestApps.OrchardCore.Reports.Builder` (`ReportsConstants.BuilderFeature`) |
| **Dependencies** | `CrestApps.OrchardCore.Reports`, `OrchardCore.DataPipelines.Core` |
| **Optional integrations** | `OrchardCore.Contents` and `OrchardCore.Queries` (data sources), `OrchardCore.Workflows` and `OrchardCore.Email` (workflow activities, share link emails), `OrchardCore.SignalR` (presence), `OrchardCore.Recipes.Core` and `OrchardCore.Deployment` (recipe and deployment steps), `CrestApps.OrchardCore.Reports.OpenXml` (Excel export) |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Reports",
        "CrestApps.OrchardCore.Reports.OpenXml",
        "CrestApps.OrchardCore.Reports.Builder",
        "OrchardCore.Contents",
        "OrchardCore.Queries",
        "OrchardCore.Workflows",
        "OrchardCore.SignalR"
      ]
    }
  ]
}
```

Install the module packages in the web (startup) project. A module that adds a data source references `OrchardCore.DataSources.Abstractions` (contract), `OrchardCore.DataSources.Core` (`AddDataSource`), `CrestApps.OrchardCore.Reports.Abstractions` (field helpers, constants) and, for the YesSql helpers, `CrestApps.OrchardCore.Reports.Core`.

## Architecture

- **`CrestApps.OrchardCore.Reports.Abstractions`** adds `DataFieldExtensions` (`Identifier`, `MarkKeyFilterable`, namespace `CrestApps.OrchardCore.Reports.DataSources`) and `ReportsConstants` (`ViewsDataSource = "ReportViews"`, `UsersDataSource`, `UsersDataSet`, `UserIdField`, `SharedLinkKind = "Reports.Report"`).
- **`CrestApps.OrchardCore.Reports.Core`** (namespace `CrestApps.OrchardCore.Reports.Designer`) holds the design model (`ReportQueryDefinition`, `ReportDataSetReference`, `ReportJoinDefinition`, `ReportCalculatedField`, `ReportFilterDefinition`, `ReportColumnDefinition`, `ReportSortDefinition`, `ReportVisualDefinition`) and the engine: `ReportQueryPlanner` checks a query against live schemas and compiles formulas, `ReportQueryEngine` runs it, `ReportDesignDocumentBuilder` turns the result into a `ReportDocument`. `ReportIndexPages`, `ReportIndexConditions` and `ReportIndexAggregator` (namespace `CrestApps.OrchardCore.Reports.Designer.DataSources`) help YesSql-backed sources.
- **`CrestApps.OrchardCore.Reports`** holds the stores (`ReportDesign`, `ReportView`, `ReportDesignDraft`, `ReportDesignVersion`, `ReportViewSnapshot`), `ReportDesignerPermissions`, `ReportDesignAuthorizationHandler`, the builder pages and JSON endpoints, the **Report views** data source, `ReportSharedLinkHandler`, the view refresh background task, `ReportsHub`, recipe, deployment and workflow integrations.

A run plans (reads every data set's schema with the principal it reads for), reads (streams only used fields, in batches, pushing down conditions the source can apply), joins (Orchard Core `DataJoin`; text keys ignore case, empty keys never match), calculates row formulas, filters and groups (`DataAggregator`), shapes (result filters, sorts with `DataSorter`, limits), and renders visuals. Joins, groupings and sorts keep at most `MemoryLimitMegabytes` of rows in memory and spill the rest to temporary files, so memory stays bounded. Data sources return `DateTime` values in UTC; the engine converts them to the tenant time zone before formulas, filters and display.

## Data sources

The builder lists every data source `IDataSourceManager` knows and the data sets the principal may read.

| Source (technical name) | Provided by | Registered when | Read permission |
| --- | --- | --- | --- |
| Report views (`ReportViews`) | Report Builder | Report Builder | `ViewAllReportDesigns` on the view (every designer) |
| Content items (`Contents`) | Orchard Core | `OrchardCore.Contents` | `ViewContent` per type; `EditContent` reads drafts |
| Users (`Users`, data sets `Users` and `UserRoles`) | Orchard Core | `OrchardCore.Users` | `ViewUsers` |
| Queries (`Queries`) | Orchard Core | `OrchardCore.Queries` | `ExecuteApi_{QueryName}` (implied by `ExecuteApiAll`, `ManageQueries`) |
| Omnichannel (`Omnichannel`) | CrestApps Omnichannel | Omnichannel Activities | `ViewOmnichannelReports` |
| Contact Center (`ContactCenter`) | CrestApps Contact Center | The feature storing each data set | `ViewContactCenterReports` (+ `ListenToAllCallRecordings`, `AccessContactCenterSharedVoicemail` for recordings and voicemail) |
| AI chat (`AIChat`) | CrestApps AI Chat | AI Chat (metrics need AI Chat Session Analytics) | `ViewChatAnalytics` |
| Messaging (`Messaging`) | CrestApps Omnichannel Messaging | Omnichannel Messaging Workspace | `ViewAllMessagingConversations` |

- Technical names are stored in designs (`ReportDataSetReference.Source`) and field references; they never change.
- Content items: common fields (`ContentItemId`, `ContentType`, `DisplayText`, `Owner`, `Author`, `Published`, `Latest`, `CreatedUtc`, `ModifiedUtc`, `PublishedUtc`), part values `{Part}.{Property}` (such as `TitlePart.Title`), single-value fields `{Part}.{Field}`, multi-value fields `{Part}.{Field}.{Property}`. Only `Owner` declares a relationship (to `Users.UserId`); join pickers and lists explicitly (`Order.Customer` = `ContentItemId`). Make sensitive content types **Securable**, since `ViewContent` is broad by default.
- Queries run once per report run without parameters, so give parameters defaults.
- Since the content items and users sources are Orchard Core's, a few fields of earlier builder versions are gone (picked items' `DisplayText`, sampled **(more)** properties, custom user settings `Properties.*`, and `UserRoles.Role`, now `RoleName`). Re-pick those fields in the builder.

To add your own data source (record-backed or external), see [references/custom-data-sources.md](references/custom-data-sources.md).

## Permissions and the owner's-access model

`ReportDesignerPermissions` (namespace `CrestApps.OrchardCore.Reports.Designer`). The Administrator stereotype gets all four; the commerce staff roles **Store manager** and **Marketing** get `ViewAllReportDesigns`:

| Permission | Name in the role editor | Implied by | Security critical |
| --- | --- | --- | --- |
| `ManageAllReportDesigns` | Manage all custom reports and views | | Yes |
| `ManageOwnReportDesigns` | Build reports and manage own custom reports and views | `ManageAllReportDesigns` | No |
| `ViewAllReportDesigns` | View all custom reports | `ManageAllReportDesigns` | No |
| `ShareReportsPublicly` | Share custom reports publicly and through share links | | Yes |

Controllers check a broad permission with the report or view as the **resource**, like content items. `ReportDesignAuthorizationHandler` grants it per item:

- `ViewAllReportDesigns` (run, export, open at `/reports/view/{id}`, clone) on a report goes to its owner, users in `SharedUserNames` (by user name, ignoring case), members of `SharedRoles`, everyone when `SharedRoles` holds `Anonymous`, and every signed-in user when it holds `Authenticated`. On a view it goes to every signed-in user with `ManageOwnReportDesigns`.
- `ManageAllReportDesigns` (edit, drafts, publish, versions, delete, share links, refresh a view) goes to the owner (`OwnerId` matched against the `NameIdentifier` claim) when they hold `ManageOwnReportDesigns`.
- Creating a report or view, the preview and the builder JSON endpoints check `ManageOwnReportDesigns` without a resource.
- Sharing with `Anonymous` and managing share links need `ShareReportsPublicly`. Export also needs the report's `AllowExport`, unless the user can manage it.

```csharp
if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ViewAllReportDesigns, design))
{
    return Forbid();
}
```

**A saved report reads data with its owner's current access**, whoever runs it; viewers need no access to the underlying data. When the owner is deleted, disabled, or loses access to a data set, the report stops with a message asking someone to clone and re-share it. The builder preview reads with the designer's own access. Scheduled views refresh with their owner's access too. Data sources are therefore the security boundary: they must hide every data set the given principal (`DataSourceContext.User`) may not read.

Exposed filter values from the query string override only filters marked `Exposed`; fixed filters cannot be changed or seen by viewers.

### Share links

Share links are Orchard Core shared links (`ISharedLinkManager`) of kind `Reports.Report`, opened by `ReportSharedLinkHandler` at `/SharedLinks/{linkId}?token=...`.

- Recipients are named users of the site (user name or email); only a signed-in recipient with the token opens it. A wrong token gives `404`, an expired, used-up or revoked link `410`, a non-recipient `403`.
- Tokens hold 256 random bits; only their SHA-256 hash is stored, and the full URL is shown once. Every link expires (`ExpiresAfterDays` at least 1); `MaxUses` (`0` for no limit) counts opens, filter changes and exports. `Note` and `AllowExport` are stored on the link.
- `NotifyByEmail` (default) emails the link through Orchard Core, which needs the Email feature.
- **Tools > Shared Links** (Orchard Core `ManageSharedLinks`) lists and revokes links of every module. Deleting a report deletes its links; deployments never export them.

## Pages and URLs

| URL | Purpose |
| --- | --- |
| `/Admin/reports/designs` | Designed reports the user can run (`q`, `status=published|unpublished`); an `AdminList` (see `orchardcore-admin-lists`) |
| `/Admin/reports/designs/{id}` and `/Admin/reports/designs/{id}/export/{format}` | Run and export in the admin |
| `/Admin/reports/builder/create`, `/Admin/reports/builder/edit/{id}` | The builder |
| `/Admin/reports/views`, `/Admin/reports/views/create`, `/Admin/reports/views/edit/{id}` | Reusable views |
| `/reports/view/{id}` | A shared report outside the admin |
| `/SharedLinks/{linkId}?token=...` | A report opened through a share link |

Run and export pages read exposed filter values from the query string: `applied=1`, then `f.{filterId}` (repeatable) or `f.{filterId}.from` and `f.{filterId}.to` for ranges. The builder's JSON endpoints under `/Admin/reports/builder/api/` (`sources`, `datasets`, `schema`, `functions`, `plan`, `users`) and the save, draft and link endpoints are internal and may change.

Admin menu: **Report Builder** (designers), **Shared Reports** (people who can run at least one report), **Report Views** (designers), and every report with `ShowInAdminMenu` for people allowed to run it.

## Configuration

`ReportQueryLimits` binds to `CrestApps:Reports:Builder:Limits` and `ReportDesignVersionOptions` to `CrestApps:Reports:Builder:Versions` of the shell configuration. In the host `appsettings.json` they sit under `OrchardCore`; a tenant's `App_Data/Sites/{tenant}/appsettings.json` uses the section without the wrapper.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Reports": {
        "Builder": {
          "Limits": {
            "MaxRowsPerDataSet": 50000,
            "MaxJoinedRows": 250000,
            "MaxResultRows": 10000,
            "MaxFilterOptions": 500,
            "MaxJoinKeys": 10000,
            "JoinKeyBatchSize": 500,
            "MemoryLimitMegabytes": 16,
            "TemporaryDirectory": null,
            "MaxConcurrentRuns": 4
          },
          "Versions": {
            "MaxVersions": 50
          }
        }
      }
    }
  }
}
```

| Setting | Default | Limits |
| --- | --- | --- |
| `MaxRowsPerDataSet` | `50000` | Rows read from one data set (warning shown when hit); also the most groups a source may return and rows a scheduled view stores |
| `MaxJoinedRows` | `250000` | Rows the joins may produce |
| `MaxResultRows` | `10000` | Result rows kept after sorting |
| `MaxFilterOptions` | `500` | Values a drop-down filter lists |
| `MaxJoinKeys` | `10000` | Distinct keys a join sends to the joined data set (more reads it in full) |
| `JoinKeyBatchSize` | `500` | Keys per read of a joined data set |
| `MemoryLimitMegabytes` | `16` | Rows one join, grouping or sort keeps in memory before spilling to temporary files |
| `TemporaryDirectory` | system temp | Where runs write temporary files (one folder per run, deleted after) |
| `MaxConcurrentRuns` | `4` | Reports a tenant runs at once; others wait |
| `Versions:MaxVersions` | `50` | Published versions kept per report (`0` keeps all) |

Environment variables use `__`, such as `OrchardCore__CrestApps__Reports__Builder__Limits__MaxRowsPerDataSet=100000`. See `orchardcore-configuration` for the shell configuration hierarchy.

Other limits: designer payloads 1 MB, formulas nested 64 levels, views reading views 8 levels deep, a view that reads itself is refused.

## Large data

- **Date filter from the start.** A data set's main date (`DataSetDescriptor.DefaultDateField`) gives new reports a **recent period** filter starting at the last 30 days. Relative date filters reach sources as a date range (`DataConditions.DateRange`).
- **Viewer filters in the database.** Row filters go to the source as `DataCondition`s; CrestApps sources apply date ranges and list-of-values filters on indexed columns (`ReportIndexConditions`). The engine always re-applies every filter.
- **Join key narrowing.** For inner and left joins on a field marked `IsKeyFilterable`, the engine sends the distinct keys as an `In` condition with `IsJoinKey`, in batches of `JoinKeyBatchSize` up to `MaxJoinKeys`. Only text and whole-number keys of the same type are narrowed.
- **Grouping in the source.** A single-data-set report with simple dimensions, date buckets (Hour of day, Day, Week, Day of week, Month, Month of year, Quarter, Year) and Count, Sum, Average, Min or Max can be answered by Orchard Core's `IDataAggregateSource`; `RecordDataSource` forwards it to data sets that implement `IAggregateRecordDataSet`, and `ReportIndexAggregator` answers with one `GROUP BY` over a YesSql map index.
- **Keyset paging.** `ReportIndexPages` reads a map index 500 records at a time, newest first, without offsets.
- **Scheduled views.** `ReportView.RefreshIntervalMinutes` (`0` live; the builder offers 15, 60, 360, 1440; other values are raised to at least 15) stores a view's result. The **Report view refresh** background task runs every 5 minutes with a distributed lock, refreshing with the owner's access and keeping the previous rows on failure. `POST /Admin/reports/views/{id}/refresh` refreshes at once. Editing the query, switching to live or importing a changed view drops the stored rows.

Checklist for a large data set: set a main date and keep a date filter, index the main date and join keys and mark keys `IsKeyFilterable`, implement grouping in the source, schedule heavy shared views, read pages with `ReportIndexPages`, and raise limits only after that.

## Drafts, versions and real time

- The builder autosaves each change into a `ReportDesignDraft` (`POST /Admin/reports/builder/{id}/draft`, with `keepalive`). A new report exists only as a draft (listed **Not published** for its owner and managers) until first published.
- Every draft save, publish, discard and restore increases the report's revision. A change based on an older revision gets `409 Conflict` with the name of whoever changed it; the page offers to reload or force. Changes are serialized with `IDistributedLock`, so this holds across nodes.
- Publishing (`POST /Admin/reports/builder/save`) adds an immutable `ReportDesignVersion` only when content changed and clears the draft. Versions can be listed, previewed and restored into the draft. At most `MaxVersions` are kept. Drafts and versions use their own index tables (`ReportDesignDraftIndex`, `ReportDesignVersionIndex`). Views have no drafts or versions.
- With `OrchardCore.SignalR` enabled, `ReportsRealTimeStartup` maps `ReportsHub` and replaces `IReportDesignNotifier` with `SignalRReportDesignNotifier`: pages show who else has a report open, and `ReportDesignChanged` (`kind`, `designId`, `revision`, `versionNumber`, `userName`) offers a reload. Groups are tenant-qualified, so it works behind a Redis or Azure SignalR backplane. Replace `IReportDesignNotifier` to react to changes in other ways (`ReportDesignChangeKind`: `DraftSaved`, `DraftDiscarded`, `Published`, `Restored`, `Deleted`).

## Recipes, deployment and workflows

- The **Designed Reports and Views** deployment step (`ReportDesignsDeploymentStep`, `IncludeAll`, `ReportIds`, `ViewIds`) exports every or picked report and view, plus the views they read, with each owner's user name.
- The `ReportDesigns` recipe step imports `Views` then `Reports`, matched by `itemId`, with owners matched by `OwnerUserName`.
- With `OrchardCore.Workflows`, the builder adds `ReportRunEvent`, `ReportExportedEvent`, `ReportPublishedEvent`, `RunReportTask`, `ExportReportTask`, `CreateReportShareLinkTask`, and (with `OrchardCore.Email`) `EmailReportTask`. Code can react without Workflows through `IReportEventHandler` (`ReportEventHandlerBase`).

The design JSON shape, a full recipe and the workflow activities are in [references/designs-recipes-and-workflows.md](references/designs-recipes-and-workflows.md). The formula language is in [references/formulas.md](references/formulas.md).

## Troubleshooting

- **A data source or data set is missing in the builder**: its feature or Data Pipelines Core is off, its startup lacks `[RequireFeatures("OrchardCore.DataPipelines.Core")]`, or the designer lacks the source's permission.
- **A shared report says it can no longer run**: the owner was deleted, disabled or lost access; clone it (the clone is owned by the person cloning) and share again.
- **A field of an older design is not found**: a field removed with the switch to Orchard Core's content and users sources; replace it in the builder (for example join the picked type and use its `DisplayText`).
- **Warning that a data set has more rows**: `MaxRowsPerDataSet` was hit; add a date filter, push down conditions, or schedule a view before raising the limit.
- **"The calculated field calls an aggregate function that reports can't compute"**: reports aggregate rows as they arrive without keeping them, so a module-added aggregate created with the `ExpressionFunction.CreateAggregate` overload that needs every value of the group is refused; use built-in aggregates or the overload that names a `DataAggregate`.
- **`409 Conflict` on save**: someone else saved first; reload or keep your changes.
- **Share link email not sent**: `NotifyByEmail` needs the Email feature configured; otherwise send the URL yourself.
- **Imported design fails when run**: the target site lacks a data source or data set; enable the same features on both sites.

---
name: orchardcore-report-builder
description: Skill for the CrestApps Orchard Core Report Builder (CrestApps.OrchardCore.Reports.Builder), the drag-and-drop report designer on Orchard Core Data Pipelines Core. Covers ReportDesign and ReportView, data sources (IDataSource, RecordDataSource, IndexedReportDataSet, AddDataSource) such as Contents, Users, Queries, ReportViews, Omnichannel, ContactCenter, AIChat, Messaging and the store sources, joins, calculated and quick formulas, exposed filters, the Rows shelf and cross-tabs, color rules, ShowAs table calculations, charts, reference lines and pivots, interactive dashboards with several queries, containers and click-to-filter, report groups, ReportDesignerPermissions and the owner's-access model, share links, drafts and versions, the Custom Report dashboard widget, large data, MaxSharedReadRows and OpenTelemetry, the ReportDesigns recipe and deployment steps, workflow tasks, and IReportEventHandler. Use it for the Report Builder, designed reports, dashboards, report data sources or sharing reports.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Report Builder

The **Report Builder** feature of the Reports module lets people design reports in the browser: add data sets from any data source, join them (inner, left, right, full), add calculated fields, filter rows or results (and let viewers change chosen filters), group and aggregate, lay dimensions out on the **Columns** and **Rows** shelves (cross-tabs), color columns, show measures as quick table calculations, and show tables, charts, headline metrics and pivot tables. A report can be an interactive dashboard: several queries, containers, and visuals that filter each other when clicked. Designed reports render through the same renderer and export formats (CSV, Excel, PDF) as coded reports (see `orchardcore-reports`), are listed under nested report groups, can be pinned to the admin menu, shared with users and roles, opened through expiring share links, and shown on the admin dashboard.

New in CrestApps.OrchardCore 3.0. The data source contract, the formula language and share links come from Orchard Core 4.0's **Data Pipelines Core** feature (see the `orchardcore-data-sources` and `orchardcore-data-pipelines` skills).

## Feature

| | |
| --- | --- |
| **Feature ID** | `CrestApps.OrchardCore.Reports.Builder` (`ReportsConstants.BuilderFeature`) |
| **Dependencies** | `CrestApps.OrchardCore.Reports`, `OrchardCore.DataPipelines.Core` |
| **Optional integrations** | `OrchardCore.Contents` and `OrchardCore.Queries` (data sources), `OrchardCore.Workflows` and `OrchardCore.Email` (workflow activities, share link emails), `OrchardCore.SignalR` (presence), `OrchardCore.Recipes.Core` and `OrchardCore.Deployment` (recipe and deployment steps), `OrchardCore.AdminDashboard` (Custom Report widget), `CrestApps.OrchardCore.Reports.OpenXml` (Excel export), `CrestApps.OrchardCore.Reports.Pdf` (PDF export) |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Reports",
        "CrestApps.OrchardCore.Reports.OpenXml",
        "CrestApps.OrchardCore.Reports.Pdf",
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
- **`CrestApps.OrchardCore.Reports.Core`** (namespace `CrestApps.OrchardCore.Reports.Designer`) holds the design model (`ReportQueryDefinition`, `ReportDataSetReference`, `ReportJoinDefinition`, `ReportCalculatedField`, `ReportFilterDefinition`, `ReportColumnDefinition`, `ReportSortDefinition`, `ReportVisualDefinition`, `ReportNamedQuery`, `ReportContainerDefinition`, `ReportVisualAction`, `ReportColumnColors`, `ReportReferenceLine`) and the engine: `ReportQueryPlanner` checks a query against live schemas and compiles formulas, `ReportQueryEngine` runs it, `ReportDesignDocumentBuilder` turns the result into a `ReportDocument` (cross-tabs, colors, table calculations, reference lines). `ReportIndexPages`, `ReportIndexConditions`, `ReportIndexAggregator`, `IndexedReportDataSet` and `ReportChildRow` (namespace `CrestApps.OrchardCore.Reports.Designer.DataSources`) help YesSql-backed sources.
- **`CrestApps.OrchardCore.Reports`** holds the stores (`ReportDesign`, `ReportGroup`, `ReportView`, `ReportDesignDraft`, `ReportDesignVersion`, `ReportViewSnapshot`), `ReportDesignerPermissions`, `ReportDesignAuthorizationHandler`, `ReportDesignRunner`, the builder pages and JSON endpoints, the **Report views** data source, `ReportSharedLinkHandler`, the view refresh background task, `ReportsHub`, the Custom Report dashboard widget, recipe, deployment and workflow integrations.

A run plans (reads every data set's schema with the principal it reads for), reads (streams only used fields, in batches, pushing down conditions the source can apply), joins (Orchard Core `DataJoin`; text keys ignore case, empty keys never match), calculates the row formulas the query uses, filters and groups (`DataAggregator`; text that differs only in case is one group, shown with its smallest ordinal spelling), shapes (result filters, sorts with `DataSorter`, limits), and renders visuals. Joins, groupings and sorts keep at most `MemoryLimitMegabytes` of rows in memory and spill the rest to temporary files, so memory stays bounded. Data sources return `DateTime` values in UTC; the engine converts them to the tenant time zone before formulas, filters and display. A table visual that lists only some of the result's dimension columns groups by the dimensions it shows and recomputes its measures over the underlying rows, so several visuals of one query can show different levels.

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
| AI chat (`AIChat`) | CrestApps AI Chat | AI Chat; session metrics, **AI completion usage**, **AI voice usage** and **Conversation evaluations** need AI Chat Session Analytics | `ViewChatAnalytics` |
| Messaging (`Messaging`) | CrestApps Omnichannel Messaging | Omnichannel Messaging Workspace | `ViewAllMessagingConversations` |

Contact Center also lists **Agent state changes**, **Dialer decisions** and **Dialer pacing cycles** besides interactions, calls, queues, agents, recordings and voicemail. Contacts, leads, accounts and opportunities are content items, read through the **Content items** source.

The store and other CrestApps modules publish their records the same way, each registered when Data Pipelines Core and the feature that stores the data are on, and each data set gated by the permission of the module's own screens:

| Source (technical name) | Data sets | Read permission |
| --- | --- | --- |
| Orders (`Orders`) | Orders, lines, refunds, disputes, discounts, taxes, returns, gift cards (never codes) | `ManageOrders` (gift cards `ManageGiftCards`) |
| Products (`Products`) | Products, variants, prices, bundle items, attributes | Content permissions of each product type |
| Customers (`Customers`) | Customers, loyalty, store credit, referrals | `ViewCustomers` (referrals `ManageCustomers`) |
| B2B accounts (`B2BAccounts`) | B2B accounts, buyers, price lists and entries (replaces the former Companies source) | `ManageB2BAccounts` |
| Inventory (`Inventory`) | Stock items and levels, movements, reservations, locations, purchase orders, back-in-stock alerts | `ViewInventory` |
| Checkout (`Checkout`) | Checkout sessions, coupons, coupon uses, saved payment methods | `ManageOrders`, `ManageCoupons`, `ManageRefunds` |
| Carts (`Carts`) | Carts, cart lines, wishlists | `ManageOrders` |
| Transactions (`Transactions`) | Transactions, payment attempts, payment refunds | `ManageTransactions` (attempts and refunds `ManageRefunds`) |
| Subscriptions (`Subscriptions`) | Subscriptions, lines, installment plans, installments | `ManageSubscriptions` (plans `ManageInstallmentPlans`) |
| Shipping (`Shipping`) | Shipments, lines, zones, methods | `FulfillOrders` (zones and methods `ManageShipping`) |
| Reviews, Downloads, Marketplace, Point of Sale (`Reviews`, `Downloads`, `Marketplace`, `PointOfSale`) | Product reviews (no text), download entitlements, vendors and payouts, registers and shifts | `ManageProductReviews`, `ManageDigitalDownloads`, `ManageMarketplace`, `ManageRegisters` |
| Phone number verifications (`PhoneNumberVerifications`), DNC registry (`DncRegistry`) | Verifications; DNC lists and entries (without the number) | `RunPhoneNumberVerificationsReport`, `ManageDncRegistrySettings` |

Bookings, Calendars and Scheduling publish sources too (see their own skills). The store sources reference each other (an order joins its customer, account, checkout session and cart; a line joins its product and variant), and every money field has a `Currency` field beside it. Ready-made recipes add adaptable reports in the **Store** report group: `CommerceReportBuilderReports`, `InventoryReportBuilderReports`, `ReviewsReportBuilderReports`, `DownloadsReportBuilderReports`, `SubscriptionsReportBuilderReports` and `ShippingReportBuilderReports`.

- Technical names are stored in designs (`ReportDataSetReference.Source`) and field references; they never change.
- Content items: common fields (`ContentItemId`, `ContentType`, `DisplayText`, `Owner`, `Author`, `Published`, `Latest`, `CreatedUtc`, `ModifiedUtc`, `PublishedUtc`), part values `{Part}.{Property}` (such as `TitlePart.Title`), single-value fields `{Part}.{Field}`, multi-value fields `{Part}.{Field}.{Property}`. Only `Owner` declares a relationship (to `Users.UserId`); join pickers and lists explicitly (`Order.Customer` = `ContentItemId`). Make sensitive content types **Securable**, since `ViewContent` is broad by default.
- Queries run once per report run without parameters, so give parameters defaults.
- Since the content items and users sources are Orchard Core's, a few fields of earlier builder versions are gone (picked items' `DisplayText`, sampled **(more)** properties, custom user settings `Properties.*`, and `UserRoles.Role`, now `RoleName`). Re-pick those fields in the builder.

To add your own data source (record-backed, a YesSql map index through `IndexedReportDataSet`, or external), see [references/custom-data-sources.md](references/custom-data-sources.md).

## Permissions and the owner's-access model

`ReportDesignerPermissions` (namespace `CrestApps.OrchardCore.Reports.Designer`). The Administrator stereotype gets all five; the commerce staff roles **Store manager** and **Marketing** get `ViewAllReportDesigns`:

| Permission | Name in the role editor | Implied by | Security critical |
| --- | --- | --- | --- |
| `ManageAllReportDesigns` | Manage all custom reports and views | | Yes |
| `ManageOwnReportDesigns` | Build reports and manage own custom reports and views | `ManageAllReportDesigns` | No |
| `ViewAllReportDesigns` | View all custom reports | `ManageAllReportDesigns` | No |
| `ManageReportGroups` | Manage report groups | `ManageAllReportDesigns` | No |
| `ShareReportsPublicly` | Share custom reports publicly and through share links | | Yes |

Controllers check a broad permission with the report or view as the **resource**, like content items. `ReportDesignAuthorizationHandler` grants it per item:

- `ViewAllReportDesigns` (run, export, open at `/reports/view/{id}`, clone) on a report goes to its owner, users in `SharedUserNames` (by user name, ignoring case), members of `SharedRoles`, everyone when `SharedRoles` holds `Anonymous`, and every signed-in user when it holds `Authenticated`. On a view it goes to every signed-in user with `ManageOwnReportDesigns`.
- `ManageAllReportDesigns` (edit, drafts, publish, versions, delete, share links, refresh a view) goes to the owner (`OwnerId` matched against the `NameIdentifier` claim) when they hold `ManageOwnReportDesigns`.
- Creating a report or view, the preview and the builder JSON endpoints check `ManageOwnReportDesigns` without a resource.
- Sharing with `Anonymous` and managing share links need `ShareReportsPublicly`. Export also needs the report's `AllowExport`, unless the user can manage it.
- Report groups (`/Admin/reports/groups`) need `ManageReportGroups`; placing a report in a group needs only the right to change the report.

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
| `/Admin/reports/designs` | Designed reports the user can run (`q` searches title and group path, `status=published|unpublished`); an `AdminList` (see `orchardcore-admin-lists`) |
| `/Admin/reports/designs/{id}` and `/Admin/reports/designs/{id}/export/{format}` | Run and export in the admin (`csv`, `xlsx` with Reports (OpenXml), `pdf` with Reports (PDF)) |
| `/Admin/reports/builder/create`, `/Admin/reports/builder/edit/{id}` | The builder |
| `/Admin/reports/views`, `/Admin/reports/views/create`, `/Admin/reports/views/edit/{id}` | Reusable views |
| `/Admin/reports/groups`, `/Admin/reports/groups/create` (`parentId` nests), `/Admin/reports/groups/edit/{id}` | Report groups |
| `/reports/view/{id}` | A shared report outside the admin |
| `/SharedLinks/{linkId}?token=...` | A report opened through a share link |

Run and export pages read exposed filter values from the query string: `applied=1`, then `f.{filterId}` (repeatable) or `f.{filterId}.from` and `f.{filterId}.to` for ranges. Click-to-filter picks travel as `sel.{visualId}` (one per value), and `rv` (comma-separated visual ids) refreshes only those visuals. The builder's JSON endpoints under `/Admin/reports/builder/api/` (`sources`, `datasets`, `schema`, `functions`, `plan`, `users`) and the save, draft and link endpoints are internal and may change.

Admin menu: **Report Builder** (designers), **Shared Reports** (people who can run at least one report), **Report Views** (designers), **Report Groups** (`ManageReportGroups`), and every report with `ShowInAdminMenu` for people allowed to run it, nested under its group's menu item (or directly under **Reports** when it has no group). A group appears only when it leads to a pinned report the user may run.

## Report groups

Designed reports are listed under nested, ordered `ReportGroup`s (`ItemId`, `DisplayText`, `ParentId`, `Order`) instead of the former free-text category; `ReportDesign.GroupId` names the group (`ReportDesign.Category` is obsolete and read only to migrate). Two groups under one parent cannot share a name, a group cannot be moved under itself or a descendant, and a group that still holds groups or reports cannot be deleted. On upgrade each category becomes a top-level group. Groups travel with reports through clones, drafts, versions, the deployment step and the recipe step.

## Columns, rows and colors

`ReportColumnDefinition` gains, besides `id`, `field`, `label`, `aggregate`, `transform`, `format` and `hidden`:

| Property | Values | Effect |
| --- | --- | --- |
| `shelf` (`ReportColumnShelf`) | `Columns` (default), `Rows` | A dimension on **Rows** turns the report's table into a cross-tab: row dimensions down the side, column dimensions spread across the top (`ReportSection.HeaderRows`), measures in the cells, with row and column totals. Measures moved to **Rows** are listed one per row. Pivot visuals take several values the same way. |
| `colors` (`ReportColumnColors`) | `headerColor`, `headerBackgroundColor`, `color`, `backgroundColor`, `scale` (`ReportColorScale`: `lowColor`, `midColor`, `highColor`, `target`), `rules` (`ReportColorRule`: `operator` as a `DataFilterOperator`, `values`, `color`, `target`) | `target` is `Background` or `Text` (`ReportColorTarget`). Rules win over the scale, the scale over cell colors; the first rule a value meets wins. Filled cells get white or dark text. Totals keep their own style. Colors reach Excel, PDF and workflow emails, never CSV. |
| `showAs` (`ReportShowAs`) | `Value` (default), `PercentOfTotal`, `RunningTotal`, `DifferenceFromPrevious`, `PercentDifference`, `Rank` | A quick table calculation over the values each table, cross-tab and chart shows, in display order. Totals and subtotals keep plain aggregates. Percentages default to `P1`, ranks to `N0`; a header-less column is named after the calculation (*% of total ...*, *Rank of ...*). Color rules compare the shown value, so 20% is `0.2`. |
| `showAsDirection` (`ReportShowAsDirection`) | `Down` (default), `Across`, `Table` | In a cross-tab, down each column, across each row, or over the whole table; in a chart split into series, along each series or across the series of each category. |

Quick formulas: the **Data** pane's field menu (**New calculated field**) and the formula window's **ƒ** button wrap a field in a common formula for its type (Sum, Average, Minimum, Maximum, Median, Count, Count (distinct), Round, Upper case, Year, Start of the month, Days until today); the function list is grouped and searchable, and **Custom formula…** accepts any formula. A numeric calculated field is added with the **Sum** aggregate; set **Aggregate** to **None** to show each row's value. Dragging a measure onto **Filters** adds a result filter (**is at least**).

Bar, line and area charts can draw up to 10 `referenceLines` (`ReportReferenceLine`: `columnId`, `kind` of `Average`, `Median` or `Constant` (`ReportReferenceLineKind`), `value` for a constant, `label`). They are drawn dashed on screen and in PDF, carried to renderers by `ReportChart.ReferenceLines` (`ReportChartReferenceLine`); pie and doughnut charts draw none.

## Interactive dashboards

A design can hold several queries, containers and click actions:

| Member | Holds |
| --- | --- |
| `query` | The main query; always runs; its rows are the report's rows |
| `queries` (`ReportNamedQuery`: `id`, `name`, `query`) | Other queries; one runs only when a visual shows it. `GetQueries()` lists the main query first, with no id. A failing extra query is a warning naming it. |
| `visuals[].queryId` | The query a visual shows (`null` for the main query) |
| `containers` (`ReportContainerDefinition`: `id`, `title`, `width` on 12 columns, `height` in pixels, `0` grows, at most `MaxHeight` 2000) | Cards that frame visuals under a title band and scroll them, with table headers kept in view |
| `visuals[].containerId` | The container a visual sits in |
| `visuals[].action` (`ReportVisualAction`: `columnId`, `targets` of `ReportActionTarget` `visualId` and optional `field`) | Clicking a value of `columnId` (a table column it shows, or a chart's category or series column) filters the target visuals; Ctrl+click picks several |

`ReportDesignNormalizer` drops queries and containers without an id or with a duplicate id, clamps sizes, and gives every visual its own id on save, import and preview. Viewer filters with the same `id` in several queries are linked and shown as one control. A click adds a row filter to a copy of each target's query (`Equals`/`In` for plain, Upper or Lower values; `Between` for dates kept to Day, Week, Month, Year or Hour; `IsEmpty` for a missing value; other transforms cannot filter). Picks are kept in filters, exports (Excel and PDF write a "Filtered by" line) and the page address; only the filtered visuals reload. A target that names a missing visual is a warning. Excel writes each container to its own worksheet; PDF puts each container's title above its visuals.

The design JSON for these members is in [references/designs-recipes-and-workflows.md](references/designs-recipes-and-workflows.md).

## Custom Report dashboard widget

With Orchard Core's **Admin Dashboard** also enabled, `DesignWidgetsStartup` (Report Builder feature) adds the **Custom Report** widget (`ReportDesignWidget` type, `ReportDesignWidgetPart`: `ReportId` or `ViewId`, `Period`, `From`, `To`, `Show` default `Everything`, `MaxRows` default 10, `CacheMinutes` default 30). It shows a **published** report (a view as one table). The period replaces, for that run only, the report's first exposed filter on a `Date` or `DateTime` field with a `DateRange` or `RelativeDate` control (and linked filters in other queries); **All time** turns it off, fixed filters are never replaced, and a report with no such filter keeps its own dates (**Its own dates**). `ViewAllReportDesigns` on the report or view is checked on every visit before the cache. A report's cached result is shared by everyone who may run it (it runs with the owner's access); a view's is keyed by viewer. The widget does not raise the report-run workflow event. The generic report widget for coded reports is covered in `orchardcore-reports`.

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
            "MaxConcurrentRuns": 4,
            "MaxSharedReadRows": 50000
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
| `MaxSharedReadRows` | `50000` | Rows of one data set a run keeps in memory to share between the queries that read it; `0` reads the data set once per query |
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

- **Only the calculated fields a query uses.** A query computes only the calculated fields its columns and filters use (and the fields those read), so dashboard queries copied with the same set of calculated fields stay cheap, and a query that uses no row-level calculated field can again be grouped by its source. Every calculated field is still checked in the builder.
- **Shared reads.** The queries of one run plan before any runs; queries reading the same data set share one read (the first reads every field any of them needs and keeps up to `MaxSharedReadRows` rows), and a query whose row filters include all of a kept read's filters, such as one a click filters, reads the kept rows. Queries run one after another, since data sources may share the request's database session.
- **Preview.** The builder preview cancels a preview still running when it starts another, shows how long it has run, and offers **Try again** on failure.

Checklist for a large data set: set a main date and keep a date filter, index the main date and join keys and mark keys `IsKeyFilterable`, implement grouping in the source, schedule heavy shared views, read pages with `ReportIndexPages`, and raise limits only after that.

### Telemetry

Report runs emit OpenTelemetry spans and metrics through the `ActivitySource` and `Meter` named `CrestApps.OrchardCore.Reports` (`ReportTelemetry.SourceName` and `ReportTelemetry.MeterName`, namespace `CrestApps.OrchardCore.Reports.Designer`). Nothing is recorded while nobody listens. Add them in the web host's `Program.cs` (or an Aspire service defaults project):

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("CrestApps.OrchardCore.Reports"))
    .WithMetrics(metrics => metrics.AddMeter("CrestApps.OrchardCore.Reports"));
```

- Spans: `report.run`, `report.plan`, `report.query`, `report.read`, `report.join`, `report.result`, `report.render`. Each carries `crestapps.report.id`, `crestapps.report.title`, `crestapps.report.source` (`Admin`, `Shared`, `Link`, `Preview` or `Other`) and `crestapps.report.query.id`; for example `report.query` adds `crestapps.report.gate_wait_ms` (waiting for `MaxConcurrentRuns`) and `crestapps.report.spills`, and `report.read` adds `crestapps.report.read_shared`.
- Metrics: `crestapps.report.run.duration` and `crestapps.report.query.duration` (histograms, seconds), and the counters `crestapps.report.rows.read`, `crestapps.report.reads.shared`, `crestapps.report.rows.returned`, `crestapps.report.run.failures` (cancellations excluded) and `crestapps.report.spills`.

## Drafts, versions and real time

- The builder autosaves each change into a `ReportDesignDraft` (`POST /Admin/reports/builder/{id}/draft`, with `keepalive`). A new report exists only as a draft (listed **Not published** for its owner and managers) until first published.
- Every draft save, publish, discard and restore increases the report's revision. A change based on an older revision gets `409 Conflict` with the name of whoever changed it; the page offers to reload or force. Changes are serialized with `IDistributedLock`, so this holds across nodes.
- Publishing (`POST /Admin/reports/builder/save`) adds an immutable `ReportDesignVersion` only when content changed and clears the draft. Versions can be listed, previewed and restored into the draft. At most `MaxVersions` are kept. Drafts and versions use their own index tables (`ReportDesignDraftIndex`, `ReportDesignVersionIndex`). Views have no drafts or versions.
- With `OrchardCore.SignalR` enabled, `ReportsRealTimeStartup` maps `ReportsHub` and replaces `IReportDesignNotifier` with `SignalRReportDesignNotifier`: pages show who else has a report open, and `ReportDesignChanged` (`kind`, `designId`, `revision`, `versionNumber`, `userName`) offers a reload. Groups are tenant-qualified, so it works behind a Redis or Azure SignalR backplane. Replace `IReportDesignNotifier` to react to changes in other ways (`ReportDesignChangeKind`: `DraftSaved`, `DraftDiscarded`, `Published`, `Restored`, `Deleted`).

## Recipes, deployment and workflows

- The **Designed Reports and Views** deployment step (`ReportDesignsDeploymentStep`, `IncludeAll`, `ReportIds`, `ViewIds`) exports every or picked report and view, plus the views any of their queries read and the report groups they sit in (with their parents, parents first), with each owner's user name.
- The `ReportDesigns` recipe step imports `Groups`, then `Views`, then `Reports`, matched by `itemId`, with owners matched by `OwnerUserName`. An item naming no known owner and no `ownerId` belongs to the person running the recipe (no owner when run at setup with nobody signed in). A report names its group by `groupId`, by a `group` path such as `"Sales / Regional"` (found by name ignoring case, created when missing), or by a legacy `category`.
- With `OrchardCore.Workflows`, the builder adds `ReportRunEvent`, `ReportExportedEvent`, `ReportPublishedEvent`, `RunReportTask`, `ExportReportTask`, `CreateReportShareLinkTask`, and (with `OrchardCore.Email`) `EmailReportTask`. The tasks export and attach `csv`, `xlsx` or `pdf` as their features allow, and emailed tables carry cross-tab header rows and column colors. Code can react without Workflows through `IReportEventHandler` (`ReportEventHandlerBase`). The **Email Report** task is the way to email a designed report; Report Subscriptions cover coded reports only.

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
- **"A click names a visual the report no longer has"** warning: a click action targets a deleted visual (often after an import); pick the targets again.
- **A query of a dashboard shows a warning and no rows**: an extra query failed to plan; the main query's problems still stop the report.
- **Imported report is in no group**: its `groupId` is unknown and it has no `group` or `category`.
- **A widget shows "Its own dates"**: the report has no exposed date-range or relative-date filter for the widget's period to replace.
- **Slow dashboards**: trace with the `CrestApps.OrchardCore.Reports` spans; check `report.read` counts against `crestapps.report.read_shared`, and keep data sets under `MaxSharedReadRows` with a date filter.

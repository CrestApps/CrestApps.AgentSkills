---
name: orchardcore-report-builder
description: Skill for the CrestApps Orchard Core Report Builder (CrestApps.OrchardCore.Reports.Builder), the drag-and-drop report designer on Orchard Core Data Pipelines Core. Covers ReportDesign, ReportView and ReportGroup, built-in and custom data sources (IndexedReportDataSet, ReportIndexPages), joins, formulas, exposed filters, cross-tabs, color rules, ShowAs calculations, charts and reference lines, interactive dashboards with click-to-filter, CSV, Excel and PDF exports, ReportDesignerPermissions and the owner's-access model, share links, drafts and versions, scheduled views, the Custom Report widget, ReportQueryLimits, OpenTelemetry metrics, the ReportDesigns recipe and deployment steps, workflow tasks and IReportEventHandler. Use this skill when requests mention the Report Builder, designed reports, report dashboards, views or groups, or report data sources. Strong matches include ReportQueryDefinition, ReportTelemetry and ReportDesignAuthorizationHandler.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Report Builder

## Designed reports on Orchard Core data sources

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the **Report Builder** feature of the CrestApps Reports module. People design reports in the browser: add data sets from any data source, join them (inner, left, right, full), add calculated fields, filter rows or results (and let viewers change chosen filters), group and aggregate, lay dimensions on the **Columns** and **Rows** shelves (cross-tabs), color columns, show measures as quick table calculations, and show tables, charts, headline metrics and pivot tables. A report can be an interactive dashboard with several queries, containers and visuals that filter each other when clicked. Designed reports render through the same renderer and export formats (CSV, Excel, PDF) as coded reports (see `orchardcore-reports`).

### Guidelines

- The builder is a feature of the `CrestApps.OrchardCore.Reports` package (no separate package). Install it, plus `CrestApps.OrchardCore.Reports.OpenXml` and `CrestApps.OrchardCore.Reports.Pdf` for Excel and PDF, in the web/startup project.
- The data source contract, formula language and share links are Orchard Core 4.0's **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`); see `orchardcore-data-sources`, `orchardcore-data-formulas` and `orchardcore-data-pipelines`. Enabling the builder enables it.
- A saved report reads data with its **owner's** current access, whoever runs it. Data sources are the security boundary: they must hide every data set `DataSourceContext.User` may not read.
- Technical names of data sources, data sets and fields are stored in designs; never rename them.
- Move designs between sites with the **Designed Reports and Views** deployment step and the `ReportDesigns` recipe step; build the report on a working site and export it to get an exact shape.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models. Mark any instance `LocalizationSource` property `[JsonIgnore]`.

### Feature

| Feature | ID | Depends on |
| --- | --- | --- |
| Report Builder | `CrestApps.OrchardCore.Reports.Builder` (`ReportsConstants.BuilderFeature`) | `CrestApps.OrchardCore.Reports`, `OrchardCore.DataPipelines.Core` |

Optional integrations, each wired by a startup that requires the feature: `OrchardCore.Recipes.Core` (recipe step), `OrchardCore.Deployment` (deployment step), `OrchardCore.Workflows` and `OrchardCore.Email` (workflow activities, share link emails), `OrchardCore.SignalR` (presence and reload offers), `OrchardCore.AdminDashboard` (Custom Report widget), `CrestApps.OrchardCore.Reports.OpenXml` and `CrestApps.OrchardCore.Reports.Pdf` (exports).

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

A module that adds a data source references `OrchardCore.DataSources.Abstractions` (contract), `OrchardCore.DataSources.Core` (`AddDataSource`), `CrestApps.OrchardCore.Reports.Abstractions` (field helpers, `ReportsConstants`, `CommerceReportDataSets`) and, for the YesSql helpers, `CrestApps.OrchardCore.Reports.Core`.

## Architecture

- **`CrestApps.OrchardCore.Reports.Abstractions`**: `DataFieldExtensions` (`Identifier`, `MarkKeyFilterable`), `ReportDataReferences` (`User()`, `ContentItem(type)`) and `CommerceReportDataSets` (namespace `CrestApps.OrchardCore.Reports.DataSources`), and `ReportsConstants` (`ViewsDataSource = "ReportViews"`, `UsersDataSource`, `UsersDataSet`, `UserIdField`, `ContentsDataSource`, `ContentItemIdField`, `SharedLinkKind = "Reports.Report"`).
- **`CrestApps.OrchardCore.Reports.Core`** (namespace `CrestApps.OrchardCore.Reports.Designer`): the design model (`ReportQueryDefinition`, `ReportDataSetReference`, `ReportJoinDefinition`, `ReportCalculatedField`, `ReportFilterDefinition`, `ReportColumnDefinition`, `ReportSortDefinition`, `ReportVisualDefinition`, `ReportNamedQuery`, `ReportContainerDefinition`, `ReportVisualAction`, `ReportColumnColors`, `ReportReferenceLine`), the engine (`ReportQueryPlanner` checks a query against live schemas and compiles formulas, `ReportQueryEngine` runs it, `ReportDesignDocumentBuilder` turns the result into a `ReportDocument`), `ReportQueryLimits`, `ReportQueryGate`, `ReportTelemetry`, and the YesSql helpers `IndexedReportDataSet`, `ReportIndexPages`, `ReportIndexConditions`, `ReportIndexAggregator`, `ReportChildRow` (namespace `CrestApps.OrchardCore.Reports.Designer.DataSources`).
- **`CrestApps.OrchardCore.Reports`** (the module): catalogs of `ReportDesign`, `ReportGroup` and `ReportView`; YesSql documents `ReportDesignDraft`, `ReportDesignVersion` and `ReportViewSnapshot` with `ReportDesignDraftIndex`, `ReportDesignVersionIndex` and `ReportViewSnapshotIndex` (each declared `.ExclusiveToDocumentType().SingleRecordPerDocument()`); permissions and `ReportDesignAuthorizationHandler`; `ReportDesignRunner`, the builder pages and JSON endpoints; the **Report views** data source; `ReportSharedLinkHandler`; the view refresh background task; `ReportsHub`; the Custom Report widget; recipe, deployment and workflow integrations.

A run plans (reads each data set's schema with the principal it reads for), reads (streams only used fields in batches, pushing down conditions the source can apply), joins (Orchard Core `DataJoin`; text keys ignore case, empty keys never match), calculates the row formulas the query uses, filters and groups (`DataAggregator`; text differing only in case is one group), shapes (result filters, sorts, limits) and renders visuals. Memory stays bounded: joins, groupings and sorts spill past `MemoryLimitMegabytes` to temporary files. Data sources return `DateTime` values in UTC; the engine converts them to the tenant time zone before formulas, filters and display (`Date` fields are never shifted). A table visual that lists only some dimension columns groups by the ones it shows and recomputes its measures over the underlying rows.

## Data sources

The builder lists every data source `IDataSourceManager` knows and the data sets the principal may read. Each CrestApps source is registered by a startup marked `[RequireFeatures("OrchardCore.DataPipelines.Core")]` together with the feature that stores the data, so data pipelines read them too.

| Source (technical name) | Provided by | Read permission |
| --- | --- | --- |
| Report views (`ReportViews`) | Report Builder | `ViewAllReportDesigns` on the view (every designer) |
| Content items (`Contents`) | Orchard Core, with `OrchardCore.Contents` | `ViewContent` per type; `EditContent` reads drafts |
| Users (`Users`: `Users`, `UserRoles`) | Orchard Core, with `OrchardCore.Users` | `ViewUsers` |
| Queries (`Queries`) | Orchard Core, with `OrchardCore.Queries` | `ExecuteApi_{QueryName}` (implied by `ExecuteApiAll`, `ManageQueries`) |
| Omnichannel (`Omnichannel`) | Omnichannel Activities | `ViewOmnichannelReports` |
| Contact Center (`ContactCenter`) | The Contact Center feature storing each data set | `ViewContactCenterReports` (+ `ListenToAllCallRecordings`, `AccessContactCenterSharedVoicemail`) |
| AI chat (`AIChat`) | AI Chat; metrics, **Completion usage**, **Voice usage** need AI Chat Session Analytics | `ViewChatAnalytics` |
| Messaging (`Messaging`) | Omnichannel Messaging Workspace | `ViewAllMessagingConversations` |

The store sources (data sets named in `CommerceReportDataSets`) and their permissions:

| Source (technical name) | Data sets | Read permission |
| --- | --- | --- |
| Orders (`Orders`) | Orders, lines, refunds, disputes, discounts, taxes, returns, gift cards (never codes) | `ManageOrders` (gift cards `ManageGiftCards`) |
| Products (`Products`) | Products, variants, prices, bundle items, attributes | Content permissions of each product type |
| Customers (`Customers`) | Customers, loyalty, store credit, referrals | `ViewCustomers` (referrals `ManageCustomers`) |
| B2B accounts (`B2BAccounts`) | B2B accounts, buyers, price lists and entries | `ManageB2BAccounts` |
| Inventory (`Inventory`) | Stock items and levels, movements, reservations, locations, purchase orders, back-in-stock alerts | `ViewInventory` |
| Checkout (`Checkout`) | Checkout sessions, coupons, coupon uses, saved payment methods | `ManageOrders`, `ManageCoupons`, `ManageRefunds` |
| Carts (`Carts`) | Carts, cart lines, wishlists | `ManageOrders` |
| Transactions (`Transactions`) | Transactions, payment attempts, payment refunds | `ManageTransactions` (attempts and refunds `ManageRefunds`) |
| Invoicing (`Invoicing`) | Invoices, invoice lines, invoice taxes, credit notes, estimates, recurring plans | `ViewInvoices` |
| Subscriptions (`Subscriptions`) | Subscriptions, lines, installment plans, installments | `ManageSubscriptions` (plans `ManageInstallmentPlans`) |
| Shipping (`Shipping`) | Shipments, lines, zones, methods | `FulfillOrders` (zones and methods `ManageShipping`) |
| `Reviews`, `Downloads`, `Bookings`, `Marketplace`, `PointOfSale` | Reviews (no text), download entitlements, bookings, vendors and payouts, registers and shifts | `ManageProductReviews`, `ManageDigitalDownloads`, `ManageBookings`, `ManageMarketplace`, `ManageRegisters` |
| `PhoneNumberVerifications`, `DncRegistry` | Verifications; DNC lists and entries (without the number) | `RunPhoneNumberVerificationsReport`, `ManageDncRegistrySettings` |

Other CrestApps modules publish sources the same way, each gated by its own screens' permissions (see the module's skill): `Calendars`, `Scheduling`, `Tickets`, `Timeline`, `ServiceLevels`, `KnowledgeBase`, `Approvals`, `Banking`, `Checks`, `Disbursements`, `Payroll`, `FieldService`, `Staff`, `Skills`, `Workforce`, the learning sources (`Learning`, `LearningAssessments`, `LearningCertifications`, `LearningCommerce`, `LearningCoursework`, `Learning.Credentials`, `LearningPrograms`, `Learning.Xapi`), and the Data Connectors sources `ConnectedData` (live) and `ImportedData` (see `orchardcore-data-connectors`).

- Related data sets join automatically through field references (an order to its customer, a line to its product); every money field has a `Currency` field beside it. Ready-made recipes add adaptable reports in a **Store** report group (`CommerceReportBuilderReports`, `InventoryReportBuilderReports`, `ReviewsReportBuilderReports`, `DownloadsReportBuilderReports`, `SubscriptionsReportBuilderReports`, `ShippingReportBuilderReports`).
- Content items: common fields (`ContentItemId`, `ContentItemVersionId`, `ContentType`, `DisplayText`, `Owner`, `Author`, `Published`, `Latest`, `CreatedUtc`, `ModifiedUtc`, `PublishedUtc`), part values `{Part}.{Property}`, single-value fields `{Part}.{Field}`, multi-value fields `{Part}.{Field}.{Property}`. Only `Owner` declares a relationship (to `Users.UserId`); join pickers explicitly (`Order.Customer` = `ContentItemId`). Make sensitive types **Securable**.
- Queries run once per report run without parameters, so give parameters defaults.
- Fields of earlier builder versions that are gone: picked items' `DisplayText`, sampled **(more)** properties, custom user settings `Properties.*`, and `UserRoles.Role` (now `RoleName`). Re-pick them in the builder.

To add your own data source (record-backed, over a YesSql map index with `IndexedReportDataSet`, or external), see [references/custom-data-sources.md](references/custom-data-sources.md).

## Designer

The builder fills the window: title, save state, **Versions**, **Run report** and **Publish** at the top, and four tabs: **Settings** (title, description, group, pin to menu, allow export), **Design** (the **Data** pane, the **Joins**, **Columns**, **Rows** and **Filters** shelves with **Sort** and **Top rows**, the live **Preview**, and the **Properties and visuals** pane), **Data model** (data sets as cards, joined by dragging one column onto another) and **Sharing** (people, roles, share links).

### Columns, rows and colors

`ReportColumnDefinition` has `id`, `field`, `label`, `aggregate` (`DataAggregate`), `transform` (`DataFieldTransform`), `format` (.NET format string), `hidden`, and:

| Property | Values | Effect |
| --- | --- | --- |
| `shelf` (`ReportColumnShelf`) | `Columns` (default), `Rows` | A dimension on **Rows** turns the table into a cross-tab: row dimensions down the side, column dimensions across the top (`ReportSection.HeaderRows`), measures in the cells, with row and column totals. Measures moved to **Rows** are listed one per row. Pivot visuals take several values the same way. |
| `colors` (`ReportColumnColors`) | `headerColor`, `headerBackgroundColor`, `color`, `backgroundColor`, `scale` (`lowColor`, `midColor`, `highColor`, `target`), `rules` (`operator` as a `DataFilterOperator`, `values`, `color`, `target`) | `target` is `Background` or `Text`. Rules win over the scale, the scale over cell colors; the first matching rule wins. Colors reach Excel, PDF and workflow emails, never CSV. |
| `showAs` (`ReportShowAs`) | `Value`, `PercentOfTotal`, `RunningTotal`, `DifferenceFromPrevious`, `PercentDifference`, `Rank` | A quick table calculation over the values each table, cross-tab and chart shows, in display order. Totals keep plain aggregates. Percentages default to `P1`, ranks to `N0`. Color rules compare the shown value (20% is `0.2`). |
| `showAsDirection` (`ReportShowAsDirection`) | `Down`, `Across`, `Table` | Direction in a cross-tab; in a chart split into series, along each series or across the series of each category. |

Every shelf pill has a menu (dimension or measure with any aggregate its type allows, transform, sort, filter, move shelf, format and colors, hide, remove). The **Data** pane's field menu and the formula window's **ƒ** button wrap a field in a common formula (Sum, Average, Minimum, Maximum, Median, Count, Count (distinct), Round, Upper case, Year, Start of the month, Days until today); **Custom formula…** accepts any formula. Dragging a measure onto **Filters** adds a result filter (**is at least**). The formula language is in [references/formulas.md](references/formulas.md).

### Filters

`ReportFilterDefinition`: `id`, `field`, `stage` (`Rows` before grouping, `Result` on a column id after it), `operator` (`DataFilterOperator`), `values`, `exposed`, `label`, `control` (`Auto`, `Text`, `Select`, `MultiSelect`, `DateRange`, `NumberRange`, `Boolean`, `RelativeDate`). A new report on a data set with a main date starts with an exposed `RelativeDate` filter on the last 30 days. Viewers change only `exposed` filters; fixed filters are neither changeable nor visible. Filters with the same `id` in several queries are linked and shown as one control. Drop-downs list at most `MaxFilterOptions` values.

### Visuals

`ReportVisualDefinition.type` is `Table`, `Chart`, `Metrics` (headline numbers) or `Pivot`. Charts take `chartType` (`Line`, `Bar`, `Doughnut`, `Pie`, `Area`, `HorizontalBar`), `categoryColumnId`, `seriesColumnId`, `valueColumnIds`, `stacked`, `showLegend`. Tables take `columnIds`, `showTotals`, `showSubtotals` (a subtotal after each group of the leading dimensions, innermost first). Bar, line and area charts can draw up to 10 `referenceLines` (`columnId`, `kind` of `Average`, `Median` or `Constant`, `value`, `label`), drawn dashed on screen and in PDF and carried by `ReportChart.ReferenceLines`.

### Interactive dashboards

| Member | Holds |
| --- | --- |
| `query` | The main query; always runs |
| `queries` (`ReportNamedQuery`: `id`, `name`, `query`) | Other queries; one runs only when a visual shows it. A failing extra query is a warning naming it. |
| `visuals[].queryId` | The query a visual shows (`null` for the main query) |
| `containers` (`ReportContainerDefinition`: `id`, `title`, `width` on 12 columns, `height` in pixels, `0` grows, at most 2000) | Cards that frame and scroll visuals, with table headers kept in view |
| `visuals[].containerId` | The container a visual sits in |
| `visuals[].action` (`ReportVisualAction`: `columnId`, `targets` of `visualId` and optional `field`) | Clicking a value filters the target visuals; Ctrl+click picks several |

`ReportDesignNormalizer` drops queries and containers without an id or with a duplicate id, clamps sizes, and gives every visual its own id on save, import and preview. A click adds a row filter to a copy of each target's query (`Equals`/`In`, `Between` for date buckets, `IsEmpty` for a missing value). Picks are kept in filters, exports (Excel and PDF write a "Filtered by" line) and the page address; only filtered visuals reload. Excel writes each container to its own worksheet; PDF puts each container's title above its visuals.

The full design JSON is in [references/designs-recipes-and-workflows.md](references/designs-recipes-and-workflows.md).

## Permissions and the owner's-access model

`ReportDesignerPermissions` (namespace `CrestApps.OrchardCore.Reports.Designer`). The Administrator stereotype gets all five; the commerce staff roles **Store manager** and **Marketing** get `ViewAllReportDesigns`.

| Permission | Name in the role editor | Implied by | Security critical |
| --- | --- | --- | --- |
| `ManageAllReportDesigns` | Manage all custom reports and views | | Yes |
| `ManageOwnReportDesigns` | Build reports and manage own custom reports and views | `ManageAllReportDesigns` | No |
| `ViewAllReportDesigns` | View all custom reports | `ManageAllReportDesigns` | No |
| `ManageReportGroups` | Manage report groups | `ManageAllReportDesigns` | No |
| `ShareReportsPublicly` | Share custom reports publicly and through share links | | Yes |

Controllers check a broad permission with the report or view as the **resource**. `ReportDesignAuthorizationHandler` grants it per item:

- `ViewAllReportDesigns` (run, export, open at `/reports/view/{id}`, clone) on a report goes to its owner, users in `SharedUserNames` (ignoring case), members of `SharedRoles`, everyone when `SharedRoles` holds `Anonymous`, and every signed-in user when it holds `Authenticated`. On a view it goes to every signed-in user with `ManageOwnReportDesigns`.
- `ManageAllReportDesigns` (edit, drafts, publish, versions, delete, share links, refresh a view) goes to the owner (`OwnerId` matched against the `NameIdentifier` claim) when they hold `ManageOwnReportDesigns`.
- Creating a report or view, the preview and the builder JSON endpoints check `ManageOwnReportDesigns` without a resource.
- Sharing with `Anonymous` and managing share links need `ShareReportsPublicly`. Export also needs the report's `AllowExport`, unless the user can manage it.

```csharp
if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ViewAllReportDesigns, design))
{
    return Forbid();
}
```

When the owner is deleted, disabled, or loses access to a data set, the report stops with a message asking someone to clone and re-share it (`crestapps.report.run.rejected` with `reason=owner_missing`). The builder preview reads with the designer's own access; scheduled views refresh with their owner's access.

### Share links

Orchard Core shared links (`ISharedLinkManager`) of kind `Reports.Report`, opened by `ReportSharedLinkHandler` at `/SharedLinks/{linkId}?token=...`.

- Recipients are named users (user name or email); only a signed-in recipient with the token opens it. Wrong token `404`, expired, used-up or revoked `410`, non-recipient `403`. The report must be published.
- Tokens hold 256 random bits, only their SHA-256 hash is stored, and the URL is shown once. `ExpiresAfterDays` is at least 1; `MaxUses` (`0` no limit) counts opens, filter changes and exports; `Note` and `AllowExport` are stored on the link. `NotifyByEmail` (default) needs the Email feature.
- **Tools → Shared Links** (Orchard Core `ManageSharedLinks`) lists and revokes links of every module. Deleting a report deletes its links; deployments never export them.

## Admin menu and pages

Everything sits under the top-level **Reports** menu (from `ReportDesignerAdminMenu`):

| Menu item | Shown to |
| --- | --- |
| **Reports → Report Builder** | Designers (`ManageOwnReportDesigns`); people who can only run reports see it as **Reports → Shared Reports** once one is shared with them |
| **Reports → Report Views** | Designers |
| **Reports → Report Groups** | `ManageReportGroups` |
| **Reports → {group} → … → {report}** | Each report with `ShowInAdminMenu` the user may run, under its nested group path (directly under **Reports** when it has no group); a group shows only when it leads to such a report |

| URL | Purpose |
| --- | --- |
| `/Admin/reports/designs` | Designed reports the user can run (`q` searches title and group path, `status=published|unpublished`) |
| `/Admin/reports/designs/{id}`, `/Admin/reports/designs/{id}/export/{format}` | Run and export (`csv`; `xlsx` with Reports (OpenXml); `pdf` with Reports (PDF)) |
| `/Admin/reports/builder/create`, `/Admin/reports/builder/edit/{id}` | The builder |
| `/Admin/reports/views`, `/Admin/reports/views/create`, `/Admin/reports/views/edit/{id}` | Reusable views |
| `/Admin/reports/groups`, `/Admin/reports/groups/create` (`parentId` nests), `/Admin/reports/groups/edit/{id}` | Report groups |
| `/reports/view/{id}`, `/reports/view/{id}/export/{format}` | A shared report outside the admin |
| `/SharedLinks/{linkId}?token=...` | A report opened through a share link |

Run and export pages read exposed filter values from the query string: `applied=1`, then `f.{filterId}` (repeatable) or `f.{filterId}.from` and `f.{filterId}.to`. Click picks travel as `sel.{visualId}`, and `rv` (comma-separated visual ids) refreshes only those visuals. The JSON endpoints under `/Admin/reports/builder/api/` (`sources`, `datasets`, `schema`, `functions`, `plan`, `users`), the save, draft, version and link endpoints are internal and may change.

## Report groups

Designed reports sit in nested, ordered `ReportGroup`s (`ItemId`, `DisplayText`, `ParentId`, `Order`); `ReportDesign.GroupId` names the group (`ReportDesign.Category` is obsolete, read only to migrate). Two groups under one parent cannot share a name, a group cannot move under itself or a descendant, and a group that still holds groups or reports cannot be deleted. Placing a report in a group needs only the right to change the report. Groups travel with reports through clones, drafts, versions, the deployment step and the recipe step.

## Exports

Designed reports export through the registered `IReportExportFormat`s: `csv` (built in), `xlsx` (Reports (OpenXml), one worksheet per container, styles applied, chart values as numbers) and `pdf` (Reports (PDF), laid out like the page with colors, reference lines and container title bands, drawn with the CrestApps PDF building block over PdfPig). Export requires `ViewAllReportDesigns` on the report and its `AllowExport`, unless the user manages it. Workflow tasks and share links export the same formats. PDF font and layout rules are in `orchardcore-reports`.

## Custom Report dashboard widget

With Orchard Core's **Admin Dashboard** enabled, `DesignWidgetsStartup` adds the **Custom Report** widget (`ReportDesignWidget` type, `ReportDesignWidgetPart`: `ReportId` or `ViewId`, `Period`, `From`, `To`, `Show` default `Everything`, `MaxRows` default 10, `CacheMinutes` default 30). It shows a **published** report (a view as one table). The period replaces, for that run only, the report's first exposed filter on a `Date` or `DateTime` field with a `DateRange` or `RelativeDate` control; **All time** turns it off, fixed filters are never replaced, and a report with no such filter keeps its own dates (**Its own dates**). `ViewAllReportDesigns` on the report or view is checked on every visit before the cache. A report's cached result is shared by everyone who may run it (owner's access); a view's is keyed by viewer. The widget does not raise the report-run workflow event.

## Drafts, versions and real time

- The builder autosaves each change into a `ReportDesignDraft` (`POST /Admin/reports/builder/{id}/draft`). A new report exists only as a draft (listed **Not published** for its owner and managers) until first published.
- Every draft save, publish, discard and restore increases the revision. A change based on an older revision gets `409 Conflict` naming who changed it; the page offers **Reload their changes** or **Keep mine**. Changes are serialized with `IDistributedLock` across nodes.
- Publishing (`POST /Admin/reports/builder/save`) adds an immutable `ReportDesignVersion` only when content changed and clears the draft. Versions can be previewed and restored into the draft; at most `MaxVersions` are kept. Views save directly, without drafts or versions.
- With `OrchardCore.SignalR`, `ReportsRealTimeStartup` maps `ReportsHub` and replaces `IReportDesignNotifier` with `SignalRReportDesignNotifier`: pages show who else has a report open and offer a reload on `ReportDesignChanged` (`kind`, `designId`, `revision`, `versionNumber`, `userName`). Groups are tenant-qualified (works behind a Redis or Azure SignalR backplane). Replace `IReportDesignNotifier` to react otherwise (`ReportDesignChangeKind`: `DraftSaved`, `DraftDiscarded`, `Published`, `Restored`, `Deleted`).

## Recipes, deployment and workflows

```json
{
  "steps": [
    {
      "name": "ReportDesigns",
      "Groups": [ { "itemId": "sales", "displayText": "Sales", "order": 1 } ],
      "Views": [],
      "Reports": [
        {
          "itemId": "orders-by-channel",
          "displayText": "Orders by channel",
          "groupId": "sales",
          "showInAdminMenu": true,
          "query": {
            "dataSets": [ { "alias": "o", "source": "Orders", "dataSet": "Orders" } ],
            "columns": [
              { "id": "channel", "field": "o.Channel", "label": "Sales channel" },
              { "id": "orders", "field": "o.ItemId", "label": "Orders", "aggregate": "Count" }
            ]
          },
          "visuals": [ { "id": "table", "type": "Table", "showTotals": true } ],
          "OwnerUserName": "admin"
        }
      ]
    }
  ]
}
```

- The `ReportDesigns` step imports `Groups`, then `Views`, then `Reports`, matched by `itemId` and replaced. Owners match by `OwnerUserName`, else `ownerId`; an item with neither belongs to the person running the recipe (no owner at setup with nobody signed in). A report names its group by `groupId`, a `group` path such as `"Sales / Regional"` (created when missing) or a legacy `category`.
- The **Designed Reports and Views** deployment step (`ReportDesignsDeploymentStep`: `IncludeAll` default `true`, `ReportIds`, `ViewIds`) writes a `ReportDesigns` step with the views their queries read and the groups they sit in (parents first), and each owner's user name.
- With `OrchardCore.Workflows`: events `ReportRunEvent`, `ReportExportedEvent`, `ReportPublishedEvent`; tasks `RunReportTask`, `ExportReportTask`, `CreateReportShareLinkTask`, and (with `OrchardCore.Email`) `EmailReportTask`. Tasks run with the owner's access. The **Email Report** task is the way to email a designed report on a schedule; Report Subscriptions cover coded reports only. Code reacts without Workflows through `IReportEventHandler` (`ReportEventHandlerBase`).

The sample reads the **Orders** data set's `Channel` and `ItemId` (order id) fields; take other field names from the builder's **Data** pane. Full shapes, the deployment plan JSON, every workflow activity and an `IReportEventHandler` sample are in [references/designs-recipes-and-workflows.md](references/designs-recipes-and-workflows.md).

## Configuration and large data

`ReportQueryLimits` binds to `CrestApps:Reports:Builder:Limits` and `ReportDesignVersionOptions` to `CrestApps:Reports:Builder:Versions` of the shell configuration (under `OrchardCore` in the host `appsettings.json`; without the wrapper in a tenant's `App_Data/Sites/{tenant}/appsettings.json`; environment variables such as `OrchardCore__CrestApps__Reports__Builder__Limits__MaxRowsPerDataSet=100000`).

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
| `MaxRowsPerDataSet` | `50000` | Rows read from one data set (warning shown); also groups a source may return and rows a scheduled view stores |
| `MaxJoinedRows` | `250000` | Rows the joins may produce |
| `MaxResultRows` | `10000` | Result rows kept after sorting |
| `MaxFilterOptions` | `500` | Values a drop-down filter lists |
| `MaxJoinKeys`, `JoinKeyBatchSize` | `10000`, `500` | Keys a join sends to the joined data set, and per read |
| `MemoryLimitMegabytes` | `16` | Rows one join, grouping or sort keeps in memory before spilling |
| `TemporaryDirectory` | system temp | Where runs write temporary files |
| `MaxConcurrentRuns` | `4` | Reports a tenant runs at once; others wait |
| `MaxSharedReadRows` | `50000` | Rows of one data set kept to share between queries of a run; `0` turns sharing off |
| `Versions:MaxVersions` | `50` | Published versions kept per report (`0` keeps all) |

Other limits: designer payloads 1 MB, formulas nested 64 levels, views reading views 8 levels deep, a view that reads itself is refused.

Large data in short: keep a date filter on the data set's main date, let sources apply conditions and join keys, group in the source (`IDataAggregateSource`, `ReportIndexAggregator`), page with `ReportIndexPages`, and schedule heavy views (`RefreshIntervalMinutes` 15, 60, 360 or 1440; the **Report view refresh** task runs every 5 minutes and locks each view). Spans and metrics come from the `CrestApps.OrchardCore.Reports` source and meter; metrics carry only bounded tags (`crestapps.report.source`, `crestapps.report.data_source`, `outcome`, and `crestapps.report.level` on `crestapps.report.run.failures`), while report, query and data set ids are span tags only. Details, every span, metric and log id: [references/large-data-and-telemetry.md](references/large-data-and-telemetry.md); the shared rules and helpers are in `orchardcore-observability`.

## Troubleshooting

- **A data source or data set is missing in the builder**: its feature or Data Pipelines Core is off, its startup lacks `[RequireFeatures("OrchardCore.DataPipelines.Core")]`, or the designer lacks the source's permission.
- **No Report Builder under Reports**: the feature is off or the user lacks `ManageOwnReportDesigns`; people who only run reports see **Shared Reports** once a report is shared with them.
- **A shared report says it can no longer run**: the owner was deleted, disabled or lost access; clone it (the clone belongs to the person cloning) and share again.
- **A field of an older design is not found**: a field removed with the switch to Orchard Core's content and users sources; replace it in the builder.
- **Warning that a data set has more rows**: `MaxRowsPerDataSet` was hit; add a date filter, push down conditions, or schedule a view before raising the limit.
- **"The calculated field calls an aggregate function that reports can't compute"**: use built-in aggregates or the `ExpressionFunction.CreateAggregate` overload that names a `DataAggregate`.
- **`409 Conflict` on save**: someone else saved first; reload or keep your changes.
- **Share link cannot be created or emailed**: every recipient must be a site user, the report must be published, and `NotifyByEmail` needs the Email feature.
- **Imported design fails when run**: the target site lacks a data source or data set; enable the same features on both sites.
- **"A click names a visual the report no longer has"**: a click action targets a deleted visual; pick the targets again.
- **A view cannot be deleted**: other reports or views read it; remove it from them first.
- **A scheduled view refresh reports `busy`**: another node is refreshing that view; it is not an error.
- **Dashboards broke after upgrading telemetry**: metrics no longer carry `crestapps.report.id`, `crestapps.report.query.id` or `crestapps.report.data_set`, and `crestapps.report.outcome` is now `outcome` (`ok`, `error`, `cancelled`); filter failures by `crestapps.report.level`.
- **Slow dashboards**: trace with the `CrestApps.OrchardCore.Reports` spans; compare `report.read` counts with `crestapps.report.read_shared`, and keep data sets under `MaxSharedReadRows` with a date filter.

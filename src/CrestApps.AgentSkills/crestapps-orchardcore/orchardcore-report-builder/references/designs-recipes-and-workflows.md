# Designs, recipes, deployment and workflows

## The design model

A designed report is a `ReportDesign` and a reusable view a `ReportView`; both are catalog items (`itemId`). Designs are serialized with web JSON defaults (camelCase, case-insensitive) and enum values by name.

| `ReportDesign` property | Meaning |
| --- | --- |
| `displayText`, `description`, `category` | Title (required), description, menu category |
| `query` | A `ReportQueryDefinition` |
| `visuals` | `ReportVisualDefinition` list |
| `showInAdminMenu` | Pin the report to the admin **Reports** menu |
| `sharedUserNames`, `sharedRoles` | Who may run it (`Anonymous` needs `ShareReportsPublicly`, `Authenticated` means every signed-in user) |
| `allowExport` | Viewers may export (default `true`) |
| `ownerId`, `author`, `createdUtc`, `modifiedUtc` | Owner and audit data |

`ReportView` has `displayText`, `description`, `query`, `ownerId`, `author`, `createdUtc`, `modifiedUtc` and `refreshIntervalMinutes` (`0` live, otherwise at least 15).

`ReportQueryDefinition` (namespace `CrestApps.OrchardCore.Reports.Designer`):

| Property | Items |
| --- | --- |
| `dataSets` | `ReportDataSetReference`: `alias`, `source` (data source name), `dataSet`, `displayName` |
| `joins` | `ReportJoinDefinition`: `alias` (the joined data set), `type` (`DataJoinType`: `Inner`, `Left`, `Right`, `Full`), `conditions` of `{ leftField, rightField }` |
| `calculatedFields` | `ReportCalculatedField`: `name`, `label`, `expression` (see `formulas.md`) |
| `filters` | `ReportFilterDefinition`: `id`, `field`, `stage` (`Rows` or `Result`), `operator` (`DataFilterOperator`, such as `Equals`, `NotEquals`, `In`, `NotIn`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Between`, `InLastDays`, `InNextDays`, `IsEmpty`, `IsNotEmpty`), `values` (strings), `exposed`, `label`, `control` (`Auto`, `Text`, `Select`, `MultiSelect`, `DateRange`, `NumberRange`, `Boolean`, `RelativeDate`) |
| `columns` | `ReportColumnDefinition`: `id`, `field`, `label`, `aggregate` (`DataAggregate`: `None`, `Count`, `CountDistinct`, `Sum`, `Average`, `Min`, `Max`, `Median`), `transform` (`DataFieldTransform`: `None`, `Hour`, `Day`, `Week`, `DayOfWeek`, `Month`, `MonthOfYear`, `Quarter`, `Year`), `format` (.NET format string), `hidden` |
| `sorts` | `ReportSortDefinition`: `columnId`, `descending` |
| `limit` | Optional row limit |

Fields are referenced as `alias.FieldName` (such as `Article.Owner`); calculated fields by `name`. A result-stage filter's `field` is a column `id`. When any column has an aggregate, rows are grouped by the other columns.

`ReportVisualDefinition`: `id`, `type` (`Table`, `Chart`, `Metrics`, `Pivot`), `title`, `width` (1 to 12), `chartType` (`Line`, `Bar`, `Doughnut`, `Pie`, `Area`, `HorizontalBar`), `categoryColumnId`, `seriesColumnId`, `valueColumnIds`, `columnIds`, `showTotals`, `showSubtotals`, `stacked`, `showLegend`.

Column **Format** defaults: whole numbers `N0`, decimals `#,##0.##`, dates `d`, date-times `g` (`d` at midnight). Booleans show as Yes and No, **Month** as month and year, **Day of week** and **Month of year** as names. A table with `showSubtotals` adds a subtotal after each group of its leading dimensions, innermost first.

## ReportDesigns recipe step

The step imports `Views` first, then `Reports`. Items are matched by `itemId` (letters, digits, `-` and `_`, at most 64 characters) and replaced, or created with that id. `OwnerUserName` sets the owner when a user with that name exists; otherwise `ownerId` is kept. An item without a valid `itemId` or a `displayText` is a recipe error. Designs are not checked against the site's data sources on import; a missing source is reported when the report runs. Importing a changed or live view drops its stored rows.

```json
{
  "steps": [
    {
      "name": "ReportDesigns",
      "Views": [
        {
          "itemId": "articles-with-authors",
          "displayText": "Articles with authors",
          "refreshIntervalMinutes": 60,
          "query": {
            "dataSets": [
              { "alias": "Article", "source": "Contents", "dataSet": "Article", "displayName": "Article" },
              { "alias": "Users", "source": "Users", "dataSet": "Users", "displayName": "Users" }
            ],
            "joins": [
              {
                "alias": "Users",
                "type": "Left",
                "conditions": [ { "leftField": "Article.Owner", "rightField": "Users.UserId" } ]
              }
            ],
            "columns": [
              { "id": "title", "field": "Article.DisplayText", "label": "Title" },
              { "id": "author", "field": "Users.UserName", "label": "Author" },
              { "id": "created", "field": "Article.CreatedUtc", "label": "Created" }
            ]
          },
          "OwnerUserName": "admin"
        }
      ],
      "Reports": [
        {
          "itemId": "articles-per-author",
          "displayText": "Articles per author",
          "category": "Content",
          "showInAdminMenu": true,
          "allowExport": true,
          "sharedRoles": [ "Editor" ],
          "query": {
            "dataSets": [
              { "alias": "Article", "source": "Contents", "dataSet": "Article", "displayName": "Article" },
              { "alias": "Users", "source": "Users", "dataSet": "Users", "displayName": "Users" }
            ],
            "joins": [
              {
                "alias": "Users",
                "type": "Left",
                "conditions": [ { "leftField": "Article.Owner", "rightField": "Users.UserId" } ]
              }
            ],
            "filters": [
              {
                "id": "period",
                "field": "Article.CreatedUtc",
                "stage": "Rows",
                "operator": "InLastDays",
                "values": [ "30" ],
                "exposed": true,
                "label": "Created",
                "control": "RelativeDate"
              }
            ],
            "columns": [
              { "id": "author", "field": "Users.UserName", "label": "Author" },
              { "id": "articles", "field": "Article.ContentItemId", "label": "Articles", "aggregate": "CountDistinct" }
            ],
            "sorts": [ { "columnId": "articles", "descending": true } ]
          },
          "visuals": [
            { "id": "chart", "type": "Chart", "title": "Articles per author", "chartType": "Bar", "categoryColumnId": "author", "valueColumnIds": [ "articles" ], "width": 6 },
            { "id": "table", "type": "Table", "title": "Detail", "columnIds": [ "author", "articles" ], "showTotals": true, "width": 6 }
          ],
          "OwnerUserName": "admin"
        }
      ]
    }
  ]
}
```

The easiest way to get an exact shape is to build the report in the builder on a site where it works and export it with a deployment plan.

## Deployment step

**Designed Reports and Views** (`ReportDesignsDeploymentStep`, category **Reporting**) writes a `ReportDesigns` step. `IncludeAll` (default `true`) exports every report and view, including ones created later; otherwise `ReportIds` and `ViewIds` pick them, and the views they read are added. Each item carries `OwnerUserName`; share links are never exported. In a `DeploymentPlans` recipe:

```json
{
  "steps": [
    {
      "name": "DeploymentPlans",
      "Plans": [
        {
          "Name": "Reports",
          "Steps": [
            {
              "Type": "ReportDesignsDeploymentStep",
              "Step": { "IncludeAll": false, "ReportIds": [ "articles-per-author" ], "ViewIds": [] }
            }
          ]
        }
      ]
    }
  ]
}
```

## Workflow activities

Registered by `ReportsWorkflowsStartup` (Report Builder + `OrchardCore.Workflows`) and `ReportsEmailWorkflowsStartup` (+ `OrchardCore.Email`), all in the **Reports** category.

Events (`ReportRunEvent`, `ReportExportedEvent`, `ReportPublishedEvent`) take `ReportIds` (empty for every report), are correlated by the report id, and have the outcome `Done`. Run and export are raised from the admin page (`Source` `Admin`), the shared page (`Shared`) or a share link (`Link`); publish from `ReportDesignHistoryService.PublishAsync`. Workflow input: `ReportId`, `ReportTitle`, `UserName` (null for anonymous), `Source`, `FilterValues`, and `RowCount`, `Format` or `VersionNumber`. Reports run by the tasks raise no events, so a workflow can run its own trigger report without looping.

Tasks find the report by `ReportId` or `ReportIdExpression` (Liquid) and take filter values from `Filters` (one `filterId = value` per line, Liquid; repeat a line for several values). They run with the report **owner's** access. A missing or unpublished report takes `Failed`. `OutputName` renames the output.

| Task | Outcomes | Output |
| --- | --- | --- |
| `RunReportTask` | `Done`, `No rows`, `Failed` | `Report`: `ReportId`, `Title`, `RowCount`, `Columns`, `Rows` (at most 1,000 dictionaries by column label), `Errors` |
| `ExportReportTask` (`Format`, `MediaFolder`) | `Done`, `Failed` | `ReportFile`: `ReportId`, `Title`, `FileName`, `ContentType`, `Format`, `RowCount`, `Size`, and `Path` and `Url` (with a media folder, needs Media) or `Base64` |
| `EmailReportTask` (`To`, `Cc`, `Bcc`, `Subject`, `Body`, `AttachmentFormat` default `csv`, `IncludeReport` default `true`) | `Done`, `Failed` | None; metrics and tables rendered as inline-styled HTML |
| `CreateReportShareLinkTask` (`Recipients`, `Note`, `ExpiresAfterDays` default 7, `MaxUses`, `AllowExport`, `NotifyByEmail` default `true`) | `Done`, `Failed` | `ReportShareLink`: `ReportId`, `Title`, `Url`, `LinkId`, `ExpiresUtc`, `Recipients`, `Notified` |

`Workflow.LastResult` is the output of run and export, the link URL for the share link task, and the report id for the email task. Share link URLs use the site's base URL (set it for timer-started workflows).

A workflow that emails a report each time a new version is published (import the report first with the same id):

```json
{
  "steps": [
    {
      "name": "WorkflowType",
      "data": [
        {
          "WorkflowTypeId": "report-published-email",
          "Name": "Email report on publish",
          "IsEnabled": true,
          "IsSingleton": false,
          "DeleteFinishedWorkflows": true,
          "Activities": [
            {
              "ActivityId": "published",
              "Name": "ReportPublishedEvent",
              "X": 0,
              "Y": 0,
              "IsStart": true,
              "Properties": { "ReportIds": [ "articles-per-author" ] }
            },
            {
              "ActivityId": "email",
              "Name": "EmailReportTask",
              "X": 300,
              "Y": 0,
              "IsStart": false,
              "Properties": {
                "ReportId": "articles-per-author",
                "To": "manager@example.com",
                "AttachmentFormat": "csv",
                "IncludeReport": true
              }
            }
          ],
          "Transitions": [
            {
              "Id": 0,
              "SourceActivityId": "published",
              "SourceOutcomeName": "Done",
              "DestinationActivityId": "email"
            }
          ]
        }
      ]
    }
  ]
}
```

## React in code with IReportEventHandler

`IReportEventHandler` (namespace `CrestApps.OrchardCore.Reports.Designer.Services`) has `RanAsync`, `ExportedAsync` and `PublishedAsync`; derive from `ReportEventHandlerBase` to override only some. `ReportEventContext` carries `Design`, `UserName`, `Source` (`ReportEventSource`: `Admin`, `Shared`, `Link`), `FilterValues`, and `RowCount`, `Format` or `VersionNumber`. Handlers run after the action; an exception in one is logged without failing the request or the others.

```csharp
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.Logging;

namespace MyModule.Reports;

public sealed class ExportAuditHandler : ReportEventHandlerBase
{
    private readonly ILogger _logger;

    public ExportAuditHandler(ILogger<ExportAuditHandler> logger)
    {
        _logger = logger;
    }

    public override Task ExportedAsync(ReportEventContext context)
    {
        _logger.LogInformation("{User} exported '{Report}' as {Format} from {Source}.",
            context.UserName, context.Design.DisplayText, context.Format, context.Source);

        return Task.CompletedTask;
    }
}
```

Register it with `services.AddScoped<IReportEventHandler, ExportAuditHandler>()` in a startup that requires `ReportsConstants.BuilderFeature`.

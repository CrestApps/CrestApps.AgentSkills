---
name: orchardcore-reports
description: Skill for building on the CrestApps Orchard Core Reports framework (CrestApps.OrchardCore.Reports, CrestApps.OrchardCore.Reports.OpenXml, CrestApps.OrchardCore.Reports.Subscriptions). Covers coded reports with static IReport descriptors and services.AddReport, report families with IReportRunner, ReportDescriptor and ReportOptions, ReportContext and ReportRunMode, the ReportFilter helpers GetDateRange, SetDateRange, TryGet, GetOrDefault and Set, report filter display drivers and IReportFilterMetadata, ReportDocument metrics, tables, bars and charts, ReportStyle cell colors, CSV and Excel exports with IReportExportFormat and AddReportExportFormat, IReportManager, IReportExportManager, the today default date range, scheduled report subscriptions, and upgrading 2.x reports. Use this skill when requests mention the admin Reports area, writing a report, adding a report filter or export format, or report troubleshooting. For designed reports use orchardcore-report-builder.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Reports

The Reports module is a reusable reporting framework. It adds one admin **Reports** area and a small contract any module implements to publish a report. Every report shares the same filter form, the same renderer (metric cards, tables, bars, Chart.js charts) and the same exports (CSV built in, Excel with an add-on). Modules such as Omnichannel, Contact Center, Phone Number Verifications, Orders and Inventory contribute their reports through it.

Designed reports that people build in the browser are the **Report Builder** feature; see the `orchardcore-report-builder` skill.

## Features

| Feature ID | Name | What it adds | Depends on |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Reports` | Reports | Admin **Reports** area, `ReportFilter` with the date range filter, renderer, CSV export | `CrestApps.OrchardCore.Resources`, `OrchardCore.Users` |
| `CrestApps.OrchardCore.Reports.OpenXml` | Reports (OpenXml) | Excel `.xlsx` export (`DocumentFormat.OpenXml`) | Reports |
| `CrestApps.OrchardCore.Reports.Subscriptions` | Report Subscriptions | Emails any registered report daily, weekly or monthly | Reports, `OrchardCore.Email` |
| `CrestApps.OrchardCore.Reports.Builder` | Report Builder | Drag-and-drop designed reports (see `orchardcore-report-builder`) | Reports, `OrchardCore.DataPipelines.Core` |

The feature IDs are constants on `ReportsConstants` (`Feature`, `OpenXmlFeature`, `SubscriptionsFeature`, `BuilderFeature`).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Reports",
        "CrestApps.OrchardCore.Reports.OpenXml",
        "CrestApps.OrchardCore.Reports.Subscriptions"
      ]
    }
  ]
}
```

Install the module packages in the web (startup) project. A module that only contributes reports references `CrestApps.OrchardCore.Reports.Abstractions`.

## Layers and namespaces

- **`CrestApps.OrchardCore.Reports.Abstractions`** holds the contracts and the document model: `IReport`, `IReportRunner`, `ReportDescriptor`, `ReportOptions`, `ReportContext`, `ReportRunMode`, `ReportFilterExtensions`, `IReportExportFormat`, `ReportExportFormatDescriptor`, `ReportsConstants`, `ReportValue` and `AddReport` / `AddReportExportFormat` (namespace `CrestApps.OrchardCore.Reports`); `ReportDocument`, `ReportSection`, `ReportFilter`, `ReportDateRange`, `ReportStyle`, `ReportFormat` and the other models (namespace `CrestApps.OrchardCore.Reports.Models`); `IReportManager` and `IReportExportManager` (namespace `CrestApps.OrchardCore.Reports.Services`).
- **`CrestApps.OrchardCore.Reports.Core`** holds the default `ReportManager`, `ReportExportManager` and `CsvReportExportFormat`.
- **`CrestApps.OrchardCore.Reports`** is the Orchard Core module: admin menu, `ReportsController`, views, and `ReportDateRangeFilterDisplayDriver`.

## Contribute a report

What describes a report is static, like the data sources of Orchard Core's Data Pipelines: `Name`, `DisplayName` and `Description` (both `LocalizationSource`), `Category` and `Permission`. `RunAsync` comes from `IReportRunner`. The framework lists, routes to and authorizes reports from their descriptors, and creates a report only to run it.

```csharp
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace MyModule.Reports;

public sealed class OrderVolumeReport : IReport
{
    private readonly IOrderReportStore _store;

    internal readonly IStringLocalizer S;

    public OrderVolumeReport(IOrderReportStore store, IStringLocalizer<OrderVolumeReport> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public static string Name => "my-order-volume";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderVolumeReport>("Order volume");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderVolumeReport>("Orders and revenue per day.");

    public static string Category => ReportsConstants.Categories.Commerce;

    public static Permission Permission => MyPermissions.ViewOrderReports;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        // The period is a filter like any other. Bounds are UTC and null when no date range filter ran.
        var range = context.Filter.GetDateRange();
        var channel = context.Filter.GetOrDefault<string>("Channel");

        var days = await _store.GetDailyTotalsAsync(range.FromUtc, range.ToUtc, channel, cancellationToken);

        var rows = days
            .Select(day => new ReportRow([day.Date.ToString("d"), ReportFormat.Number(day.Orders), day.Revenue.ToString("C")]))
            .ToList();

        rows.Add(new ReportRow(
            [S["Total"].Value, ReportFormat.Number(days.Sum(day => day.Orders)), days.Sum(day => day.Revenue).ToString("C")],
            ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Orders"].Value, ReportFormat.Number(days.Sum(day => day.Orders))),
                new ReportMetric(S["Days"].Value, ReportFormat.Number(days.Count)),
            ]))
            .Add(ReportSection.ForChart(S["Orders per day"].Value, new ReportChart
            {
                Type = ReportChartType.Line,
                Labels = days.Select(day => day.Date.ToString("d")).ToList(),
                Datasets = [new ReportChartDataset(S["Orders"].Value, days.Select(day => (double)day.Orders))],
            }, width: 12))
            .Add(ReportSection.ForTable(S["Daily totals"].Value,
            [
                new ReportColumn(S["Day"].Value),
                new ReportColumn(S["Orders"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Revenue"].Value, ReportColumnAlign.End),
            ], rows));
    }
}
```

Register it in a startup that runs only when Reports is enabled. `AddReport<TReport>()` registers the class as scoped and adds its descriptor to `ReportOptions`:

```csharp
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReport<OrderVolumeReport>();
    }
}
```

Rules:

- `Name` is the stable route key (`/Admin/Reports/view/{name}`). `IReportManager` throws when two descriptors share a name.
- `Permission` gates the menu entry, the page and the export. Declare it in your own permission provider and grant it to roles through stereotypes.
- `Category` is one of `ReportsConstants.Categories` (`Executive`, `Operations`, `QueueRouting`, `AgentPerformance`, `WorkforcePayroll`, `BillingUsage`, `Commerce`, `CrmCampaigns`, `AIReports`, `ComplianceAudit`, `Technical`, `General`). An empty category is listed under **General**; an unknown category sorts after the known ones.
- Keep calculations independent of the renderer: the same `ReportDocument` is rendered in the browser and serialized by every export format.

## ReportContext and run mode

`ReportContext` carries the `Filter` and the `Mode`: `ReportRunMode.Display` when the document is rendered on screen, `ReportRunMode.Export` when an export format serializes it. The Reports controller sets `Export` on the export action. A report that pages its rows on screen reads the mode to give an export every matching row. `new ReportContext(filter)` still means `Display`.

## The report document

`ReportDocument` is an ordered list of `ReportSection`s, added with `Add` (fluent). Each section has a `Title`, an optional `Description`, and a `Width` on the shared twelve-column grid (default 12; `ForChart` defaults to 6).

| Factory | Content |
| --- | --- |
| `ReportSection.ForMetrics(title, metrics)` | `ReportMetric(label, value, hint)` cards |
| `ReportSection.ForTable(title, columns, rows)` | `ReportColumn(label, align, headerStyle)` and `ReportRow(cells, kind)` |
| `ReportSection.ForBars(title, bars)` | Horizontal `ReportBar(label, value, ratio)` with `ratio` from 0 to 1 |
| `ReportSection.ForChart(title, chart, width)` | `ReportChart` with `Type`, `Labels`, `Datasets`, `Stacked`, `ShowLegend`, `PercentageScale` |

- `ReportChartType` is `Line`, `Bar`, `Doughnut`, `Pie`, `Area` or `HorizontalBar`. Charts are drawn by the `CrestApps.OrchardCore.Reports.Charts` script resource (depends on `chart.js`), loaded only when the page has a chart. Exports write a chart as a label and dataset table.
- `ReportRowKind` is `Detail`, `Subtotal` or `GrandTotal`; non-detail rows are emphasized and exported bold.
- `ReportFormat.Number(long)`, `ReportFormat.Percent(rate)` (rate 0 to 1) and `ReportFormat.Duration(seconds)` format values with the invariant culture.
- `ReportValue.UserDisplayName(userName, fallback)` writes a token that the page and the exports replace with the user's rendered `UserDisplayName` shape.

### Color-coded cells with ReportStyle

`ReportStyle.Create(color, backgroundColor, bold)` styles a header (`ReportColumn.HeaderStyle`), a row (`ReportRow.WithStyle`) or a single cell (`ReportRow.WithCellStyle(index, style)`, which wins over the row style). Use hexadecimal colors such as `#2563EB`: the HTML renderer also accepts simple named colors, but only hexadecimal colors reach the Excel export. CSV never carries styles.

```csharp
var columns = new[]
{
    new ReportColumn(S["Queue"].Value, ReportColumnAlign.Start, ReportStyle.Create("#FFFFFF", "#2563EB", bold: true)),
    new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
};

var rows = new List<ReportRow>
{
    new ReportRow(["Support", "18"]).WithCellStyle(1, ReportStyle.Create("#B91C1C")),
    new ReportRow(["All queues", "18"], ReportRowKind.GrandTotal).WithStyle(ReportStyle.Create(backgroundColor: "#EFF6FF")),
};
```

## Filters

`ReportFilter` is an `Entity` with a `ReportName` and a `Properties` bag. A report declares no fixed filter dimensions: every filter, including the date range, is a `DisplayDriver<ReportFilter>` that writes into the bag. Browser display and export build the filter the same way, so values apply to both.

Typed helpers from `ReportFilterExtensions`:

- `bool TryGet<T>(string key, out T value)` reads a value (enums stored as strings parse).
- `T GetOrDefault<T>(string key)` reads a value or the default.
- `void Set<T>(string key, T value)` writes a value, or removes the key for `null` or an empty string. `Remove(key)` removes it.
- `ReportDateRange GetDateRange()` and `SetDateRange(ReportDateRange range)` read and write `FromUtc`, `ToUtc` and the preset `Key` (stored under `FromUtc`, `ToUtc` and `DateRangeKey`).

### The built-in date range

`ReportDateRangeFilterDisplayDriver` renders for every report at `Content:1`. It uses the shared `DateRangePicker` view component and `date-range-picker` resource from CrestApps Resources (see `orchardcore-crestapps-resources`), with presets (Today, Yesterday, Last 7/30/90 Days, This/Last Week, Month, Quarter, Year, Last 3/6/12 Months), **Custom Range**, **On or before** and **On or after**.

- The default period is today, from `00:00:00` to `23:59:59` in the site's time zone (2.x used the last 30 days).
- An empty custom range defaults to today `00:00` to `23:59`; **On or before** to today `23:59`; **On or after** to today `00:00`. A submitted `23:59` upper bound is treated as `23:59:59`.
- Local values are converted to UTC with `ILocalClock`, and inverted bounds are swapped.

### A report-specific filter

Gate the driver to your report with `filter.ReportName`, and write the value with `Set`. The full driver, view model and view are in [references/reports-examples.md](references/reports-examples.md).

```csharp
public sealed class ChannelFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (filter.ReportName != OrderVolumeReport.Name)
        {
            return null;
        }

        return Initialize<ChannelFilterViewModel>("ChannelFilter_Edit", model =>
        {
            model.Channel = filter.GetOrDefault<string>("Channel");
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (filter.ReportName != OrderVolumeReport.Name)
        {
            return null;
        }

        var model = new ChannelFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        filter.Set("Channel", model.Channel);

        return Edit(filter, context);
    }
}
```

Register it with `services.AddDisplayDriver<ReportFilter, ChannelFilterDisplayDriver>()` in the same `[RequireFeatures(ReportsConstants.Feature)]` startup. A driver shared by many reports can instead check whether the report implements `IReportFilterMetadata` and lists the driver's filter name in `FilterNames` (`_reportManager.GetReport(filter.ReportName) is IReportFilterMetadata metadata`), as the Omnichannel and Contact Center filters do.

## A family of reports

When one class runs many reports that differ only by what they measure, it cannot describe each with static members. Implement `IReportRunner` and add a `ReportDescriptor` per report to `ReportOptions`, with a factory that creates the runner:

```csharp
private static readonly ObjectFactory<CatalogReport> _factory =
    ActivatorUtilities.CreateFactory<CatalogReport>([typeof(CatalogReportKind)]);

services.Configure<ReportOptions>(options => options.Add(new ReportDescriptor(
    "my-volume-report",
    LocalizationSource.Create<ReportsStartup>("Volume"),
    LocalizationSource.Create<ReportsStartup>("How much work came in, by day."),
    ReportsConstants.Categories.Operations,
    MyPermissions.ViewReports,
    typeof(CatalogReport),
    services => _factory(services, [CatalogReportKind.Volume]))));
```

Another feature can add or remove reports of the family through the same options. Omnichannel (enterprise activity reports) and Contact Center (interaction and agent workforce catalogs) use this pattern.

## Using reports from code

- `IReportManager.GetReports()` lists `ReportDescriptor`s without creating reports; `GetDescriptor(name)` finds one; `GetReport(name)` creates the `IReportRunner` to run.
- `IReportExportManager.GetFormats()` lists `ReportExportFormatDescriptor`s; `GetDescriptor(name)` and `GetFormat(name)` resolve one by name, ignoring case.
- Localize a descriptor's `DisplayName` and `Description` with `IStringLocalizerFactory.Localize(source)`.
- Always authorize `descriptor.Permission` before running a report for a user.

```csharp
var descriptor = _reportManager.GetDescriptor("my-order-volume");

if (descriptor is null || !await _authorizationService.AuthorizeAsync(User, descriptor.Permission))
{
    return Forbid();
}

var filter = new ReportFilter { ReportName = descriptor.Name };
filter.SetDateRange(new ReportDateRange { FromUtc = fromUtc, ToUtc = toUtc, Key = "custom" });

var document = await _reportManager.GetReport(descriptor.Name)
    .RunAsync(new ReportContext(filter, ReportRunMode.Export), cancellationToken);

var csv = _exportManager.GetFormat(ReportsConstants.CsvExportFormat).Serialize(document);
```

The admin pages replace `ReportValue.UserDisplayName` tokens through `ReportDisplayValueResolver` (`ResolveAsync(document)`, in the Reports module); call it yourself before serializing when your report writes such tokens.

## Export formats

An export format is static too: `Name`, `DisplayName` (a `LocalizationSource`), `Extension` (with its dot) and `ContentType`, plus `byte[] Serialize(ReportDocument document)`. Register it with `services.AddReportExportFormat<TFormat>()`. CSV (`csv`, `ReportsConstants.CsvExportFormat`) ships with Reports; Reports (OpenXml) adds `xlsx` (`ReportsConstants.XlsxExportFormat`), one worksheet per section with styles applied. One format renders as an **Export** button; several render as an **Export** dropdown. A full example is in [references/reports-examples.md](references/reports-examples.md).

## Admin area

- `/Admin/Reports` lists the reports the user may run; the menu groups them by category in a fixed order (Executive, Operations, Queue & Routing, Agent Performance, Workforce & Payroll, Billing & Usage, Commerce, CRM & Campaigns, Artificial Intelligence Reports, Compliance & Audit, Technical & IT, General) and alphabetizes inside each.
- `/Admin/Reports/view/{name}` shows the filter form, the document and the export actions.
- `/Admin/Reports/view/{name}/export/{format}` exports with the same filter (CSV when no format is given).
- Other modules adding items under **Reports** use a menu priority below `ReportsConstants.AdminMenuPriority` so the top item keeps its `reports` id and icon.

## Report subscriptions

`CrestApps.OrchardCore.Reports.Subscriptions` emails a registered report on a schedule. A `ReportSubscription` (a catalog item with `ReportSubscriptionIndex`) holds `Name`, `ReportName`, `Frequency` (`Daily`, `Weekly` on `DayOfWeek`, `Monthly` on `DayOfMonth` 1 to 28), `Hour` in the site's time zone, `Recipients` (comma separated), an optional `AttachmentFormat` (`csv`, `xlsx`), `IsEnabled`, and `NextRunUtc`, `LastRunUtc`, `LastError`.

- A run covers the day, the seven days or the calendar month before it, midnight to midnight in the site's time zone (`ReportSubscriptionSchedule`).
- `ReportSubscriptionsBackgroundTask` runs every five minutes and sends due subscriptions; a late run still covers the period it was due for. Failures are stored in `LastError`.
- **Reports > Subscriptions** (`/Admin/Reports/subscriptions`) is an Orchard Core `AdminList` (see `orchardcore-admin-lists`) that needs `ManageReportSubscriptions` (**Manage report subscriptions**, Administrator by default). It lists only reports the person may run; **Save and send now** sends at once without moving the schedule.
- The Email feature must be configured, or each run records the error.

## Upgrading from 2.x

- Replace `services.AddScoped<IReport, TReport>()` with `services.AddReport<TReport>()`.
- Make `Name`, `DisplayName`, `Description`, `Category` and `Permission` `static`. `DisplayName` and `Description` become `LocalizationSource` (`LocalizationSource.Create<TReport>("...")`) instead of `LocalizedString`.
- `ReportFilter.FromUtc`, `ToUtc`, `DateRangeKey` and `ReportContext.FromUtc`, `ToUtc` are gone. Use `context.Filter.GetDateRange()` and `filter.SetDateRange(...)`; read other values with `TryGet`, `GetOrDefault` and `Set`.
- `IReportManager.GetReports()` and `GetDescriptor(name)` return `ReportDescriptor`s; `GetReport(name)` returns an `IReportRunner`; `FindByName` is removed.
- `IReportProvider` is replaced by `ReportOptions` plus `IReportRunner` and one `ReportDescriptor` per report.
- `IReportExportFormat` members `Name`, `DisplayName`, `Extension`, `ContentType` are static; register with `AddReportExportFormat<TFormat>()`; `IReportExportManager.GetFormats()` returns `ReportExportFormatDescriptor`s.
- Report pages now open on today instead of the last 30 days.
- The Supervisor role now defaults to `ViewOmnichannelReports` and `RunPhoneNumberVerificationsReport`; on existing tenants grant them under **Security > Roles**.

## Troubleshooting

- **Report missing from the menu**: the startup that calls `AddReport` lacks `[RequireFeatures(ReportsConstants.Feature)]` or its feature is off, or the user lacks the report's permission.
- **"More than one report is registered with the name"**: two descriptors share a `Name`; names must be unique across modules.
- **Custom filter shows on every report**: the driver does not check `filter.ReportName` (or `IReportFilterMetadata`).
- **Export differs from the screen**: the report pages rows on screen; give the full result when `context.Mode == ReportRunMode.Export`.
- **Colors missing in Excel**: use hexadecimal colors; named colors are HTML only.
- **Date range empty in `RunAsync`**: `GetDateRange()` returns null bounds when no date range filter wrote them, such as when a report is run from code; set it with `SetDateRange`.

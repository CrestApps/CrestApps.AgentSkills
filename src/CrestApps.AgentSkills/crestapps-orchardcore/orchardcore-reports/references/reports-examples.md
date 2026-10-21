# Reports examples

Longer examples for the `orchardcore-reports` skill. They target CrestApps.OrchardCore 3.0.

## A report-specific filter (driver, view model, view)

The view model (view models are not sealed):

```csharp
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyModule.ViewModels;

public class ChannelFilterViewModel
{
    public string Channel { get; set; }

    public IList<SelectListItem> Channels { get; set; } = [];
}
```

The driver renders only for its report, after the date range (`Content:1`):

```csharp
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using MyModule.Reports;
using MyModule.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace MyModule.Drivers;

public sealed class ChannelFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    public const string ChannelKey = "Channel";

    internal readonly IStringLocalizer S;

    public ChannelFilterDisplayDriver(IStringLocalizer<ChannelFilterDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (!string.Equals(filter.ReportName, OrderVolumeReport.Name, StringComparison.Ordinal))
        {
            return null;
        }

        return Initialize<ChannelFilterViewModel>("ChannelFilter_Edit", model =>
        {
            model.Channel = filter.GetOrDefault<string>(ChannelKey);
            model.Channels =
            [
                new SelectListItem(S["Web"], "Web"),
                new SelectListItem(S["Store"], "Store"),
            ];
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (!string.Equals(filter.ReportName, OrderVolumeReport.Name, StringComparison.Ordinal))
        {
            return null;
        }

        var model = new ChannelFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // An empty value removes the key, so "All channels" stores nothing.
        filter.Set(ChannelKey, model.Channel);

        return Edit(filter, context);
    }
}
```

`Views/ChannelFilter.Edit.cshtml`, following the layout of the built-in filters:

```cshtml
@model MyModule.ViewModels.ChannelFilterViewModel

<div class="col p-1">
    <label class="form-label" asp-for="Channel">@T["Channel"]</label>
    <select asp-for="Channel" asp-items="Model.Channels" class="form-select">
        <option value="">@T["All channels"]</option>
    </select>
</div>
```

Registration:

```csharp
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReport<OrderVolumeReport>();
        services.AddDisplayDriver<ReportFilter, ChannelFilterDisplayDriver>();
    }
}
```

## A filter shared by several reports with IReportFilterMetadata

A report lists the filters it wants; one driver serves them all:

```csharp
public sealed class StoreSalesReport : IReport, IReportFilterMetadata
{
    public IReadOnlyCollection<string> FilterNames { get; } = [StoreFilterDisplayDriver.FilterName];

    // Static descriptor members and RunAsync as usual...
}

public sealed class StoreFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    public const string FilterName = "MyModule.Store";

    private readonly IReportManager _reportManager;

    public StoreFilterDisplayDriver(IReportManager reportManager)
    {
        _reportManager = reportManager;
    }

    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (!AppliesTo(filter.ReportName))
        {
            return null;
        }

        return Initialize<StoreFilterViewModel>("StoreFilter_Edit", model =>
        {
            model.StoreId = filter.GetOrDefault<string>("StoreId");
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (!AppliesTo(filter.ReportName))
        {
            return null;
        }

        var model = new StoreFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        filter.Set("StoreId", model.StoreId);

        return Edit(filter, context);
    }

    // GetReport creates the report, so keep report constructors cheap.
    private bool AppliesTo(string reportName)
        => _reportManager.GetReport(reportName) is IReportFilterMetadata metadata &&
            metadata.FilterNames.Contains(FilterName, StringComparer.Ordinal);
}
```

`IReportManager` is in `CrestApps.OrchardCore.Reports.Services`.

## A family of reports run by one class

```csharp
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;

namespace MyModule.Reports;

public enum CatalogReportKind
{
    Volume,
    Backlog,
}

public sealed class CatalogReport : IReportRunner
{
    private readonly CatalogReportKind _kind;
    private readonly IWorkItemStore _store;

    // The kind comes from the descriptor's factory; the other arguments from the services.
    public CatalogReport(CatalogReportKind kind, IWorkItemStore store)
    {
        _kind = kind;
        _store = store;
    }

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var rows = await _store.GetRowsAsync(_kind, range.FromUtc, range.ToUtc, cancellationToken);

        return new ReportDocument()
            .Add(ReportSection.ForTable(_kind.ToString(), [new ReportColumn("Item"), new ReportColumn("Count", ReportColumnAlign.End)], rows));
    }
}
```

```csharp
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures(ReportsConstants.Feature)]
public sealed class CatalogReportsStartup : StartupBase
{
    private static readonly ObjectFactory<CatalogReport> _factory =
        ActivatorUtilities.CreateFactory<CatalogReport>([typeof(CatalogReportKind)]);

    public override void ConfigureServices(IServiceCollection services)
    {
        Add(services, "my-catalog-volume", LocalizationSource.Create<CatalogReportsStartup>("Volume"),
            LocalizationSource.Create<CatalogReportsStartup>("Work that came in, by day."), CatalogReportKind.Volume);

        Add(services, "my-catalog-backlog", LocalizationSource.Create<CatalogReportsStartup>("Backlog"),
            LocalizationSource.Create<CatalogReportsStartup>("Open work by age."), CatalogReportKind.Backlog);
    }

    private static void Add(IServiceCollection services, string name, LocalizationSource displayName, LocalizationSource description, CatalogReportKind kind)
    {
        services.Configure<ReportOptions>(options => options.Add(new ReportDescriptor(
            name,
            displayName,
            description,
            ReportsConstants.Categories.Operations,
            MyPermissions.ViewReports,
            typeof(CatalogReport),
            serviceProvider => _factory(serviceProvider, [kind]))));
    }
}
```

`ReportOptions.Add` ignores a second descriptor with the same `Type` and `Name`; a different type with the same name makes `IReportManager` throw.

## A custom export format

```csharp
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.Localization;

namespace MyModule.Exports;

public sealed class JsonReportExportFormat : IReportExportFormat
{
    public static string Name => "json";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<JsonReportExportFormat>("JSON");

    public static string Extension => ".json";

    public static string ContentType => "application/json";

    public byte[] Serialize(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sections = document.Sections.Select(section => new
        {
            section.Title,
            Kind = section.Kind.ToString(),
            Metrics = section.Metrics.Select(metric => new { metric.Label, metric.Value }),
            Columns = section.Columns.Select(column => column.Label),
            Rows = section.Rows.Select(row => row.Cells),
            Chart = section.Chart is null
                ? null
                : new { section.Chart.Labels, Datasets = section.Chart.Datasets.Select(dataset => new { dataset.Label, dataset.Values }) },
        });

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { document.Title, Sections = sections }));
    }
}
```

```csharp
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ExportStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReportExportFormat<JsonReportExportFormat>();
    }
}
```

The format appears in the **Export** dropdown of every coded report, in the attachment formats of Report Subscriptions, and in the exports of designed reports, all by its `Name`.

## Bars and a stacked chart

```csharp
var bars = queues.Select(queue => new ReportBar(
    queue.Name,
    ReportFormat.Number(queue.Count),
    total == 0 ? 0 : (double)queue.Count / total));

document
    .Add(ReportSection.ForBars(S["Work by queue"].Value, bars))
    .Add(ReportSection.ForChart(S["Outcomes per day"].Value, new ReportChart
    {
        Type = ReportChartType.Bar,
        Stacked = true,
        Labels = days,
        Datasets =
        [
            new ReportChartDataset(S["Completed"].Value, completed),
            new ReportChartDataset(S["Canceled"].Value, canceled),
        ],
    }));
```

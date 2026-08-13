---
name: orchardcore-reports
description: Skill for building and using the CrestApps OrchardCore Reports framework. Covers report contracts, admin report pages, date range filters, metric and table documents, charts, CSV and OpenXml exports, permissions, custom filters, and report registration. Use this skill when requests mention CrestApps.OrchardCore.Reports, IReport, ReportDocument, IReportExportFormat, or related reporting implementation and troubleshooting.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps OrchardCore Reports

The Reports module provides a shared admin **Reports** area and contracts for
modules that publish reports. Reports use one filter and rendering model for
browser output and exports.

## Features and packages

- `CrestApps.OrchardCore.Reports` enables the framework and admin area.
- `CrestApps.OrchardCore.Reports.OpenXml` adds Excel `.xlsx` export support.
- The Reports feature depends on `CrestApps.OrchardCore.Resources`.
- CSV export is provided by the base Reports feature.

Enable the features with a root-wrapped recipe:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Reports",
        "CrestApps.OrchardCore.Reports.OpenXml"
      ]
    }
  ]
}
```

Enable the OpenXml add-on only when Excel export is required. When several
exporters are available, the admin page groups them under one **Export**
dropdown.

## Report contracts

Implement `IReport` for a report definition. Provide a technical `Name`, a
display name, a description, a category, a permission, and a `RunAsync`
implementation that returns a `ReportDocument` for the supplied
`ReportContext`.

Register reports as scoped services:

```csharp
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IReport, SalesReport>();
    }
}
```

`IReport`, `IReportManager`, `IReportExportFormat`, and
`IReportExportManager` are in `CrestApps.OrchardCore.Reports`.
`ReportDocument`, `ReportSection`, `ReportContext`, `ReportFilter`, and
`ReportFormat` are in `CrestApps.OrchardCore.Reports.Models`.

Use `ReportSection.ForMetrics`, `ReportSection.ForTable`,
`ReportSection.ForBars`, and `ReportSection.ForChart` to compose the document.
Use `ReportFormat` for consistent number, duration, and percentage formatting.
Charts use ordered labels and one or more numeric datasets in the shared
responsive twelve-column layout.

`IReportManager` resolves and runs reports. `IReportExportFormat` and
`IReportExportManager` provide the extension points for additional export
formats. Keep report calculations independent from the browser renderer so the
same document can be exported.

## Date range and custom filters

Every report receives the tenant-local date range through `ReportContext`.
Common presets include today, yesterday, the last 7, 30, or 90 days, calendar
periods, rolling months, custom ranges, and open-ended `on or before` or
`on or after` selections. The selected values are converted to UTC before
`RunAsync` executes.

Add a report-specific filter by registering a display driver for
`ReportFilter`. Check `filter.ReportName` so the filter appears only for the
intended report. Store validated values in `filter.Properties`; use the same
values for browser output and exports.

The reusable date range control is supplied by the CrestApps Resources
feature. Do not implement a separate date picker for each report.

## Admin visibility and permissions

Each report declares its own permission. The Reports menu groups and displays
only reports the current user can run. Choose a stable permission name and
enforce it in the report definition rather than relying only on menu visibility.

Modules such as Omnichannel and Phone Number Verifications contribute reports
through this framework. Enabling the Reports feature makes their reports use
the shared filters and export behavior.

---
name: orchardcore-mini-profiler
description: Skill for adding the MiniProfiler performance widget to Orchard Core front-end and admin pages. Covers enabling the feature, the ViewMiniProfilerOnFrontEnd and ViewMiniProfilerOnBackEnd permissions, widget placement in the Footer zone and docking on admin pages, shape rendering timings, YesSql SQL profiling with duplicate and error query badges, MiniProfilerOptions configuration (ResultsAuthorize, ShouldProfile, IgnoredPaths, popup options), result storage across multiple instances, custom timings, and troubleshooting. Use this skill when requests mention Orchard Core Mini Profiler, MiniProfiler, performance profiling, request timings, SQL query profiling, duplicate queries, N+1 queries, slow page diagnosis, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.MiniProfiler, MiniProfilerFilter, MiniProfilerConnectionFactory, CurrentDbProfiler, ShapeStep, MiniProfilerOptions, StackExchange.Profiling.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Mini Profiler - Prompt Templates

## Add Performance Profiling

You are an Orchard Core expert. Enable and configure the MiniProfiler widget so authorized users can see request, shape, and database timings on Orchard Core pages.

### Guidelines

- Enable the `OrchardCore.MiniProfiler` feature per tenant. Use it to investigate a specific problem: profiling adds work to every request of the tenant and exposes route names, SQL, and timings.
- Two permissions decide whether the widget is added to a page:
  - `ViewMiniProfilerOnFrontEnd` — front-end (themed) pages.
  - `ViewMiniProfilerOnBackEnd` — admin pages.
  - The `Administrator` role receives both by default. Never grant them to `Anonymous`.
- Widget permissions do **not** protect the stored results. Configure `MiniProfilerOptions.ResultsAuthorize` (or `ResultsAuthorizeAsync`) before enabling the module where untrusted users can reach the site.
- The module registers early (`Startup.Order = -500`) and calls `app.UseMiniProfiler()` so the profiler wraps all downstream middleware.
- `MiniProfilerFilter` is an `IAsyncResultFilter` that adds the `MiniProfiler` shape to the layout's `Footer` zone for view and Razor Page results when the user has the matching permission.
- `ShapeStep` (`IShapeDisplayEvents`) records a `Shape: <shape-type>` step for every shape rendered through display management.
- The YesSql `IStore` connection factory is wrapped with `MiniProfilerConnectionFactory` (backed by `CurrentDbProfiler`), so SQL executed through YesSql appears as custom timings.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Mini Profiler

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.MiniProfiler"
      ],
      "disable": []
    }
  ]
}
```

### Granting Visibility to a Role

```json
{
  "steps": [
    {
      "name": "Roles",
      "Roles": [
        {
          "Name": "Developer",
          "Permissions": [
            "ViewMiniProfilerOnFrontEnd",
            "ViewMiniProfilerOnBackEnd"
          ]
        }
      ]
    }
  ]
}
```

### The Permissions

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace OrchardCore.MiniProfiler;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ViewMiniProfilerOnFrontEnd =
        new("ViewMiniProfilerOnFrontEnd", LocalizationSource.Create<Permissions>("View Mini Profiler widget on front end pages"));

    public static readonly Permission ViewMiniProfilerOnBackEnd =
        new("ViewMiniProfilerOnBackEnd", LocalizationSource.Create<Permissions>("View Mini Profiler widget on back end pages"));

    // Both permissions default to the Administrator role via GetDefaultStereotypes().
}
```

### Configure MiniProfiler

The module registers the upstream `StackExchange.Profiling.MiniProfilerOptions` in each tenant container. Configure them from the web project with `ConfigureServices()` after `AddOrchardCms()`. The module does not bind `MiniProfilerOptions` from configuration.

```csharp
using StackExchange.Profiling;

builder.Services
    .AddOrchardCms()
    .ConfigureServices(tenantServices =>
        tenantServices.Configure<MiniProfilerOptions>(options =>
        {
            options.IgnoredPaths.Add("/health");
            options.PopupRenderPosition = RenderPosition.Right;

            // Protect stored results. MiniProfiler routes run before Orchard authentication,
            // so authenticate explicitly instead of relying on HttpContext.User.
            options.ResultsAuthorizeAsync = async request =>
            {
                var result = await request.HttpContext.AuthenticateAsync();

                return result.Succeeded && result.Principal.IsInRole("Administrator");
            };
        }));
```

Useful options to reduce overhead and noise:

- `ShouldProfile` — limit profiling by environment, identity, path, or endpoint.
- `IgnoredPaths` — skip health checks or high-volume endpoints.
- `EnableMvcFilterProfiling`, `EnableMvcViewProfiling`, `MvcFilterMinimumSaveMs`, `MvcViewMinimumSaveMs` — trim MVC timings.
- `TrackConnectionOpenClose`, `ExcludeStackTraceSnippetFromCustomTimings` — trim SQL detail.
- Widget options: `PopupRenderPosition`, `PopupShowTrivial`, `PopupShowTimeWithChildren`, `PopupMaxTracesToShow`, `PopupStartHidden`, `PopupToggleKeyboardShortcut`, `ShowControls`, `ColorScheme`.

The shape and YesSql integrations have no separate settings; they are active whenever the feature is enabled.

### What You See

- On front-end pages the widget uses MiniProfiler's default position (top-left).
- On admin pages it is docked to the middle of the right edge (left edge in right-to-left languages), clear of the admin menu and top navbar. Its toggle hides or shows the profiles, the browser remembers whether it is collapsed, and it follows the admin light or dark theme.
- Click a timing to see request, MVC, view, and `Shape: <type>` steps.
- Click the SQL duration to open the queries view: commands in execution order (`#1`, `#2`, ...) with duration, start offset, time to first result, and the step that ran them. Gaps between queries show which step spent the time.
  - **Duplicate** marks a command whose text already ran in the same request (missing cache or a query inside a loop).
  - **Error** marks a command that threw.
  - A red `!` in the timing tree and on the widget button flags requests and steps with duplicate or failing commands.

### Storage and Multiple Instances

Results are kept in the tenant's in-memory cache for 30 minutes, are lost on restart, and are not shared between instances. Behind a load balancer use session affinity while diagnosing, or set `MiniProfilerOptions.Storage` to a shared `IAsyncStorage` from a MiniProfiler storage package (installed in the web project).

### Custom Timings

```csharp
using StackExchange.Profiling;

using (MiniProfiler.Current?.Step("Load recommendations"))
{
    await recommendationService.LoadAsync();
}
```

`MiniProfiler.Current` is `null` when the request is not profiled.

### Troubleshooting

- **No widget**: check the feature is enabled for the tenant, the role has the front-end or back-end permission for that page, the page is an MVC view or Razor Page using an Orchard layout that renders the `Footer` zone, and `ShouldProfile`/`IgnoredPaths` allow the request. APIs and decoupled responses are profiled but get no widget. Check `/mini-profiler-resources` requests, including the tenant URL prefix.
- **Missing result**: results expire after 30 minutes, are cleared on restart, and live in one instance's cache; also check `ResultsAuthorize`.
- **Missing SQL or shape timings**: only YesSql calls through the tenant's wrapped connection and shapes rendered by display management are instrumented. Other data access libraries need their own MiniProfiler integration.

### Notes

- For production-style, always-on measurements of shape rendering, prefer the `OrchardCore.DisplayManagement` activity source and meter with OpenTelemetry (see `orchardcore-display-management`) over MiniProfiler.
- Use it together with `orchardcore-diagnostics` and `orchardcore-logging-serilog` when investigating performance and failures.
- Repeated identical SQL (the **Duplicate** badge) usually indicates an N+1 pattern that a batched YesSql query, an index, or a cache would fix.

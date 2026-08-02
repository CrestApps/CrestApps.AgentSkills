---
name: orchardcore-mini-profiler
description: Skill for adding the MiniProfiler performance widget to Orchard Core front-end and admin pages. Covers enabling the feature, permissions, database connection profiling, and shape rendering timings. Use this skill when requests mention Orchard Core Mini Profiler, MiniProfiler, performance profiling, request timings, SQL query profiling, slow page diagnosis, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.MiniProfiler, MiniProfilerFilter, MiniProfilerConnectionFactory, CurrentDbProfiler, ShapeStep, ViewMiniProfilerOnFrontEnd, and ViewMiniProfilerOnBackEnd permissions. It also helps with diagnosing slow shapes, profiling YesSql database calls, and the configuration patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Mini Profiler - Prompt Templates

## Add Performance Profiling

You are an Orchard Core expert. Enable and configure the MiniProfiler widget so authorized users can see request, shape, and database timings on Orchard Core pages.

### Guidelines

- Enable the `OrchardCore.MiniProfiler` feature. It has no content dependencies and is intended for development and diagnostics, not production end users.
- The MiniProfiler widget only renders for users who hold the relevant permission, so it is safe to leave enabled while restricting visibility by role.
- Two permissions gate visibility:
  - `ViewMiniProfilerOnFrontEnd` — show the widget on front-end (themed) pages.
  - `ViewMiniProfilerOnBackEnd` — show the widget on admin pages.
  - By default both permissions are granted to the `Administrator` role via a permission stereotype.
- The module registers early in the pipeline (`Startup.Order = -500`) so `app.UseMiniProfiler()` wraps all downstream middleware and captures the full request timing.
- A `MiniProfilerFilter` MVC filter starts and stops the profiler around action execution.
- An `IShapeDisplayEvents` implementation (`ShapeStep`) records per-shape display timings, so you can find slow shapes/templates.
- Database calls are profiled by wrapping the YesSql `IStore` connection factory with `MiniProfilerConnectionFactory` (backed by `CurrentDbProfiler`), so SQL executed through YesSql appears in the timing breakdown.
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

Grant one or both permissions so non-administrators can see the widget. In a recipe:

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
using OrchardCore.Security.Permissions;

namespace OrchardCore.MiniProfiler;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ViewMiniProfilerOnFrontEnd =
        new("ViewMiniProfilerOnFrontEnd", "View Mini Profiler widget on front end pages");

    public static readonly Permission ViewMiniProfilerOnBackEnd =
        new("ViewMiniProfilerOnBackEnd", "View Mini Profiler widget on back end pages");

    // Both permissions default to the Administrator role via GetDefaultStereotypes().
}
```

### How Profiling Is Wired

- `services.AddMiniProfiler()` registers the underlying StackExchange.Profiling services.
- `options.Filters.Add<MiniProfilerFilter>()` scopes a profiler step around each MVC action.
- `services.AddScoped<IShapeDisplayEvents, ShapeStep>()` times each rendered shape.
- In `Configure`, the current `IStore.Configuration.ConnectionFactory` is wrapped with `MiniProfilerConnectionFactory`, so all YesSql database traffic is measured.

### What You See

- A profiler badge is injected into the page for authorized users.
- Expanding it shows total request time, per-action timings, per-shape display timings, and the SQL queries executed with their durations and (optionally) duplicated-query warnings.

### Notes

- Because it early-wraps the pipeline and profiles the database connection, keep it disabled or permission-restricted in production to avoid overhead and information disclosure.
- Use it together with `OrchardCore.Diagnostics` (error pages) and `OrchardCore.Logging.Serilog` when investigating performance and failures.
- For deeper query analysis, look for repeated identical SQL in the timing breakdown — that usually indicates an N+1 access pattern that a batched YesSql query or index would fix.

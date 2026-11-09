---
name: orchardcore-background-tasks
description: Skill for creating background tasks and scheduled jobs in Orchard Core. Covers IBackgroundTask implementation, the BackgroundTask attribute (Title, Schedule, Description, Enable, LockTimeout, LockExpiration, UsePipeline), atomic tasks with distributed locks, BackgroundServiceOptions, IBackgroundTaskEventHandler, the Background Tasks admin list with execution state, details page, Run now and bulk actions, IBackgroundTaskMonitor, and web farm behavior. Use this skill when requests mention Orchard Core Background Tasks, creating or registering a background task, task schedules or lock settings, running a task on demand, why a task failed, cron expressions, or closely related setup or troubleshooting work. Strong matches include OrchardCore.BackgroundTasks, IBackgroundTask, BackgroundTaskAttribute, BackgroundTaskSettings, BackgroundServiceOptions, MaxRecentRuns, IBackgroundTaskMonitor, BackgroundTaskState, RequestRunAsync, ManageBackgroundTasks.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Background Tasks - Prompt Templates

## Create Background Tasks

You are an Orchard Core expert. Generate background task implementations for Orchard Core.

### Guidelines

- Background tasks implement `IBackgroundTask` and run on a cron schedule declared with `[BackgroundTask]`.
- Tasks are registered in `Startup.cs` as `IBackgroundTask` singletons. Inject only singleton-safe services in the constructor; resolve scoped services (`ISession`, `IContentManager`, ...) from the `IServiceProvider` passed to `DoWorkAsync`.
- Each run gets its own tenant shell scope. The schedule is evaluated in the site time zone.
- `[BackgroundTask]` properties: `Title`, `Schedule` (default `*/5 * * * *`), `Description`, `Enable` (default `true`), `LockTimeout` and `LockExpiration` (milliseconds), and `UsePipeline` (build the tenant routing pipeline so the task can generate URLs).
- A task is atomic when both `LockTimeout` and `LockExpiration` are greater than `0`: on a distributed lock (for example Redis), only one instance runs it at a time. Lock values are not limited to 30 seconds.
- Enable `OrchardCore.BackgroundTasks` to manage tasks under **Tools** → **Background Tasks**: enable or disable, change the schedule, the lock values, and `UsePipeline`, overriding the attribute values. This requires the `ManageBackgroundTasks` permission.
- The admin list supports bulk actions: select several tasks and use **Actions** → **Enable**, **Disable** or **Run now**.
- The list shows each task's execution state (running or queued, last run outcome and duration, next run, failures, last error), and a **Status** filter for running/queued or failed tasks. Clicking a title opens a details page with run counts, the last error's stack trace and recent runs. Pages poll and update in place.
- **Run now** queues a task within a second or two regardless of its schedule and of whether it is enabled, unless it is already running or queued. It runs only on the server that handled the request, while scheduled runs happen on every server of a farm.
- The execution state lives in memory per server (reset on restart, not stored in the database), so in a web farm these admin pages need session affinity.
- A task without a `Title` is shown with a title built from its type name (`ChunkFileUploadBackgroundTask` → **Chunk File Upload**). Prefer setting `Title` explicitly.
- The editor rejects a schedule that is not a valid cron expression. An unusable schedule (for example `0 0 31 2 *`) shows the next run as **never** and only stops that task from running on schedule.
- Use `ILogger` for logging task execution and errors, and honor the `CancellationToken`.
- Tasks should be idempotent and handle concurrent execution gracefully.
- Always seal classes.

### Basic Background Task

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

[BackgroundTask(
    Title = "{{TaskTitle}}",
    Schedule = "*/15 * * * *",
    Description = "{{TaskDescription}}")]
public sealed class {{TaskName}} : IBackgroundTask
{
    private readonly ILogger<{{TaskName}}> _logger;

    public {{TaskName}}(ILogger<{{TaskName}}> logger)
    {
        _logger = logger;
    }

    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {{TaskName}}...");

        // Task logic here

        return Task.CompletedTask;
    }
}
```

### Background Task with Service Dependencies

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

[BackgroundTask(
    Title = "{{TaskTitle}}",
    Schedule = "0 */6 * * *",
    Description = "{{TaskDescription}}")]
public sealed class {{TaskName}} : IBackgroundTask
{
    private readonly ILogger<{{TaskName}}> _logger;

    public {{TaskName}}(ILogger<{{TaskName}}> logger)
    {
        _logger = logger;
    }

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // Resolve scoped services from the service provider of the run.
        var contentManager = serviceProvider.GetRequiredService<IContentManager>();
        var session = serviceProvider.GetRequiredService<ISession>();

        _logger.LogInformation("Running {{TaskName}}...");

        var items = await session
            .Query<ContentItem, ContentItemIndex>(x =>
                x.ContentType == "{{ContentType}}" && x.Published)
            .ListAsync();

        var count = 0;
        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Process item
            count++;
        }

        _logger.LogInformation("{{TaskName}} completed. Processed {Count} items.", count);
    }
}
```

### Atomic Background Task

Use a lock when the task must not run concurrently on several instances of a web farm. Values are in milliseconds and may exceed 30 seconds.

```csharp
using OrchardCore.BackgroundTasks;

[BackgroundTask(
    Title = "Nightly export",
    Schedule = "0 3 * * *",
    Description = "Exports data once per night across all instances.",
    LockTimeout = 3_000,
    LockExpiration = 300_000,
    UsePipeline = true)]
public sealed class NightlyExportTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // Long running work; the lock is held up to 5 minutes.
        return Task.CompletedTask;
    }
}
```

### Registering a Background Task

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IBackgroundTask, {{TaskName}}>();
    }
}
```

### Reacting to Task Execution

Implement `IBackgroundTaskEventHandler` to run code before and after every task of the tenant (for example telemetry). `BackgroundTaskEventContext` exposes `Name`, `Tenant`, `Services`, and `Exception`.

```csharp
using OrchardCore.BackgroundTasks;

public sealed class TaskTelemetryHandler : IBackgroundTaskEventHandler
{
    public Task ExecutingAsync(BackgroundTaskEventContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task ExecutedAsync(BackgroundTaskEventContext context, CancellationToken cancellationToken)
    {
        if (context.HasException)
        {
            // Report context.Exception for context.Name on context.Tenant.
        }

        return Task.CompletedTask;
    }
}
```

```csharp
services.AddScoped<IBackgroundTaskEventHandler, TaskTelemetryHandler>();
```

### Background Service Configuration

The host polls every tenant's tasks. Configure it in `appsettings.json` of the startup project under `OrchardCore:BackgroundService` (the 3.x name `OrchardCore_BackgroundService` is deprecated):

```json
{
  "OrchardCore": {
    "BackgroundService": {
      "PollingTime": "00:01:00",
      "MinimumIdleTime": "00:00:10",
      "ShellWarmup": false,
      "MaxRecentRuns": 10
    }
  }
}
```

`MaxRecentRuns` (default `10`) is the number of completed runs kept in memory per task for the details page.

### Reading the Execution State from Code

`IBackgroundTaskMonitor` (namespace `OrchardCore.BackgroundTasks`) is a host-level service registered by `AddBackgroundService()`: `GetStatesAsync(tenant)`, `GetStateAsync(tenant, name)` and `RequestRunAsync(tenant, name)`. Inject `IEnumerable<IBackgroundTaskMonitor>` when the code may run without the background service. The task name is the full type name returned by `IBackgroundTask.GetTaskName()`.

```csharp
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell;

public sealed class NightlyExportStatus
{
    private readonly IBackgroundTaskMonitor _monitor;
    private readonly ShellSettings _shellSettings;

    public NightlyExportStatus(IBackgroundTaskMonitor monitor, ShellSettings shellSettings)
    {
        _monitor = monitor;
        _shellSettings = shellSettings;
    }

    public async Task<bool> LastRunFailedAsync()
    {
        var state = await _monitor.GetStateAsync(_shellSettings.Name, typeof(NightlyExportTask).FullName);

        return state?.LastRun?.Result == BackgroundTaskRunResult.Failed;
    }

    // Returns Queued, AlreadyQueued, AlreadyRunning or NotFound.
    public Task<BackgroundTaskRunRequestResult> RunNowAsync()
        => _monitor.RequestRunAsync(_shellSettings.Name, typeof(NightlyExportTask).FullName);
}
```

`BackgroundTaskState` exposes `Status`, `QueuedTime`, `NextStartTime`, `LastRun`, `RunCount`, `FailureCount` and `RecentRuns`. Right after a tenant starts, `RequestRunAsync` can return `NotFound` until the next polling, which the request triggers; retry a moment later.

### Web Farms

- Scheduled tasks run on every server, each with its own schedule. Make a task atomic (`LockTimeout` and `LockExpiration` > 0) and enable a distributed lock such as `OrchardCore.Redis.Lock` so only one server runs it at a time; without a distributed lock the lock settings are ignored.
- Set `LockExpiration` above the longest expected run.
- Without `ShellWarmup`, a server only runs a tenant's tasks after that tenant served a request on it.

### Enabling Background Tasks Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.BackgroundTasks"
      ],
      "disable": []
    }
  ]
}
```

### Common Cron Schedule Expressions

- `* * * * *` — Every minute.
- `*/5 * * * *` — Every 5 minutes.
- `*/15 * * * *` — Every 15 minutes.
- `0 * * * *` — Every hour.
- `0 */6 * * *` — Every 6 hours.
- `0 0 * * *` — Daily at midnight.
- `0 0 * * 0` — Weekly on Sunday at midnight.
- `0 0 1 * *` — Monthly on the 1st at midnight.

### Upgrading from 3.x

- The Background Tasks admin list now renders through the shared `AdminList` shape (list name `BackgroundTasks`, see `orchardcore-admin-lists`) and offers bulk **Enable** and **Disable** actions.
- The editor no longer caps **Lock timeout** and **Lock expiration** at 30000 ms.
- Rename the `OrchardCore_BackgroundService` configuration section to `BackgroundService`.
- `BackgroundTaskState` gained execution-state properties, and `BackgroundTaskScheduler` gained `RequestRun()`, `Complete()`, `GetState()` and `GetNextStartTime()`.
- An invalid stored schedule no longer stops the background tasks of all tenants; it only prevents that task from running on schedule.
- Tasks without a `Title` now display a humanized type name instead of the full type name.

See [references/background-tasks-examples.md](references/background-tasks-examples.md) for a complete example.

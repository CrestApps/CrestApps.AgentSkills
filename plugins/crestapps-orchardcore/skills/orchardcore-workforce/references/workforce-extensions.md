# Workforce Extension Examples

Reference `CrestApps.OrchardCore.Workforce.Abstractions` for contracts and models, and `CrestApps.OrchardCore.Workforce.Core` for base types (`WorkforceEventActivity`, `WorkforceEventActivityDisplayDriver<TActivity>`, `RecipeImport`, `WorkforceReportDataSet<TRecord, TIndex>`, `WorkforceAIToolBase`, `MyWorkPortal`, `WorkforceSetupStep`). Never reference the Workforce module project.

## Send Worker Notices by SMS

Channels receive every notice the worker hasn't turned off, except during their quiet hours (only the inbox gets those). A failing channel is logged and the others still run.

```csharp
public sealed class SmsWorkforceNotificationChannel : IWorkforceNotificationChannel
{
    private readonly ISmsService _smsService;

    public SmsWorkforceNotificationChannel(ISmsService smsService)
    {
        _smsService = smsService;
    }

    public string Name => "Sms";

    public string DisplayName => "Text message";

    public async Task SendAsync(WorkerProfile worker, WorkforceNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(worker.Phone))
        {
            return;
        }

        await _smsService.SendAsync(new SmsMessage
        {
            To = worker.Phone,
            Body = $"{notification.Title}. {notification.Body}",
        }, cancellationToken);
    }
}

// Startup (in a feature that depends on CrestApps.OrchardCore.Workforce.Portal and OrchardCore.Sms):
// services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkforceNotificationChannel, SmsWorkforceNotificationChannel>());
```

## React to a Publish

`ISchedulePublishHandler.PublishedAsync` runs in the publishing request, after the shifts are written.

```csharp
public sealed class PublishSummaryHandler : ISchedulePublishHandler
{
    private readonly ILogger _logger;

    public PublishSummaryHandler(ILogger<PublishSummaryHandler> logger)
    {
        _logger = logger;
    }

    public Task PublishedAsync(SchedulePublishedContext context, CancellationToken cancellationToken = default)
    {
        var result = context.Result;

        _logger.LogInformation(
            "{Actor} published {PeriodId}: {Added} added, {Updated} updated, {Removed} removed, {Open} open.",
            context.Actor,
            context.Period.ItemId,
            result.Added,
            result.Updated,
            result.Removed,
            result.Open);

        return Task.CompletedTask;
    }
}

// Startup: services.AddScoped<ISchedulePublishHandler, PublishSummaryHandler>();
```

To refuse or warn before anything is written, implement `ISchedulePublishValidator.ValidateAsync(SchedulePublishValidationContext)` instead; the labor rules validator is one.

## React to Every Schedule Change

Every `ScheduleChange` (republish, trade, claim, time off, a manager on the shift's page) goes through the `IScheduleChangeHandler`s; the built-in ones raise the shift workflow events, notify the worker and record predictability pay.

```csharp
public sealed class LateChangeAuditHandler : IScheduleChangeHandler
{
    private readonly LateChangeLog _log;

    public LateChangeAuditHandler(LateChangeLog log)
    {
        _log = log;
    }

    public Task RecordedAsync(ScheduleChange change, CancellationToken cancellationToken = default)
    {
        if (change.InitiatedBy == ScheduleChangeInitiator.Employer)
        {
            _log.Add(change);
        }

        return Task.CompletedTask;
    }
}

// Startup: services.AddScoped<IScheduleChangeHandler, LateChangeAuditHandler>();
```

## Add a Payroll Export Format

```csharp
public sealed class HoursOnlyPayrollExportProvider : IPayrollExportProvider
{
    public string Name => "HoursOnly";

    public string DisplayName => "Hours per worker (CSV)";

    public Task<PayrollExportFile> ExportAsync(PayrollExportContext context, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder("EmployeeNumber,Hours\r\n");
        var rows = 0;

        foreach (var timesheet in context.Timesheets)
        {
            var worker = context.Workers.GetValueOrDefault(timesheet.WorkerId);
            var hours = timesheet.Lines.Sum(line => line.Hours);

            builder.Append(worker?.EmployeeNumber).Append(',').Append(hours.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            rows++;
        }

        return Task.FromResult(new PayrollExportFile
        {
            FileName = $"hours-{context.PeriodStart:yyyyMMdd}-{context.PeriodEnd:yyyyMMdd}.csv",
            ContentType = "text/csv",
            Content = builder.ToString(),
            RowCount = rows,
        });
    }
}

// Startup: services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayrollExportProvider, HoursOnlyPayrollExportProvider>());
```

Only locked timesheets are exported, once per provider and period unless asked again. Escape values that start like a formula (`=`, `+`, `-`, `@`) when you write a spreadsheet format.

## Add a Page to My Work

```csharp
public sealed class TrainingMyWorkLinkProvider : IMyWorkLinkProvider
{
    private readonly TrainingService _training;

    public TrainingMyWorkLinkProvider(TrainingService training)
    {
        _training = training;
    }

    public async Task<IEnumerable<MyWorkLink>> GetLinksAsync(ClaimsPrincipal user, WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        if (worker is null)
        {
            return [];
        }

        var due = await _training.CountDueAsync(worker.ItemId, cancellationToken);

        return [new MyWorkLink("training", "Training", "~/Admin/my-work/training", due)];
    }
}

// Startup: services.AddScoped<IMyWorkLinkProvider, TrainingMyWorkLinkProvider>();
```

Serve the page from your own admin controller at `[Admin("my-work/training", "MyWorkTraining")]`, authorize `WorkforcePermissions.UseWorkforcePortal`, find the worker with `IWorkerManager.FindByUserIdAsync`, and build the menu with `MyWorkPortal` so it matches the other pages.

## Replace the Auto-Scheduler

Solvers are registered as an enumerable and picked by name with `ShiftPlanningOptions.DefaultSolver`. A solver must never write; it returns a `ShiftScheduleProposal` the planning service adds to the scenario. Start from the built-in answer and keep the better one, as the OR-Tools add-on does.

```csharp
public sealed class MyShiftScheduleSolver : IShiftScheduleSolver
{
    private readonly DefaultShiftScheduleSolver _fallback;

    public MyShiftScheduleSolver(DefaultShiftScheduleSolver fallback)
    {
        _fallback = fallback;
    }

    public string Name => "Mine";

    public async Task<ShiftScheduleProposal> SolveAsync(ShiftScheduleProblem problem, CancellationToken cancellationToken = default)
    {
        var baseline = await _fallback.SolveAsync(problem, cancellationToken);

        // Improve on the baseline here, within problem's time limit, and return the better proposal.
        return baseline;
    }
}

// Startup:
// services.TryAddEnumerable(ServiceDescriptor.Singleton<IShiftScheduleSolver, MyShiftScheduleSolver>());
// services.Configure<ShiftPlanningOptions>(options => options.DefaultSolver = "Mine");
```

Register `DefaultShiftScheduleSolver` itself as a singleton if you inject it, as above.

## A Workflow Event Limited to Some Teams

Derive from `WorkforceEventActivity` (category **Workforce**, a team filter, a `Done` outcome) and raise it with `IWorkforceEvents.RaiseAsync(name, input, correlationId)` using the activity's class name; with Workflows on, `WorkflowWorkforceEvents` triggers the workflows that start with it.

```csharp
await _workforceEvents.RaiseAsync(
    nameof(TrainingCompletedEvent),
    new Dictionary<string, object>
    {
        ["WorkerId"] = worker.ItemId,
        ["TeamId"] = worker.PrimaryTeamId,
        ["CourseId"] = course.ItemId,
    },
    correlationId: course.ItemId);

// Startup (behind [RequireFeatures("OrchardCore.Workflows")]):
// services.AddActivity<TrainingCompletedEvent, WorkforceEventActivityDisplayDriver<TrainingCompletedEvent>>();
```

## Worker Editor Section View

The worker editor sections are cards with one field a row, like the built-in `WorkerProfileFields_Edit`:

```cshtml
@model WorkerUniformViewModel

<div class="card text-bg-theme mb-3">
    <div class="card-header">@T["Uniform"]</div>
    <div class="card-body">
        <div class="ocat-wrapper">
            <label asp-for="Size" class="ocat-label">@T["Size"]</label>
            <div class="ocat-end">
                <input asp-for="Size" class="form-control" style="max-width: 10rem" />
                <span asp-validation-for="Size" class="text-danger"></span>
            </div>
        </div>
    </div>
</div>
```

The same `IDisplayManager<T>` pattern applies to the shared team editor (`DisplayDriver<Team>`, see `orchardcore-teams`), the work order and asset editors (`orchardcore-work-orders`, `orchardcore-assets`) and the scheduling resource and appointment type editors (`orchardcore-scheduling`).

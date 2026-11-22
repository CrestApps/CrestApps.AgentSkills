# Service Levels Extension Examples

A complete example of putting SLA timers on a custom record kind, `Inspection`, owned by your own module. Reference `CrestApps.OrchardCore.ServiceLevels.Abstractions` and make your feature depend on `CrestApps.OrchardCore.ServiceLevels`.

## Scope

The scope names the metrics and priorities the policy editor shows. Every `LocalizationSource` property is `[JsonIgnore]`.

```csharp
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

public sealed class InspectionServiceLevelScope : IServiceLevelScopeProvider
{
    public const string ScopeName = "Inspections";

    private static readonly LocalizationSource _displayName = LocalizationSource.Create<InspectionServiceLevelScope>("Inspections");
    private static readonly LocalizationSource _description = LocalizationSource.Create<InspectionServiceLevelScope>("How fast inspections are scheduled and closed, at each priority.");

    internal readonly IStringLocalizer S;

    public InspectionServiceLevelScope(IStringLocalizer<InspectionServiceLevelScope> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Name => ScopeName;

    [JsonIgnore]
    public LocalizationSource DisplayName => _displayName;

    [JsonIgnore]
    public LocalizationSource Description => _description;

    public Task<IReadOnlyList<ServiceLevelOption>> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServiceLevelOption> metrics =
        [
            new(ServiceLevelMetrics.FirstResponse, S["Scheduled"]),
            new(ServiceLevelMetrics.Resolution, S["Closed"]),
        ];

        return Task.FromResult(metrics);
    }

    public Task<IReadOnlyList<ServiceLevelOption>> GetPrioritiesAsync(CancellationToken cancellationToken = default)
    {
        // The key is what targets store; keep it stable (an id or an enum name), never a translated text.
        IReadOnlyList<ServiceLevelOption> priorities =
        [
            new("High", S["High"]),
            new("Normal", S["Normal"]),
        ];

        return Task.FromResult(priorities);
    }
}
```

## Driving Timers From the Owner

Call the tracker from the code that changes the record, under the record's lock.

```csharp
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;

public sealed class InspectionTimers
{
    public const string RecordKind = "Inspection";

    private readonly IServiceLevelTracker _tracker;

    public InspectionTimers(IServiceLevelTracker tracker)
    {
        _tracker = tracker;
    }

    public async Task CreatedAsync(string inspectionId, string policyId, string priority)
    {
        var subject = new RecordRef(RecordKind, inspectionId);

        foreach (var metric in new[] { ServiceLevelMetrics.FirstResponse, ServiceLevelMetrics.Resolution })
        {
            await _tracker.StartAsync(new ServiceLevelStart
            {
                Subject = subject,
                Metric = metric,
                PolicyId = policyId,
                Priority = priority,
            });
        }
    }

    public Task ScheduledAsync(string inspectionId)
        => _tracker.CompleteAsync(new RecordRef(RecordKind, inspectionId), ServiceLevelMetrics.FirstResponse);

    public Task WaitingForCustomerAsync(string inspectionId)
        => _tracker.PauseAsync(new RecordRef(RecordKind, inspectionId));

    public Task CustomerAnsweredAsync(string inspectionId)
        => _tracker.ResumeAsync(new RecordRef(RecordKind, inspectionId));

    public Task PriorityChangedAsync(string inspectionId, string policyId, string priority)
        => _tracker.RetargetAsync(new RecordRef(RecordKind, inspectionId), policyId, priority);

    public Task ClosedAsync(string inspectionId)
        => _tracker.CompleteAsync(new RecordRef(RecordKind, inspectionId), ServiceLevelMetrics.Resolution);

    public Task DeletedAsync(string inspectionId)
        => _tracker.CancelAsync(new RecordRef(RecordKind, inspectionId));
}
```

To bring timers from another system, set `ServiceLevelStart.StartUtc`, `DueUtc`, `PausedSinceUtc`, `CompletedUtc`, `AlreadyWarned` and `AlreadyBreached` so the monitor doesn't announce them again.

## Observer

The monitor calls observers after its own save commits. Only act on your own record kind.

```csharp
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.Logging;

public sealed class InspectionServiceLevelObserver : IServiceLevelObserver
{
    private readonly ILogger _logger;

    public InspectionServiceLevelObserver(ILogger<InspectionServiceLevelObserver> logger)
    {
        _logger = logger;
    }

    public Task WarningAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
    {
        if (timer.Subject.Kind == InspectionTimers.RecordKind)
        {
            _logger.LogInformation("Inspection {Id} is close to missing its {Metric} target.", timer.Subject.Id, timer.Metric);
        }

        return Task.CompletedTask;
    }

    public Task BreachedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
    {
        if (timer.Subject.Kind == InspectionTimers.RecordKind)
        {
            _logger.LogWarning("Inspection {Id} missed its {Metric} target due {Due}.", timer.Subject.Id, timer.Metric, timer.DueUtc);
        }

        return Task.CompletedTask;
    }
}
```

## Policy Usage and Timer Access

```csharp
using System.Security.Claims;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.AspNetCore.Authorization;

public sealed class InspectionPolicyUsage : IServiceLevelPolicyUsageProvider
{
    private readonly IInspectionTypeStore _types;

    public InspectionPolicyUsage(IInspectionTypeStore types)
    {
        _types = types;
    }

    public async Task<IReadOnlyList<string>> GetUsagesAsync(ServiceLevelPolicy policy, CancellationToken cancellationToken = default)
    {
        var types = await _types.ListAsync(cancellationToken);

        return [.. types.Where(type => type.ServiceLevelPolicyId == policy.ItemId).Select(type => $"Inspection type: {type.Name}")];
    }
}

public sealed class InspectionTimerAccess : IServiceLevelTimerAccessProvider
{
    private readonly IAuthorizationService _authorizationService;

    public InspectionTimerAccess(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public string SubjectKind => InspectionTimers.RecordKind;

    public async Task<IReadOnlySet<string>> GetVisibleAsync(ClaimsPrincipal user, IReadOnlyCollection<string> subjectIds, CancellationToken cancellationToken = default)
    {
        // One permission check for the batch; a provider over a large store filters ids with one query instead.
        return await _authorizationService.AuthorizeAsync(user, InspectionPermissions.ViewInspections)
            ? subjectIds.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }
}
```

`IInspectionTypeStore` and `InspectionPermissions` stand for your module's own types.

## Registration

```csharp
using CrestApps.OrchardCore.ServiceLevels;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<InspectionTimers>();
        services.AddServiceLevelScope<InspectionServiceLevelScope>();
        services.AddServiceLevelObserver<InspectionServiceLevelObserver>();
        services.AddServiceLevelPolicyUsage<InspectionPolicyUsage>();
        services.AddServiceLevelTimerAccessProvider<InspectionTimerAccess>();
    }
}
```

Add a policy card for your scope with a `DisplayDriver<ServiceLevelPolicy>` that returns `null` when `policy.Scope` isn't yours and stores its values with `policy.Put(...)`.

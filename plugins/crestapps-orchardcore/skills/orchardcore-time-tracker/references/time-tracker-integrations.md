# Time Tracker Integration Examples

Reference `CrestApps.OrchardCore.TimeTracker.Abstractions` for `ITimeTrackerService`, `TimeEntrySync`, `IExternalTimeClock` and `ITimeEntryObserver`.

## Keep Another Clock's Time in Time Tracker

A module with its own record of work (a badge system, a field app) keeps it as the source of truth and mirrors it into Time Tracker, so the Hours by Category report counts it with everyone else's time. Rebuild a range after every change; `SyncAsync` diffs the result, keeps the ids of unchanged entries and cuts your entries around time a manager added here.

```csharp
public sealed class BadgeTimeTracker
{
    public const string ClockId = "Badges";

    private readonly ITimeTrackerService _timeTracker;
    private readonly BadgeSwipeStore _swipes;

    public BadgeTimeTracker(ITimeTrackerService timeTracker, BadgeSwipeStore swipes)
    {
        _timeTracker = timeTracker;
        _swipes = swipes;
    }

    public Task<TimeTrackerResult> SyncDayAsync(string userId, string userName, string workStageId, DateTime dayStartUtc)
    {
        var fromUtc = dayStartUtc.AddDays(-1);
        var toUtc = dayStartUtc.AddDays(2);

        return _timeTracker.SyncAsync(new TimeEntrySync
        {
            UserId = userId,
            UserName = userName,
            ManagedBy = ClockId,
            Source = TimeTrackerConstants.Sources.Kiosk,
            FromUtc = fromUtc,
            ToUtc = toUtc,

            // Called under the person's lock, so two syncs never write stale time.
            BuildAsync = async cancellationToken =>
            {
                var pairs = await _swipes.ListPairsAsync(userId, fromUtc, toUtc, cancellationToken);

                return pairs.Select(pair => new TimeEntry
                {
                    UserId = userId,
                    UserName = userName,
                    StageId = workStageId,
                    StartUtc = pair.InUtc,
                    EndUtc = pair.OutUtc,
                    SourceRef = pair.InSwipeId,
                }).ToList();
            },
        });
    }
}
```

- Leave `EndUtc` empty for the stretch the person is in now.
- For a clock that reads the end of a growing log (as the Contact Center does), set `ReplacesRangeOnly = true` and sync only the last minutes: the entry running into the range ends at its start, or continues when the range starts in the same stage.
- Set `Overrides = ["Workforce.TimeClock"]` when your entries should win over the time clock's for the same minutes.

## Tell Time Tracker the Clock Keeps a Person

Without an `IExternalTimeClock`, people could also clock themselves in on My Time and double their time. With one, My Time refuses `SwitchAsync` for them and shows your controls or a link, and entries with your `ManagedBy` are shown as kept by your clock.

```csharp
public sealed class BadgeExternalTimeClock : IExternalTimeClock
{
    private readonly BadgeHolderStore _holders;

    public BadgeExternalTimeClock(BadgeHolderStore holders)
    {
        _holders = holders;
    }

    public async Task<ExternalTimeClockInfo> FindAsync(string userId, CancellationToken cancellationToken = default)
        => await _holders.HasBadgeAsync(userId, cancellationToken) ? Info() : null;

    public Task<ExternalTimeClockInfo> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(id == BadgeTimeTracker.ClockId ? Info() : null);

    private static ExternalTimeClockInfo Info() => new()
    {
        Id = BadgeTimeTracker.ClockId,
        Name = "Badge readers",
        Url = "~/Admin/badges/my",
        ManageUrl = "~/Admin/badges/swipes",
    };
}

// Startup: services.TryAddEnumerable(ServiceDescriptor.Scoped<IExternalTimeClock, BadgeExternalTimeClock>());
```

Set `ControlsViewComponent` to the name of a view component to render your own buttons inside My Time, as the Workforce time clock does with `TimeClockControls`.

## Observe Every Change

```csharp
public sealed class PayrollFeedTimeEntryObserver : ITimeEntryObserver
{
    private readonly PayrollFeed _feed;

    public PayrollFeedTimeEntryObserver(PayrollFeed feed)
    {
        _feed = feed;
    }

    public Task OnChangedAsync(TimeEntryChangeKind kind, TimeEntry entry, string before, string actor, string reason, CancellationToken cancellationToken = default)
    {
        if (kind is TimeEntryChangeKind.Edited or TimeEntryChangeKind.Deleted)
        {
            _feed.Flag(entry.UserId, entry.StartUtc, actor, reason);
        }

        return Task.CompletedTask;
    }
}

// Startup: services.AddScoped<ITimeEntryObserver, PayrollFeedTimeEntryObserver>();
```

## Set Up Stages From Code

```csharp
var category = await _categoryManager.NewAsync();
category.Name = "Billable";
category.Code = "BILL";

var validation = await _categoryManager.ValidateAsync(category);

if (validation.Succeeded)
{
    await _categoryManager.CreateAsync(category);
}

var stage = await _stageManager.NewAsync();
stage.Name = "Client work";
stage.Code = "CLIENT";
stage.CategoryIds.Add(category.ItemId);

await _stageManager.CreateAsync(stage);
```

`_categoryManager` is `ITimeCategoryManager` and `_stageManager` is `ITimeStageManager`.

## Contact Center Stages

With the Contact Center Time Tracker feature (`CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracker`), the `ContactCenterTimeStages` site setting maps each agent presence status, and each break reason (which wins), to a stage by name. When turned on, it seeds Available, On a call, Wrap-up, Meeting and Training into `Paid` and `Worked`, and Break and Away into no category; Do not disturb, after hours and offline have no stage and aren't tracked. It is exported with the **Contact Center time stages** deployment step.

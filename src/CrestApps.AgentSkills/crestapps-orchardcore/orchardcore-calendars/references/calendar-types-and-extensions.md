# Calendar Types and Extensions

Worked examples for building on `CrestApps.OrchardCore.Calendars` from another module. Reference `CrestApps.OrchardCore.Calendars.Abstractions` (namespaces `CrestApps.OrchardCore.Calendars`, `.Models`, `.Services`, `.Recurrence`). For an optional integration, put the registrations in a startup of your own feature:

```csharp
[RequireFeatures("CrestApps.OrchardCore.Calendars")]
public sealed class CalendarStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCalendarType("Vehicles", type => type
            .WithDisplayName(LocalizationSource.Create<CalendarStartup>("Vehicles"))
            .WithDescription(LocalizationSource.Create<CalendarStartup>("Pool vehicles. Fleet creates these calendars."))
            .WithIcon("fa-solid fa-car")
            .WithColor("#1971c2")
            .WithPosition(70)
            .ManagedByCode()
            .AddEventType("Trip", LocalizationSource.Create<CalendarStartup>("Trip"), e => e.Color("#1971c2").Busy())
            .AddEventType("Service", LocalizationSource.Create<CalendarStartup>("Service"), e => e.Color("#868e96").AllDayByDefault()));

        services.AddScoped<FleetCalendarWriter>();
        services.AddCalendarBusyTimeSource<FleetBusyTimeSource>();
        services.AddCalendarEventSource<FleetEventSource>();
        services.AddCalendarAccessContributor<FleetCalendarAccessContributor>();
        services.AddScoped<ICalendarEventHandler, TripRulesHandler>();
        services.AddDisplayDriver<Calendar, VehicleSettingsDisplayDriver>();
    }
}
```

Code outside that startup (a controller, a view) resolves the integration's services with `GetService` and treats `null` as "Calendars is off".

## A Calendar Your Module Manages

Create it once and find it again by `Source` and `SourceId`. `NewAsync` returns `null` while the type's feature is off.

```csharp
public sealed class FleetCalendarWriter
{
    private const string Source = "Fleet";

    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventManager _eventManager;

    public FleetCalendarWriter(ICalendarService calendarService, ICalendarEventManager eventManager)
    {
        _calendarService = calendarService;
        _eventManager = eventManager;
    }

    public async Task<Calendar> GetCalendarAsync(Vehicle vehicle)
    {
        var calendar = await _calendarService.FindBySourceAsync(Source, vehicle.Id);

        if (calendar is not null)
        {
            return calendar;
        }

        calendar = await _calendarService.NewAsync("Vehicles");

        if (calendar is null)
        {
            return null;
        }

        calendar.Name = vehicle.Name;
        calendar.TimeZoneId = "America/New_York";
        calendar.Source = Source;
        calendar.SourceId = vehicle.Id;

        var result = await _calendarService.CreateAsync(calendar, new CalendarChangeOptions { Actor = Source });

        return result.Succeeded ? result.Value : null;
    }

    public async Task<IReadOnlyList<CalendarError>> AddTripAsync(Calendar calendar, Trip trip)
    {
        var calendarEvent = await _eventManager.NewAsync(calendar, "Trip");

        calendarEvent.Title = trip.Title;
        calendarEvent.Start = CalendarTime.ToLocal(trip.StartsUtc, calendarEvent.TimeZoneId);
        calendarEvent.End = CalendarTime.ToLocal(trip.EndsUtc, calendarEvent.TimeZoneId);
        calendarEvent.Source = Source;
        calendarEvent.SourceId = trip.Id;
        calendarEvent.Editable = false;
        calendarEvent.Attendees.Add(new CalendarAttendee { UserId = trip.DriverId, Role = CalendarAttendeeRole.Required });

        var result = await _eventManager.CreateAsync(calendarEvent, new CalendarChangeOptions
        {
            Actor = Source,
            SendInvitations = false,
        });

        // CalendarError.ConflictCode marks an overlap refusal on a LimitConcurrent calendar.
        return result.Errors;
    }

    public async Task MoveTripAsync(Trip trip)
    {
        foreach (var calendarEvent in await _eventManager.FindBySourceAsync(Source, trip.Id))
        {
            await _eventManager.UpdateAsync(calendarEvent, target =>
            {
                target.Start = CalendarTime.ToLocal(trip.StartsUtc, target.TimeZoneId);
                target.End = CalendarTime.ToLocal(trip.EndsUtc, target.TimeZoneId);
            }, new CalendarChangeOptions { Actor = Source, SendInvitations = false });
        }
    }
}
```

- `UpdateAsync(event, change, options)` rolls the change back on the object when refused.
- Repeating events: `UpdateOccurrenceAsync(series, originalStartUtc, CalendarEditScope.ThisOccurrence, change, options)` and `CancelOccurrenceAsync(...)`; `ThisAndFollowing` splits the series, `AllOccurrences` edits the master.
- Write the event in the same request as your record so both commit or neither does (Bookings writes its event from `IBookingHandler.SavingAsync`).
- `SkipConflictCheck = true` only when you already checked availability under your own lock. If your own lock is held, add `CalendarLockKeys.Calendar(calendarId)` keys to the same ordered lock set and pass `CalendarLocksHeld = true` so the calendar's own overlap check runs under your locks (Scheduling does this).
- `Notify = false` keeps workflows and observers from hearing about the change.

## Type-Specific Fields

`Calendar` and `CalendarEvent` are `CatalogItem`s: keep typed data in `Properties` (`Put<T>`, `TryGet<T>`, `GetOrCreate<T>`, `Alter<T>`; key = type name) and edit it with a driver that answers only for your type.

```csharp
public sealed class VehicleSettings
{
    public string Plate { get; set; }
}

public class VehicleSettingsViewModel
{
    public string Plate { get; set; }
}

public sealed class VehicleSettingsDisplayDriver : DisplayDriver<Calendar>
{
    public override IDisplayResult Edit(Calendar calendar, BuildEditorContext context)
    {
        if (calendar.Type != "Vehicles")
        {
            return null;
        }

        var settings = calendar.GetOrCreate<VehicleSettings>();

        return Initialize<VehicleSettingsViewModel>("VehicleSettings_Edit", model => model.Plate = settings.Plate)
            .Location("Content:3%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(Calendar calendar, UpdateEditorContext context)
    {
        if (calendar.Type != "Vehicles")
        {
            return null;
        }

        var model = new VehicleSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        calendar.Put(new VehicleSettings { Plate = model.Plate?.Trim() });

        return Edit(calendar, context);
    }
}
```

Any wall-clock `DateTime` you add to a model or a typed properties object needs `[JsonConverter(typeof(WallClockDateTimeJsonConverter))]` (`WallClockDateTimeListJsonConverter` for lists); YesSql would otherwise treat it as server-local time.

## Validate Inside the Request

```csharp
public sealed class TripRulesHandler : CalendarEventHandlerBase
{
    public override Task ValidatingAsync(CalendarEventValidatingContext context, CancellationToken cancellationToken = default)
    {
        if (context.CalendarType?.Name != "Vehicles" || context.Event.EventType != "Trip")
        {
            return Task.CompletedTask;
        }

        if ((context.Event.End - context.Event.Start) > TimeSpan.FromHours(12))
        {
            context.Errors.Add(new CalendarError(nameof(CalendarEvent.End), "A trip can't be longer than 12 hours."));
        }

        return Task.CompletedTask;
    }
}
```

`ICalendarHandler` (`CalendarHandlerBase`) does the same for calendars: `ValidatingAsync(CalendarValidatingContext)` (with `IsNew` and `Errors`), `CreatedAsync`, `UpdatedAsync`, `DeletingAsync`. Handlers run for every calendar, so always check the type.

## Show Records Read-Only on the Calendar

Use an event source when records already live elsewhere and must not be copied; use real events when they need overlap checks, invitations, feeds or workflows.

```csharp
public sealed class FleetEventSource : ICalendarEventSource
{
    private readonly ITripStore _trips;

    public FleetEventSource(ITripStore trips)
    {
        _trips = trips;
    }

    public string Name => "Fleet";

    public async Task<IEnumerable<CalendarOccurrence>> GetOccurrencesAsync(CalendarOccurrenceQuery query, CancellationToken cancellationToken = default)
    {
        if ((query.Sources.Count > 0 && !query.Sources.Contains(Name)) ||
            (query.EventTypes.Count > 0 && !query.EventTypes.Contains("Trip")))
        {
            return [];
        }

        var trips = await _trips.ListForCalendarsAsync(query.CalendarIds, query.FromUtc, query.ToUtc, cancellationToken);

        return trips.Select(trip => new CalendarOccurrence
        {
            Event = new CalendarEvent
            {
                ItemId = trip.Id,
                CalendarId = trip.CalendarId,
                EventType = "Trip",
                Title = trip.Title,
                Source = Name,
                SourceId = trip.Id,
                Editable = false,
            },
            StartUtc = trip.StartsUtc,
            EndUtc = trip.EndsUtc,
        });
    }
}
```

The query service applies none of its own rules to a source's occurrences: honor `query.Viewer` (hide details the viewer may not see), `Sources`, `EventTypes`, `CalendarIds` / `AttendeeUserIds` / `AllCalendars`, and stay inside `CalendarOptions.MaxEventsPerQuery`. A source that throws is logged and skipped. The reference implementation is the Contact Center's callback source (`ContactCenter.Callbacks`).

## Grant Access for Your Own Reasons

```csharp
public sealed class FleetCalendarAccessContributor : ICalendarAccessContributor
{
    private readonly IFleetMembership _membership;

    public FleetCalendarAccessContributor(IFleetMembership membership)
    {
        _membership = membership;
    }

    public async Task<CalendarAccessLevel> GetAccessLevelAsync(ClaimsPrincipal user, Calendar calendar, CancellationToken cancellationToken = default)
    {
        if (calendar.Source != "Fleet")
        {
            return CalendarAccessLevel.None;
        }

        return await _membership.IsDriverAsync(user, calendar.SourceId, cancellationToken)
            ? CalendarAccessLevel.Read
            : CalendarAccessLevel.None;
    }

    public Task<IReadOnlyCollection<string>> GetCalendarIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => _membership.ListCalendarIdsAsync(user, cancellationToken);
}
```

A contributor can only raise a level and is asked only for users with **Use calendars**. Workforce's `WorkforceCalendarAccessContributor` grants team managers and members access to their team's Workforce calendar this way.

## Reminders

With Calendar Reminders on, implement `ICalendarReminderOffsetProvider.GetOffsetsAsync` (how long before an occurrence "starting soon" fires) and `ICalendarReminderObserver` (`OnStartingSoonAsync`, `OnCompletedAsync` with a `CalendarReminderContext` holding `Occurrence`, `Calendar` and `Offset`). Each moment is raised once per occurrence and offset, even across restarts and nodes; moving an occurrence changes its key so old reminders never fire.

## Time Helpers in Abstractions

| Helper | Use |
|---|---|
| `CalendarTime.ToUtc(local, timeZoneId)`, `ToLocal(utc, timeZoneId)`, `IsValidZone`, `NormalizeZoneId`, `GetZone` | Convert wall-clock and instants with NodaTime's tz database |
| `TimeWindow(StartUtc, EndUtc)` | The one interval type: `Overlaps`, `Intersect`, `Contains`, static `Merge`, `Subtract`, `Clip` |
| `RecurrenceRule` / `RecurrenceExpander` | Parse, format and expand RRULEs in local time (skips spring-forward gaps, takes the earlier fall-back instant) |

```csharp
var busy = await _availability.GetBusyAsync(new CalendarBusyQuery
{
    CalendarIds = [roomCalendarId],
    UserIds = [userId],
    FromUtc = fromUtc,
    ToUtc = toUtc,
});

var free = await _availability.FindFreeWindowsAsync(new CalendarBusyQuery
{
    CalendarIds = [roomCalendarId],
    UserIds = [userId],
    FromUtc = fromUtc,
    ToUtc = toUtc,
}, TimeSpan.FromMinutes(30), maxResults: 10);
```

## Pitfalls

- Never set `StartUtc`/`EndUtc`; they are recomputed from `Start`, `End` and `TimeZoneId` on every save. All-day events store dates with an exclusive end.
- Overrides are separate events; don't edit a master's rule to move one occurrence.
- Don't take a module lock and the calendar locks in different orders; build one ordered key set.
- Queries are capped at 400 days, 5,000 events and 5,000 occurrences; ask only for the range you need.
- A new calendar type is user-facing; it appears in the calendar list, recipe step and report data source.

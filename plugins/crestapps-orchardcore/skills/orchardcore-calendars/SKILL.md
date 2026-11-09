---
name: orchardcore-calendars
description: Skill for the CrestApps Orchard Core Calendars module (CrestApps.OrchardCore.Calendars) and its Team, Resource, Office, Reminders, Invitations, Feeds, External Calendars and API features. Covers calendar types from AddCalendarType (owners, visibility, LimitConcurrent, ManagedByCode, event types), events with IANA time zones and RFC 5545 recurrence, occurrence overrides, attendees, locked overlap checks, free/busy and sharing levels, categories, reminders, the dashboard widget, iCalendar feeds, external sync providers, workflows, api/calendars/v1, AI tools, the Report Builder data source and the Calendars recipe step. Use this skill when requests mention Orchard Core calendars, room or desk booking, free/busy time, recurring events, calendar sharing or syncing an external calendar. Strong matches include ICalendarService, ICalendarEventManager, ICalendarAvailabilityService, ICalendarBusyTimeSource, ICalendarEventSource, ICalendarAccessContributor, ICalendarProvider and CalendarPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Calendars

## A Shared Calendar Other Modules Build On

You are an Orchard Core expert. Generate accurate setup, recipes, API calls and extension code for the CrestApps Calendars module. It stores calendars and events in the `Calendars` YesSql collection, expands repeating events, works out free/busy time, enforces overlap limits for rooms, equipment and desks, and tells other features about changes once they are committed. What a calendar is for comes from its **calendar type**, which a feature registers in code; the module never switches on a type name.

### Guidelines

- Install `CrestApps.OrchardCore.Calendars` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it with Scheduling and Bookings) in the web/startup project.
- Contracts live in `CrestApps.OrchardCore.Calendars.Abstractions` (models, type builders, services, `CalendarTime`, `TimeWindow`, `RecurrenceRule`, `RecurrenceExpander`). Reference `CrestApps.OrchardCore.Calendars.Core` only for the iCalendar reader/writer or the external provider contracts.
- For an optional integration, add a startup to your own feature with `[RequireFeatures("CrestApps.OrchardCore.Calendars")]` instead of a separate feature, as Bookings does.
- Always write events through `ICalendarEventManager` and calendars through `ICalendarService`, never through a store: validation, handlers, overlap checks and after-commit observers run there.
- `CalendarEvent.Start`/`End` are **wall-clock** times in the event's IANA `TimeZoneId`. Never set `StartUtc`/`EndUtc` yourself.
- Our calendar service is `ICalendarService`, not Orchard Core's `ICalendarManager` (culture calendars). Alias `Calendar` (`using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;`) in files that import `System.Globalization`.
- Shifts, time off and team calendars for staff come from `orchardcore-workforce`; appointments from `orchardcore-scheduling`; commerce bookings from `orchardcore-bookings`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on | Adds |
|---|---|---|---|
| Calendars | `CrestApps.OrchardCore.Calendars` | `OrchardCore.Users`, `OrchardCore.Roles`, `CrestApps.OrchardCore.Resources` | Calendars, events, the `General` and `Personal` types, the calendar view, editors, permissions, recipe and deployment |
| Team Calendars | `CrestApps.OrchardCore.Calendars.Teams` | Calendars | The `Team` type and **Team Availability** |
| Resource Calendars | `CrestApps.OrchardCore.Calendars.Resources` | Calendars | The `Room` and `Equipment` types (location, seats, amenities); rooms and equipment invited to events |
| Office Calendars | `CrestApps.OrchardCore.Calendars.Office` | Calendars | The `Office` type: in office, desk bookings up to the desk count, visitors, office closed |
| Calendar Reminders | `CrestApps.OrchardCore.Calendars.Reminders` | Calendars | On-screen reminders until dismissed; once-per-occurrence "starting soon" and "completed" moments |
| Calendar Invitations | `CrestApps.OrchardCore.Calendars.Invitations` | Calendars, `OrchardCore.Email` | Invitation, update and cancellation emails with `.ics`, and answer links |
| Calendar Feeds | `CrestApps.OrchardCore.Calendars.Feeds` | Calendars | A private iCalendar feed per calendar, `.ics` import and export |
| External Calendars | `CrestApps.OrchardCore.Calendars.External` | Calendars | Subscriptions, the sync background task, the `ics` provider, the `calendars-external-sync` health check |
| Calendars API | `CrestApps.OrchardCore.Calendars.Api` | Calendars | `/api/calendars/v1` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Calendars",
        "CrestApps.OrchardCore.Calendars.Teams",
        "CrestApps.OrchardCore.Calendars.Resources",
        "CrestApps.OrchardCore.Calendars.Office",
        "CrestApps.OrchardCore.Calendars.Reminders",
        "CrestApps.OrchardCore.Calendars.Feeds",
        "CrestApps.OrchardCore.Calendars.Api"
      ],
      "disable": []
    }
  ]
}
```

### Integrations Lit Up by Other Features

| Feature on | What it adds |
|---|---|
| `OrchardCore.Workflows` | Calendar events and tasks (Starting Soon and Completed also need Calendar Reminders) |
| `CrestApps.OrchardCore.AI` | Six calendar AI tools (`AIToolsStartup`) |
| `OrchardCore.DataPipelines.Core` | The **Calendars** Report Builder data source |
| `OrchardCore.AdminDashboard` | The **Calendar** dashboard widget (`CalendarWidget` content type) |
| `CrestApps.OrchardCore.Teams` | Members of the team linked to a Team calendar's role get **Write** |
| `OrchardCore.Recipes.Core` / `OrchardCore.Deployment` | The `Calendars` recipe step / deployment step |
| `CrestApps.OrchardCore.Recipes` | JSON schemas for the step (`CalendarsRecipeStepSchema`, `CalendarsDeploymentStepSchema`) |

## Admin UI

| Menu | Route | Permission |
|---|---|---|
| **Calendars > Calendar** | `calendar` (`?date=&view=&calendars=`, or `?schedule=everyone` / comma-separated user ids for whose schedule) | `AccessCalendars` (`ListCalendarEvents` for others' schedules) |
| **Calendars > Manage Calendars** | `calendars`, `calendars/create/{type}`, `calendars/edit/{id}` | `AccessCalendars` (creating needs `CreateCalendars`; editing needs Manage on the calendar) |
| **Calendars > Categories** | `calendar-categories` | `AccessCalendars` (shared categories need `ManageAllCalendars`) |
| **Calendars > Team Availability** | `calendar/availability` (Team Calendars) | `AccessCalendars` |
| Event editor | `calendar/events/choose`, `create`, `edit/{id}` (+ `cancel`, `delete`, `confirm`, `respond`); `modal=true` returns a partial and `{ "succeeded", "message" }` | per-calendar **Write** |
| Feed and files | `calendars/{id}/feed` (Manage), `calendars/{id}/export` (Read), `calendars/{id}/import` (Write) (Calendar Feeds) | per-calendar level |

Scheduling adds Appointments, Appointment Types, Scheduling Resources and Waitlist under the same **Calendars** menu; Workforce adds its shift planning pages there.

## Permissions and Access

| Permission | Display name | Implied by | Default role |
|---|---|---|---|
| `ManageAllCalendars` | Manage all calendars | none | Administrator |
| `ViewAllCalendars` | View all calendars | `ManageAllCalendars` | Administrator |
| `CreateCalendars` | Create calendars | `ManageAllCalendars` | Administrator, Editor |
| `AccessCalendars` | Use calendars | `ViewAllCalendars`, `CreateCalendars` | Administrator, Authenticated |
| `ListCalendarEvents` | List anyone's calendar events | `ViewAllCalendars` | Administrator |
| `ListOwnCalendarEvents` | List own calendar events | `ListCalendarEvents`, `AccessCalendars` | (through Use calendars) |
| `ViewCalendarEvents` | View anyone's calendar events | `ViewAllCalendars` | Administrator |
| `ViewOwnCalendarEvents` | View own calendar events | `ViewCalendarEvents`, `AccessCalendars` | (through Use calendars) |

Per-calendar access is a `CalendarAccessLevel` from `ICalendarAccessService`: `None` < `FreeBusy` < `Read` < `Write` < `Manage`. In order: `ManageAllCalendars` gives Manage; `ViewAllCalendars` at least Read; without `AccessCalendars` nothing more applies; the owner of a user-owned calendar gets Manage; members of a Team calendar's role get Write; each `CalendarShare` (user or role) adds its level; `Visibility` `Public` raises signed-in users to Read and `Shared` to FreeBusy; each `ICalendarAccessContributor` may raise it. FreeBusy callers get occurrences with `BusyOnly` and no details.

Check the broad permission with a resource; `CalendarEventAuthorizationHandler` grants it through the Own permission:

```csharp
await _authorizationService.AuthorizeAsync(User, CalendarPermissions.ListCalendarEvents, new CalendarSchedule(userId)); // or CalendarSchedule.Everyone
await _authorizationService.AuthorizeAsync(User, CalendarPermissions.ViewCalendarEvents, calendarEvent); // false: show as busy
```

## Calendar Types

A type is a `CalendarTypeDescriptor` registered with `services.AddCalendarType(name, builder)`. Calling it again with the same name **extends** that type.

| Builder | Effect |
|---|---|
| `WithDisplayName`, `WithDescription` (`LocalizationSource`), `WithIcon`, `WithColor`, `WithPosition` | How the type shows in **Add calendar** and the list |
| `WithOwner(kind, onePerOwner)` | `CalendarOwnerKind` `None`, `User` (owner id = user id), `Role` (role name) or `Resource` |
| `WithCapabilities` | `CalendarCapabilities` flags `Attendees`, `Recurrence`, `AllDay`, `FreeBusy`, `ExternalSync`, `Feed` (default `All`) |
| `WithVisibility` | Default `Private`, `Shared` or `Public` |
| `LimitConcurrent(capacity = 1)` | New calendars refuse a busy event when `capacity` busy events already overlap |
| `ManagedByCode()` | Not in **Add calendar**; only code creates calendars of it |
| `AddEventType(name, displayName, e => ...)` | `Color`, `Icon`, `RemindBefore(minutes)`, `Busy()`/`Free()`, `AllDayByDefault()`, `Lasts(minutes)` (60), `NeedsApproval()` (created Tentative), `RequiresAttendee()`, `IgnoresCapacity()`. A type with none gets `Event`. |

| Type | Registered by | Owner | Overlaps | Event types (technical names) |
|---|---|---|---|---|
| `General` | Calendars | None | Allowed | `Event`, `Meeting`, `Holiday` (all day, free), `Reminder` (15 min, free) |
| `Personal` | Calendars | User, one per user, `Shared` | Allowed | `Event`, `Meeting`, `Focus`, `OutOfOffice` |
| `Team` | Team Calendars | Role | Allowed | `Meeting`, `OnCall`, `OutOfOffice`, `Milestone` |
| `Room`, `Equipment` | Resource Calendars | Resource, `Public` | Limit 1 | `Booking` (room) or `Reservation` (equipment), `Maintenance` |
| `Office` | Office Calendars | None, `Public` | Limit 20 (desk count) | `InOffice`, `DeskBooking`, `Visitor`, `Closed` |
| `Appointments` | Scheduling | None, managed by code | Allowed (Scheduling locks) | `Appointment`, `Blocked` |
| `Bookings` | Bookings (with Calendars on) | None, managed by code | Allowed | `Booking` |
| `Workforce` | Workforce | None, managed by code, one per team | Allowed | Shift (free), time off, training, meeting, on call |

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCalendarType("Vehicles", type => type
            .WithDisplayName(LocalizationSource.Create<Startup>("Vehicle"))
            .WithIcon("fa-solid fa-car")
            .WithOwner(CalendarOwnerKind.Resource)
            .WithVisibility(CalendarVisibility.Public)
            .LimitConcurrent(1)
            .AddEventType("Trip", LocalizationSource.Create<Startup>("Trip"), e => e.Color("#1971c2").RequiresAttendee().Lasts(240)));
    }
}
```

Module-managed calendars, writing events from code, type-specific fields and every handler are in [references/calendar-types-and-extensions.md](references/calendar-types-and-extensions.md).

## Events, Recurrence and Concurrency

- `Calendar`: `Name`, `Description`, `Type`, `Color`, `TimeZoneId`, `OwnerKind`, `OwnerId`, `Visibility`, `Shares` (`PrincipalKind` `User`/`Role`, `PrincipalId`, `DisplayName`, `Access`), `ConflictPolicy` (`AllowOverlaps`, `LimitConcurrent`), `Capacity`, `BookingVersion`, `Enabled`, `Source`, `SourceId`, `FeedToken`.
- `CalendarEvent`: `CalendarId`, `EventType`, `Title`, `Description`, `Location`, `Color`, `CategoryId`, `AllDay`, `Start`, `End`, `TimeZoneId`, `Status` (`Confirmed`, `Tentative`, `Cancelled`), `Transparency` (`Busy`, `Free`), `Visibility` (`Default`, `Public`, `Private`), `ReminderMinutes`, `Attendees`, `Recurrence` (`Rule`, `ExceptionDates`, `EndUtc`), `SeriesId`, `OriginalStartUtc`, `Source`, `SourceId`, `Editable`, `Uid`, `Sequence`, `ProviderMappings`.
- Attendees (`CalendarAttendee`): `UserId`, or `Email` + `Name`, or `ResourceCalendarId`; `Role` (`Required`, `Optional`, `Organizer`, `Resource`); `Response` (`NeedsAction`, `Accepted`, `Tentative`, `Declined`). `GetKey()` gives `user:{id}`, `email:{address}` or `resource:{calendarId}`.
- `RecurrenceRule` supports `FREQ` (`DAILY`, `WEEKLY`, `MONTHLY`, `YEARLY`), `INTERVAL`, `COUNT`, `UNTIL`, `BYDAY` (with ordinals such as `2TU`, `-1FR`), `BYMONTHDAY`, `BYMONTH`, `BYSETPOS`, `WKST`. `BYHOUR`, `BYMINUTE` and `BYSECOND` are refused. Expansion is in local time with NodaTime, so a 09:00 weekly meeting stays at 09:00 across DST.
- A changed or cancelled occurrence is an **override** (its own event with `SeriesId` + `OriginalStartUtc`). **This and following** ends the master with `UNTIL` and starts a new master. Use `UpdateOccurrenceAsync` / `CancelOccurrenceAsync` with `CalendarEditScope` `ThisOccurrence`, `ThisAndFollowing` or `AllOccurrences`.
- On every create, change and move to a `LimitConcurrent` calendar (or an invited room/equipment calendar), the manager takes the distributed lock `CALENDAR_{id}` (`CalendarLockKeys.Calendar`) on each target in id order, checks overlaps (`CalendarConflictChecker`, next 366 days and up to 500 occurrences for a series), bumps each calendar's `BookingVersion` with YesSql's concurrency check, then stages the event. A refusal is a `CalendarError` with `Code == CalendarError.ConflictCode`.
- Every write takes `CalendarChangeOptions`: `Actor`, `Reason`, `SkipConflictCheck`, `CalendarLocksHeld`, `Notify` (true), `SendInvitations` (true).
- Queries are bounded by `CalendarOptions`: `MaxOccurrencesPerQuery` 5000, `MaxEventsPerQuery` 5000, `MaxQueryDays` 400; `DefaultReminderMinutes` 10. Reminder choices are 0, 5, 10, 15, 30, 60, 120 or 1440 minutes.

## Services and Extension Points

| Contract | Use |
|---|---|
| `ICalendarTypeManager` | List and find calendar and event types |
| `ICalendarService` | `NewAsync(type)` + `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `FindBySourceAsync`, `GetOrCreatePersonalCalendarAsync` |
| `ICalendarEventManager` | `NewAsync(calendar, eventType)`, create, update, cancel, delete, occurrences, attendees, `RespondAsync`, `FindBySourceAsync`, `ValidateAsync` |
| `ICalendarQueryService` | `GetOccurrencesAsync(CalendarOccurrenceQuery)` (expanded, overrides applied, `Viewer` reduces to busy blocks) |
| `ICalendarAvailabilityService` | `GetBusyAsync(CalendarBusyQuery)` and `FindFreeWindowsAsync(query, duration, within, maxResults)` |
| `ICalendarAccessService` | Access level per calendar, calendars a user reaches, `CanCreateAsync` |
| `ICalendarHandler` / `CalendarHandlerBase` | Validate and react to calendars inside the request |
| `ICalendarEventHandler` / `CalendarEventHandlerBase` | `ValidatingAsync` (add `context.Errors`), `SavingAsync`, `SavedAsync`, `DeletingAsync`, inside the request |
| `ICalendarEventObserver` | `OnChangedAsync(CalendarEventChange)` after commit, in a new scope (`Kind`, `Event`, `Previous`, `Calendar`, `OccurrenceStartUtc`, `Attendee`, `Actor`, `Reason`) |
| `ICalendarBusyTimeSource` | Busy time not stored as events; `services.AddCalendarBusyTimeSource<T>()` |
| `ICalendarEventSource` | Read-only occurrences in the calendar view; `services.AddCalendarEventSource<T>()` |
| `ICalendarAccessContributor` | Raise a user's level for your own reasons; `services.AddCalendarAccessContributor<T>()` (Workforce team calendars use it) |
| `ICalendarReminderOffsetProvider` / `ICalendarReminderObserver` | Starting-soon offsets and once-per-occurrence callbacks (Calendar Reminders) |
| `DisplayDriver<Calendar>` / `DisplayDriver<CalendarEvent>` | Type-specific fields; calendar tabs `General;1`, `Owner;2`, `Sharing;3`, `Overlaps;4`, `Subscription;5`; event tabs `General;1`, `Repeat;2`, `Attendees;3` |

### Make Your Records Busy Time

```csharp
public sealed class FleetBusyTimeSource : ICalendarBusyTimeSource
{
    private readonly ITripStore _trips;

    public FleetBusyTimeSource(ITripStore trips)
    {
        _trips = trips;
    }

    public string Name => "Fleet";

    public async Task<IEnumerable<CalendarBusyInterval>> GetBusyTimeAsync(CalendarBusyQuery query, CancellationToken cancellationToken = default)
    {
        // Answer only for query.UserIds / query.CalendarIds between query.FromUtc and query.ToUtc, through an index.
        var trips = await _trips.ListForDriversAsync(query.UserIds, query.FromUtc, query.ToUtc, cancellationToken);

        return trips.Select(trip => new CalendarBusyInterval
        {
            StartUtc = trip.StartsUtc,
            EndUtc = trip.EndsUtc,
            UserId = trip.DriverId,
            Source = Name,
        });
    }
}

// Startup: services.AddCalendarBusyTimeSource<FleetBusyTimeSource>();
```

Free/busy, Team Availability, the API's free-time endpoint and Scheduling's open times include it (unless `IncludeBusyTimeSources = false`).

### Act After a Change Commits

```csharp
public sealed class CancelledMeetingObserver : ICalendarEventObserver
{
    private readonly ILogger _logger;

    public CancelledMeetingObserver(ILogger<CancelledMeetingObserver> logger)
    {
        _logger = logger;
    }

    public Task OnChangedAsync(CalendarEventChange change, CancellationToken cancellationToken = default)
    {
        if (change.Kind == CalendarEventChangeKind.Cancelled && change.Calendar?.Type == "Team")
        {
            _logger.LogInformation("Team event {Title} was cancelled by {Actor}.", change.Event.Title, change.Actor);
        }

        return Task.CompletedTask;
    }
}

// Startup: services.AddScoped<ICalendarEventObserver, CancelledMeetingObserver>();
```

## Reminders, Invitations and Feeds

- **Reminders.** `CalendarReminderBackgroundTask` runs every minute, raises each offset once per occurrence through `CalendarReminderLog`, and turns event reminders into a `CalendarUserReminder` per person. Every full admin page polls `GET Admin/api/calendars/reminders` and dismisses with `POST Admin/api/calendars/reminders/{id}/dismiss`.
- **Invitations.** After commit, attendees with an email (not the organizer, not decliners) get an invitation (`.ics` `REQUEST`), update or cancellation (`CANCEL`). Answers go to the anonymous `calendars/respond/{eventId}/{token}/{response}`. Writes with `SendInvitations = false` (recipes, imports) send nothing.
- **Feeds.** `GET /calendars/feed/{token}.ics` (anonymous, private events as busy only); import reads up to 10,000 VEVENTs (with `RRULE`, `EXDATE`, `RECURRENCE-ID`, `TZID`) and matches by UID.
- **Choosing a time from any page.** Render the `CalendarPicker` shape and call `CrestAppsCalendarPicker.open({ date, durationMinutes, title })`; it resolves with `{ start, end, local }` or `null`.

## External Calendars

External Calendars keeps a calendar in step with an outside calendar through an `ICalendarProvider`. **Only one provider ships: the read-only iCalendar address provider `ics`** (`ICalendarUrlProvider`; `https://`, `http://` or `webcal://`, 30-second timeout, private and loopback addresses refused). There is **no Google Calendar or Microsoft 365 / Graph provider** in the module; the two-way contracts (`IIncrementalCalendarProvider`, `IWritableCalendarProvider`) exist so such providers can be added as separate modules (tracked in issue #1238). For one-way publishing the other way, use Calendar Feeds.

- The subscription is a `CalendarSubscription` in the calendar's properties: `Provider` (`ics`), `Url`, `IntervalMinutes` (60, 5 to 10,080), `Enabled`, `TwoWay`, `ConflictPolicy` (`ExternalWins` default, `LocalWins`), plus sync bookkeeping (`SyncToken`, `PendingPush`, `PendingDeletes`, `NeedsReconnect`, `LastSyncedUtc`, `LastError`, `Failures`, `LastResult`).
- `CalendarSyncBackgroundTask` runs every five minutes, each calendar in its own scope; failures back off up to 32 times the interval; an authorization failure pauses the subscription (`NeedsReconnect`) until it is saved again.
- The `calendars-external-sync` health check (tag `calendars`) reports **Degraded** while a subscription needs reconnecting or has failed three times in a row.

Building a provider (contracts, registration with `services.AddCalendarProvider<T>()`, event mapping, OAuth, tests) is in [references/external-calendar-providers.md](references/external-calendar-providers.md).

## Dashboard Widget

With `OrchardCore.AdminDashboard` on, add **Manage Dashboard > Add Widget > Calendar**. The `CalendarWidget` content type (stereotype `DashboardWidget`, `TitlePart` + `CalendarWidgetPart`) stores `Source` (`MyCalendars`, `Type`, `Calendars`), `CalendarType`, `CalendarIds` and `View` (`Agenda`, `Day`, `Week`, `Month`). Each viewer sees only calendars they may open (FreeBusy ones as busy blocks); users without **Use calendars** see a message. Widgets are content items, so they travel with the `Content` step.

## Workflows

Activities are in the **Calendar** category, triggered by `CalendarWorkflowObserver` after commit and correlated by event id. Every event filters on `CalendarIds`, `CalendarTypes`, `EventTypes` (comma-separated) and `IgnoreWorkflowChanges` (default true, skips actor `workflow`).

| Events | Tasks (outcomes) |
|---|---|
| `CalendarEventCreatedEvent`, `CalendarEventUpdatedEvent`, `CalendarEventCancelledEvent`, `CalendarEventDeletedEvent`, `CalendarAttendeeInvitedEvent`, `CalendarAttendeeRespondedEvent`; `CalendarEventStartingSoonEvent` and `CalendarEventCompletedEvent` (need Calendar Reminders) | Create Calendar Event (Done, Failed), Update and Cancel Calendar Event (Done, NotFound, Failed), Get Calendar Event (Found, NotFound), Find Calendar Events (Found, None), Add/Remove Calendar Attendee (Done, NotFound, Failed), Find Free Time (Found, None) |

Input keys include `CalendarEvent`, `EventId`, `CalendarId`, `CalendarName`, `CalendarType`, `EventType`, `Title`, `StartUtc`, `EndUtc`, `Source`, `SourceId`, `Kind`, `Actor`, `Previous`, attendee keys, and `OccurrenceStartUtc`, `OccurrenceEndUtc`, `MinutesBefore`. Task fields are Liquid; times without an offset are wall-clock in the event's zone; attendees are emails, `Name <email>` or `user:{userId}`.

## HTTP API

The Calendars API maps `/api/calendars/v1` (Orchard Core `Api` scheme, no antiforgery). Each call checks calendar access like the screens; list, events, free-busy and free-time answer `403` without `AccessCalendars`.

| Method and route | Does |
|---|---|
| `GET calendars` | Reachable calendars with the caller's `access` |
| `GET events?calendarIds=&from=&to=` | Occurrences (busy blocks for FreeBusy calendars) |
| `GET events/{id}` | One event (Read) |
| `POST events` | Create from `calendarId`, `eventType`, `title`, `description`, `location`, `allDay`, `start`, `end`, `timeZoneId`, `status`, `transparency`, `recurrence` (RRULE), `attendees`; `201` |
| `PUT events/{id}?occurrence=&scope=` | Change an event or occurrences |
| `POST events/{id}/cancel?occurrence=&scope=` | Cancel |
| `DELETE events/{id}` | Delete; `204` |
| `POST events/{id}/respond` | `{ "response": "Accepted" }` (or `Tentative`, `Declined`) |
| `POST free-busy` | Busy intervals for up to 50 `calendarIds`/`userIds`, at most 62 days |
| `POST free-time` | Windows of `durationMinutes` (5 to 1440, default 30), up to 50 |

An overlap refusal is `409` Problem Details; validation errors are `400`. Repeated cancels or identical answers succeed without raising a second workflow event.

## AI Tools

With `CrestApps.OrchardCore.AI` on, tools in the **Calendar** category act for the signed-in user with their calendar access (refusing anonymous callers and callers without `AccessCalendars`): `listCalendars`, `findCalendarEvents` (at most 62 days), `findFreeTime` (at most 31 days), `createCalendarEvent`, `moveCalendarEvent`, `cancelCalendarEvent`. See `orchardcore-ai-tools`.

## Report Builder Data Source

With `OrchardCore.DataPipelines.Core` on, the **Calendars** data source (`Calendars`) offers the data sets `Calendars` (one row per calendar) and `CalendarEvents` (one row per stored event; series are not expanded). They need `AccessCalendars` and only report calendars the report owner can **Read**; private events on calendars the owner can't change have empty `Title` and attendees and `DetailsHidden = true`. Descriptions, locations, attendee names and emails, tokens and external links are never exposed. See `orchardcore-report-builder`.

## Recipes and Deployment

The `Calendars` step (needs `OrchardCore.Recipes.Core`) imports `Categories` first, then `Calendars` with each calendar's `Events`, matched by `itemId`. List names start with a capital letter; every other property is camelCase and enums are names. Recipe writes skip the overlap check and send no invitations.

```json
{
  "steps": [
    {
      "name": "Calendars",
      "Calendars": [
        {
          "itemId": "4cmpnycalendar000000000001",
          "name": "Company",
          "type": "General",
          "timeZoneId": "America/New_York",
          "visibility": "Public",
          "conflictPolicy": "AllowOverlaps",
          "Events": [
            {
              "itemId": "4cmpnyallhands000000000001",
              "eventType": "Meeting",
              "title": "All hands",
              "start": "2026-11-02T09:00:00",
              "end": "2026-11-02T10:00:00",
              "timeZoneId": "America/New_York",
              "recurrence": { "rule": "FREQ=WEEKLY;BYDAY=MO" }
            }
          ]
        }
      ]
    }
  ]
}
```

The **Calendars** deployment step (category **Calendar**, `CalendarsDeploymentStep`) exports every calendar, with `IncludeEvents` (false) their events and with `IncludeCategories` (true) the categories. Full shapes (rooms, shares, categories, attendees, recurrence exceptions) are in [references/calendars-recipes.md](references/calendars-recipes.md).

## Gotchas

| Symptom | Check |
|---|---|
| A room imported by recipe allows double booking | The step copies `conflictPolicy`, `visibility` and `enabled` as given; omitting `conflictPolicy` stores `AllowOverlaps`. Set `"conflictPolicy": "LimitConcurrent"` and `capacity`. |
| `The calendar type '...' is not enabled` | Enable the feature that registers the type before the step runs; `NewAsync` returns `null` while it is off. |
| Event times shift by hours | Wall-clock `DateTime`s in your own models need `WallClockDateTimeJsonConverter`; never set `StartUtc`/`EndUtc`. |
| "The calendar ... is busy. Try again in a moment." | The `CALENDAR_{id}` lock could not be taken; retry. If you hold your own lock, take calendar locks after it in one ordered set and pass `CalendarLocksHeld`. |
| An event can't be dragged or edited | `Editable = false` (owned by Scheduling, Bookings or a source) or the user has only Read/FreeBusy. |
| Someone sees busy blocks only | Their level is FreeBusy, or the event is `Private`. |
| Workflow loops | Keep `IgnoreWorkflowChanges` on; task changes use actor `workflow`. |
| Google or Outlook calendar can't be connected | No such provider ships; subscribe to its `.ics` address with the `ics` provider, or build a provider. |

Related skills: `orchardcore-scheduling`, `orchardcore-bookings`, `orchardcore-workforce`, `orchardcore-teams`, `orchardcore-contact-center`, `orchardcore-omnichannel-management`, `orchardcore-report-builder`, `orchardcore-ai-tools`, `orchardcore-time-zones`.

---
name: orchardcore-business-hours
description: Skill for the CrestApps Orchard Core Business Hours module (CrestApps.OrchardCore.BusinessHours), the one place modules ask whether they are open. Covers schedules with weekly windows, lunch breaks, holidays and special days in a time zone, Content > Business Hours, the ManageBusinessHoursSchedules permission, the BusinessHoursSchedules recipe and deployment step, the HoursSchedulePicker view component, the Check Business Hours workflow task, IOpenHoursService, IHoursScheduleSource, IBusinessHoursScheduleObserver, HoursScheduleEvaluator and the move of Contact Center calendars onto schedules. Use this skill when requests mention business hours, opening hours, holidays, after-hours routing, business-hour SLA clocks or schedule pickers. Strong matches include BusinessHoursSchedule, HoursSchedule, HoursWindow, HoursDateException, AddOpenHours, AddHoursScheduleSource, CheckBusinessHoursTask and BusinessHoursConstants.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Business Hours

## One Schedule, Every Module

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Business Hours module. A schedule is entered once (a time zone, the hours of each weekday, holidays and special days) and every module reads it through `IOpenHoursService`: Contact Center queues, entry points and the dialer, automated Omnichannel sends, Service Levels timers (tickets and work orders), the Tickets **Time elapsed** event and workflows.

### Guidelines

- Install `CrestApps.OrchardCore.BusinessHours` (or a CrestApps bundle that includes it) in the web/startup project.
- Reference `CrestApps.OrchardCore.BusinessHours.Abstractions` (models, `IOpenHoursService`, `IHoursScheduleSource`, `HoursScheduleEvaluator`, `BusinessHoursConstants`, `BusinessHoursPermissions`) or `CrestApps.OrchardCore.BusinessHours.Core` (`DefaultOpenHoursService`, `AddOpenHours()`, `AddBusinessHoursSchedules()`) from your module, never the module project.
- Ask through `IOpenHoursService`, never by reading `BusinessHoursSchedule` records yourself: other features may add schedule sources.
- Treat `IsOpenAsync` returning `null` (empty id, unknown or disabled schedule) as open, so a missing schedule never blocks live work. `AddOpenTimeAsync` and `GetOpenTimeBetweenAsync` count an unknown schedule around the clock.
- Store a schedule id (`HoursScheduleId`, or `BusinessHoursCalendarId` on older Contact Center records), never a copy of the hours.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Feature | ID | Depends on |
|---|---|---|
| Business Hours | `CrestApps.OrchardCore.BusinessHours` | `CrestApps.OrchardCore.Resources` |

Category **Organization**. Features that need it pull it in: Service Levels (`CrestApps.OrchardCore.ServiceLevels`) and Contact Center Business Hours (`CrestApps.OrchardCore.ContactCenter.BusinessHours`) depend on it.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.BusinessHours"
      ],
      "disable": []
    }
  ]
}
```

| Project | Holds |
|---|---|
| `CrestApps.OrchardCore.BusinessHours.Abstractions` | `HoursSchedule`, `HoursWindow`, `HoursDateException`, `HoursScheduleOption`, the `BusinessHoursSchedule` record, `IOpenHoursService`, `IHoursScheduleSource`, `IBusinessHoursScheduleObserver`, `AddHoursScheduleSource<T>()`, `HoursScheduleEvaluator`, `BusinessHoursConstants`, `BusinessHoursPermissions` |
| `CrestApps.OrchardCore.BusinessHours.Core` | `DefaultOpenHoursService`, `AddOpenHours()`, `AddBusinessHoursSchedules()`, `BusinessHoursScheduleHandler`, `BusinessHoursScheduleSource`, editor helpers `WeeklyHoursRows` and `HoursDateExceptionText` |
| `CrestApps.OrchardCore.BusinessHours` (module) | Admin pages, `HoursSchedulePicker` view component, recipe and deployment step, **Check Business Hours** workflow task |

## Admin UI and Permission

| Menu | Routes | Permission |
|---|---|---|
| **Content → Business Hours** | `Admin/business-hours`, `Admin/business-hours/create`, `Admin/business-hours/edit/{id}`, `Admin/business-hours/clone/{id}`, `Admin/business-hours/delete/{id}` (shared admin list, List and Grid layouts) | `ManageBusinessHoursSchedules` |

| Permission | Display name | Default role |
|---|---|---|
| `ManageBusinessHoursSchedules` | Manage business hours schedules | Administrator |

The editor (`IDisplayManager<BusinessHoursSchedule>`) has three cards: **General** (name, description, time zone, enabled), **Weekly hours** (each day open or closed, up to two windows so a lunch break fits) and **Holidays and special days** (one date per line: `2026-12-25 Christmas Day` is closed all day, `2026-12-24 09:00-13:00 Christmas Eve` has special hours). Add your own card with a `DisplayDriver<BusinessHoursSchedule>`.

## The Schedule Model

`BusinessHoursSchedule` is a `CatalogItem` (`INameAwareModel`, `IModifiedUtcAwareModel`) with `Name` (unique, required), `Description`, `Enabled` (default `true`), `Hours` (`HoursSchedule`), `CreatedUtc` and `ModifiedUtc`.

| Type | Members |
|---|---|
| `HoursSchedule` | `TimeZoneId` (IANA or Windows id; empty or unknown means UTC), `Windows`, `DateExceptions` |
| `HoursWindow` | `Day` (`DayOfWeek`), `OpenMinute`, `CloseMinute` (minutes from local midnight, close exclusive) |
| `HoursDateException` | `Date` (`DateOnly`), `Name`, `Closed` (default `true`), `OpenMinute`, `CloseMinute` |
| `HoursScheduleOption` | `record (Id, Name)` for pickers |

Rules (`HoursScheduleEvaluator`):

- A day can have several windows (lunch break) or none (closed).
- Same open and close minute means open all day; close before open runs past midnight (Friday 22:00 to 06:00 is open Friday night and early Saturday).
- A date exception owns its whole local date: closed all day, or open only in its own hours. The after-midnight part of an overnight window that runs into a holiday is closed.
- Minutes outside 0 to 1440 make a window invalid and it is ignored (the handler refuses them on save).
- Daylight-saving gaps move forward by the gap; a repeated hour resolves to the earlier one.

`BusinessHoursScheduleHandler` trims values, sorts windows (Monday first) and exceptions, keeps one exception per date, refuses a duplicate name, an unknown time zone and out-of-range minutes, and gives a new schedule the site's time zone.

A **disabled** schedule is unknown to `IOpenHoursService`: queues and entry points that use it are always open, SLA timers count around the clock, and **Check Business Hours** takes **Unknown**. A dialer profile whose calling hours are disabled places no calls (its window is never open).

## Recipes and Deployment

The `BusinessHoursSchedules` step (`BusinessHoursConstants.RecipeStepName`, needs `OrchardCore.Recipes.Core`) creates or updates schedules, matched by `ItemId`, then by `Name`, so running a recipe twice never duplicates one. `Day` is the `DayOfWeek` number (0 Sunday to 6 Saturday).

```json
{
  "steps": [
    {
      "name": "BusinessHoursSchedules",
      "Schedules": [
        {
          "ItemId": "44fy8z1wfv91trshefx9dqcsyj",
          "Name": "Support desk",
          "Description": "Weekdays with a lunch break.",
          "Enabled": true,
          "Hours": {
            "TimeZoneId": "America/New_York",
            "Windows": [
              { "Day": 1, "OpenMinute": 540, "CloseMinute": 720 },
              { "Day": 1, "OpenMinute": 780, "CloseMinute": 1020 },
              { "Day": 2, "OpenMinute": 540, "CloseMinute": 1020 },
              { "Day": 3, "OpenMinute": 540, "CloseMinute": 1020 },
              { "Day": 4, "OpenMinute": 540, "CloseMinute": 1020 },
              { "Day": 5, "OpenMinute": 540, "CloseMinute": 1020 }
            ],
            "DateExceptions": [
              { "Date": "2026-12-25", "Name": "Christmas Day", "Closed": true },
              { "Date": "2026-12-24", "Name": "Christmas Eve", "Closed": false, "OpenMinute": 540, "CloseMinute": 780 }
            ]
          }
        }
      ]
    }
  ]
}
```

- Give a schedule a fixed, valid 26-character `ItemId` when other steps of the same recipe name it (service-level policies, ticket desks, Contact Center queues).
- The **Business hours schedules** deployment step (needs `OrchardCore.Deployment`) exports every schedule in this shape. With CrestApps Recipes on, both steps publish a JSON schema.
- The legacy `ContactCenterBusinessHoursCalendar` step (`Calendars` with `WeeklySchedule` and `Holidays`) is still read by Contact Center Business Hours and turns each calendar into a schedule, keeping its id when valid. Write `BusinessHoursSchedules` in new recipes.

## Asking Whether Something Is Open

| `IOpenHoursService` method | Returns |
|---|---|
| `IsOpenAsync(scheduleId, utcInstant, timeZoneId = null)` | `bool?`; `null` for an empty id or an unknown or disabled schedule |
| `GetOpenWindowsAsync(scheduleId, fromUtc, toUtc, timeZoneId = null)` | Merged, clipped `TimeWindow`s (`CrestApps.OrchardCore.Calendars`), or `null` for an unknown schedule |
| `ListSchedulesAsync()` | Every source's `HoursScheduleOption`s, once each, by name |
| `AddOpenTimeAsync(scheduleId, fromUtc, duration, timeZoneId = null)` | The instant the schedule has been open for `duration` ("8 business hours from Friday 4 pm"); unknown or never-open schedules count around the clock |
| `GetOpenTimeBetweenAsync(scheduleId, fromUtc, toUtc, timeZoneId = null)` | Open time between two instants; unknown schedules count around the clock |

`timeZoneId` reads the schedule in another zone, such as the contact's local time for a calling window.

```csharp
public sealed class CallbackWindowService
{
    private readonly IOpenHoursService _openHours;
    private readonly IClock _clock;

    public CallbackWindowService(IOpenHoursService openHours, IClock clock)
    {
        _openHours = openHours;
        _clock = clock;
    }

    public async Task<DateTime> GetCallbackDueAsync(string scheduleId, string contactTimeZoneId)
    {
        var now = _clock.UtcNow;

        if (await _openHours.IsOpenAsync(scheduleId, now, contactTimeZoneId) is false)
        {
            // Closed now: due four business hours after the next opening.
            return await _openHours.AddOpenTimeAsync(scheduleId, now, TimeSpan.FromHours(4), contactTimeZoneId);
        }

        return now.AddMinutes(30);
    }
}

// Startup: services.AddOpenHours().AddScoped<CallbackWindowService>();
```

`services.AddOpenHours()` registers `IOpenHoursService` once, whichever feature asks first. A module that holds an `HoursSchedule` in memory can call `HoursScheduleEvaluator.IsOpen`, `GetOpenWindows`, `GetDayStretches`, `AddOpenTime` (366-day horizon, `null` when never open) and `GetOpenTime` without a service.

## Extension Points

| Seam | Use it to |
|---|---|
| `IHoursScheduleSource` | Offer schedules your module keeps (`FindAsync(id)`, `ListAsync()`); register with `services.AddHoursScheduleSource<T>()`. The first source that knows an id answers. |
| `IBusinessHoursScheduleObserver` | Hear `ChangingAsync(scheduleId)` before a schedule is changed or deleted (reads still return the old hours) and `ChangedAsync(scheduleId)` after. Service Levels uses it to settle running timers on the old hours and reschedule them on the new ones. Register with `TryAddEnumerable`. |
| `DisplayDriver<BusinessHoursSchedule>` | Add a card to the schedule editor. |
| `ICatalogEntryHandler<BusinessHoursSchedule>` | Keep your own records in step with schedules. |
| `HoursSchedulePicker` view component | Let users pick a schedule from every source. |

### A Schedule Source

```csharp
public sealed class StoreHoursScheduleSource : IHoursScheduleSource
{
    public Task<HoursSchedule> FindAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        if (scheduleId != "store")
        {
            return Task.FromResult<HoursSchedule>(null);
        }

        var schedule = new HoursSchedule { TimeZoneId = "America/New_York" };

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            schedule.Windows.Add(new HoursWindow { Day = day, OpenMinute = 9 * 60, CloseMinute = 17 * 60 });
        }

        schedule.DateExceptions.Add(new HoursDateException { Date = new DateOnly(2026, 12, 25), Name = "Christmas Day" });

        return Task.FromResult(schedule);
    }

    public Task<IReadOnlyList<HoursScheduleOption>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<HoursScheduleOption>>([new HoursScheduleOption("store", "Store hours")]);
}

// Startup: services.AddOpenHours().AddHoursScheduleSource<StoreHoursScheduleSource>();
```

### The Schedule Picker

A module whose feature depends on `CrestApps.OrchardCore.BusinessHours` renders the picker by name:

```cshtml
@await Component.InvokeAsync("HoursSchedulePicker", new
{
    name = Html.NameFor(model => model.HoursScheduleId).ToString(),
    selectedValue = Model.HoursScheduleId,
    emptyText = T["Around the clock"].Value,
})
```

`required = true` hides the empty choice. A selected schedule that was disabled or deleted stays listed as missing, so saving the form doesn't drop it silently.

## Check Business Hours Workflow Task

With `OrchardCore.Workflows` on, **Check Business Hours** (`CheckBusinessHoursTask`, category **Business Hours**) checks the schedule (`ScheduleId`) now and takes **Open**, **Closed** or **Unknown** (disabled or unknown schedule). `TimeZoneExpression` (Liquid, optional) reads the schedule in another zone, such as the contact's. With CrestApps Recipes on, the task has a workflow activity schema.

## Who Reads Business Hours

| Consumer | How |
|---|---|
| Contact Center queues, entry points, dialer calling windows, manual-dial screening | `IBusinessHoursService` (Contact Center Business Hours), which asks `IOpenHoursService`. See `orchardcore-contact-center`. |
| Omnichannel automated sends and activity loads | `IBusinessHoursGate` (`OpenHoursBusinessHoursGate` in `CrestApps.OrchardCore.Omnichannel.Core`), open when no source knows the calendar |
| Service-level timers (tickets, work orders, any scope) | `AddOpenTimeAsync` and `GetOpenTimeBetweenAsync` on the policy's `HoursScheduleId`. See `orchardcore-service-levels`. |
| Ticket desks | `TicketDesk.HoursScheduleId` for the **Ticket Time Elapsed** event counted in business hours. See `orchardcore-tickets`. |

## Upgrading From Contact Center Calendars

Before CrestApps.OrchardCore 3.0, Contact Center kept its own calendars under **Interaction Center → Management → Business hours** with `ManageContactCenterBusinessHours`. Now:

- Contact Center Business Hours (`CrestApps.OrchardCore.ContactCenter.BusinessHours`) depends on Business Hours and keeps no calendars. Its upgrade copies each calendar to a schedule with the **same id** (a duplicate name gets a number, such as *Support (2)*), so every queue's and entry point's `BusinessHoursCalendarId`, dialer profile's `CallingCalendarId` and activity load's calendar keeps working.
- Roles that held `ManageContactCenterBusinessHours` or `ManageContactCenter` get `ManageBusinessHoursSchedules`.
- The Business Hours projects used to be a library with no feature; they now come with this module, its admin pages and its own schedules. The Contact Center calendar source, `OpenHoursServiceHoursProvider` and `IServiceHoursProvider` are gone (work-order SLA clocks now run on Service Levels).

## Troubleshooting

| Symptom | Check |
|---|---|
| `IsOpenAsync` returns `null` | Empty id, the schedule is disabled, or no source knows the id (Business Hours feature off). |
| A queue never closes | Its schedule is disabled or deleted: unknown schedules are treated as open. |
| A dialer never dials | Its calling-hours schedule is disabled: a calling window that is never open blocks every call. |
| Hours are an hour off | `TimeZoneId` is empty (UTC) or wrong; a new schedule takes the site's time zone only when created without one. |
| A recipe creates a second schedule | The `ItemId` changed and the `Name` differs; match by a stable `ItemId` or the exact name. |
| Recipe refused "Another schedule has this name." | Two entries, or an existing schedule, share the name with different ids. |
| SLA due times jumped after editing hours | Expected: Service Levels counts time so far on the old hours and the rest on the new ones. |

Related skills: `orchardcore-service-levels`, `orchardcore-tickets`, `orchardcore-contact-center`, `orchardcore-omnichannel`, `orchardcore-teams`, `orchardcore-work-orders`, `orchardcore-calendars`, `orchardcore-workflows`.

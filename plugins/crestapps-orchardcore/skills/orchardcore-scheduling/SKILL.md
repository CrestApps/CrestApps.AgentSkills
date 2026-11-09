---
name: orchardcore-scheduling
description: Skill for the CrestApps Orchard Core Scheduling module (CrestApps.OrchardCore.Scheduling) with its Self-Service and API features. Covers appointment types (length, interval, buffers, notice, horizon, daily and weekly limits, group sessions, confirmation, holds), staff, rooms and equipment with weekly hours, overrides or Workforce shifts, assignment modes, resource requirement groups with weights, self-service booking policies, the waitlist, book pages and manage links, locked booking, IAppointmentService, IAppointmentSlotFilter, IAppointmentObserver, editor sections from display drivers, work orders from bookings, workflows, api/scheduling/v1, AI tools, the Report Builder data source and the Scheduling recipe step. Use this skill when requests mention booking appointments, online booking pages, staff availability, appointment types or waitlists. Strong matches include AppointmentType, SchedulingResource, AppointmentRequest, ISchedulingAvailabilityService, AppointmentSlot and SchedulingPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Scheduling

## Book Appointments with Staff, Rooms and Equipment

You are an Orchard Core expert. Generate accurate setup, recipes, API calls and extension code for the CrestApps Scheduling module. It works out open times from each resource's working hours (or its shifts on a calendar) less its busy time on the shared calendar, books them under locks so a resource is never taken twice, and puts every appointment on the resource's calendar as an event.

### Guidelines

- Install `CrestApps.OrchardCore.Scheduling` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it and Calendars) in the web/startup project.
- Scheduling depends on `CrestApps.OrchardCore.Calendars`; calendars, free/busy and calendar workflows are covered by `orchardcore-calendars`.
- Contracts live in `CrestApps.OrchardCore.Scheduling.Abstractions` (models, `IAppointmentService`, `ISchedulingAvailabilityService`, `IAppointmentSlotFilter`, `IAppointmentObserver`, `SchedulingPermissions`); stores and services in `CrestApps.OrchardCore.Scheduling.Core`.
- Always book, confirm, move, cancel and complete through `IAppointmentService`. It locks, re-checks availability, versions resources and writes the calendar event in the same session.
- Records live in the `Scheduling` YesSql collection. Appointment events go on `Appointments` calendars (`Source = Scheduling`, `SourceId` = appointment id, `Editable = false`).
- Scheduling sends no email. Send confirmations from a workflow on **Appointment Confirmed** with the `ManageToken` link.
- Commerce bookings sold through the cart are `orchardcore-bookings`, not Scheduling. Work orders from bookings are `orchardcore-work-orders`; technician capacity is `orchardcore-dispatch`; shifts come from `orchardcore-workforce`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on | Adds |
|---|---|---|---|
| Scheduling | `CrestApps.OrchardCore.Scheduling` | `CrestApps.OrchardCore.Calendars` | Appointment types, resources, availability, appointments, holds, waitlist, the `Appointments` calendar type, admin screens |
| Scheduling Self-Service | `CrestApps.OrchardCore.Scheduling.SelfService` | Scheduling | Public `book/{slug}` pages and `book/manage/{id}/{token}` links |
| Scheduling API | `CrestApps.OrchardCore.Scheduling.Api` | Scheduling | `/api/scheduling/v1` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Calendars",
        "CrestApps.OrchardCore.Calendars.Reminders",
        "CrestApps.OrchardCore.Scheduling",
        "CrestApps.OrchardCore.Scheduling.SelfService",
        "CrestApps.OrchardCore.Scheduling.Api"
      ],
      "disable": []
    }
  ]
}
```

### Integrations Lit Up by Other Features

| Feature on | What it adds |
|---|---|
| `OrchardCore.Workflows` | Appointment events and tasks; **Appointment Starting Soon** also needs `CrestApps.OrchardCore.Calendars.Reminders` |
| `CrestApps.OrchardCore.AI` | Four customer-facing AI tools |
| `OrchardCore.DataPipelines.Core` | The **Scheduling** Report Builder data source |
| `OrchardCore.Recipes.Core` / `OrchardCore.Deployment` | The `Scheduling` recipe step / deployment step |
| `CrestApps.OrchardCore.Recipes` | `SchedulingRecipeStepSchema` and `SchedulingDeploymentStepSchema` |
| Workforce | A team's `Workforce` calendar holds `Shift` events that resources in **Shifts** mode read; breaks inside shifts are busy (`ShiftSegmentBusyTimeSource`) |
| `CrestApps.OrchardCore.WorkOrders` | A work order type lists `AppointmentTypeIds`; each confirmed booking makes a work order, moves it when rescheduled and cancels it with the booking |
| `CrestApps.OrchardCore.Dispatch` | `DispatchCapacitySlotFilter` offers a work-order appointment slot only while a qualified technician can take the job |

## Admin UI

| Menu | Route | Permission |
|---|---|---|
| **Calendars > Appointments** | `scheduling/appointments`, `scheduling/appointments/new`, `scheduling/appointments/{id}` (actions post to `{id}/do`) | `ViewAppointments` (list, detail), `ManageAppointments` (book, act) |
| **Calendars > Appointment Types** | `scheduling/types`, `scheduling/types/edit/{id?}` | `ManageScheduling` |
| **Calendars > Scheduling Resources** | `scheduling/resources`, `scheduling/resources/edit/{id?}` | `ManageScheduling` |
| **Calendars > Waitlist** | `scheduling/waitlist`, `scheduling/waitlist/add` | `ManageAppointments` |

## Permissions

| Permission | Display name | Implied by | Default role |
|---|---|---|---|
| `ManageScheduling` | Manage scheduling | none | Administrator |
| `ManageAppointments` | Manage appointments | `ManageScheduling` | Administrator, Editor |
| `ViewAppointments` | View appointments | `ManageAppointments` | Administrator, Editor (through Manage appointments) |

## Records

| Record | Key properties |
|---|---|
| `SchedulingResource` | `Name`, `Kind` (`Staff`, `Room`, `Equipment`, `Other`), `UserId` (a staff member's user; their own calendars also make them busy), `CalendarId`, `TimeZoneId`, `AvailabilityMode` (`WorkingHours`, `Shifts`), `ShiftCalendarIds` (legacy single `ShiftCalendarId` still read), `WorkingHours` (`Day`, `Start`, `End` as `HH:mm`; several per day, gaps are breaks), `DateOverrides` (`Date`, `Closed`, `Start`, `End`), `Enabled`, `BookingVersion` |
| `AppointmentType` | `Name`, `Description`, `Slug`, `DurationMinutes` (30), `SlotIntervalMinutes` (30), `BufferBeforeMinutes`, `BufferAfterMinutes`, `MinimumNoticeMinutes` (60), `HorizonDays` (60), `MaxPerDay` (0 = none), `MaxPerWeek` (0), `MaxUpcomingPerCustomer` (0), `CancelNoticeMinutes` (0), `RescheduleNoticeMinutes` (0), `Capacity` (1; more is a group session), `ResourceIds`, `Assignment`, `ResourceRequirements`, `RequiresConfirmation`, `HoldMinutes` (10), `AllowSelfService`, `Location`, `Color`, `Enabled` |
| `AppointmentResourceRequirement` | `Name`, `ResourceIds` (preference order), `Assignment`, `Weights` (resource id to round-robin weight, 1 when absent) |
| `Appointment` | `AppointmentTypeId`, `ResourceIds`, `StartUtc`, `EndUtc`, `TimeZoneId`, `Status`, `HoldExpiresUtc`, `CustomerName`, `CustomerEmail`, `CustomerPhone`, `UserId`, `Notes`, `CalendarEventId`, `ManageToken`, `Source` (`Admin`, `SelfService`, `Api`, `AI`, `Workflow`), `CancelReason` |
| `WaitlistEntry` | `AppointmentTypeId`, `FromUtc`, `ToUtc`, `CustomerName`, `CustomerEmail`, `CustomerPhone`, `Status` (`Waiting`, `Notified`, `Booked`, `Removed`), `NotifiedUtc` |

`AppointmentStatus` is `Held`, `Requested`, `Confirmed`, `Completed`, `NoShow`, `Cancelled` or `Expired`. `AppointmentAssignment` is `AnyAvailable` (first free in list order), `RoundRobin` (fewest appointments in the last 30 days divided by weight) or `AllTogether` (every resource at once, such as a doctor and a nurse).

Saving a resource **in the editor** without a calendar creates an `Appointments` calendar for it (`Source = Scheduling`, `SourceId` = resource id). A resource with no calendar can't be booked.

## How Open Times Are Worked Out

`ISchedulingAvailabilityService.FindSlotsAsync(type, fromUtc, toUtc, resourceId, maxResults, excludeAppointmentId)` returns `AppointmentSlot`s (`StartUtc`, `EndUtc`, free `ResourceIds`, `Remaining` places):

1. The window is cut to `MinimumNoticeMinutes` and `HorizonDays`.
2. Open hours per resource: weekly `WorkingHours` less `DateOverrides`, in the resource's time zone; or in **Shifts** mode the `Shift` events on its `ShiftCalendarIds` that name its user as an attendee (a person on two teams gets hours from both).
3. Busy time from `ICalendarAvailabilityService.GetBusyAsync` for the resource's calendar and user (including every `ICalendarBusyTimeSource`), each interval widened by the type's buffers. A booked appointment also keeps **its own type's** buffers clear, so a later booking can't fall inside them (a group session's own start is still offered while it has places).
4. Starts step by `SlotIntervalMinutes` from the start of each open range. The appointment itself must fit inside open hours (buffers may run outside, only clear of busy time), and `MaxPerDay` / `MaxPerWeek` (Monday to Sunday in the resource's zone) must not be reached.
5. Every resource requirement must be met (a free resource per group, or all of an `AllTogether` group). An appointment makes every one of its resources busy.
6. Every registered `IAppointmentSlotFilter` can then drop slots or lower `Remaining`, never add any; booking and rescheduling check against the same filtered slots.

### Resource Requirements

```json
{
  "resourceRequirements": [
    { "name": "Staff", "resourceIds": [ "4sresdrsmith00000000000001", "4sresdrjones00000000000001" ], "assignment": "RoundRobin", "weights": { "4sresdrsmith00000000000001": 2 } },
    { "name": "Room", "resourceIds": [ "4sresroom10000000000000001", "4sresroom20000000000000001" ], "assignment": "AnyAvailable" }
  ]
}
```

Groups are picked in order and never take the same resource twice; the appointment's `ResourceIds` hold the picks in group order and its event goes on the first one's calendar. A chosen `resourceId` narrows only its own group. A type without `ResourceRequirements` behaves as one group of `ResourceIds` + `Assignment` (`GetResourceRequirements()`, `GetAllResourceIds()`). A resource may be in only one group of a type.

## Booking and Concurrency

| `IAppointmentService` member | Does |
|---|---|
| `BookAsync(AppointmentRequest)` | Book, or with `Hold = true` hold for `HoldMinutes`; `Requested` when the type `RequiresConfirmation` |
| `ConfirmAsync(appointment, actor)` | Confirm a held or requested appointment |
| `RescheduleAsync(appointment, startUtc, resourceId, actor, selfService)` | Move; keeps resources when all are still free and valid |
| `CancelAsync(appointment, reason, actor, selfService)` | Cancel and free the time (offered to the waitlist) |
| `CompleteAsync(appointment, noShow, actor)` | Took place, or no-show |
| `ExpireHoldsAsync()` | End holds that ran out (background task every minute) |

`AppointmentRequest`: `AppointmentTypeId`, `StartUtc`, `ResourceId`, `CustomerName`, `CustomerEmail`, `CustomerPhone`, `UserId`, `Notes`, `TimeZoneId`, `Source`, `Actor`, `Hold`, `SelfService`. Results are `AppointmentResult` (`Appointment`, `Errors`, `Succeeded`, `Conflict`).

Every booking and reschedule locks `SCHEDULING_RESOURCE_{id}` for every resource it could take plus their calendars' `CALENDAR_{id}` keys (and `SCHEDULING_CUSTOMER_{typeId}_{identifier}` with a per-customer limit) in one ordinal-ordered set, re-checks availability, picks resources, bumps every taken resource's `BookingVersion` with YesSql's concurrency check, then writes the appointment and its calendar event (tentative while held or requested) with `CalendarLocksHeld`. A booking for a customer who already has that type at that time is a `Conflict`, so retries can't take a second place.

```csharp
var result = await _appointmentService.BookAsync(new AppointmentRequest
{
    AppointmentTypeId = typeId,
    StartUtc = slot.StartUtc,
    CustomerName = "Admin",
    CustomerEmail = "admin@example.com",
    Source = "Portal",
    Actor = User.Identity?.Name,
    SelfService = true,
});

if (result.Conflict)
{
    // The time was taken meanwhile; offer the slots again.
}
```

## Booking Policies

Applied only to self-service calls (`AppointmentRequest.SelfService = true`, `selfService: true` on cancel and reschedule):

| Property | Self-service rule |
|---|---|
| `MaxUpcomingPerCustomer` | At most this many held, requested or confirmed future appointments of the type per customer (matched by user id, email or phone, under a customer lock) |
| `CancelNoticeMinutes` | No cancelling within this many minutes of the start |
| `RescheduleNoticeMinutes` | No moving within this many minutes of the start |

The booking page and manage link are always self-service; AI tools are unless the user has `ManageAppointments`; admin screens, the API and workflows never are. `CustomerCanCancel` / `CustomerCanReschedule` tell a page whether to offer the actions. `MinimumNoticeMinutes`, `HorizonDays`, `MaxPerDay` and `MaxPerWeek` shape open times for everyone.

## Self-Service and Waitlist

- A type with `AllowSelfService`, `Enabled` and a `Slug` gets `/book/{slug}` (route `SchedulingBook`): pick a day and time (site time zone), enter name, email, phone and notes. The customer lands on `/book/manage/{id}/{token}` (`SchedulingManage`) to move or cancel without signing in. Otherwise `404`.
- Cancelling or moving frees a time: the oldest `Waiting` entry whose range covers it is marked `Notified` and **Waitlist Slot Available** is raised. A group session shares one calendar event per time (`SourceId = session:...`).

## Extension Points

| Seam | Use it to |
|---|---|
| `IAppointmentService` / `ISchedulingAvailabilityService` | Book and find open times from another module |
| `IAppointmentSlotFilter` | `FilterAsync(AppointmentSlotFilterContext, slots)` narrows offered slots (context has `AppointmentType`, `ExcludeAppointmentId`); register with `services.AddScoped<IAppointmentSlotFilter, T>()` |
| `IAppointmentObserver` | `OnChangedAsync(AppointmentChange)` after commit: `Kind` (`Held`, `Requested`, `Confirmed`, `Rescheduled`, `Cancelled`, `Completed`, `NoShow`, `HoldExpired`, `WaitlistSlotAvailable`), `Appointment`, `AppointmentType`, `PreviousStartUtc`, `WaitlistEntry`, `Actor` |
| `DisplayDriver<AppointmentType>` / `DisplayDriver<SchedulingResource>` | Add a section to the appointment type or resource editor, both built with `IDisplayManager<T>`; built-in fields are `AppointmentTypeFields_Edit` / `SchedulingResourceFields_Edit` at `Content:1` |
| Calendar seams | Appointments are calendar events; `ICalendarBusyTimeSource` closes slots, `ICalendarEventObserver` sees appointment events |

```csharp
public sealed class LunchSlotFilter : IAppointmentSlotFilter
{
    public Task<IReadOnlyList<AppointmentSlot>> FilterAsync(AppointmentSlotFilterContext context, IReadOnlyList<AppointmentSlot> slots, CancellationToken cancellationToken = default)
    {
        if (context.AppointmentType?.Slug != "consultation")
        {
            return Task.FromResult(slots);
        }

        IReadOnlyList<AppointmentSlot> kept = slots
            .Where(slot => CalendarTime.ToLocal(slot.StartUtc, "America/New_York").Hour != 12)
            .ToList();

        return Task.FromResult(kept);
    }
}

// Startup: services.AddScoped<IAppointmentSlotFilter, LunchSlotFilter>();
```

An observer, an editor section driver with validation, and more are in [references/scheduling-extensions.md](references/scheduling-extensions.md).

## Workflows

Activities are in the **Scheduling** category; each event can be limited to `AppointmentTypeIds`.

| Events | Tasks (outcomes) |
|---|---|
| `AppointmentRequestedEvent`, `AppointmentConfirmedEvent`, `AppointmentRescheduledEvent`, `AppointmentCancelledEvent`, `AppointmentCompletedEvent`, `AppointmentNoShowEvent`, `AppointmentHoldExpiredEvent`, `WaitlistSlotAvailableEvent`, `AppointmentStartingSoonEvent` (needs Calendar Reminders) | Find Appointment Slots (Found, None), Book Appointment (Done, Taken, Failed), Confirm Appointment (Done, Failed), Reschedule Appointment (Done, Taken, Failed), Cancel Appointment (Done, Failed), Get Appointment (Found, NotFound), Add to Waitlist (Done, Failed) |

Input holds `Appointment`, `AppointmentId`, `AppointmentTypeId`, `AppointmentTypeName`, `ResourceIds`, `StartUtc`, `EndUtc`, `Status`, customer fields, `UserId`, `Notes`, `CalendarEventId`, `ManageToken`, `Source`, `CancelReason`, `Actor`, `PreviousStartUtc`; the waitlist event holds `WaitlistEntryId` and the freed `StartUtc`. Appointments booked by tasks have `Source = Workflow`.

## HTTP API

`/api/scheduling/v1` (Orchard Core `Api` scheme):

| Method and route | Does | Permission |
|---|---|---|
| `GET types` | Types with `resourceRequirements` | `ViewAppointments` |
| `GET types/{id}/slots?from=&to=&resourceId=` | Open times, at most 31 days | `ViewAppointments` |
| `POST appointments` | Book from an `AppointmentRequest` body; `201` | `ManageAppointments` |
| `GET appointments/{id}` | One appointment | `ViewAppointments` |
| `POST appointments/{id}/confirm` | Confirm | `ManageAppointments` |
| `POST appointments/{id}/reschedule` | `{ "startUtc": "...", "resourceId": null }` | `ManageAppointments` |
| `POST appointments/{id}/cancel` | `{ "reason": "..." }` | `ManageAppointments` |

A taken time or duplicate booking is `409` Problem Details; a missing body or backwards range is `400`.

## AI Tools

With `CrestApps.OrchardCore.AI` on, the **Scheduling** category has `listAppointmentTypes`, `findAppointmentSlots`, `bookAppointment` and `changeAppointment`. They work for anonymous visitors but only with enabled self-service types (unless the caller has `ManageAppointments`); `changeAppointment` needs the manage code or the email or phone on the appointment. See `orchardcore-ai-tools`.

## Report Builder Data Source

With `OrchardCore.DataPipelines.Core` on, the **Scheduling** data source (`Scheduling`) has `Appointments`, `AppointmentResources` (one row per resource booked), `AppointmentTypes` and `SchedulingResources` (need `ViewAppointments`) and `WaitlistEntries` (needs `ManageAppointments`). Notes, cancel reasons, manage links and working hours are never exposed. See `orchardcore-report-builder`.

## Recipes and Deployment

The `Scheduling` step (needs `OrchardCore.Recipes.Core`) imports `Resources`, `AppointmentTypes`, `Appointments` and `Waitlist` in that order, each created or, when `itemId` matches, replaced in place. Properties are camelCase; enums by name. Run it **after** the `Calendars` step.

```json
{
  "steps": [
    {
      "name": "Scheduling",
      "Resources": [
        {
          "itemId": "4sresdrsmith00000000000001",
          "name": "Dr. Smith",
          "kind": "Staff",
          "calendarId": "4cdrsmithappointments00001",
          "timeZoneId": "America/New_York",
          "availabilityMode": "WorkingHours",
          "workingHours": [ { "day": "Monday", "start": "09:00", "end": "17:00" } ],
          "enabled": true
        }
      ],
      "AppointmentTypes": [
        {
          "itemId": "4stypeconsultation00000001",
          "name": "Consultation",
          "slug": "consultation",
          "durationMinutes": 30,
          "slotIntervalMinutes": 30,
          "resourceIds": [ "4sresdrsmith00000000000001" ],
          "assignment": "AnyAvailable",
          "allowSelfService": true,
          "enabled": true
        }
      ]
    }
  ]
}
```

The **Scheduling** deployment step (category **Calendar**) exports resources and types, with `IncludeAppointments` (false) appointments without manage links, and with `IncludeWaitlist` (false) the waitlist. Full examples (shifts, overrides, groups, policies, appointments) are in [references/scheduling-recipes.md](references/scheduling-recipes.md).

## Gotchas

| Symptom | Check |
|---|---|
| No times offered | Type has resources, resources are `Enabled`, working hours cover the day, the day is inside notice and horizon, nothing busy on the resource's or user's calendars, and no slot filter drops it. |
| "The resource has no calendar to put the appointment on." | The recipe step never creates calendars; create an `Appointments` calendar in the `Calendars` step and set `calendarId`. |
| Imported appointments are missing from the calendar | The step writes records only; export and import the `Calendars` step with events too. |
| Shift staff never offered | Set `ShiftCalendarIds`; the `Shift` events must name the resource's `UserId` as attendee, and the Workforce schedule must be published. |
| Booking page 404 | Enable Self-Service; the type needs `Enabled`, `AllowSelfService` and a `slug`. |
| Customer limit or notice not enforced | Policies apply only to self-service calls; staff, API and workflows bypass them. |
| Editor refuses to save | A section driver added a model error, or a resource is in two requirement groups. |

Related skills: `orchardcore-calendars`, `orchardcore-workforce`, `orchardcore-teams`, `orchardcore-work-orders`, `orchardcore-dispatch`, `orchardcore-bookings`, `orchardcore-ai-tools`, `orchardcore-report-builder`.

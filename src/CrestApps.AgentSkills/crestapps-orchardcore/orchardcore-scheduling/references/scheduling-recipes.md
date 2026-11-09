# Scheduling Recipe Examples

The `Scheduling` step is registered when `OrchardCore.Recipes.Core` is on. It reads four sections in this order: `Resources`, `AppointmentTypes`, `Appointments`, `Waitlist`. Each entry is deserialized into its model (camelCase, case-insensitive; enums by name or number) and:

- created when its `itemId` is unknown (with that id), or
- **replaced in place** when `itemId` matches: every public property takes the incoming value, so omitted properties fall back to the model defaults (for example `durationMinutes` 30, `enabled` true, an empty `workingHours`).

Validation is minimal: a resource and a type need a `name`, an appointment and a waitlist entry an `appointmentTypeId`; anything else invalid is stored as given. The step does **not** run the editors or `IAppointmentService`:

- No calendar is created for a resource; set `calendarId` to an `Appointments` calendar from a preceding `Calendars` step (see the Calendars skill's recipe reference).
- Imported appointments get no calendar event and raise no workflow events; import their events with the `Calendars` step (deployment with **Include events**).
- A resource's `bookingVersion` is kept from the site; an appointment keeps its existing `manageToken`, or gets a new random one.
- A type's `slug` is trimmed and lower-cased.

## Staff with Weekly Hours, a Room, and a Two-Group Type

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Recipes.Core",
        "CrestApps.OrchardCore.Calendars",
        "CrestApps.OrchardCore.Scheduling",
        "CrestApps.OrchardCore.Scheduling.SelfService"
      ],
      "disable": []
    },
    {
      "name": "Calendars",
      "Calendars": [
        { "itemId": "4cdrsmithappointments00001", "name": "Dr. Smith", "type": "Appointments", "timeZoneId": "America/New_York", "visibility": "Private", "conflictPolicy": "AllowOverlaps", "source": "Scheduling", "sourceId": "4sresdrsmith00000000000001" },
        { "itemId": "4croom1appointments0000001", "name": "Room 1", "type": "Appointments", "timeZoneId": "America/New_York", "visibility": "Private", "conflictPolicy": "AllowOverlaps", "source": "Scheduling", "sourceId": "4sresroom10000000000000001" }
      ]
    },
    {
      "name": "Scheduling",
      "Resources": [
        {
          "itemId": "4sresdrsmith00000000000001",
          "name": "Dr. Smith",
          "kind": "Staff",
          "userId": null,
          "calendarId": "4cdrsmithappointments00001",
          "timeZoneId": "America/New_York",
          "availabilityMode": "WorkingHours",
          "workingHours": [
            { "day": "Monday", "start": "09:00", "end": "12:00" },
            { "day": "Monday", "start": "13:00", "end": "17:00" },
            { "day": "Wednesday", "start": "09:00", "end": "17:00" }
          ],
          "dateOverrides": [
            { "date": "2026-12-24T00:00:00", "closed": true },
            { "date": "2026-12-30T00:00:00", "closed": false, "start": "10:00", "end": "14:00" }
          ],
          "enabled": true
        },
        {
          "itemId": "4sresroom10000000000000001",
          "name": "Room 1",
          "kind": "Room",
          "calendarId": "4croom1appointments0000001",
          "timeZoneId": "America/New_York",
          "availabilityMode": "WorkingHours",
          "workingHours": [
            { "day": "Monday", "start": "08:00", "end": "18:00" },
            { "day": "Wednesday", "start": "08:00", "end": "18:00" }
          ],
          "enabled": true
        }
      ],
      "AppointmentTypes": [
        {
          "itemId": "4stypeconsultation00000001",
          "name": "Consultation",
          "description": "A 45-minute first visit.",
          "slug": "consultation",
          "durationMinutes": 45,
          "slotIntervalMinutes": 15,
          "bufferBeforeMinutes": 0,
          "bufferAfterMinutes": 15,
          "minimumNoticeMinutes": 240,
          "horizonDays": 45,
          "maxPerDay": 8,
          "maxPerWeek": 30,
          "maxUpcomingPerCustomer": 2,
          "cancelNoticeMinutes": 1440,
          "rescheduleNoticeMinutes": 720,
          "capacity": 1,
          "resourceIds": [ "4sresdrsmith00000000000001", "4sresroom10000000000000001" ],
          "assignment": "AnyAvailable",
          "resourceRequirements": [
            { "name": "Staff", "resourceIds": [ "4sresdrsmith00000000000001" ], "assignment": "RoundRobin", "weights": { "4sresdrsmith00000000000001": 1 } },
            { "name": "Room", "resourceIds": [ "4sresroom10000000000000001" ], "assignment": "AnyAvailable" }
          ],
          "requiresConfirmation": false,
          "holdMinutes": 10,
          "allowSelfService": true,
          "location": "Main clinic",
          "color": "#0b7285",
          "enabled": true
        }
      ]
    }
  ]
}
```

- `workingHours[].day` is a `DayOfWeek` name; `start` and `end` are `HH:mm` in the resource's `timeZoneId`. Several entries on one day make breaks.
- With several groups, list every resource in `resourceIds` too and the first group's assignment in `assignment`, as the editor saves them.
- Self-service policies (`maxUpcomingPerCustomer`, `cancelNoticeMinutes`, `rescheduleNoticeMinutes`) only bind customers; `maxPerDay` and `maxPerWeek` close times for everyone.

## A Staff Member Who Works Shifts

Shifts mode reads `Shift` events that name the resource's user as an attendee on each calendar in `shiftCalendarIds` (such as each team's `Workforce` calendar). The resource still needs its own appointments `calendarId`.

```json
{
  "steps": [
    {
      "name": "Scheduling",
      "Resources": [
        {
          "itemId": "4srestechlee00000000000001",
          "name": "Sam Lee",
          "kind": "Staff",
          "userId": "4usersamlee000000000000001",
          "calendarId": "4csamleeappointments000001",
          "timeZoneId": "America/Chicago",
          "availabilityMode": "Shifts",
          "shiftCalendarIds": [ "4cworkforcenorth0000000001", "4cworkforcesouth0000000001" ],
          "enabled": true
        }
      ]
    }
  ]
}
```

Resources saved before `shiftCalendarIds` existed have a single `shiftCalendarId`; it is still read.

## A Group Class with Confirmation

```json
{
  "steps": [
    {
      "name": "Scheduling",
      "AppointmentTypes": [
        {
          "itemId": "4stypeyogaclass00000000001",
          "name": "Yoga class",
          "slug": "yoga",
          "durationMinutes": 60,
          "slotIntervalMinutes": 60,
          "capacity": 12,
          "requiresConfirmation": true,
          "resourceIds": [ "4sresroom10000000000000001" ],
          "assignment": "AnyAvailable",
          "allowSelfService": true,
          "enabled": true
        }
      ]
    }
  ]
}
```

A group session shares one calendar event per time; new bookings are `Requested` until confirmed.

## Appointments and the Waitlist

```json
{
  "steps": [
    {
      "name": "Scheduling",
      "Appointments": [
        {
          "itemId": "4sapptadmin000000000000001",
          "appointmentTypeId": "4stypeconsultation00000001",
          "resourceIds": [ "4sresdrsmith00000000000001", "4sresroom10000000000000001" ],
          "startUtc": "2026-11-16T15:00:00Z",
          "endUtc": "2026-11-16T15:45:00Z",
          "timeZoneId": "America/New_York",
          "status": "Confirmed",
          "customerName": "Admin",
          "customerEmail": "admin@example.com",
          "source": "Admin"
        }
      ],
      "Waitlist": [
        {
          "itemId": "4swaitadmin000000000000001",
          "appointmentTypeId": "4stypeconsultation00000001",
          "fromUtc": "2026-11-16T13:00:00Z",
          "toUtc": "2026-11-20T22:00:00Z",
          "customerName": "Admin",
          "customerEmail": "admin@example.com",
          "status": "Waiting"
        }
      ]
    }
  ]
}
```

Prefer booking through `IAppointmentService` (admin, API, workflow task) on a live site; import appointments only when moving a site, together with their calendar events.

## Deployment

The **Scheduling** deployment step (category **Calendar**, title **Scheduling**, needs `OrchardCore.Deployment`) writes the same shape:

| Setting | Default | Effect |
|---|---|---|
| `IncludeAppointments` | false | Every appointment, with customer name, email and phone, without its manage link |
| `IncludeWaitlist` | false | The waitlist |

Add the **Calendars** step to the same plan (with **Include events** when appointments are included) so resource calendars and appointment events come along, and order it before **Scheduling**. With **CrestApps Recipes** on, the step is described by `SchedulingRecipeStepSchema` and its settings by `SchedulingDeploymentStepSchema`.

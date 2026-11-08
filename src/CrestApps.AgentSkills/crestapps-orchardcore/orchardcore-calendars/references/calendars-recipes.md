# Calendars Recipe Examples

The `Calendars` step is registered when `OrchardCore.Recipes.Core` is on. It reads:

- `Categories`: each `CalendarCategory` (`itemId`, `name`, `color`, `ownerId`; `ownerId` null for a category everyone can use). Created or updated first. A category without a name is an error.
- `Calendars`: each `Calendar`, matched by `itemId` (created with that id when unknown, through `ICalendarService.NewAsync(type)`), then its `Events`, each matched by `itemId` and created or updated through `ICalendarEventManager`.

Rules that matter:

- List names (`Categories`, `Calendars`, `Events`) start with a capital letter; everything else is camelCase (read case-insensitively). Enums are written by name; numbers are read too.
- A calendar needs a `type` whose feature is on, or the step reports "The calendar type ... is not enabled".
- On update the step copies `name`, `description`, `visibility`, `shares`, `conflictPolicy`, `enabled`, `source` and `sourceId` **as given**, and `color`, `timeZoneId`, `ownerId` and `capacity` (when 1 or more) only when present. `ownerKind` always comes from the type. Omitting `conflictPolicy` therefore stores `AllowOverlaps`, and omitting `visibility` stores `Private`.
- `properties` entries (such as `ResourceCalendarSettings`) are merged into the calendar's properties.
- Event `start` and `end` are wall-clock times in the event's `timeZoneId`, written `yyyy-MM-ddTHH:mm:ss` without an offset. Events are created as given (the event type's defaults are not applied), so set `eventType`, `status` and `transparency` when they matter.
- Writes use actor `recipe`, skip the overlap check and send no invitations.
- Run it before the `Scheduling` step, which references calendar ids.

## Categories, a Company Calendar and a Repeating Meeting

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Recipes.Core",
        "CrestApps.OrchardCore.Calendars"
      ],
      "disable": []
    },
    {
      "name": "Calendars",
      "Categories": [
        { "itemId": "4ccatholiday00000000000001", "name": "Company holiday", "color": "#e03131", "ownerId": null },
        { "itemId": "4ccatcustomer0000000000001", "name": "Customer meeting", "color": "#2f9e44", "ownerId": null }
      ],
      "Calendars": [
        {
          "itemId": "4cmpnycalendar000000000001",
          "name": "Company",
          "description": "Company-wide events and holidays.",
          "type": "General",
          "color": "#1971c2",
          "timeZoneId": "America/New_York",
          "visibility": "Public",
          "conflictPolicy": "AllowOverlaps",
          "enabled": true,
          "Events": [
            {
              "itemId": "4cmpnyallhands000000000001",
              "eventType": "Meeting",
              "title": "All hands",
              "location": "Main hall",
              "categoryId": "4ccatcustomer0000000000001",
              "allDay": false,
              "start": "2026-11-02T09:00:00",
              "end": "2026-11-02T10:00:00",
              "timeZoneId": "America/New_York",
              "status": "Confirmed",
              "transparency": "Busy",
              "reminderMinutes": [ 10 ],
              "recurrence": {
                "rule": "FREQ=WEEKLY;BYDAY=MO",
                "exceptionDates": [ "2026-12-28T09:00:00" ]
              }
            },
            {
              "itemId": "4cmpnythanksgiving00000001",
              "eventType": "Holiday",
              "title": "Thanksgiving",
              "categoryId": "4ccatholiday00000000000001",
              "allDay": true,
              "start": "2026-11-26T00:00:00",
              "end": "2026-11-28T00:00:00",
              "timeZoneId": "America/New_York",
              "transparency": "Free"
            }
          ]
        }
      ]
    }
  ]
}
```

All-day events use midnight dates with an **exclusive** end: the example covers 26 and 27 November.

## A Room, a Team Calendar Shared with a Role, and an Office

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Recipes.Core",
        "CrestApps.OrchardCore.Calendars",
        "CrestApps.OrchardCore.Calendars.Teams",
        "CrestApps.OrchardCore.Calendars.Resources",
        "CrestApps.OrchardCore.Calendars.Office"
      ],
      "disable": []
    },
    {
      "name": "Calendars",
      "Calendars": [
        {
          "itemId": "4croomboardroom00000000001",
          "name": "Boardroom",
          "type": "Room",
          "timeZoneId": "America/New_York",
          "visibility": "Public",
          "conflictPolicy": "LimitConcurrent",
          "capacity": 1,
          "properties": {
            "ResourceCalendarSettings": { "location": "Floor 3", "seats": 12, "amenities": "Screen, video bar" }
          }
        },
        {
          "itemId": "4cteamsupport0000000000001",
          "name": "Support team",
          "type": "Team",
          "ownerId": "Support",
          "timeZoneId": "America/New_York",
          "visibility": "Shared",
          "conflictPolicy": "AllowOverlaps",
          "shares": [
            { "principalKind": "Role", "principalId": "Manager", "displayName": "Manager", "access": "Read" }
          ],
          "Events": [
            {
              "itemId": "4cteamsupportoncall0000001",
              "eventType": "OnCall",
              "title": "On call",
              "start": "2026-11-09T08:00:00",
              "end": "2026-11-09T16:00:00",
              "timeZoneId": "America/New_York",
              "attendees": [
                { "email": "admin@example.com", "name": "Admin", "role": "Required", "response": "Accepted" }
              ],
              "recurrence": { "rule": "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR;COUNT=20" }
            }
          ]
        },
        {
          "itemId": "4cofficehq0000000000000001",
          "name": "Head office",
          "type": "Office",
          "timeZoneId": "America/New_York",
          "visibility": "Public",
          "conflictPolicy": "LimitConcurrent",
          "capacity": 40,
          "properties": {
            "OfficeCalendarSettings": { "address": "1 Main Street", "contact": "reception@example.com" }
          }
        }
      ]
    }
  ]
}
```

- A Team calendar's `ownerId` is a **role name**; a Personal calendar's is a user id. Event types are technical names: Team `Meeting`, `OnCall`, `OutOfOffice`, `Milestone`; Personal `Event`, `Meeting`, `Focus`, `OutOfOffice`; Room `Booking`, `Maintenance`; Equipment `Reservation`, `Maintenance`; Office `InOffice`, `DeskBooking`, `Visitor`, `Closed`. The office `capacity` is the desk count.
- `shares[].access` is `FreeBusy`, `Read`, `Write` or `Manage`; `principalKind` is `User` or `Role`.
- Attendees: `userId`, or `email` + `name`, or `resourceCalendarId` (with `role` `Resource`) to book a room from another calendar's event.

## A Calendar for a Scheduling Resource

The `Scheduling` step stores resources as given and never creates their calendars (only the resource editor does). Create the `Appointments` calendar here and point the resource's `calendarId` at it:

```json
{
  "steps": [
    {
      "name": "Calendars",
      "Calendars": [
        {
          "itemId": "4cdrsmithappointments00001",
          "name": "Dr. Smith",
          "type": "Appointments",
          "timeZoneId": "America/New_York",
          "visibility": "Private",
          "conflictPolicy": "AllowOverlaps",
          "source": "Scheduling",
          "sourceId": "4sresdrsmith00000000000001"
        }
      ]
    }
  ]
}
```

## Deployment

The **Calendars** deployment step (category **Calendar**, title **Calendars**, needs `OrchardCore.Deployment`) writes the same shape:

| Setting | Default | Effect |
|---|---|---|
| `IncludeEvents` | false | Each calendar's stored events (up to 10,000 per calendar) as `Events` |
| `IncludeCategories` | true | Everyone's categories as `Categories` |

Export first to get the exact shape for a site, then trim it. With **CrestApps Recipes** on, the step is described by `CalendarsRecipeStepSchema` and its settings by `CalendarsDeploymentStepSchema`.

Treat an export as secret until checked: it is meant to drop each calendar's feed token and booking version, but review the file for `feedToken` and attendees' `responseToken` values before sharing it.

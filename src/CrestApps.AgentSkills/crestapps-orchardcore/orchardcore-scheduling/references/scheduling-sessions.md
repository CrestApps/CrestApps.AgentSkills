# Scheduling Sessions

Reference `CrestApps.OrchardCore.Scheduling.Abstractions` for `SchedulingSession`, `ISchedulingSessionService`, `SessionRegistrationRequest`, `SessionRegistrationResult` and `SessionAttendanceMark`. The service is registered by `AddSchedulingCore()`.

## The Session

| Property | Meaning |
|---|---|
| `AppointmentTypeId` | The type whose rules (confirmation, cancel notice) the registrations follow. |
| `Title` | The name shown. |
| `Meetings` | `SchedulingSessionMeeting { StartUtc, EndUtc }` in order; `StartUtc`, `EndUtc` and `TotalMinutes` are read from them. |
| `Capacity` | How many people may register (default `1`). |
| `Location`, `MeetingUrl` | Where it takes place, and a meeting link pasted by the organizer. |
| `ResourceIds` | Instructors, rooms. The first one's calendar holds the session's event (`CalendarEventId`) with the registered people as attendees. |
| `RegistrationOpensUtc`, `RegistrationClosesUtc` | Empty opens at once and closes when the first meeting starts. |
| `WaitlistMode` | `AutoPromote` (default: the next person is offered the place, held for `OfferHours`, then it passes on), `Notify` (the next person is told and registers themselves) or `None` (a full session refuses). |
| `OfferHours` | How long an offered place is held (default `24`). |
| `Status` | `Scheduled` or `Cancelled`. |
| `Source`, `SourceId` | What made it, such as `Learning.Class` and the class item. |

## ISchedulingSessionService

| Member | Does |
|---|---|
| `SaveAsync(session, actor)` | Creates or updates the session and its calendar event; returns the errors. Moving the meetings tells the registrations' observers (`Rescheduled`). |
| `FindByIdAsync`, `ListBySourceAsync(source, sourceId)`, `ListAsync(fromUtc, toUtc)` | Read sessions. |
| `CancelAsync(sessionId, reason, actor)` | Cancels the session and every registration and wait of it. |
| `RegisterAsync(SessionRegistrationRequest)` | Registers `UserId` (or `CustomerName` and `CustomerEmail`) with `Source`, `SourceId` and `Actor`; when full and `JoinWaitlistWhenFull` (default `true`), joins the waitlist. `IgnoreWindow` skips the registration window (staff registering someone). |
| `AcceptOfferAsync(appointmentId, actor)` | Confirms a place offered from the waitlist while it is held. |
| `CancelRegistrationAsync(appointmentId, reason, actor)` | Cancels and offers the place to the next person waiting. |
| `LeaveWaitlistAsync(waitlistEntryId)` | Leaves the waitlist. |
| `ListRegistrationsAsync(sessionId)`, `ListRegistrationsForUserAsync(userId)`, `ListWaitlistAsync(sessionId)`, `CountTakenAsync(sessionId)` | Registrations (cancelled ones included), the waitlist (first come, first served) and places taken (held offers included). |
| `RecordAttendanceAsync(sessionId, marks, actor)` | Marks several registrations at once from a roster; saving the same marks again changes nothing. Returns how many changed. |
| `ExpireOffersAsync()` | Passes run-out offers to the next people waiting (the appointment holds background task, every minute). |

`SessionRegistrationResult`: `Appointment`, `WaitlistEntry`, `Error`, `Succeeded`, `Waitlisted`.

## Waitlist Offers

With `AutoPromote`, a freed place becomes a `Held` appointment for the first person waiting; their `WaitlistEntry` gets `AppointmentId` and `OfferExpiresUtc`, and observers hear `WaitlistSlotAvailable`. Accept it with `AcceptOfferAsync` (or `IAppointmentService.ConfirmAsync`, which hands off). When the offer runs out, the entry becomes `OfferExpired` and the place goes to the next person. Session waits never take a place an ordinary appointment frees, and ordinary waitlist entries never take a session's place.

## Attendance

```csharp
var changed = await _sessionService.RecordAttendanceAsync(sessionId,
[
    new SessionAttendanceMark(anaAppointmentId, AppointmentAttendance.Attended),
    new SessionAttendanceMark(benAppointmentId, AppointmentAttendance.Partial, MinutesAttended: 95),
    new SessionAttendanceMark(caraAppointmentId, AppointmentAttendance.Absent),
], actor: User.Identity?.Name);
```

`Attended` and `Partial` set the registration's status to `Completed`, `Absent` to `NoShow`, and observers hear `Completed` or `NoShow`; `Unmarked` puts a completed or no-show registration back to `Confirmed`. `IAppointmentService.CompleteAsync(appointment, noShow)` on a registration records `Absent` (no-show) or `Attended`.

## React to Registrations

```csharp
public sealed class CourseSessionObserver : IAppointmentObserver
{
    private readonly CourseProgressService _progress;

    public CourseSessionObserver(CourseProgressService progress)
    {
        _progress = progress;
    }

    public async Task OnChangedAsync(AppointmentChange change, CancellationToken cancellationToken = default)
    {
        if (change.Appointment?.Source != "Training.Course" || string.IsNullOrEmpty(change.Appointment.SessionId))
        {
            return;
        }

        if (change.Appointment.Attendance == AppointmentAttendance.Attended)
        {
            await _progress.MarkSessionAttendedAsync(change.Appointment.SourceId, change.Appointment.UserId, cancellationToken);
        }
    }
}

// Startup: services.AddScoped<IAppointmentObserver, CourseSessionObserver>();
```

Learning Classes is built this way: a class item's sessions use source `Learning.Class`, and attendance completes the class item.

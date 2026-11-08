# Workforce Shift Planning, Time Off, Time Clock and My Work Details

## Shift Planning

| Record | Holds |
|---|---|
| `ShiftTemplate` | Name, color, start time of day, length in minutes (15 minutes to 24 hours, may end after midnight), `Segments` (activity code, offset, length), teams that may use it (empty for all), required skills. |
| `ShiftPattern` (rotation) | Team, 1 to 12 weeks, `Days` (week, weekday, template), `Workers` (worker, date they started week 1). |
| `CoverageRequirement` | Team, optional skill, `Rows` (weekday or date, start, end, minimum, target, maximum), effective dates, `Source` (`Forecast` when Workforce Forecasting wrote it). A date row replaces that weekday's rows. |
| `SchedulePeriod` | 1 to 12 weeks, status `Draft`, `Published`, `Locked`, primary scenario, published version. |
| `ScheduleScenario`, `DraftShift` | Drafts of a period; a draft shift has a worker (or none for an open shift), day, wall-clock start and end, template, segments and a stable `ShiftKey`. |
| `ScheduleChange` | Every change after the first publish: added, removed, moved, reassigned, segments changed, swapped, open shift filled; `InitiatedBy` `Employer` or `Worker`. |
| `OpenShift`, `ShiftTrade` | Offered shifts with claims; give-aways and swaps with their status. |

- `ISchedulePlanningService`: create, list and delete periods; save and delete draft shifts; `ApplyPatternsAsync` (fill from rotations), `CopyFromPeriodAsync`, `AutoScheduleAsync(scenarioId, AutoScheduleOptions)`, `CheckPublishAsync`, `PublishAsync(periodId, actor, overrideReason)`, `ListChangesAsync`.
- `ICoverageService.ComputeAsync(CoverageQuery)` returns `CoverageInterval`s (minimum, target, maximum, scheduled, gap) in 5 to 240 minute intervals in the team's time zone; a shift counts except during segments whose activity code doesn't count toward coverage, and a skill requirement counts only its holders.
- `IShiftService` changes published shifts under per-worker locks taken in id order: `ReassignAsync`, `SwapAsync`, `ChangeTimesAsync`, `RemoveAsync` (also deletes the draft so a republish doesn't bring it back), `ListWorkerShiftsAsync`, `CheckAsync`.
- `IShiftAssignmentValidator` (built-in): active, on the team that day, holds the required skills, no overlap, `MinRestHours` rest, weekly (Monday to Sunday) and daily hour limits. Labor rules add their own.
- `IOpenShiftService`: post, claim, withdraw, assign, decline, cancel, `ListEligibleWorkersAsync`, `AwardDueAsync` (seniority, then hire date, then claim time). The **Open shifts awarded by seniority** task runs every five minutes.
- `IShiftTradeService`: offer, accept, reject, approve, decline, cancel. A shift is in one pending trade at a time and must not have started; without approval an accepted trade applies at once.
- **Publishing** matches existing `Shift` events to drafts by `SourceId` = `ShiftKey`: new drafts become events (`Source` `Workforce`, not editable), changed ones are updated, removed ones canceled, unchanged ones untouched. Drafts with nobody become open shifts. Each publish bumps the version; from the second publish on every change is a `ScheduleChange`. A shift the calendar refuses is reported in `Errors` and the rest still publish.
- **Auto-schedule** demand is the coverage rows per skill and interval (`AutoScheduleOptions.IntervalMinutes`, 30). The built-in solver builds greedily, then improves within `SolverTimeLimitSeconds` with a repeatable seed. Hard limits: one shift a day, availability, skills, rest, weekly and daily hours, `MaxConsecutiveDays`.

## Time Off

| Record | Holds |
|---|---|
| `TimeOffType` | `Unit` (`Hours`, `Days`), `HoursPerDay`, `IsPaid`, `RequiresBalance`, `AllowPartialDay`, `RequiresReason`, `CountsAgainstAllotment`, `VisibleToWorkers`, `PayCode`, `StageId`. |
| `TimeOffPolicy` | `TypeId`, `AccrualMethod` (`None`, `PerPeriod`, `PerHourWorked`, `AnnualLumpSum`), `Frequency` (`Weekly`, `Biweekly`, `SemiMonthly`, `Monthly`), `AnchorDate`, `Rate`, `TenureTiers`, `WaitingPeriodDays`, `MaxBalance`, `CarryoverMax`, `ResetMonth`, `ResetDay`, `NegativeBalanceLimit`, `Assignments` (team or worker with dates). |
| `TimeOffLedgerEntry` | Append-only; `Kind` `Opening`, `Accrual`, `Taken`, `Reversal`, `Adjustment`, `Carryover`, `Expiry`, `Payout`, `Grant`; signed `Amount`; `EffectiveDate`; `ExpiresOn` (a grant's last day); `IdempotencyKey`. |
| `TimeOffRequest` | Worker, team, type, policy, days or part of a day, `Amount`, `Status` (`Pending`, `Waitlisted`, `Approved`, `Declined`, `Canceled`, `Withdrawn`), calendar event, flagged shifts. |
| `TimeOffAllotment` | `WeekdaySlots` (Sunday first), `Dates`, `TypeIds`, `AutoApprove`, `WhenFull` (`Waitlist`, `Pending`). |
| `BlackoutPeriod` | Team (or all), dates, `TypeIds`, `Message`. |

- A worker's policy for a type is their own assignment first, then the nearest team up the tree.
- `ITimeOffBalanceService.GetBalanceAsync(worker, policy, date)` returns `Current`, `ProjectedAccrual`, `Upcoming`, `Pending`, `Expiring`, `Available` and the `Grants` still usable. `AdjustAsync(TimeOffAdjustment)` writes an opening balance, adjustment, carryover, payout or grant once per idempotency key.
- **Accrual** runs hourly at a quarter past (**Time off accrual** task), once per period per worker and policy (keys `accrual:` and `expiry:`), capped at `MaxBalance`; at each reset date, what is above `CarryoverMax` expires.
- **Requests** count each day's published paid shift time, or `HoursPerDay` on a weekday without shifts. Submitting checks blackout dates of the team and teams above, the balance less the negative limit, overlaps and the reason, under the worker's lock and one lock per allotment (`WORKFORCE_ALLOTMENT_{id}`), so two requests can't both take the last place.
- Approving writes a busy, confirmed, non-editable `TimeOff` event and a `Taken` entry; overlapping shifts are flagged or removed per the team's `TimeOffShiftHandling`. Canceling approved time off writes a `Reversal`, cancels the event, puts back the shifts it removed, and promotes waitlisted requests that now fit (raising **Time Off Waitlist Promoted**). Pending requests show on the team calendar as tentative (`PendingTimeOffEventSource`, `WorkforceTimeOffRequests`).

### Grant Time With a Last Day

```csharp
var result = await _balanceService.AdjustAsync(new TimeOffAdjustment
{
    WorkerId = worker.ItemId,
    PolicyId = vacationPolicy.ItemId,
    Kind = TimeOffLedgerKind.Grant,
    Amount = 40,
    EffectiveDate = new DateOnly(2026, 11, 9),
    ExpiresOn = new DateOnly(2026, 12, 30),
    Note = "Bonus time for the holiday rush",
    IdempotencyKey = $"bonus-2026:{worker.ItemId}",
}, actor: "hr");
```

Time off comes out of grants first, the one ending soonest first; what is left expires the day after `ExpiresOn` with an `Expiry` entry (key `grant-expiry:` and the grant id). Balances and the worker's My Work page show what is left of each grant.

## Time Clock

- `TimePunch` records never change. `ITimeClockService.PunchAsync` records one under the worker's lock, once per `IdempotencyKey`, when it follows the last punch (an open punch older than 16 hours no longer counts), the source is allowed, it isn't earlier than `BlockEarlyPunchInMinutes` before a shift, and the day's timesheet is open. `CorrectAsync` and `RemoveAsync` record a manager's punch with a reason. A team's policy is its own `TimeClockPolicyId` or the nearest parent's (`GetPolicyAsync`).
- `ITimesheetCalculator` (pure) pairs punches, applies grace and rounding, splits at midnight in the worker's zone (NodaTime), takes the automatic meal, classifies regular, overtime and double time by day and pay week (weekly overtime counts regular hours only), applies seventh-day overtime, pays holidays and weekend days with their codes, adds shift differentials, approved time off and predictability pay, and raises attendance exceptions.
- `ITimesheetService`: one `Timesheet` per worker and pay period; `SubmitAsync` (attestation), `ApproveAsync`, `RejectAsync`, `ReopenAsync`, `LockPeriodAsync` (approved timesheets of an ended period). The **Timesheet refresh** task runs hourly.
- `IAttendanceService` pages, excuses and confirms exceptions, sums points in the policy window and handles `PunchCorrection` requests; reaching the threshold calls every `IAttendancePointsHandler` (the built-in one tells the line manager).
- `IPayrollExportService.RunAsync` exports a period's locked timesheets through an `IPayrollExportProvider` (built-in `Csv` with `PayrollCsvSettings` columns), keeps a `PayrollExportBatch` and marks predictability pay exported.
- **Kiosks** live at `workforce/kiosk/{token}` (only a hash is stored); workers use employee number and PIN (PBKDF2, five wrong PINs lock for 15 minutes).
- **Time Tracker**: every punch change rebuilds the worker's entries (`ManagedBy` `Workforce.TimeClock`) for workers whose team has a policy. See `orchardcore-time-tracker`.

## My Work Portal

- Pages are admin pages under `Admin/my-work` (`MyWorkController`, `MyTimeController`, `MyTeamController`, plus pages other modules add through `IMyWorkLinkProvider`), listed in the **My Work** admin menu, which shows managers **Approvals** and **Who's working** too.
- `WorkerAdminAccessAuthorizationHandler` grants `AccessAdminPanel` to a signed-in, non-terminated worker with `UseWorkforcePortal`, so workers need no admin role; every other admin page still checks its own permission.
- `my-work/{**path}` redirects to the same page under `Admin/my-work`. `my-work/sw.js` (service worker, scope `Admin/my-work`), `my-work/manifest.webmanifest` and `my-work/feed/{token}.ics` stay anonymous.
- **Notifications** go through `IWorkforceNotifier` to every `IWorkforceNotificationChannel`: `Inbox` (can't be turned off), `Push` (web push with VAPID keys made on first use, `aes128gcm`, the `WorkforceWebPush` named `HttpClient`) and `Email` (with `OrchardCore.Email`). Workers choose channels and kinds (`SchedulePublished`, `ShiftChanged`, `TimeOff`, `OpenShift`, `ShiftTrade`, `Timesheet`, `Attendance`, `ShiftReminder`) and quiet hours in their `WorkerPortalSettings`; during quiet hours only the inbox gets notices.
- **Shift reminders** (task every five minutes): each published shift starting within the worker's `ReminderMinutes` (60 by default, 0 off, at most 240), once per shift and start time, recorded before sending so it is never sent twice.
- **Calendar feed**: `IWorkerFeedService.GetTokenAsync(worker, renew)` keeps a 256-bit secret; the feed writes shifts and approved time off from 28 days back to 84 days ahead. Renewing stops the old address.
- Headless sites build their own worker front end on the same services and the Workforce API.

## Background Tasks

| Task | Schedule | Feature |
|---|---|---|
| Open shifts awarded by seniority | `*/5 * * * *` | Shift Planning |
| Shift reminders | `*/5 * * * *` | My Work Portal |
| Time off accrual | `15 * * * *` | Time Off |
| Time off to Time Tracker | `5 * * * *` | Time Off with Time Tracker |
| Timesheet refresh | `40 * * * *` | Time Clock |
| Time clock to Time Tracker | `*/5 * * * *` (once, 25 workers a run) | Time Clock |

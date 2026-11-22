---
name: orchardcore-workforce
description: Skill for the CrestApps Orchard Core Workforce module (CrestApps.OrchardCore.Workforce) and its features. Covers workers (Staff members with WorkforceStaffSettings), WorkforceTeamSettings on shared teams, activity codes, team Workforce calendars, Shift Planning under the Calendars menu (templates, rotations, coverage, auto-schedule, open shifts, shift trades, segment placement), Time Off (accruals, grants, allotments, waitlist, the shared ledger), Labor Rules with AddLaborRule, Time Clock (policies, timesheets, kiosks, pay inputs for Payroll Pay Setup), the My Work portal, web push, the Workforce API, AI tools, the report data source, workflow events, the Scheduling card on the staff editor, telemetry and the WorkforceSample recipe. Use this skill when requests mention workforce management, shifts, schedules, time off, labor rules, punches or timesheets. Strong matches include WorkforceStaffSettings, ISchedulePlanningService, IShiftService, ITimeOffRequestService and ILaborRule.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Workforce

## People, Shifts, Time Off and Pay on the Shared Calendar

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Workforce module. Workforce schedules the people who work (**workers**, which are staff members of the Staff building block, see `orchardcore-staff`) and gives every shared team a **Workforce calendar** where published shifts and approved time off live. Its features add shift planning, time off, labor rules, a time clock, an HTTP API and the **My Work** self-service pages.

### Guidelines

- Install `CrestApps.OrchardCore.Workforce` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes Workforce, Teams, Skills, Time Tracker and Workforce for Contact Center) in the web/startup project.
- The base feature depends on Calendars, Skills, Staff and Teams. Staff, teams, skills and Time Tracker have their own skills: `orchardcore-staff`, `orchardcore-teams`, `orchardcore-skills`, `orchardcore-time-tracker`. The Time Clock also needs Payroll's Pay Setup (`orchardcore-payroll`). The calendar type is described in `orchardcore-calendars`; Scheduling reads shifts as staff hours (`orchardcore-scheduling`).
- Reference `CrestApps.OrchardCore.Workforce.Abstractions` (models, contracts, `WorkforcePermissions`, `WorkforceConstants`) and `CrestApps.OrchardCore.Workforce.Core` (services, base types such as `WorkforceEventActivity`, `RecipeImport`, `WorkforceReportDataSet<TRecord, TIndex>`, `MyWorkPortal`) from your module, never the module project.
- Workers are `StaffMember` records: read and save them with Staff's `IStaffMemberManager` and `IStaffDirectory`. `WorkerProfile`, `IWorkerManager`, `WorkerQuery` and the **Workforce > Workers** list are gone.
- Change Workforce records through the services (`ISchedulePlanningService`, `IShiftService`, `ITimeOffRequestService`, `ITimeClockService`); they validate, take distributed locks per worker (`WORKFORCE_WORKER_{id}`, `WORKFORCE_TIMEOFF_{id}`), log schedule changes and raise events.
- Nothing is on a worker's calendar until a schedule is **published**. Dispatch (`orchardcore-dispatch`) only assigns technicians on published shifts.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Workforce | `CrestApps.OrchardCore.Workforce` | `CrestApps.OrchardCore.Calendars`, `CrestApps.OrchardCore.Skills`, `CrestApps.OrchardCore.Staff`, `CrestApps.OrchardCore.Teams` |
| Workforce Shift Planning | `CrestApps.OrchardCore.Workforce.ShiftPlanning` | `CrestApps.OrchardCore.Workforce` |
| Workforce Time Off | `CrestApps.OrchardCore.Workforce.TimeOff` | `CrestApps.OrchardCore.Workforce` |
| Workforce Labor Rules | `CrestApps.OrchardCore.Workforce.LaborRules` | `CrestApps.OrchardCore.Workforce.ShiftPlanning` |
| Workforce Time Clock | `CrestApps.OrchardCore.Workforce.TimeClock` | `CrestApps.OrchardCore.Workforce`, `CrestApps.OrchardCore.TimeTracker`, `CrestApps.OrchardCore.Payroll.PaySetup` |
| Workforce API | `CrestApps.OrchardCore.Workforce.Api` | `CrestApps.OrchardCore.Workforce` |
| Workforce My Work Portal | `CrestApps.OrchardCore.Workforce.Portal` | `CrestApps.OrchardCore.Workforce` |

Forecasting, intraday and adherence live in the Workforce for Contact Center module (`orchardcore-workforce-contact-center`).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Workforce",
        "CrestApps.OrchardCore.Workforce.ShiftPlanning",
        "CrestApps.OrchardCore.Workforce.TimeOff",
        "CrestApps.OrchardCore.Workforce.LaborRules",
        "CrestApps.OrchardCore.Workforce.TimeClock",
        "CrestApps.OrchardCore.Workforce.Portal",
        "CrestApps.OrchardCore.Workforce.Api"
      ],
      "disable": []
    }
  ]
}
```

### Integrations Lit Up by Other Features

| Feature on | What it adds |
|---|---|
| `CrestApps.OrchardCore.TimeTracker` | Time Off keeps approved time off as Time Tracker entries (the Time Clock always does for punches). |
| `CrestApps.OrchardCore.Geo` | A team's time clock geofence point is geocoded from the team's address (`orchardcore-geo`); Staff geocodes home bases. |
| `CrestApps.OrchardCore.AI` | The Workforce AI tools. |
| `OrchardCore.Workflows` | The workforce events. |
| `OrchardCore.DataPipelines.Core` | The **Workforce** report data source (`orchardcore-report-builder`), and the payroll CSV export as a data pipeline over Payroll's **Pay inputs** data set. |
| `OrchardCore.Email` | The email notification channel of My Work. |
| `CrestApps.OrchardCore.OrTools.ShiftScheduling` | A CP-SAT shift solver that keeps the better of its answer and the built-in one (`orchardcore-or-tools`). |

## Workers and Teams

A worker is a `StaffMember` (Staff, `Staff` collection; see `orchardcore-staff`): the person, employment, manager, dated `TeamMemberships`, time zone, home base and contracted hours belong to Staff. Workforce keeps its scheduling values on the staff member in `WorkforceStaffSettings` (`MinHoursPerWeek`, `MaxHoursPerWeek`, `MaxHoursPerDay`, `Availability` windows of `Day`, `Start`, `End`, `Preference` `Available`/`Preferred`/`Unavailable`), read and written with the C# extension properties `staff.MinHoursPerWeek`, `staff.MaxHoursPerWeek`, `staff.MaxHoursPerDay` and `staff.Availability` (`GetWorkforceSettings()` for the whole object). Workforce's own catalog handler refuses fewest hours above most hours. Other Workforce records stay in the `Workforce` collection; the time-off ledger has its own (`WorkforceTimeOffLedger`, a shared quantity ledger).

- **Editing workers.** Workers are listed and edited under **Staff > All Staff**. Workforce adds a **Scheduling** card (`WorkforceStaffFields_Edit` at `Content:5`, `WorkforceStaffDisplayDriver`) to the staff editor and a **Work** section (shift rules, time clock, timesheets, time off) to the staff profile. The user editor's **Staff** card (from Staff) replaces the old Workforce card.
- **Managers.** Workforce registers `AddStaffScopePermissions(ManageAllWorkforce, ManageWorkers)`, so Workforce managers see and edit the staff of their teams in Staff.
- **Leaving.** `WorkforceStaffLifecycleObserver` removes a terminated person's published shifts from the day after their last day, so planners see the gaps.
- **Team assignment.** `WorkforceShiftAvailabilitySource` (an `ITeamMemberAvailabilitySource` of Teams) reports members who work to a schedule but are off shift or on a break right now.
- **Scope.** `IWorkforceScopeService.GetManagedScopeAsync(user)` is everything for `ManageAllWorkforce`, otherwise the teams where the user's staff member is a `Manager` or `Scheduler` today, with sub-teams. Lists, approvals, reports and AI tools are limited to it.
- **Team settings.** `WorkforceTeamSettings` lives on the shared `Team` (`team.GetWorkforceSettings()`, `team.AlterWorkforceSettings(...)`) and is edited on the team's **Workforce** card: `Location` (a `WorkforceLocation` with only `Latitude` and `Longitude`, the geofence point; the address is the team's own `Team.Address`), `CalendarId`, `LeadWorkerId` (crew lead), `OpenShiftClaimPolicy` (`ManagerApproves`, `FirstCome`, `BySeniority`), `ShiftTradesNeedApproval`, `TimeOffShiftHandling` (`Flag`, `Remove`) and `TimeClockPolicyId`; each `null` uses the parent team's. A **Members** card lists the team's workers today.
- **Calendar.** Each team gets a calendar of type `Workforce` (`Source` `Workforce`, `SourceId` the team id) that follows its name, time zone and enabled flag. Event types: `Shift` (free), `TimeOff` (busy), `Training`, `Meeting` (busy), `OnCall` (free). Breaks, meals, meetings and training inside a shift are busy (`ShiftSegmentBusyTimeSource`). Managers in scope get **Manage**, members and the linked role **Read** (`WorkforceCalendarAccessContributor`).
- **Deletion.** A team with current workers can't be deleted (`WorkforceTeamDeletionGuard`).
- **Activity codes.** `ActivityCode` (`Name`, `Code`, `Kind` `Work`/`Break`/`Meal`/`Meeting`/`Training`/`Coaching`/`OfflineWork`/`Travel`/`OnCall`/`TimeOff`/`Other`, `Color`, `IsPaid`, `CountsAsWork`, `CountsTowardCoverage`, `Enabled`, `StageId`). The usual nine are seeded.
- **Upgrade.** The Workforce migration moves every `WorkerProfile` into Staff with the same id (`WorkforceWorkersMove`) and each team's one-line `Location.Address` onto `Team.Address` (`WorkforceTeamAddressMove`). Old pay periods and pay codes move to Pay Setup (`TimeClockPaySetupMove`), and the time-off ledger is copied to its new collection at start-up (`TimeOffLedgerCopy`, under a distributed lock; accrual waits until the copy completes).

## Feature Highlights

| Feature | What it does |
|---|---|
| Shift Planning | Shift templates with segments; rotations of 1 to 12 weeks; coverage requirements by weekday or date (optionally per skill); schedule periods of 1 to 12 weeks planned as draft scenarios on a planner board; **Auto-schedule** (`IShiftScheduleSolver`, built-in `Default` solver, 30 second limit); publishing writes `Shift` events, a republish changes only what changed and logs each `ScheduleChange`; shifts with nobody become `OpenShift`s claimed by manager approval, first come or seniority; shift trades (give away, swap) with optional approval; a published shift can be reassigned, moved or removed on its own (`IShiftService`). |
| Time Off | Types (hours or days, paid, balance, part days, reason, allotments, pay code, Time Tracker stage), policies (`None`, `PerPeriod`, `PerHourWorked`, `AnnualLumpSum`, tenure tiers, waiting period, cap, carryover, negative limit), an append-only ledger, **grants with a last day** (`Grant` entries with `ExpiresOn`, used first, soonest-ending first, the rest expiring the day after), requests checked against blackout dates and balances, allotments that auto-approve and waitlist the rest, and approved time off on the team calendar. |
| Labor Rules | Rule sets of catalog rules (`MaxHoursPerDay`, `MaxHoursPerWeek`, `MinHoursPerWeek`, `MinRest`, `MaxConsecutiveDays`, `DaysOffPerWeek`, `MaxShiftLength`, `MinShiftLength`, `Breaks`, `MinorLimits`, `SkillsRequired`, `NoWorkDuringTimeOff`, `ScheduleNotice`, `Overtime`, `PredictabilityPay`), each `Warn`, `Block` or `RequireOverride`, for teams, workers and employment types; checked before publishing and on claims and trades; overrides recorded with a reason; late employer changes record predictability pay. No legal presets ship. |
| Time Clock | Punches from My Work, kiosks (`workforce/kiosk/{token}`, employee number and PIN), the contact center and the API, once per idempotency key; policies with rounding, grace, early clock-in limits, geofences, automatic meals, daily, weekly and seventh-day overtime, holiday and weekend pay codes and attendance points; pay schedules and pay codes from Payroll's Pay Setup (a code's shift differential window lives in `WorkforcePayCodeSettings`: `WindowStart`, `WindowEnd`, `Weekdays`); timesheets that workers submit and managers approve and lock; punch corrections; locked timesheets reach payroll as pay inputs (`TimeClockPayInputSource`), and the CSV export is a data pipeline. PINs are Staff Badges (`StaffBadge`). Each worker's day is mirrored into Time Tracker. |
| My Work Portal | Admin pages under `Admin/my-work` (schedule, shifts, trades, time off, availability, timesheet, inbox, settings; approvals and team for managers) in the admin theme. People with a staff record that isn't terminated and `UseWorkforcePortal` get into the admin without an admin role (`WorkerAdminAccessAuthorizationHandler`). Old `/my-work/{**path}` addresses redirect. Shift reminders, quiet hours, a private calendar feed (`my-work/feed/{token}.ics`) and web push (VAPID). |
| Workforce API | Minimal APIs at `api/workforce/v1` with the Api authentication scheme. |

Full details of time off, the time clock and the portal are in [references/workforce-time-and-portal.md](references/workforce-time-and-portal.md).

## Admin UI

| Menu | Routes (all under `Admin/`) | Permission |
|---|---|---|
| **Staff > All Staff** (workers are staff members; Scheduling card) | Staff module | `ViewWorkers`/`ManageWorkers` through `AddStaffScopePermissions`, or Staff's own permissions |
| A worker's time clock page and PIN | `workforce/workers/{workerId}/time-clock`, `workforce/workers/{workerId}/time-clock/pin` | `ApproveTimesheets`; rates need `ManageStaffPay` or `ExportPayrollData` |
| **Workforce > Activity Codes** | `workforce/activity-codes` | `ManageActivityCodes` |
| Get started checklist | `workforce/get-started` | `ManageWorkforceTeams` |
| **Content > Teams** (Workforce card) | `teams/edit/{id}` | `ManageTeams` |
| **Calendars > Shift Schedules**, **Rotations**, **Coverage** | `workforce/schedules`, `workforce/schedules/plan/{id}`, `workforce/rotations`, `workforce/coverage` | `ManageWorkforceSchedules` |
| **Calendars > Open Shifts** | `workforce/open-shifts` | `ManageWorkforceOpenShifts` |
| **Calendars > Shift Trades** | `workforce/shift-trades` | `ApproveWorkforceShiftTrades` |
| **Calendars > Shift Templates** | `workforce/shift-templates` | `ManageShiftTemplates` |
| **Workforce > Time Management > Time-Off Requests** | `workforce/time-off` | `ApproveTimeOff` |
| **Workforce > Time Management > Time-Off Types**, **Policies**, **Allotments**, **Blackout Dates** | `workforce/time-off-types`, `workforce/time-off-policies`, `workforce/time-off-allotments`, `workforce/blackout-dates` | `ManageTimeOffPolicies` |
| **Workforce > Time Management > Timesheets**, **Attendance** | `workforce/timesheets`, `workforce/attendance` | `ApproveTimesheets` |
| **Workforce > Time Management > Past Payroll Exports** (files the old export wrote, to download) | `workforce/payroll-export` | `ExportPayrollData` (Payroll) |
| **Workforce > Time Management > Time Clock Policies**, **Time Clock Kiosks** | `workforce/time-clock-policies`, `workforce/time-clock-kiosks` | `ManageTimeClockPolicies` |
| Pay schedules and pay codes (Payroll's Pay Setup pages, see `orchardcore-payroll`) | Payroll module | `ManagePaySetup` |
| **Workforce > Time Management > Predictability Pay** | `workforce/predictability-pay` | `ApprovePredictabilityPay` |
| **Workforce > Labor Rules**, **Rule Overrides** | `workforce/labor-rules`, `workforce/rule-overrides` | `ManageLaborRules` |
| **My Work** | `my-work`, `my-work/schedule`, `my-work/time-off`, `my-work/timesheet`, `my-work/approvals`, ... | `UseWorkforcePortal` |

A team's Workforce calendar shows **Shift schedules** in its actions on Manage Calendars.

## Permissions

| Permission | Display name | Default role |
|---|---|---|
| `ManageAllWorkforce` | Manage the whole workforce (implies all below except the self-service ones) | Administrator |
| `ManageWorkforceTeams` | Manage workforce teams (grants `ManageTeams`) | Administrator |
| `ManageWorkers` / `ViewWorkers` | Manage workers / View workers (implied by Manage workers); also decide whom a person manages in Staff | Administrator |
| `ViewSensitiveWorkerData` | View sensitive worker details | Administrator |
| `ManageActivityCodes` | Manage activity codes | Administrator |
| `ManageShiftTemplates`, `PublishWorkforceSchedules`, `ManageWorkforceSchedules` (implied by publishing), `ManageWorkforceOpenShifts`, `ApproveWorkforceShiftTrades` | Shift Planning | Administrator |
| `ManageTimeOffPolicies`, `ApproveTimeOff`, `AdjustTimeOffBalances` | Time Off | Administrator |
| `ManageLaborRules`, `OverrideLaborRules`, `ApprovePredictabilityPay` | Labor Rules | Administrator |
| `ManageTimeClockPolicies`, `ApproveTimesheets`, `EditPunches`, `LockTimesheets` | Time Clock (pay schedules and pay codes need Payroll's `ManagePaySetup`) | Administrator |
| `PunchTime` | Clock in and out (Time Clock) | Administrator, Authenticated |
| `RequestTimeOff` | Request time off (Time Off; not implied by Manage the whole workforce) | Administrator, Authenticated |
| `UseWorkforcePortal` | Use the My Work portal (Portal) | Administrator, Authenticated |

`ExportPayroll` and `ViewPayRates` were removed: `LockTimesheets` (Lock timesheets for payroll) locks periods, and exporting, past exports and rates need Payroll's `ExportPayrollData` (or `ManageStaffPay` for rates). Upgrades grant `LockTimesheets` and `ExportPayrollData` to roles that had `ExportPayroll`, `ExportPayrollData` to roles that had `ViewPayRates`, and `ManagePaySetup` to roles with `ManageTimeClockPolicies`.

`ManageWorkforceForecasts`, `ManageWorkforceAdherence` and `ViewWorkforceAdherence` are declared in `WorkforcePermissions` but registered by the Workforce for Contact Center features.

## Recipes and Deployment

| Step | Feature | Sections |
|---|---|---|
| `Workforce` | Workforce | `ActivityCodes`, `Teams` (team fields including `address`, plus Workforce settings, parent by `parentName`), `Workers` (saved as staff members, matched by id, employee number or user; memberships by `teamName`) |
| `WorkforceShiftPlanning` | Shift Planning | `Templates` (`teamNames`, segment `activityName`), `Patterns` (`teamName`, day `templateName`), `CoverageRequirements` (`teamName`, `skillName`) |
| `WorkforceTimeOff` | Time Off | `Types`, `Policies` (`typeName`, `teamNames`), `Allotments` and `BlackoutPeriods` (`teamName`, `typeNames`) |
| `WorkforceLaborRules` | Labor Rules | `RuleSets` (`teamNames`, `rules` with `ruleKey`, `enforcement`, `parameters`) |
| `WorkforceTimeClock` | Time Clock | `Policies` (`payPeriodsName`, `teamNames`), `Kiosks` (`teamName`); legacy `PayPeriods` and `PayCodes` (saved as Pay Setup's pay schedules and codes) and `PayrollCsv` (saved as the draft data pipeline `workforcepayrollcsv0000001`) |

```json
{
  "steps": [
    {
      "name": "Workforce",
      "ActivityCodes": [
        { "name": "Break", "code": "BRK", "kind": "Break", "isPaid": true, "countsTowardCoverage": false }
      ],
      "Teams": [
        { "name": "North Region", "kind": "Region", "timeZoneId": "America/Chicago" },
        { "name": "Front Desk", "kind": "Team", "parentName": "North Region", "openShiftClaimPolicy": "FirstCome", "address": { "addressLine1": "100 River Road", "city": "Springfield", "country": "US" } }
      ],
      "Workers": [
        { "displayName": "Jordan Lee", "employeeNumber": "1002", "jobTitle": "Receptionist", "employmentType": "FullTime", "contractedHoursPerWeek": 40, "teamMemberships": [ { "teamName": "Front Desk", "role": "Member" } ] }
      ]
    }
  ]
}
```

- Properties are camelCase, enums are names, records are matched by id or name so a rerun updates them; a missing name is reported and that record skipped.
- `Workers` entries are read as `StaffMember`: use Staff's field names (`managerId`, not `managerWorkerId`). Scheduling hours and availability aren't `StaffMember` properties, so set them on the staff editor's Scheduling card. New recipes create people with the `Staff` step (`orchardcore-staff`).
- New recipes put pay schedules and pay codes in Payroll's `PaySchedules` and `PayCodes` steps; a pay code's shift differential window goes in its `WorkforcePayCodeSettings`.
- Deployment steps (category **Workforce**): **Workforce teams and workers** (step `Workforce`, `IncludeWorkers`, never the date of birth) and **Workforce settings** (writes the settings steps of the features that are on; no longer `PayrollCsv`, which travels with the data pipelines). Kiosk tokens are never exported; after importing a kiosk, open it and choose **Give it a new address**.
- The **Workforce sample** recipe (`WorkforceSample`) sets up a region, a clinic, two teams, six workers, templates, time off, a rule set and a time clock. Every step with full JSON is in [references/workforce-recipes.md](references/workforce-recipes.md).

## Workflow Events

Category **Workforce**; each can be limited to some teams. Input always has `WorkerId`, `TeamId` and the record ids.

| Activity | Display name |
|---|---|
| `WorkforceTimeOffRequestedEvent`, `WorkforceTimeOffApprovedEvent`, `WorkforceTimeOffDeclinedEvent`, `WorkforceTimeOffCanceledEvent`, `WorkforceTimeOffWaitlistPromotedEvent` | Time Off Requested, Approved, Declined, Canceled, Waitlist Promoted |
| `WorkforceSchedulePublishedEvent`, `WorkforceOpenShiftPostedEvent` | Schedule Published, Open Shift Posted |
| `WorkforceShiftAssignedEvent`, `WorkforceShiftChangedEvent`, `WorkforceShiftRemovedEvent` | Shift Assigned, Shift Changed, Shift Removed (every change to a published schedule) |
| `WorkforcePunchedInEvent`, `WorkforcePunchedOutEvent` | Punched In, Punched Out |
| `WorkforceAttendanceExceptionRaisedEvent`, `WorkforceAttendancePointsThresholdReachedEvent` | Attendance Exception Raised, Attendance Points Threshold Reached |
| `WorkforceTimesheetSubmittedEvent`, `WorkforceTimesheetApprovedEvent` | Timesheet Submitted, Timesheet Approved |

Services raise them through `IWorkforceEvents.RaiseAsync(name, input, correlationId)`; replace it to send events elsewhere.

## API, AI Tools and Reports

- **API** (`api/workforce/v1`): `GET me`, `GET me/schedule?from=&days=`, `GET me/balances`, `GET|POST me/time-off`, `GET me/clock`, `POST me/punch`, `GET me/timesheet?date=`; for managed teams `GET teams`, `GET workers?teamId=`, `GET shifts?teamId=&from=&to=` (up to 62 days), `GET time-off/requests`, `POST time-off/requests/{id}/approve|decline`, `POST punches` (needs `EditPunches`), `GET timesheets`, `POST timesheets/{id}/approve`. Punches are recorded once per `Idempotency-Key` header; refusals answer 400.
- **AI tools** (with `CrestApps.OrchardCore.AI`): `workforceWhoIsWorking`, `workforceMySchedule`, `workforceTimeOffBalance`, `workforceRequestTimeOff`, `workforceFindCover`, `workforceExplainViolations`, each running as the signed-in user with the admin's checks. See `orchardcore-ai-tools`.
- **Report data source** `Workforce`: `WorkforceWorkers`, `WorkforceScheduleChanges`, `WorkforcePunches`, `WorkforceTimesheetLines`, `WorkforceAttendanceExceptions`, `WorkforceTimeOffRequests`, `WorkforcePredictabilityPay`, rows limited to the reader's teams.

## Extension Points

| Seam | Use it to |
|---|---|
| `DisplayDriver<StaffMember>` | Add a card to the staff editor where workers are edited (Staff's fields `StaffMemberFields_Edit` at `Content:1`, Workforce's `WorkforceStaffFields_Edit` at `Content:5`), or to the profile's **Work** section (`StaffProfileSections.Work`). See `orchardcore-staff`. |
| `IShiftService.PlaceSegmentAsync(ShiftSegmentPlacement)`, `RemoveSegmentsAsync` | Put your own time (such as a class as training) on a worker's published shifts with a `Source` and `SourceId`; the draft follows and a `SegmentsChanged` schedule change is logged. Fails when no published shift covers the time. |
| `ILaborRule` + `services.AddLaborRule<T>()` | Add a rule to the labor rule catalog. |
| `IShiftScheduleSolver` | Replace the auto-scheduler (`TryAddEnumerable`, pick it with `ShiftPlanningOptions.DefaultSolver`). |
| `ISchedulePublishHandler`, `ISchedulePublishValidator`, `IScheduleChangeHandler`, `IShiftAssignmentValidator` | React to or check publishes, schedule changes and shift claims. |
| `ITimeOffApprovalHandler`, `ITimeOffRequestObserver`, `IWorkedTimeSource` | React to time off, or say how much a worker worked for per-hour accrual. |
| `ITimePunchSourceProvider`, `IAttendancePointsHandler`, `ITimePunchObserver` | Punch sources, attendance thresholds, punch changes. Payroll formats are data pipelines over Payroll's pay inputs (`IPayrollExportProvider` and `IPayrollExportService` were removed). |
| `IWorkforceNotificationChannel`, `IMyWorkLinkProvider` | Send worker notices another way (SMS); add pages to the My Work menu. |
| `IWorkforceReportDataSet` | Add a data set to the Workforce report data source. |

### Add a Labor Rule

```csharp
public sealed class LatestFinishRule : ILaborRule
{
    public string Key => "LatestFinish";

    public string DisplayName => "Latest finish";

    public string Description => "A shift may not end later than a number of hours after midnight UTC.";

    public IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            new LaborRuleParameter("Hours", "Hours after midnight", LaborRuleParameterKind.Hours, "23"),
        ];

    public Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var limit = instance.Number("Hours", 23);

        foreach (var shift in context.Candidates)
        {
            if ((decimal)(shift.EndUtc - shift.EndUtc.Date).TotalHours > limit)
            {
                context.Report(this, instance, $"{context.Worker.DisplayName} finishes too late on {shift.Date}.", shift.Date, [shift.ShiftKey]);
            }
        }

        return Task.CompletedTask;
    }
}

// Startup: services.AddLaborRule<LatestFinishRule>();
```

### Add a Card Where Workers Are Edited

Workers are edited in the staff editor, so a card is a `DisplayDriver<StaffMember>`. Bind your own view model with the driver's `Prefix` and report errors under `$"{Prefix}.Field"`; the staff member's own validation runs after every card, and when anything fails nothing is saved.

```csharp
public sealed class WorkerUniformDisplayDriver : DisplayDriver<StaffMember>
{
    public override IDisplayResult Edit(StaffMember staff, BuildEditorContext context)
    {
        return Initialize<WorkerUniformViewModel>("WorkerUniform_Edit", model =>
        {
            model.Size = staff.TryGet<WorkerUniform>(out var uniform) ? uniform.Size : null;
        }).Location("Content:20");
    }

    public override async Task<IDisplayResult> UpdateAsync(StaffMember staff, UpdateEditorContext context)
    {
        var model = new WorkerUniformViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Size?.Length > 10)
        {
            context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.Size)}", "Use a short size code.");
        }

        staff.Alter<WorkerUniform>(uniform => uniform.Size = model.Size);

        return Edit(staff, context);
    }
}

// Startup: services.AddDisplayDriver<StaffMember, WorkerUniformDisplayDriver>();
```

More examples (notification channel, publish handler, segment placement, solver, My Work page, report data set) are in [references/workforce-extensions.md](references/workforce-extensions.md).

## Options

`ShiftPlanningOptions` is registered with `AddOptions` and not bound to configuration; change it in code with `services.Configure<ShiftPlanningOptions>(...)`: `MinRestHours` (10), `MaxConsecutiveDays` (6), `DefaultMaxHoursPerWeek` (40), `SolverTimeLimitSeconds` (30), `DefaultSolver` (`Default`), `SeniorityClaimHours` (24). Labor rule sets override rest and hour limits per worker (`IWorkerScheduleLimitsProvider`). `MyWorkOfflineOptions.Pages` lists My Work pages the service worker keeps for offline use.

## Gotchas

| Symptom | Check |
|---|---|
| Shifts missing from calendars, Scheduling or Dispatch | The schedule is still a draft; publish it. |
| Publish refused | A `Block` labor rule, or a `RequireOverride` one without a reason and `OverrideLaborRules`. |
| Auto-schedule adds nothing | No coverage requirements or templates for the team, or workers are unavailable, lack the skills or hit their hour limits. |
| Worker can't open My Work | Needs a staff member linked to the user (not terminated), the Portal feature, and `UseWorkforcePortal`. |
| No Workers menu | Workers are staff members: use **Staff > All Staff**. |
| Hours or availability from a `Workers` recipe entry are lost | They are `WorkforceStaffSettings`, not `StaffMember` fields; set them on the Scheduling card. |
| Accruals don't run after an upgrade | The time-off ledger copy hasn't finished; watch the log for *The time-off ledger copy is complete*. |
| Punch refused | No time clock policy on the team (or a parent), the source isn't allowed, too early before the shift, or the timesheet is locked. |
| Time off refused | Blackout dates, the balance below the negative limit, an overlapping request, a missing reason, or a full allotment set to `Pending`. |
| Team can't be deleted | It still has current workers. |
| Kiosk link dead after import | Tokens aren't exported; give the kiosk a new address. |

Related skills: `orchardcore-staff`, `orchardcore-teams`, `orchardcore-skills`, `orchardcore-time-tracker`, `orchardcore-workforce-contact-center`, `orchardcore-calendars`, `orchardcore-scheduling`, `orchardcore-dispatch`, `orchardcore-or-tools`, `orchardcore-geo`, `orchardcore-report-builder`, `orchardcore-crestapps-users`, `orchardcore-payroll`.

The time clock's pay schedules and pay codes belong to Payroll's **Pay Setup** feature. Locked timesheets (`TimeClockPayInputSource`) and time-off payouts (`TimeOffPayoutPayInputSource`, source kind `TimeOffPayout`) reach pay runs as pay inputs (`IPayInputSource`), and paid time-off balances appear on pay statements (`TimeOffPayStatementBalanceSource`). A pay run or pipeline that takes a timesheet records it in `ExportBatchIds`. See `orchardcore-payroll`.

**Telemetry.** Workforce traces and measures under the `CrestApps.OrchardCore.Workforce` meter and activity source (`WorkforceTelemetry`: notifications per channel, payroll exports, adherence alerts); see `orchardcore-observability`.

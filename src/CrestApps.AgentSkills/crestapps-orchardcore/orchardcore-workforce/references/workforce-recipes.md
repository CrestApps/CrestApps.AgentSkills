# Workforce Recipe Examples

Every Workforce step reads camelCase property names (case-insensitive), writes enums as their names, matches records by id or name (pay codes by `code`) so a second run updates them, and names the records it points at (`teamName`, `teamNames`, `typeName`, `templateName`, `activityName`, `skillName`, `payPeriodsName`). A name that doesn't exist is reported as a recipe error and that record is skipped; the others are still saved. Every record goes through its manager, so it gets the admin editor's validation.

Run the steps in this order: `Workforce` (activity codes, teams, workers), then `WorkforceShiftPlanning`, `WorkforceTimeOff`, `WorkforceLaborRules` and `WorkforceTimeClock`. Each settings step only runs when its feature is enabled.

## The Workforce Sample

This is the module's `WorkforceSample` recipe (**Workforce sample**), trimmed of its header. It is a good starting point to copy.

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
        "CrestApps.OrchardCore.Workforce.Portal"
      ],
      "disable": []
    },
    {
      "name": "Workforce",
      "Teams": [
        { "name": "North Region", "kind": "Region", "timeZoneId": "America/Chicago" },
        { "name": "Riverside Clinic", "kind": "Site", "parentName": "North Region", "location": { "address": "100 River Road" } },
        { "name": "Front Desk", "kind": "Team", "parentName": "Riverside Clinic", "description": "Check-in, phones and scheduling." },
        { "name": "Nursing", "kind": "Team", "parentName": "Riverside Clinic", "description": "Clinic nurses." }
      ],
      "Workers": [
        { "displayName": "Avery Brooks", "employeeNumber": "1001", "jobTitle": "Clinic manager", "employmentType": "FullTime", "teamMemberships": [ { "teamName": "Riverside Clinic", "role": "Manager" } ] },
        { "displayName": "Jordan Lee", "employeeNumber": "1002", "jobTitle": "Receptionist", "employmentType": "FullTime", "contractedHoursPerWeek": 40, "teamMemberships": [ { "teamName": "Front Desk" } ] },
        { "displayName": "Sam Rivera", "employeeNumber": "1003", "jobTitle": "Receptionist", "employmentType": "PartTime", "contractedHoursPerWeek": 24, "teamMemberships": [ { "teamName": "Front Desk" } ] },
        { "displayName": "Casey Morgan", "employeeNumber": "1004", "jobTitle": "Nurse", "employmentType": "FullTime", "contractedHoursPerWeek": 40, "teamMemberships": [ { "teamName": "Nursing" } ] }
      ]
    },
    {
      "name": "WorkforceShiftPlanning",
      "Templates": [
        {
          "name": "Early",
          "color": "#2b8a3e",
          "start": "07:00:00",
          "lengthMinutes": 510,
          "teamNames": [ "Front Desk", "Nursing" ],
          "segments": [
            { "activityName": "Break", "offsetMinutes": 120, "lengthMinutes": 15 },
            { "activityName": "Meal", "offsetMinutes": 270, "lengthMinutes": 30 }
          ]
        },
        {
          "name": "Late",
          "color": "#1971c2",
          "start": "12:00:00",
          "lengthMinutes": 510,
          "teamNames": [ "Front Desk", "Nursing" ],
          "segments": [
            { "activityName": "Meal", "offsetMinutes": 240, "lengthMinutes": 30 },
            { "activityName": "Break", "offsetMinutes": 390, "lengthMinutes": 15 }
          ]
        }
      ]
    },
    {
      "name": "WorkforceTimeOff",
      "Types": [
        { "name": "Vacation", "description": "Planned time away.", "color": "#e8590c", "unit": "Hours", "hoursPerDay": 8, "payCode": "PTO" },
        { "name": "Sick time", "description": "When you or someone you care for is unwell.", "color": "#c2255c", "unit": "Hours", "hoursPerDay": 8, "payCode": "PTO", "countsAgainstAllotment": false }
      ],
      "Policies": [
        { "name": "Vacation", "typeName": "Vacation", "accrualMethod": "PerPeriod", "frequency": "Monthly", "rate": 8, "maxBalance": 160, "carryoverMax": 40, "teamNames": [ "Riverside Clinic" ] },
        { "name": "Sick time", "typeName": "Sick time", "accrualMethod": "PerHourWorked", "rate": 0.0333, "maxBalance": 48, "teamNames": [ "Riverside Clinic" ] }
      ],
      "Allotments": [
        { "name": "Nursing vacation", "teamName": "Nursing", "typeNames": [ "Vacation" ], "weekdaySlots": [ 1, 1, 1, 1, 1, 1, 1 ] }
      ]
    },
    {
      "name": "WorkforceLaborRules",
      "RuleSets": [
        {
          "name": "Clinic hourly staff",
          "description": "An example. Check the rules and numbers that apply where your teams work.",
          "teamNames": [ "Riverside Clinic" ],
          "rules": [
            { "ruleKey": "MaxHoursPerWeek", "enforcement": "Warn", "parameters": { "Hours": "40" } },
            { "ruleKey": "MinRest", "enforcement": "Block", "parameters": { "Hours": "10" } },
            { "ruleKey": "Breaks", "enforcement": "Warn", "parameters": { "ShiftHours": "6", "BreakMinutes": "30" } }
          ]
        }
      ]
    },
    {
      "name": "WorkforceTimeClock",
      "PayPeriods": [
        { "name": "Every two weeks", "frequency": "Biweekly", "anchorDate": "2026-01-04", "weekStart": "Sunday" }
      ],
      "PayCodes": [
        { "code": "NIGHT", "name": "Night premium", "kind": "ShiftDifferential", "windowStart": "22:00:00", "windowEnd": "06:00:00", "premiumPerHour": 2 }
      ],
      "Policies": [
        {
          "name": "Clinic hourly staff",
          "payPeriodsName": "Every two weeks",
          "roundingMinutes": 5,
          "roundingDirection": "Nearest",
          "earlyGraceMinutes": 5,
          "lateGraceMinutes": 5,
          "autoMealAfterHours": 6,
          "autoMealMinutes": 30,
          "weeklyOvertimeHours": 40,
          "attendancePoints": [ { "kind": "NoShow", "points": 2 }, { "kind": "Late", "points": 0.5 }, { "kind": "MissedPunch", "points": 0.5 } ],
          "pointsWindowDays": 180,
          "pointsThreshold": 6,
          "teamNames": [ "Riverside Clinic" ]
        }
      ]
    }
  ]
}
```

## Workforce Step Details

- `ActivityCodes`: `name`, `code`, `kind` (`Work`, `Break`, `Meal`, `Meeting`, `Training`, `Coaching`, `OfflineWork`, `Travel`, `OnCall`, `TimeOff`, `Other`), `color`, `isPaid`, `countsAsWork`, `countsTowardCoverage`, `enabled`.
- `Teams`: the Teams fields (`name`, `kind`, `parentName`, `description`, `linkedRoleName`, `timeZoneId`, `enabled`) plus the Workforce settings `location` (`address`, `latitude`, `longitude`), `leadWorkerId`, `openShiftClaimPolicy` (`ManagerApproves`, `FirstCome`, `BySeniority`), `shiftTradesNeedApproval`, `timeOffShiftHandling` (`Flag`, `Remove`) and `timeClockPolicyId`. The calendar is never imported; each new team gets its own.
- `Workers`: the `WorkerProfile` fields in camelCase (`displayName`, `email`, `phone`, `employeeNumber`, `externalId`, `jobTitle`, `employmentType`, `status`, `hireDate`, `seniorityDate`, `timeZoneId`, `contractedHoursPerWeek`, `minHoursPerWeek`, `maxHoursPerWeek`, `maxHoursPerDay`, `availability`, `teamMemberships` with `teamName` and `role`). Matched by id, then `employeeNumber`, then `userId`.

```json
{
  "steps": [
    {
      "name": "Workforce",
      "Workers": [
        {
          "displayName": "Riley Chen",
          "employeeNumber": "1005",
          "email": "riley@example.com",
          "employmentType": "PartTime",
          "hireDate": "2025-04-01",
          "maxHoursPerWeek": 30,
          "availability": [
            { "day": "Monday", "start": "08:00:00", "end": "16:00:00", "preference": "Preferred" },
            { "day": "Friday", "start": "00:00:00", "end": "23:59:00", "preference": "Unavailable" }
          ],
          "teamMemberships": [ { "teamName": "Nursing", "role": "Member", "effectiveFrom": "2025-04-01" } ]
        }
      ]
    }
  ]
}
```

## Rotations and Coverage

```json
{
  "steps": [
    {
      "name": "WorkforceShiftPlanning",
      "Patterns": [
        {
          "name": "Two-week rotation",
          "teamName": "Front Desk",
          "weekCount": 2,
          "days": [
            { "week": 0, "day": "Monday", "templateName": "Early" },
            { "week": 0, "day": "Tuesday", "templateName": "Early" },
            { "week": 1, "day": "Monday", "templateName": "Late" }
          ]
        }
      ],
      "CoverageRequirements": [
        {
          "name": "Weekday mornings",
          "teamName": "Front Desk",
          "skillName": "Spanish",
          "rows": [
            { "day": "Monday", "start": "08:00:00", "end": "12:00:00", "minimum": 1, "target": 2 },
            { "day": "Tuesday", "start": "08:00:00", "end": "12:00:00", "minimum": 1, "target": 2, "maximum": 3 }
          ]
        }
      ]
    }
  ]
}
```

A rotation's `workers` hold worker ids and the date each started week 1, so they are site-specific; add workers to rotations in the admin after import. A coverage requirement is matched by id, or by team and name.

## Blackout Dates

```json
{
  "steps": [
    {
      "name": "WorkforceTimeOff",
      "BlackoutPeriods": [
        { "name": "Year-end close", "teamName": "Front Desk", "startDate": "2026-12-28", "endDate": "2026-12-31", "typeNames": [ "Vacation" ], "message": "Front desk is fully staffed for year-end." }
      ]
    }
  ]
}
```

Leave `teamName` out for every team, and `typeNames` out for every type. Allotments take `weekdaySlots` (Sunday first), `dates` (`date`, `slots`), `autoApprove` and `whenFull` (`Waitlist`, `Pending`).

## Time Clock Kiosks and Payroll Columns

```json
{
  "steps": [
    {
      "name": "WorkforceTimeClock",
      "PayCodes": [
        { "code": "HOL", "name": "Holiday", "kind": "Holiday", "multiplier": 1.5 }
      ],
      "Policies": [
        {
          "name": "Warehouse",
          "payPeriodsName": "Every two weeks",
          "allowedSources": [ "Web", "Mobile", "Kiosk" ],
          "requireGeofence": true,
          "geofenceRadiusMeters": 200,
          "blockEarlyPunchInMinutes": 15,
          "dailyOvertimeHours": 8,
          "weeklyOvertimeHours": 40,
          "seventhDayOvertime": true,
          "holidays": [ "2026-12-25", "2027-01-01" ],
          "weekendDays": [ "Saturday", "Sunday" ],
          "teamNames": [ "Warehouse" ]
        }
      ],
      "Kiosks": [
        { "name": "Warehouse door", "teamName": "Warehouse", "enabled": true }
      ],
      "PayrollCsv": {
        "columns": [
          { "header": "EmployeeId", "field": "WorkerExternalId" },
          { "header": "Name", "field": "WorkerName" },
          { "header": "Code", "field": "PayCode" },
          { "header": "Date", "field": "Date" },
          { "header": "Hours", "field": "Hours" }
        ]
      }
    }
  ]
}
```

- Pay code kinds: `Regular`, `Overtime`, `DoubleTime`, `Holiday`, `PaidTimeOff`, `PredictabilityPay`, `CallBack`, `ShiftDifferential`, `Unpaid`, `Weekend`.
- Payroll CSV fields: `WorkerExternalId`, `EmployeeNumber`, `WorkerName`, `PayCode`, `Date`, `Hours`, `Rate`, `Amount`, `PeriodStart`, `PeriodEnd`, `ActivityCode`, `Job`.
- Punch sources are open strings: `Web`, `Mobile`, `Kiosk`, `Api`, `Manager`, and `ContactCenter` while Contact Center Sign-in Punches is on.
- A policy with `teamNames` is set as those teams' `timeClockPolicyId`.
- Kiosk tokens are never exported or changed by the step; open an imported kiosk and choose **Give it a new address**.

## Deployment Steps

| Deployment step | Category | Writes |
|---|---|---|
| **Workforce teams and workers** (`Workforce`) | Workforce | Activity codes and teams, and workers when `IncludeWorkers` is checked (never the date of birth). |
| **Workforce settings** (`WorkforceSettings`) | Workforce | One step per enabled feature: `WorkforceShiftPlanning`, `WorkforceTimeOff`, `WorkforceTimeClock`, `WorkforceLaborRules`. |

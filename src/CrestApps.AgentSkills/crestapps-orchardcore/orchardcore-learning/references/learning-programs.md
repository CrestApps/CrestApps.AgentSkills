# Learning Programs: Paths, Required Learning and Team Learning

Supporting detail for `orchardcore-learning`. Module `CrestApps.OrchardCore.Learning.Programs` (contracts in `Learning.Programs.Abstractions` and `Learning.Programs.Core`).

| Feature | ID | Depends on |
|---|---|---|
| Learning Paths | `CrestApps.OrchardCore.Learning.Programs` | Learning |
| Required Learning | `CrestApps.OrchardCore.Learning.Programs.RequiredLearning` | Learning Paths, User Audiences (`CrestApps.OrchardCore.Users.Audiences`) |
| Team Learning | `CrestApps.OrchardCore.Learning.Programs.TeamLearning` | Required Learning, Staff (`CrestApps.OrchardCore.Staff`) |

## Learning Paths

A learning path is a `LearningPath` content item (`TitlePart`, `LearningPathPart`). There is no Learning menu entry: create one with **Content > New > Learning Path**; **People** in its content list actions opens `/Admin/learning/paths/{id}/people`.

| `LearningPathPart` | Meaning |
|---|---|
| `Sets` | Ordered `LearningPathCourseSet { Id, Title, Connector, Join, MinimumCount, CourseIds }` |
| `Connector` | `Then` starts a new stage; `And`, `Or` stay in the same stage (and binds tighter than or) |
| `Join` | `All`, `Any` or `AtLeast` (`MinimumCount`) |
| `AllowSelfStart` | Shows **Start this path** to signed-in people who can see it (`POST learning/paths/{id}/start`) |

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4safetypath0000000000path1",
          "ContentType": "LearningPath",
          "DisplayText": "Field safety",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Field safety" },
          "LearningPathPart": {
            "AllowSelfStart": true,
            "Sets": [
              { "Id": "basics", "Title": "Basics", "Connector": "Then", "Join": "All", "CourseIds": [ "<course id>" ] },
              { "Id": "electives", "Title": "Electives", "Connector": "Then", "Join": "AtLeast", "MinimumCount": 2, "CourseIds": [ "<course id>", "<course id>", "<course id>" ] }
            ]
          }
        }
      ]
    }
  ]
}
```

- `ILearningPathService.StartAsync(LearningPathStartRequest)` is idempotent per path and person; it enrolls in the open stage's courses with source `Learning.LearningPath`, and each completed stage enrolls the next. `EvaluateAsync`, `EvaluateCourseAsync`, `StopAsync`, `SupportsIssuingAsync`, `PageAsync`.
- Progress is a `LearningPathProgress` record in the `LearningPrograms` collection, saved under a lock per path and person before any enrollment.
- Unpublished courses are never enrolled and an **All** set does not wait for them, but credentials, credits and competencies are issued on a path only when its rules hold over all courses with evidence (`CompletionEvidence.SupportsIssuing`).
- Hear completions with `services.AddLearningPathObserver<T>()` (`ILearningPathObserver.CompletedAsync`).

## Required Learning

A `LearningRequirement` requires a target of an audience and named people.

| Property | Meaning |
|---|---|
| `TargetKind`, `TargetId` | `Course`, `LearningPath` or, with Learning Certifications, `CertificationPath` (`IRequiredLearningTarget`, register with `services.AddRequiredLearningTarget<T>()`) |
| `AudienceName`, `UserIds` | Everyone in the audience plus named user ids |
| `DueRule`, `DueDate`, `DueDays` | `None`, `FixedDate`, `DaysAfterRequired`, `DaysAfterHire` (staff hire date, else when required); due at the last second of the day in the site time zone |
| `ReminderDays` | Days before the due date; a late upkeep sends only the nearest |
| `EscalateAfterDays` | Days after due to tell the manager on the staff record |
| `Enabled` | Rule runs |

Each person gets a `RequiredLearningAssignment`: Required, In progress, Completed, Overdue, Exempt or No longer required. `IRequiredLearningService`: `RunAsync` (source `Learning.RequiredLearning`), `PreviewAsync`, `ExemptAsync`, `RemoveExemptionAsync`, `SendRemindersAsync`. Hear changes with `services.AddRequiredLearningObserver<T>()`. Exemption evidence files are in the protected area `RequiredLearningEvidence`.

The **Required learning upkeep** task (`15 * * * *`) runs each enabled rule not run for 20 hours and sends due reminders and escalations; **Run now** applies a rule at once.

```json
{
  "steps": [
    {
      "name": "LearningRequirements",
      "Requirements": [
        {
          "name": "Safety onboarding",
          "enabled": true,
          "targetKind": "LearningPath",
          "targetId": "4safetypath0000000000path1",
          "audienceName": "field-veterans",
          "userIds": [],
          "dueRule": "DaysAfterHire",
          "dueDays": 30,
          "reminderDays": [ 14, 7, 1 ],
          "escalateAfterDays": 3
        }
      ]
    }
  ]
}
```

Rules match by `itemId` or name (case ignored); enums are written as names. The **Required learning** deployment step (category **Learning**) exports rules without people's states.

## Team Learning

**Learning > Team Learning** (`/Admin/learning/team`, `ViewTeamLearning`) lists the people who report to the signed-in user on their staff records (`IStaffHierarchy.GetReportsAsync`) with the state and due date of their required learning, never answers or attempts.

## Pages and Permissions

| Page | Address | Permission |
|---|---|---|
| Required learning list, editor, people, preview, exempt | `/Admin/learning/required` | `ManageRequiredLearning`; exempting needs `ExemptFromRequiredLearning` |
| Team learning | `/Admin/learning/team` | `ViewTeamLearning` |
| People on a path | `/Admin/learning/paths/{id}/people` | Edit the path |
| My required learning and paths | `/learning/required` | Signed in |

| Permission | Display name | Implied by |
|---|---|---|
| `ManageRequiredLearning` | Manage required learning | |
| `ExemptFromRequiredLearning` | Exempt people from required learning | `ManageRequiredLearning` |
| `ViewTeamLearning` | See the required learning of the people who report to you | `ManageRequiredLearning` |

## Integrations

- **Notifications**: Learning required (`LearningRequired`), due soon (`LearningRequiredDueSoon`), overdue (`LearningRequiredOverdue`, also to the manager on escalation), Learning path completed (`LearningPathCompleted`).
- **Workflows**: events Learning Required, Required Learning Due Soon, Required Learning Overdue, Required Learning Exemption Granted, Learning Path Completed; tasks Require Learning, Grant Required Learning Exemption.
- **Data source** `LearningPrograms`: path progress, required learning, state per person.
- **Certifications**: with Learning Certifications on, a path with the Education Credits part awards credits, and the Competencies part grants skills, only on evidence.
- **Personal data**: path progress and required learning states with exemption reasons and evidence files are exported and deleted.

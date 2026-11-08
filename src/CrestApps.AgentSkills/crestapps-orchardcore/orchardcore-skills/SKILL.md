---
name: orchardcore-skills
description: Skill for the CrestApps Orchard Core Skills module (CrestApps.OrchardCore.Skills), the one skill catalog of a site. Covers skills held at a proficiency from 1 to 5 with effective dates, users and workforce workers as skill subjects, certifications that expire, certificates, Content > Skills and Certificates, the daily expiry check, ISkillAssignmentService, ICertificationRecordService, ISkillAssignmentHandler, ICertificationExpiryObserver, Skills sections in editors through SkillSubjectSkillsDisplayDriverBase, the Skills workflow events and Assign Skill task, the Skills recipe and deployment step, and how Contact Center shares the catalog. Use this skill when requests mention skills, proficiency, certifications, licences or certificates expiring. Strong matches include Skill, SkillAssignment, SkillSubject, Certification, CertificationRecord, ISkillManager, ISkillSubjectLinkProvider, AssignSkills and AddSkillsCore.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Skills

## One Skill Catalog for Routing and Scheduling

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Skills module. A person (a user, or a workforce worker who may have no account) holds skills at a proficiency from 1 (trainee) to 5 (expert), optionally between two dates. A skill can require a certification, and then it only counts on days the person holds a valid certificate. Contact Center routing, Workforce scheduling and Dispatch matching all read the same catalog.

### Guidelines

- Install `CrestApps.OrchardCore.Skills` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- Reference `CrestApps.OrchardCore.Skills.Abstractions` (models, contracts, `SkillsPermissions`) or `CrestApps.OrchardCore.Skills.Core` (stores, services, `AddSkillsCore()`, `SkillSubjectSkillsDisplayDriverBase<TModel>`) from your module.
- The feature depends on CrestApps Users and CrestApps Resources. Workforce and the Contact Center Agents feature depend on Skills, so they turn it on.
- Change skills only through `ISkillAssignmentService` and certificates through `ICertificationRecordService`; they validate, keep one assignment per person and skill, and call the handlers.
- Proficiency is an `int` checked with `SkillProficiency.IsValid` (`Minimum` 1, `Maximum` 5, `Default` 3).
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Feature | ID | Depends on |
|---|---|---|
| Skills | `CrestApps.OrchardCore.Skills` | `CrestApps.OrchardCore.Users`, `CrestApps.OrchardCore.Resources` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Skills"
      ],
      "disable": []
    }
  ]
}
```

## Records

All live in the `Skills` YesSql collection. Names are unique ignoring case and surrounding spaces.

| Record | Key properties |
|---|---|
| `Skill` | `Name`, `Category`, `Description`, `RequiredCertificationId`, `Enabled` |
| `Certification` (shown as **Certification Types**) | `Name`, `Issuer`, `Description`, `ValidityMonths`, `RequiresDocument`, `ReminderDays` (for example `[60, 30, 7]`), `Enabled` |
| `SkillAssignment` | `SubjectKind`, `SubjectId`, `SkillId`, `Proficiency`, `EffectiveFrom`, `EffectiveTo`, `AssignedBy`, `AssignedUtc` |
| `CertificationRecord` (a certificate) | `SubjectKind`, `SubjectId`, `SubjectName`, `CertificationId`, `Number`, `IssuedOn`, `ExpiresOn`, `DocumentPath` (media library), `Status` (`Pending`, `Valid`, `Expired`, `Revoked`), `VerifiedBy`, `VerifiedUtc`, `Notes` |

A person is a `SkillSubject(Kind, Id)`: `SkillSubject.User(userId)` or `SkillSubject.Worker(workerId)`. `SkillSubjectKind.User` and `SkillSubjectKind.Worker` are the well-known kinds; the kind is an open string, so `new SkillSubject("Volunteer", id)` works without changing Skills. Older records that stored the kind as a number are still read (`0` is `User`, `1` is `Worker`).

## Services

| Contract | Key members |
|---|---|
| `ISkillManager`, `ICertificationManager` | `ICatalogManager<T>` plus `FindByNameAsync`, `GetEnabledAsync`, `SearchAsync(search, skip, take)` |
| `ISkillAssignmentService` | `GetAssignmentsAsync(subject)`, `GetEffectiveAsync(subject, date)` (inside dates, skill enabled, required certificate valid that day, linked subjects included), `SetAsync`, `AssignAsync(subject, SkillAssignmentInput, actor)`, `RemoveAsync`, `FindHoldersAsync(skillId, minimumProficiency, date, kind)`, `HoldsAsync` |
| `ICertificationRecordService` | `SaveAsync` (fills `ExpiresOn` from `IssuedOn` and `ValidityMonths`; refuses a valid record without its copy when the certification `RequiresDocument`), `VerifyAsync`, `RevokeAsync(id, actor, reason)`, `DeleteAsync`, `GetForSubjectAsync`, `IsValidOnAsync`, `PageAsync` |

```csharp
var subject = SkillSubject.User(userId);

var result = await _assignmentService.AssignAsync(subject, new SkillAssignmentInput
{
    SkillId = spanish.ItemId,
    Proficiency = 4,
    EffectiveFrom = new DateOnly(2026, 12, 1),
}, actor: "hr-import");

if (!result.Succeeded)
{
    // result.Errors lists why.
}

var holders = await _assignmentService.FindHoldersAsync(spanish.ItemId, minimumProficiency: 3, today, SkillSubjectKind.Worker);
```

## Admin UI

| Menu | Routes | Permission |
|---|---|---|
| **Content > Skills** | `Admin/skills`, `Admin/skills/edit/{id?}`, `Admin/skills/delete/{id}` | `ManageSkills` |
| **Content > Certification Types** | `Admin/certifications`, `Admin/certifications/edit/{id?}`, `Admin/certifications/delete/{id}` | `ManageSkills` |
| **Content > Certificates** | `Admin/certificates` (filters include expiring in the next 60 days), `Admin/certificates/edit/{id?}`, `.../verify/{id}`, `.../revoke/{id}`, `.../delete/{id}` | `ViewSkills` to list; `AssignSkills` to add, verify, revoke |
| User editor **Skills** section | `UserSkills_Edit` at `Content:12` | `AssignSkills` |

## Permissions

| Permission | Display name | Implied by | Default role |
|---|---|---|---|
| `ManageSkills` | Manage skills and certifications | none | Administrator |
| `AssignSkills` | Assign skills and record certifications | `ManageSkills` | Administrator |
| `ViewSkills` | View skills and certifications | `AssignSkills` | Administrator |

With Contact Center on, a role with `ManageContactCenterSkills` (Manage Contact Center skills) may also manage and assign the shared skills (`ContactCenterSkillsAuthorizationHandler`).

## Daily Check

The **Skills and certificates daily check** background task (schedule `5 * * * *`) runs `SkillsDailyCheck` once per day in the site's time zone (`SkillsDailyCheckpoint` remembers the last day):

1. A valid certificate reaching a reminder offset raises one reminder (only the nearest of the offsets already reached), once per expiry date; renewing it (a new expiry date) starts the reminders again.
2. The day after `ExpiresOn`, `Status` becomes `Expired`.
3. People whose assignment starts today or ended yesterday are passed to every `ISkillAssignmentHandler` with `EffectiveDatesOnly = true`.

## Recipes and Deployment

The `Skills` step (needs `OrchardCore.Recipes.Core`) imports, in order, `Certifications` and `Skills` (by `itemId`, else by name), then `Assignments` and `CertificationRecords`. Records use camelCase property names and enum names. `RequiredCertificationName` (PascalCase, on a skill), `certificationName` (on a certificate) and `skillName` (on an assignment) name records so a recipe imports on another site.

```json
{
  "steps": [
    {
      "name": "Skills",
      "Certifications": [
        { "name": "Gas safety", "issuer": "Gas Board", "validityMonths": 12, "reminderDays": [ 60, 30, 7 ], "requiresDocument": true }
      ],
      "Skills": [
        { "name": "Spanish", "category": "Languages" },
        { "name": "Boiler servicing", "category": "Heating", "RequiredCertificationName": "Gas safety" }
      ],
      "Assignments": [
        { "subjectKind": "User", "subjectId": "4x2kf6wqmq7a9y3p0v1d8hn5bt", "skillName": "Spanish", "proficiency": 4, "effectiveFrom": "2026-12-01" }
      ],
      "CertificationRecords": [
        { "subjectKind": "User", "subjectId": "4x2kf6wqmq7a9y3p0v1d8hn5bt", "subjectName": "John Smith", "certificationName": "Gas safety", "number": "GS-1042", "issuedOn": "2026-03-15", "status": "Valid" }
      ]
    }
  ]
}
```

- An assignment needs a `subjectId` and a skill that exists (`skillId` or `skillName`); `proficiency` defaults to 3. Refusals are reported as recipe errors.
- The **Skills and certifications** deployment step (category **Workforce**) exports every certification and skill, and optionally `IncludeAssignments` and `IncludeCertificationRecords` (up to 50,000 certificates, without reminder bookkeeping).

## Workflow Activities

With `OrchardCore.Workflows` on (category **Skills**). Events start after the change is committed; certificate events can be limited to some certifications (`CertificationIds`).

| Activity | Display name | Input |
|---|---|---|
| `CertificationExpiringEvent` | Certificate Expiring | `CertificationRecordId`, `CertificationId`, `CertificationName`, `SubjectKind`, `SubjectId`, `SubjectName`, `Number`, `ExpiresOn`, `DaysBefore` |
| `CertificationExpiredEvent` | Certificate Expired | same, without `DaysBefore` |
| `SkillsChangedEvent` | Skills Changed | `SubjectKind`, `SubjectId`, `SkillIds`, `ChangedSkillIds`, `RemovedSkillIds`, `EffectiveDatesOnly`, `Actor` |
| `AssignSkillTask` (task) | Assign Skill | Properties `SubjectKind` (default `User`), `SubjectId`, `Skill` (name or id), `Proficiency` (default `3`; `0` removes), all expressions. Outcomes `Done`, `Failed`. |

## Extension Points

| Seam | Use it to |
|---|---|
| `ISkillAssignmentHandler` | `AssignmentsChangedAsync(SkillAssignmentsChangedContext)` runs in the request, before the session commits, and from the daily check (`EffectiveDatesOnly`). Keep a copy of a person's skills in step. |
| `ICertificationExpiryObserver` | `ExpiringAsync(record, certification, daysBefore)` and `ExpiredAsync(record, certification)` from the daily check. A failing observer is logged and the check goes on. |
| `ISkillSubjectLinkProvider` | `GetLinkedSubjectsAsync(subject)` tells Skills which subjects are the same person; the higher proficiency counts. Workforce links a worker and its user. |
| `ICatalogEntryHandler<Skill>`, `ICatalogEntryHandler<Certification>` | React to catalog changes. |
| `SkillSubjectSkillsDisplayDriverBase<TModel>` | Add a Skills section (one proficiency choice per enabled skill) to any editor built with `IDisplayManager<TModel>`. |

### Hear About Expiring Certificates

```csharp
public sealed class CertificateExpiryNotifier : ICertificationExpiryObserver
{
    private readonly ILogger _logger;

    public CertificateExpiryNotifier(ILogger<CertificateExpiryNotifier> logger)
    {
        _logger = logger;
    }

    public Task ExpiringAsync(CertificationRecord record, Certification certification, int daysBefore, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{Name}'s {Certification} expires in {Days} days.", record.SubjectName, certification.Name, daysBefore);

        return Task.CompletedTask;
    }

    public Task ExpiredAsync(CertificationRecord record, Certification certification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// Startup: services.AddScoped<ICertificationExpiryObserver, CertificateExpiryNotifier>();
```

### Add a Skills Section to Another Editor

The user editor (`UserSkills_Edit`) and the Workforce worker editor (`WorkerSkills_Edit`, `DisplayDriver<WorkerProfile>`) both derive from the shared base. Derive a small driver that says who holds the skills, names the shape and may move it:

```csharp
public sealed class VolunteerSkillsDisplayDriver : SkillSubjectSkillsDisplayDriverBase<Volunteer>
{
    public VolunteerSkillsDisplayDriver(
        ISkillManager skillManager,
        ISkillAssignmentService assignmentService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
        : base(skillManager, assignmentService, httpContextAccessor, authorizationService)
    {
    }

    protected override string ShapeType => "VolunteerSkills_Edit";

    protected override string Location => "Content:20";

    protected override SkillSubject GetSubject(Volunteer model, bool isNew)
        => string.IsNullOrEmpty(model.ItemId) ? default : new SkillSubject("Volunteer", model.ItemId);
}

// Startup: services.AddDisplayDriver<Volunteer, VolunteerSkillsDisplayDriver>();
```

The view `VolunteerSkills_Edit.cshtml` takes a `SubjectSkillsViewModel` (copy the user editor's `UserSkills_Edit`). The section shows only to users with `AssignSkills` and only when enabled skills exist; an empty subject hides it. Assignments are written during the update, so the host editor must cancel the session when the record isn't saved. More examples are in [references/skills-extensions.md](references/skills-extensions.md).

## Contact Center Shares the Catalog

- The Contact Center Agents feature depends on Skills. With Work Distribution (Queues) on, `IContactCenterSkillStore` reads and writes the shared catalog; Contact Center-only skills are copied in once with the same ids (a name the catalog already has is skipped).
- Skills are edited on `Admin/skills`; the old `Admin/contact-center/skills` addresses redirect there.
- An agent's routing proficiencies (`AgentProfile.SkillProficiencies`, keyed by skill name) and the user's assignments stay in step both ways. Changes are written after the agent's save commits, in their own scope, so a broken catalog never cancels an agent state change; a failed sync is logged and retried at the next save.
- The **Contact Center shared skills backfill** background task pushes agents saved before Skills was on, 100 every five minutes. See `orchardcore-contact-center`.

## Gotchas

| Symptom | Check |
|---|---|
| A held skill doesn't count | It is outside its dates, the skill is disabled, or its required certificate isn't `Valid` that day. |
| Valid certificate refused | The certification `RequiresDocument`; set `DocumentPath`. |
| Skills section missing in an editor | The user lacks `AssignSkills`, no enabled skill exists, or the subject is empty (new record without an id). |
| Recipe assignment skipped | `skillName` doesn't match, or `subjectId` is empty. |
| `RequiredCertificationName` ignored | It is read with that exact PascalCase name. |
| No reminders | Certificate must be `Valid` with `ExpiresOn`, and the certification needs `ReminderDays`. |

Related skills: `orchardcore-workforce`, `orchardcore-contact-center`, `orchardcore-dispatch`, `orchardcore-work-orders`, `orchardcore-teams`, `orchardcore-crestapps-users`.

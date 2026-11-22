---
name: orchardcore-skills
description: Skill for the CrestApps Orchard Core Skills module (CrestApps.OrchardCore.Skills), the one skill catalog of a site. Covers skills held at a proficiency from 1 to 5 by users and staff members, SkillRequirement and HoldsAllAsync, certifications and certificates, certificates the site issues with ICertificateIssuer, certificate templates, PDFs and the public verification page, the daily expiry check, ISkillAssignmentService, ICertificationRecordService, observers, Skills editor sections, workflows, webhooks, audience rules, importing skills and Moodle competency frameworks, the Skill holders report and the Skills recipe step. Use this skill when requests mention skills, proficiency, required skills, certifications, licences, issuing or verifying certificates, or certificates expiring. Strong matches include Skill, SkillAssignment, SkillSubject, SkillRequirement, CertificationRecord, ICertificateIssuer, CertificateTemplate and AddSkillsCore.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Skills

## One Skill Catalog for Routing and Scheduling

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Skills module. A person (a user, or a staff member who may have no account) holds skills at a proficiency from 1 (trainee) to 5 (expert), optionally between two dates. A skill can require a certification, and then it only counts on days the person holds a valid certificate. Contact Center routing, Workforce scheduling, Dispatch matching, Work Orders, Tickets and Learning all read the same catalog. The site can also issue its own certificates, numbered and provable on a public page.

### Guidelines

- Install `CrestApps.OrchardCore.Skills` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- Reference `CrestApps.OrchardCore.Skills.Abstractions` (models, contracts, `SkillsPermissions`) or `CrestApps.OrchardCore.Skills.Core` (stores, services, `AddSkillsCore()`, `SkillSubjectSkillsDisplayDriverBase<TModel>`) from your module.
- The feature depends on CrestApps Users and CrestApps Resources. Workforce and the Contact Center Agents feature depend on Skills, so they turn it on.
- Change skills only through `ISkillAssignmentService` and certificates through `ICertificationRecordService`; they validate, keep one assignment per person and skill, and call the handlers. Issue, renew and revoke the site's own certificates through `ICertificateIssuer`.
- A certificate the site issued (`VerificationCode` set) is revoked or renewed, never edited or deleted; the admin screens refuse to.
- Staff members are the `Staff` subject kind (it replaced `Worker`, see `orchardcore-staff`); Staff links each staff member to their user, so skills given to either count for both.
- Proficiency is an `int` checked with `SkillProficiency.IsValid` (`Minimum` 1, `Maximum` 5, `Default` 3).
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Feature | ID | Depends on |
|---|---|---|
| Skills | `CrestApps.OrchardCore.Skills` | `CrestApps.OrchardCore.Users`, `CrestApps.OrchardCore.Resources` |
| Skills: Issued Certificates | `CrestApps.OrchardCore.Skills.IssuedCertificates` | Skills, `OrchardCore.Contents`, `OrchardCore.Title`, `OrchardCore.Liquid`, `OrchardCore.Media` |

| Feature on | What Skills adds |
|---|---|
| `OrchardCore.Workflows` | Skills events and the **Assign Skill** task. |
| CrestApps Webhooks | Events `skills.certificate.issued`, `skills.certificate.renewed`, `skills.certificate.revoked` (category **Skills**, never the verification code). |
| CrestApps Reports | The **Skill holders** report (`skills-skill-holders`, category Operations, `ViewSkills`) with the **Category (framework)** filter. |
| `OrchardCore.DataPipelines.Core` | The `Skills` data source with the `SkillsCertifications` and `SkillsCertificates` data sets (never the code), for Data Pipelines and the Report Builder. |
| CrestApps Content Transfer | The `Skills.Skill` import target ("Skills and competency frameworks", `ManageSkills`) and an **Import** button on **Content > Skills**. |
| `OrchardCore.RateLimits` (with Issued Certificates) | 30 verification lookups a minute per visitor (group `Skills.CertificateVerification`). |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Skills",
        "CrestApps.OrchardCore.Skills.IssuedCertificates"
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
| `Certification` (shown as **Certification Types**) | `Name`, `Issuer`, `Description`, `ValidityMonths`, `RequiresDocument`, `ReminderDays` (for example `[60, 30, 7]`), `Enabled`, `GenerateNumbers`, `NumberPrefix` (default `CERT-`), `CertificateTemplateId` |
| `SkillAssignment` | `SubjectKind`, `SubjectId`, `SkillId`, `Proficiency`, `EffectiveFrom`, `EffectiveTo`, `AssignedBy`, `AssignedUtc` |
| `CertificationRecord` (a certificate) | `SubjectKind`, `SubjectId`, `SubjectName`, `CertificationId`, `Number`, `IssuedOn`, `ExpiresOn`, `DocumentPath` (media library), `Status` (`Pending`, `Valid`, `Expired`, `Revoked`), `VerifiedBy`, `VerifiedUtc`, `Notes`; for issued certificates `Source`, `SourceId`, `Evidence`, `VerificationCode`, `PreviousRecordId`, `RenewedByRecordId`; on revocation `RevokedReason`, `RevokedBy`, `RevokedUtc` |

A person is a `SkillSubject(Kind, Id)`: `SkillSubject.User(userId)` or `SkillSubject.Staff(staffMemberId)`. `SkillSubjectKind.User` and `SkillSubjectKind.Staff` are the well-known kinds; the kind is an open string, so `new SkillSubject("Volunteer", id)` works without changing Skills. `SkillSubject.Worker` and `SkillSubjectKind.Worker` are gone: `SkillSubjectKind.Parse` (and the JSON converter) read the old `Worker` value, or the number `1`, as `Staff` (`0` is `User`), and migration 3 re-indexes old assignments and certificates as held by staff.

The index providers (`SkillIndex`, `CertificationIndex`, `SkillAssignmentIndex`, `CertificationRecordIndex`) are declared `.ExclusiveToDocumentType().SingleRecordPerDocument()`. `CertificationRecordIndex` gained `Source`, `SourceId` and `VerificationCode` (migration 4).

### Required Skills

`SkillRequirement { SkillId, MinProficiency = 1 }` is the shared shape other modules keep for "needs these skills" (Tickets' `TicketPartSettings.RequiredSkills`, Work Orders' `WorkOrder.RequiredSkills`). Check a person against a list with the `ISkillAssignmentService` extension `HoldsAllAsync` (in `CrestApps.OrchardCore.Skills.Services`); empty skill ids are skipped and a proficiency under 1 counts as 1.

```csharp
public sealed class RepairType
{
    public List<SkillRequirement> RequiredSkills { get; set; } = [];
}

var qualified = await _assignmentService.HoldsAllAsync(SkillSubject.Staff(staffMemberId), repairType.RequiredSkills, today, cancellationToken);
```

## Services

| Contract | Key members |
|---|---|
| `ISkillManager`, `ICertificationManager` | `ICatalogManager<T>` plus `FindByNameAsync`, `GetEnabledAsync`, `SearchAsync(search, skip, take)` |
| `ISkillAssignmentService` | `GetAssignmentsAsync(subject)`, `GetEffectiveAsync(subject, date)` (inside dates, skill enabled, required certificate valid that day, linked subjects included), `SetAsync`, `AssignAsync(subject, SkillAssignmentInput, actor)`, `RemoveAsync`, `FindHoldersAsync(skillId, minimumProficiency, date, kind)`, `HoldsAsync` |
| `ICertificationRecordService` | `SaveAsync` (fills `ExpiresOn` from `IssuedOn` and `ValidityMonths`; refuses a valid hand-recorded record without its copy when the certification `RequiresDocument`), `VerifyAsync`, `RevokeAsync(id, actor, reason)` (keeps the reason, who and when in `Revoked*`, commits, then tells `ICertificationRecordObserver`s; a second revoke changes nothing), `DeleteAsync`, `GetForSubjectAsync`, `IsValidOnAsync`, `PageAsync` |
| `ICertificateIssuer` | `IssueAsync(CertificateIssueRequest)`, `RenewAsync(previousRecordId, request)`, `RevokeAsync(recordId, actor, reason)`, `FindByVerificationCodeAsync(code)`, `FindIssuedAsync(certificationId, source, sourceId)`. See [Issued Certificates](#issued-certificates). |

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

var holders = await _assignmentService.FindHoldersAsync(spanish.ItemId, minimumProficiency: 3, today, SkillSubjectKind.Staff);
```

## Issued Certificates

`ICertificateIssuer` (`DefaultCertificateIssuer`, registered by `AddSkillsCore()`) lets the site issue certificates itself, for example when a learner finishes a certification path:

- `IssueAsync` needs `CertificationId` (enabled), `Subject`, `Source` and `SourceId` (what earned it, such as `Learning.CertificationPath`), plus `SubjectName`, `Evidence`, `IssuedOn`, `ExpiresOn` (else from `ValidityMonths`) and `Actor`. It is idempotent: one certificate per certification, source and source id (a revoked one doesn't count), under a distributed lock; a repeat returns the existing record with `AlreadyIssued = true`.
- Each issued record gets a 16-character verification code (80 random bits; `CertificateVerificationCodes.Format` groups it as `XXXX-XXXX-XXXX-XXXX`, `Normalize` reads what a visitor typed) and, when the certification has `GenerateNumbers`, a number from `INumberSequence` (prefix plus six digits, after any hand-recorded number with the same prefix).
- `RenewAsync` creates a new record linked both ways (`PreviousRecordId`, `RenewedByRecordId`); once per certificate, never for a revoked one. The daily check sends no reminder for a renewed certificate and expires it quietly.
- `ICertificationRecordObserver` (`IssuedAsync`, `RenewedAsync`, `RevokedAsync`, each with a `CertificationRecordContext { Record, Certification, PreviousRecord, Actor }`) runs after the commit. Workflows and webhooks are observers.

```csharp
var result = await _certificateIssuer.IssueAsync(new CertificateIssueRequest
{
    CertificationId = certification.ItemId,
    Subject = SkillSubject.User(userId),
    SubjectName = displayName,
    Source = "Training.Course",
    SourceId = courseId,
    Evidence = "Passed the final assessment with 92%.",
    IssuedOn = today,
    Actor = "system",
}, cancellationToken);

if (result.Succeeded)
{
    // With Issued Certificates on, the public proof is /certificates/verify/{code}.
    var verifyPath = $"/certificates/verify/{result.Record.VerificationCode}";
}
```

The **Skills: Issued Certificates** feature prints and proves them: the `CertificateTemplate` content type (`TitlePart` plus `CertificateTemplatePart`: paper size and orientation, font family, colors, border, QR code position and size, Liquid text blocks), the certificate PDF, and the public, `noindex` verification page `/certificates/verify` (`/certificates/verify/{code}`, PDF at `/certificates/verify/{code}/certificate.pdf`). Template details, Liquid values and admin routes are in [references/issued-certificates.md](references/issued-certificates.md).

## Admin UI

| Menu | Routes | Permission |
|---|---|---|
| **Content > Skills** | `Admin/skills`, `Admin/skills/edit/{id?}`, `Admin/skills/delete/{id}`; **Import** (`Admin/import/contents/Skills.Skill`) when Content Transfer is on | `ManageSkills` |
| **Content > Certification Types** | `Admin/certifications`, `Admin/certifications/edit/{id?}`, `Admin/certifications/delete/{id}`; the editor is built with `IDisplayManager<Certification>` (`CertificationFields_Edit` card plus module cards) | `ManageSkills` |
| **Content > Certificates** | `Admin/certificates` (filters include expiring in the next 60 days), `Admin/certificates/edit/{id?}`, `.../verify/{id}`, `.../revoke/{id}`, `.../delete/{id}`; an issued certificate has a **Certificate** (PDF) button and no edit or delete | `ViewSkills` to list; `AssignSkills` to add, verify, revoke |
| User editor **Skills** section | `UserSkills_Edit` at `Content:12` | `AssignSkills` |
| Staff editor **Skills** section (Staff module) | `StaffMemberSkills_Edit` | `AssignSkills` |

Skills also registers two user audience rules (`IUserAudienceRuleSource`, see `orchardcore-crestapps-users`): `SkillHolder` ("Holds a skill") and `CertificationHolder` ("Holds a valid certification"); both count linked staff members as their users.

## Permissions

| Permission | Display name | Implied by | Default role |
|---|---|---|---|
| `ManageSkills` | Manage skills and certifications | none | Administrator |
| `AssignSkills` | Assign skills and record certifications | `ManageSkills` | Administrator |
| `ViewSkills` | View skills and certifications | `AssignSkills` | Administrator |

With Contact Center on, a role with `ManageContactCenterSkills` (Manage Contact Center skills) may also manage and assign the shared skills (`ContactCenterSkillsAuthorizationHandler`).

## Daily Check

The **Skills and certificates daily check** background task (schedule `5 * * * *`) runs `SkillsDailyCheck` once per day in the site's time zone (`SkillsDailyCheckpoint` remembers the last day):

1. A valid certificate reaching a reminder offset raises one reminder (only the nearest of the offsets already reached), once per expiry date; renewing it (a new expiry date) starts the reminders again. A certificate renewed by another record (`RenewedByRecordId`) gets no reminders and just expires.
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

- `subjectKind` is `User` or `Staff` (an old `Worker` is read as `Staff`).
- A certification also takes `generateNumbers`, `numberPrefix` and `certificateTemplateId`; a certificate takes `source`, `sourceId`, `evidence`, `verificationCode`, `previousRecordId`, `renewedByRecordId`, `revokedReason`, `revokedBy` and `revokedUtc` (see [references/issued-certificates.md](references/issued-certificates.md)).
- An assignment needs a `subjectId` and a skill that exists (`skillId` or `skillName`); `proficiency` defaults to 3. Refusals are reported as recipe errors.
- The **Skills and certifications** deployment step (category **Workforce**) exports every certification and skill, and optionally `IncludeAssignments` and `IncludeCertificationRecords` (up to 50,000 certificates, without reminder bookkeeping).

## Workflow Activities

With `OrchardCore.Workflows` on (category **Skills**). Events start after the change is committed; certificate events can be limited to some certifications (`CertificationIds`).

| Activity | Display name | Input |
|---|---|---|
| `CertificationExpiringEvent` | Certificate Expiring | `CertificationRecordId`, `CertificationId`, `CertificationName`, `SubjectKind`, `SubjectId`, `SubjectName`, `Number`, `ExpiresOn`, `DaysBefore` |
| `CertificationExpiredEvent` | Certificate Expired | same, without `DaysBefore` |
| `CertificateIssuedEvent` | Certificate Issued | the certificate fields plus `IssuedOn`, `Source`, `SourceId`, `Actor` |
| `CertificateRenewedEvent` | Certificate Renewed | same, plus `PreviousRecordId` |
| `CertificateRevokedEvent` | Certificate Revoked | same, plus `Reason` |
| `SkillsChangedEvent` | Skills Changed | `SubjectKind`, `SubjectId`, `SkillIds`, `ChangedSkillIds`, `RemovedSkillIds`, `EffectiveDatesOnly`, `Actor` |
| `AssignSkillTask` (task) | Assign Skill | Properties `SubjectKind` (default `User`), `SubjectId`, `Skill` (name or id), `Proficiency` (default `3`; `0` removes), all expressions. Outcomes `Done`, `Failed`. |

## Extension Points

| Seam | Use it to |
|---|---|
| `ISkillAssignmentHandler` | `AssignmentsChangedAsync(SkillAssignmentsChangedContext)` runs in the request, before the session commits, and from the daily check (`EffectiveDatesOnly`). Keep a copy of a person's skills in step. |
| `ICertificationExpiryObserver` | `ExpiringAsync(record, certification, daysBefore)` and `ExpiredAsync(record, certification)` from the daily check. A failing observer is logged and the check goes on. |
| `ICertificationRecordObserver` | `IssuedAsync`, `RenewedAsync`, `RevokedAsync(CertificationRecordContext)` after the commit; default no-op members, so implement only what you need. |
| `ISkillSubjectLinkProvider` | `GetLinkedSubjectsAsync(subject)` tells Skills which subjects are the same person; the higher proficiency counts. Staff (`StaffSkillSubjectLinkProvider`) links a staff member and their user. |
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

The user editor (`UserSkills_Edit`) and the Staff editor (`StaffMemberSkills_Edit`, `StaffSkillsDisplayDriver` on `StaffMember`, which replaced the Workforce worker editor) both derive from the shared base. Derive a small driver that says who holds the skills, names the shape and may move it:

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

## Import Skills From a File

With CrestApps Content Transfer on, `SkillsImportTarget` (`Skills.Skill`, checked by `ManageSkills`) runs in Content Transfer's background import, with progress, pause, resume and a failed-rows file. `SkillImportRowReader` (Skills.Core) reads each row and `SkillImporter` saves it:

- A **Moodle competency framework export** (known by its `isframework` column): the framework's short name becomes the category; each competency becomes a skill named by its short name (or ID number), with a plain-text description. Nested competencies are imported flat.
- A **plain file** with `Name` and optional `Category`, `Description` and `Enabled` columns.

Skills are matched by name in any case: a new name adds a skill; a known one in the same category (or none) is updated, an empty cell keeping the value; a known one in another category, or a name repeated in the batch, fails. Importing the same file again changes nothing. See `orchardcore-content-transfer`.

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
| No reminders | Certificate must be `Valid` with `ExpiresOn`, and the certification needs `ReminderDays`; a renewed certificate gets none. |
| "A certificate the site issued cannot be edited or deleted" | It has a `VerificationCode`; revoke or renew it through `ICertificateIssuer` instead. |
| `IssueAsync` returns the old record | Issuing is idempotent per certification, source and source id (`AlreadyIssued` is `true`); revoke it first to issue again. |
| Verification page returns 404 | Enable `CrestApps.OrchardCore.Skills.IssuedCertificates`; too many lookups return 429 when Rate Limits is on. |
| Old `Worker` assignments missing after the upgrade | Migration 3 re-indexes them as `Staff` in a deferred task; a failure is logged and each record is re-indexed when next saved. |
| **Import** button missing on Content > Skills | Enable CrestApps Content Transfer. |

Related skills: `orchardcore-staff`, `orchardcore-workforce`, `orchardcore-contact-center`, `orchardcore-dispatch`, `orchardcore-work-orders`, `orchardcore-teams`, `orchardcore-crestapps-users`, `orchardcore-content-transfer`, `orchardcore-reports`, `orchardcore-webhooks`, `orchardcore-learning-certifications` (certificates and competencies earned through Learning courses).

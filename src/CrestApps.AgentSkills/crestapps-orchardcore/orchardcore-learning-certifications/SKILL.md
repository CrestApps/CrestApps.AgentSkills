---
name: orchardcore-learning-certifications
description: Skill for CrestApps Orchard Core Learning Certifications and Digital Credentials (CrestApps.OrchardCore.Learning.Certifications and CrestApps.OrchardCore.Learning.Credentials). Covers certification paths that issue Skills certificates from courses, quizzes and credits, renewal windows, cycles, grace periods, lapses and revocation, education credit types and the credit ledger, external learning, competencies granted on completion, and Open Badges 3.0 credentials signed as JWTs with a Bitstring Status List and a verification page, with the LearningCertificationPaths, LearningCreditTypes and LearningCredentialAwards recipe steps. Use this skill when requests mention certification paths, renewals, CEU or CPD credits, competencies, badges, Open Badges or verifiable credentials in CrestApps Learning. Strong matches include CertificationPath, ICertificationPathService, AddCertificationRequirement, EducationCreditsPart, CompetenciesPart, CredentialDefinitionPart, ICredentialAwardService and AddCredentialRuleKind.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Learning Certifications and Credentials

## Certificates, Credits, Competencies and Open Badges Earned Through Learning

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for how CrestApps Learning issues proof of learning. Certifications and certificates belong to Skills (see `orchardcore-skills`); Learning Certifications adds how a certificate is earned and renewed, so a certificate earned in a course is the same record Workforce and Dispatch read. Digital credentials are separate: signed, portable Open Badges 3.0 statements a learner keeps in a wallet.

### Guidelines

- Install `CrestApps.OrchardCore.Learning.Certifications` and/or `CrestApps.OrchardCore.Learning.Credentials` (or `CrestApps.OrchardCore.Cms.Core.Targets`) in the web/startup project. Reference the `.Abstractions` / `.Core` projects from your module.
- Everything is issued only on evidence: `CompletionEvidence.SupportsIssuing` refuses a failed completion, one under review, and one resting on a required item's result reported by the browser (a SCORM or cmi5 package) unless that item has **Let a result reported by the browser count toward a certification** (`CourseItemPart.AllowSelfReported`).
- Issuing is idempotent (certificates by certification, source and source id; credit ledger by idempotency key; credential awards by definition, rule kind, source id and recipient), and hourly upkeep catches up lost events. Observers you add for something issued on completion must be idempotent too.
- Set the site's **Base URL** before awarding digital credentials; without it nothing is awarded (`NoIssuerAddress`).
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Learning Certifications | `CrestApps.OrchardCore.Learning.Certifications` | Learning, Skills Issued Certificates (`CrestApps.OrchardCore.Skills.IssuedCertificates`) |
| Learning Competencies | `CrestApps.OrchardCore.Learning.Competencies` | Learning, Skills (`CrestApps.OrchardCore.Skills`) |
| Digital Credentials | `CrestApps.OrchardCore.Learning.Credentials` | Learning, Secrets (`OrchardCore.Secrets`) |

With Learning Assessments on, `AssessmentPassed` requirements and `QuizPassed` credential rules light up; with Learning Paths, path credits, competencies and the `LearningPathCompleted` credential rule.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Learning.Certifications",
        "CrestApps.OrchardCore.Learning.Competencies",
        "CrestApps.OrchardCore.Learning.Credentials"
      ],
      "disable": []
    }
  ]
}
```

## Certification Paths

One `CertificationPath` per Skills certification, edited in the **Earned through learning** card of the certification editor (**Content > Certification Types**). It is saved with its certification.

| Property | Meaning |
|---|---|
| `Enabled` | Whether the path issues certificates |
| `Join` | `All` (0) or `Any` (1) requirement |
| `InitialRequirements`, `RenewalRequirements` | `{ Id, Kind, TargetId, MinimumGrade }`; empty renewal means the initial requirements |
| `ReferenceDate` | `CompletionDate` (0), `ExpiryDate` (1, keeps the anniversary) or `FixedDate` (2, with `FixedMonth`, `FixedDay`, `MinimumIntervalMonths`) |
| `RenewalWindowDays` | Renewal opens this many days before expiry (default 60) |
| `GracePeriodDays` | Renewal still counts this long after expiry; after it, start over |
| `StartsOnFirstRequirement` | Meeting a requirement starts the path (default true) |
| `EnrollOnRenewal` | Enroll the holder again in renewal courses when renewal opens (default true) |

Requirement kinds: `CourseCompleted` (optional minimum grade), `AssessmentPassed` (site-scored attempts only, optional minimum percent), `CreditsEarned` (target is the credit type; the number of credits earned in the cycle goes in `MinimumGrade`). The validity length comes from the certification's **Valid for (months)**.

- `ICertificationPathService`: `EvaluateAsync` issues through the Skills `ICertificateIssuer` (`IssueAsync`, `RenewAsync`); `StartAsync`; `OpenRenewalAsync`. Every change runs under a lock per path and person.
- `CertificationPathProgress` keeps the state (in progress, certified, renewal open, lapsed, revoked), the cycle, the current certificate and every cycle with its evidence.
- `CertificateHolderResolver` issues to the learner's staff record when Staff is on, otherwise to the user.
- Skills tells Learning about expiry (`ICertificationExpiryObserver`, lapses the progress) and revocation (`ICertificationRecordObserver`).
- A completed enrollment flagged for review offers **Revoke the certificate** on its review card in **Learning > Enrollments**.

## Education Credits and External Learning

- `EducationCreditType` (`Name`, `Unit`, `Description`, `AllowExternalLearning`), managed in **Learning > Education Credits**.
- `EducationCreditsPart` (attachable; put on `Course` by the migration) lists `Awards` of `{ CreditTypeId, Amount, ValidityMonths }`. A completed course awards once per enrollment; a learning path with the part awards once per path progress.
- Credits post to a quantity ledger (`IQuantityLedger`, collection `LearningCreditLedger`): account is the user, bucket the credit type.
- External learning (`ExternalLearningSubmission`) is recorded by the learner on **My education credits** (`/learning/credits`) with optional evidence (protected area `ExternalLearningEvidence`) and reviewed under **Learning > External Learning**.
- `IEducationCreditService` reads balances and awards credits; `services.AddEducationCreditObserver<T>()` hears awards, submissions and reviews. The **Record external learning** data pipeline step (`LearningRecordExternalLearning`) records rows as accepted.

## Competencies

- `CompetenciesPart` (`SkillIds`, `Proficiency` 1 to 5, default 3) is put on `Course` and, when it exists, `LearningPath` by `CompetenciesMigrations`.
- `CompetencyEnrollmentObserver` and `LearningPathCompetenciesObserver` grant through `CompetencyGrants.GrantAsync` (`ISkillAssignmentService.AssignAsync`, `SkillSubject.User`), never lowering a held level; granting again changes nothing.
- A competency framework is a Skills category; load frameworks with the Skills import or the `Skills` recipe step. **My competencies** is at `/learning/competencies`. Outside the feature, register with `services.AddLearningCompetenciesCore()`.

## Digital Credentials (Open Badges 3.0)

A `CredentialDefinition` content item (type part with an `Image` media field, plus `CredentialDefinitionPart`) defines the achievement. Awards are kept in the `LearningCredentials` collection (`CredentialAwardIndex`) with a copy of the achievement, a salted hash of the recipient's e-mail (never the address), the status list slot and the signed token.

| `CredentialDefinitionPart` | Meaning |
|---|---|
| `Description`, `Criteria` | Required; achievement `description` and `criteria.narrative` |
| `AchievementType` | `Badge`, `Certificate`, `CertificateOfCompletion`, `Certification`, `Course`, `Competency`, `LearningProgram`, `MicroCredential`, `Award`, `Achievement`, `License`, `Membership` |
| `Tags`, `Alignments` | `tag`; `alignment` of `{ TargetName, TargetUrl, TargetDescription, TargetFramework, TargetCode, TargetType }` |
| `SkillId`, `CertificationId` | With Skills, stated as alignments |
| `ValidityMonths` | Empty for no expiry |
| `Rules` | `{ Kind, TargetId }`: `CourseCompleted`, `QuizPassed`, `LearningPathCompleted`, `CertificationIssued`, `Manual`, `Workflow`; any one awards |

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4safetybadge000000000badge",
          "ContentType": "CredentialDefinition",
          "DisplayText": "Workplace safety badge",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Workplace safety badge" },
          "CredentialDefinitionPart": {
            "Description": "Completed the workplace safety course.",
            "Criteria": "Complete SAFE-101 with a passing grade.",
            "AchievementType": "Badge",
            "Tags": [ "safety" ],
            "ValidityMonths": 24,
            "Rules": [ { "Kind": "CourseCompleted", "TargetId": "<course id>" } ]
          }
        }
      ]
    }
  ]
}
```

- `ICredentialAwardService.AwardAsync(CredentialAwardRequest { DefinitionId, UserId, RuleKind, SourceId, Actor, Reason })` is idempotent under a lock; a valid award of the definition blocks a second one. Rules hear `IEnrollmentObserver`, `IAttemptObserver`, `ILearningPathObserver` and `ICertificationRecordObserver` and award in a deferred task.
- A revoked certificate, or a voided or no-longer-passing attempt, revokes the awards made from it (`CredentialSourceRevoker`). A review only offers **Revoke the credential**.
- The signing key is the `Learning.Credentials` key set of the shared signing keys: an RS256 `RsaKeySecret` in Secrets, rotated every 365 days, retired keys published for ten years. Credential structure, endpoints and verification are in [Open Badges details](references/open-badges-credentials.md).

## Admin Menu and Pages

| Menu or page | Address | Permission |
|---|---|---|
| **Learning > Education Credits** | `/Admin/learning/credit-types` | `ManageEducationCredits` |
| **Learning > External Learning** | `/Admin/learning/external-learning` | `ReviewExternalLearning` |
| **Learning > Credential Definitions** | Content list filtered to `CredentialDefinition` | `ManageCredentialDefinitions` |
| **Learning > Credential Awards** | `/Admin/learning/credential-awards` (award by hand at `/new`, revoke) | `ViewCredentialAwards`; awarding needs `AwardCredentials` |
| Content > Certification Types | The certification editor's **Earned through learning** card | Skills permissions |
| People on a path | `/Admin/learning/certification-paths/{id}/people` | View enrollments; Manage enrollments to start or check again |

Learner pages (entries of the storefront account menu when My Account is on): **My certifications** `/learning/certifications`, **My competencies** `/learning/competencies`, **My education credits** `/learning/credits`, **My credentials** `/learning/credentials` (account or user menu entry only with Learner Pages on). The course completion panel lists *Your certificate*, *Counts toward* and *Your credential* links.

## Permissions

| Permission | Display name | Implied by | Notes |
|---|---|---|---|
| `ManageEducationCredits` | Manage education credit types and award credits | | |
| `ReviewExternalLearning` | Accept or reject external learning | `ManageEducationCredits` | |
| `ManageCredentialDefinitions` | Manage credential definitions | | Grants content permissions on definitions |
| `AwardCredentials` | Award and revoke digital credentials | | Security critical |
| `ViewCredentialAwards` | View digital credential awards | `AwardCredentials` | |

## Recipes and Deployment

```json
{
  "steps": [
    {
      "name": "LearningCreditTypes",
      "CreditTypes": [
        { "name": "CPD hours", "unit": "hours", "description": "Continuing professional development.", "allowExternalLearning": true }
      ]
    },
    {
      "name": "LearningCertificationPaths",
      "Paths": [
        {
          "certificationName": "Forklift operator",
          "enabled": true,
          "join": 0,
          "initialRequirements": [
            { "kind": "CourseCompleted", "targetId": "<course id>", "minimumGrade": 80 }
          ],
          "renewalRequirements": [
            { "kind": "CreditsEarned", "targetId": "<credit type id>", "minimumGrade": 10 }
          ],
          "referenceDate": 1,
          "renewalWindowDays": 60,
          "gracePeriodDays": 30,
          "startsOnFirstRequirement": true,
          "enrollOnRenewal": true
        }
      ]
    }
  ]
}
```

- `LearningCreditTypes` matches by `itemId` or name. Deployment step **Education credit types**.
- `LearningCertificationPaths` matches a path to its certification by `certificationName` (wins) or `certificationId`, else by `itemId`. Write `join` and `referenceDate` as numbers: the step deserializes without a string enum converter, so a name such as `"All"` fails the row. Progress is not exported. Deployment step **Learning certification paths**.
- `LearningCredentialAwards` carries `Awards` with `RecipientUserName`, `AwardedByUserName`, `RevokedByUserName`; it skips codes already on the site, rejects slots outside or held in the status lists, and keeps the signed credential as issued (it verifies only against the issuing site's keys). The **Credential awards (learner records)** deployment step is opt-in and needs **Export learner records**.
- Definitions move with `ContentDefinition` and `Content`.

## Workflows, Notifications, Reports and Data

| Area | What it adds |
|---|---|
| Workflows | Certification Renewal Opened, Certification Lapsed events; Start Certification Path task; Education Credits Awarded, External Learning Submitted events; Award Education Credits task; Credential Awarded event (input `AwardId`, `PublicId`, `DefinitionId`, `AchievementName`, `UserId`, `RuleKind`, `SourceId`, `IssuedUtc`, `ExpiresUtc`); Award Credential task (definition id, user id, optional source id; outcomes Done and Failed). Skills adds Certificate Issued, Renewed, Revoked, Expiring and Expired. |
| Notifications | `LearningCertificateIssued`, `LearningCertificateExpiring`, `LearningCertificationRenewalOpened`, `LearningCertificationLapsed`, `LearningCertificateRevoked`, `LearningCredentialAwarded`, `LearningCredentialRevoked` |
| Report | **Certification compliance** (`learning-certification-compliance`) |
| Data sources | `LearningCertifications` (`LearningCertificationProgress`, education credits, external learning); `Learning.Credentials` (`CredentialAwards`, needs **View digital credential awards**, no e-mail or identity hash) |
| Background task | **Learning certification upkeep** (`25 * * * *`): opens due renewals from a leased queue and re-checks people who met a requirement since the last look |
| Personal data | Progress, credits and external learning are exported and deleted; credential erasure revokes each award and strips the recipient |
| Telemetry | `crestapps.learning.certification.changes` (issued, renewed, renewal_opened, lapsed, revoked) |

## Extension Points

| Registration | Use it to |
|---|---|
| `services.AddCertificationRequirement<T>()` | `ICertificationRequirement` (`Kind`, `DisplayName`, `FindEvidenceAsync`, `ListUsersSinceAsync`, `GetTitleAsync`) |
| `services.AddCertificationPathObserver<T>()` | Hear certification path changes |
| `services.AddEducationCreditObserver<T>()` | Hear credit awards and external learning |
| `services.AddCredentialRuleKind<T>()` | `ICredentialRuleKind` (`Kind`, `DisplayName`, `ListTargetsAsync`); the owning module calls `AwardAsync` with its rule kind |
| `services.AddCredentialAwardObserver<T>()` | `ICredentialAwardObserver.AwardedAsync` / `RevokedAsync` after the save |
| `IEnrollmentReviewActionProvider` | Add actions to a completed enrollment's review card |
| `ICredentialDefinitionCatalog` | Read published definitions |

### Award a Credential for an Event Attendance

```csharp
public sealed class EventAttendedRuleKind : ICredentialRuleKind
{
    internal readonly IStringLocalizer S;

    public EventAttendedRuleKind(IStringLocalizer<EventAttendedRuleKind> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Kind => "EventAttended";

    public LocalizedString DisplayName => S["An event is attended"];

    public Task<IReadOnlyList<CredentialRuleTarget>> ListTargetsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CredentialRuleTarget>>([new CredentialRuleTarget("annual-summit", "Annual summit")]);
}

public sealed class EventCredentialAwarder
{
    private readonly ICredentialAwardService _awards;

    public EventCredentialAwarder(ICredentialAwardService awards)
    {
        _awards = awards;
    }

    public Task AwardAsync(string definitionId, string userId, string attendanceId)
        => _awards.AwardAsync(new CredentialAwardRequest
        {
            DefinitionId = definitionId,
            UserId = userId,
            RuleKind = "EventAttended",
            SourceId = attendanceId,
            Actor = "Events",
        });
}

// Startup:
// services.AddCredentialRuleKind<EventAttendedRuleKind>();
// services.AddScoped<EventCredentialAwarder>();
```

## Troubleshooting

- **No certificate after completing the course**: the path is disabled, `StartsOnFirstRequirement` is off and nobody started the person, the completion is under review or failed, or a required package item reported the result without **AllowSelfReported**. The hourly upkeep re-checks.
- **Renewal never opens**: `RenewalWindowDays` is zero, or the certification has no **Valid for (months)**.
- **Recipe reports "A certification path could not be read"**: an enum was written as a name; use numbers for `join` and `referenceDate`.
- **No credential awarded**: the site has no Base URL, the recipient has no e-mail address, or they already hold a valid award of the definition.
- **External verifiers cannot fetch the status list**: endpoints are rate limited (group `LearningCredentials`, 120 requests a minute) when Rate Limits is on; status lists are computed per request.

Related skills: `orchardcore-skills`, `orchardcore-learning`, `orchardcore-learning-assessments`, `orchardcore-learning-standards`, `orchardcore-secrets`, `orchardcore-workforce`.

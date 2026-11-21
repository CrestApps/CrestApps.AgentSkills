---
name: orchardcore-learning
description: Skill for the CrestApps Orchard Core Learning management system (CrestApps.OrchardCore.Learning and its companion modules). Covers courses and lessons as content items, the course outline, enrollments with grants, approval, keys and access dates, progress and completion criteria, release rules, learner pages and the player, course categories, enrollment by cohort, Secure Media lesson files, the LearningEnrollments recipe step, workflows, webhooks, reports and data sources, plus Learning Paths, Required Learning, Classes, Coursework, Surveys, Learning Commerce and Learning AI tutoring. Use this skill when requests mention an LMS, courses, lessons, enrollments, learners, instructors, learning paths, required learning, classes, coursework, surveys, selling courses or a learning tutor. Strong matches include CoursePart, CourseItemPart, IEnrollmentService, ILearnerProgressService, AddCourseItemKind, AddCourseItemReleaseRule, AddEnrollmentObserver, LearningPathPart, LearningRequirements and LearningProductPart.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Learning

## Courses Built from Orchard Core Content

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Learning modules. A course and its lessons are content items (versions, localization, media, the content API); Learning adds who may open a lesson, what a learner has done and when a course is complete. Learning records (enrollments, progress, attempts) are YesSql documents, not content.

### Guidelines

- Install `CrestApps.OrchardCore.Learning` and the companion module packages you need (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes all thirteen Learning modules) in the web/startup project.
- From your own module, reference the contracts (`CrestApps.OrchardCore.Learning.Abstractions`, `...Learning.Core`, and the `*.Abstractions`/`*.Core` project of each companion), never the module projects.
- Write enrollments through `IEnrollmentService` and progress through `ILearnerProgressService`: they take a distributed lock per enrollment, commit the whole session inside it and raise observers after the commit. Finish your own writes before calling them.
- Every Learning admin page lives under the top-level **Learning** menu; package settings are under **Settings > Learning packages**. Features, recipes and deployments are under **Tools**.
- Learner records (enrollments, attempts, submissions, statements, credential awards, runtime states) move only through opt-in deployment steps that need **Export learner records**; people are written by user name.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

## Which Skill Covers Which Feature

| Feature | ID | Depends on | Skill |
|---|---|---|---|
| Learning | `CrestApps.OrchardCore.Learning` | Contents, Lists, Title, Html, Autoroute, Media, Content Fields, Roles, CrestApps Users (`CrestApps.OrchardCore.Users`), CrestApps Resources | this skill |
| Learner Pages | `CrestApps.OrchardCore.Learning.LearnerPages` | Learning | this skill |
| Self Enrollment | `CrestApps.OrchardCore.Learning.SelfEnrollment` | Learner Pages | this skill |
| Course Categories | `CrestApps.OrchardCore.Learning.CourseCategories` | Learning, `OrchardCore.Taxonomies`, `OrchardCore.Taxonomies.ContentsAdminList` | this skill |
| Enrollment by Cohort | `CrestApps.OrchardCore.Learning.Cohorts` | Learning, `CrestApps.OrchardCore.Users.Cohorts` | this skill |
| Learning Paths | `CrestApps.OrchardCore.Learning.Programs` | Learning | this skill, [programs reference](references/learning-programs.md) |
| Required Learning | `CrestApps.OrchardCore.Learning.Programs.RequiredLearning` | Learning Paths, `CrestApps.OrchardCore.Users.Audiences` | this skill, [programs reference](references/learning-programs.md) |
| Team Learning | `CrestApps.OrchardCore.Learning.Programs.TeamLearning` | Required Learning, `CrestApps.OrchardCore.Staff` | this skill, [programs reference](references/learning-programs.md) |
| Learning Classes | `CrestApps.OrchardCore.Learning.Classes` | Learning, `CrestApps.OrchardCore.Scheduling` | this skill, [activities reference](references/learning-activities.md) |
| Learning Coursework | `CrestApps.OrchardCore.Learning.Coursework` | Learning, Learning Gradebook | this skill, [activities reference](references/learning-activities.md) |
| Learning Surveys | `CrestApps.OrchardCore.Learning.Surveys` | Learning | this skill, [activities reference](references/learning-activities.md) |
| Learning Commerce | `CrestApps.OrchardCore.Learning.Commerce` | Learning, `CrestApps.OrchardCore.Products.Listing`, `CrestApps.OrchardCore.Orders`, `CrestApps.OrchardCore.Accounts.Members` | this skill, [commerce reference](references/learning-commerce.md) |
| Learning Assistant | `CrestApps.OrchardCore.Learning.AI` | Learning, `CrestApps.OrchardCore.AI` | this skill, [AI reference](references/learning-ai.md) |
| Learning Authoring Assistant | `CrestApps.OrchardCore.Learning.AI.Authoring` | Learning Assistant | this skill, [AI reference](references/learning-ai.md) |
| Learning Assessments, Learning Gradebook, Learning QTI Import, Learning Moodle Import | `CrestApps.OrchardCore.Learning.Assessments`, `...Assessments.Gradebook`, `...Assessments.QtiImport`, `...Assessments.MoodleImport` | see that skill | `orchardcore-learning-assessments` |
| Learning Certifications, Learning Competencies, Digital Credentials | `CrestApps.OrchardCore.Learning.Certifications`, `...Learning.Competencies`, `...Learning.Credentials` | see that skill | `orchardcore-learning-certifications` |
| Learning SCORM Packages, Learning cmi5 Packages, Learning Record Store, External Tools | `CrestApps.OrchardCore.Learning.Packages.Scorm`, `...Packages.Cmi5`, `...Learning.Xapi`, `...Learning.Lti` | see that skill | `orchardcore-learning-standards` |

Every feature is in the **Learning** category of **Tools > Features**.

### Turn Learning On

The **Learning setup** recipe (`LearningSetup`) enables Learning, Learner Pages, Self Enrollment, Secure Media (`OrchardCore.Media.Security`) and Notifications, then runs **Learning roles** (`LearningRoles`: Learning Administrator, Course Author, Instructor, Grader, Training Manager, Learner) and **Learning course catalog** (`LearningCatalog`: a Lucene index `courses` searchable at `/search/courses`). **Learning public media** (`LearningPublicMedia`) gives the media root folder to everyone while lesson files stay protected.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Learning",
        "CrestApps.OrchardCore.Learning.LearnerPages",
        "CrestApps.OrchardCore.Learning.SelfEnrollment",
        "CrestApps.OrchardCore.Learning.CourseCategories",
        "CrestApps.OrchardCore.Learning.Assessments",
        "CrestApps.OrchardCore.Learning.Assessments.Gradebook",
        "OrchardCore.Media.Security",
        "OrchardCore.Notifications"
      ],
      "disable": []
    },
    {
      "name": "Recipes",
      "Values": [
        { "ExecutionId": "CrestApps.OrchardCore.Learning", "Name": "LearningRoles" },
        { "ExecutionId": "CrestApps.OrchardCore.Learning", "Name": "LearningCatalog" }
      ]
    }
  ]
}
```

## Content Types and Parts

| Type | Parts and fields | Course item kind |
|---|---|---|
| `Course` | Title, Autoroute (`courses/{title}`), Html body, `Cover` media field, `CoursePart`, `CourseOutlinePart`, List of the lesson types | |
| `PageLesson` | Title, Html body, `Attachments` media field | `Page` |
| `VideoLesson` | Title, Html body, `Video` and `Captions` media fields, `YouTube` field, `VideoLessonPart` | `Video` |
| `FileLesson` | Title, Html body, `Files` media field | `File` |
| `LinkLesson` | Title, Html body, `Url` link field | `Link` |
| `Acknowledgement` | Title, Html body, `AcknowledgementPart` | `Acknowledgement` |
| `TextBlock` | Title, Html body | `Text` (inline: never required, not tracked, shown in place) |

Lessons are contained in the course through Orchard's List part; `CourseOutlinePart.Sections` orders them. Companion modules add `QuizLesson`, `LearningPackage`, `ExternalToolItem`, `LearningClass`, `CourseworkLesson`, `FeedbackSurvey`, `Choice` and `LearningPath`.

| Part | Key properties |
|---|---|
| `CoursePart` | `Code`, `Summary`, `Level`, `EstimatedMinutes`, `EnrollmentMode` (`Closed`, `Open`, `Key`, `Approval`), `AccessDays` (self enrollments only), `StartsUtc`, `EndsUtc`, `PassMark`, `GradeCategories`, `Prerequisites`, `CompletionCriteria`, `NeverCompletes`, `ItemsInOrder`, `Staff` (`UserId` + `Instructor`, `Assistant`, `Grader` or `Observer`), `SourceIdentifier` |
| `CourseOutlinePart` | `Sections` of `{ Id, Title, Summary, ReleaseRules, Items: [{ ContentItemId, Required }] }` |
| `CourseItemPart` | `Kind`, `CompletionMode`, `MinimumMinutes`, `EstimatedMinutes`, `Gradable`, `Weight`, `GradeCategoryId`, `PassMark`, `FreePreview`, `AllowSelfReported`, `ReleaseRules`, `SourceIdentifier` |

Completion modes (`CompletionModes`): `Viewed`, `MarkedDone`, `MinimumTime`, `Watched`, `Acknowledged`, `Submitted`, `Graded`, `Passed`, `Attended`, `PackageReported`, `MinimumScore`. Each kind lists the modes it supports.

Rules are `LearningRuleSettings { Name, Values }` with string values:

| Rule | Kind | Values |
|---|---|---|
| `ItemCompleted` | Release | `ItemId`, optional `MinimumScore` |
| `Date` | Release | `FromUtc` |
| `DaysAfterEnrollment` | Release | `Days` |
| `People` | Release | `UserIds`, `Audience` (a User Audiences audience) |
| `MinimumGrade` | Completion | `Grade` |
| `MinimumTime` | Completion | `Minutes` |
| `ItemPassed` | Completion | `ItemId` |
| `StaffApproval` | Completion | optional `Role` (course staff role that approves) |

### Create a Course with a Recipe

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4safetylesson01pagelesson1",
          "ContentType": "PageLesson",
          "DisplayText": "Why safety matters",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Why safety matters" },
          "HtmlBodyPart": { "Html": "<p>Read this first.</p>" },
          "CourseItemPart": { "Kind": "Page", "CompletionMode": "Viewed", "EstimatedMinutes": 5 },
          "ContainedPart": { "ListContentItemId": "4safetycourse000000course1", "Order": 0 }
        },
        {
          "ContentItemId": "4safetycourse000000course1",
          "ContentType": "Course",
          "DisplayText": "Workplace safety",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Workplace safety" },
          "CoursePart": {
            "Code": "SAFE-101",
            "Summary": "The basics of staying safe at work.",
            "EnrollmentMode": "Open",
            "AccessDays": 90,
            "ItemsInOrder": true,
            "CompletionCriteria": [ { "Name": "MinimumTime", "Values": { "Minutes": "10" } } ]
          },
          "CourseOutlinePart": {
            "Sections": [
              {
                "Id": "intro",
                "Title": "Introduction",
                "Items": [ { "ContentItemId": "4safetylesson01pagelesson1", "Required": true } ]
              }
            ]
          }
        }
      ]
    }
  ]
}
```

The enrollment key of a `Key` course is never in the content item: it is a salted hash in `LearningSecretStore`, set on the course editor or carried as `CourseKeys` by the `LearningEnrollments` step.

## Enrollments, Progress and Completion

Records live in the `Learning` YesSql collection: `Enrollment` (`Number` such as `E000001`, `Status`, `Cycle`, `PreviousEnrollmentId`, dates, `ProgressPercent`, `Grade`, `Passed`, `Grants`, `History`, `Completion` snapshot, `Review`), `EnrollmentGrant` (`Source` + `SourceId`; built-in sources `Manual`, `Self`, `Key`, `Pipeline`, `Workflow`, `Cohort`; revoking the last grant withdraws the enrollment) and `EnrollmentProgress` (one per enrollment, an entry per course item).

| From | To |
|---|---|
| Pending | Active, Withdrawn (rejected) |
| Active | Completed, Suspended, Expired, Withdrawn |
| Suspended | Active, Withdrawn |
| Completed, Expired, Withdrawn | A new enrollment (the next cycle) |

- An item completes when its completion mode is met; the course completes when every required item is complete and every completion criterion passes. `NeverCompletes` courses never complete.
- A completed enrollment never changes. When a result it rests on changes later (an attempt voided or regraded lower), `Enrollment.Review` is set, instructors get **Completion needs review**, and someone who manages the enrollment clears it with `ClearReviewAsync`.
- Who may open a lesson is decided by a content authorization handler on `ViewContent`, so the display route, content API, GraphQL, the content data source and Secure Media all follow it. With Secure Media on, files in a lesson's media fields return 404 to people who are not enrolled. Never give **View media content in all folders** to Anonymous or Authenticated.
- Long material on records, locks, access, learner pages, cohorts and categories is in [records and access](references/learning-records-and-access.md).

```csharp
public sealed class PartnerEnrollmentService
{
    private readonly IEnrollmentService _enrollmentService;

    public PartnerEnrollmentService(IEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    public async Task<string> EnrollAsync(string userId, string courseId, string partnerOrderId)
    {
        var result = await _enrollmentService.EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = "Partner.Portal",
            SourceId = partnerOrderId,
            AccessEndsUtc = DateTime.UtcNow.AddDays(180),
            Actor = "Partner portal",
        });

        // ErrorCode is a refusal such as Busy; enrolling twice from the same source changes nothing.
        return result.Succeeded ? result.Enrollment.ItemId : result.ErrorCode;
    }
}

// Startup:
// services.AddEnrollmentSource("Partner.Portal", LocalizationSource.Create("Partner portal", typeof(Startup)));
// services.AddScoped<PartnerEnrollmentService>();
```

## Admin Menu and Pages

| Menu | Page | Permission |
|---|---|---|
| **Learning > Courses** | The content list filtered to `Course` | `ManageOwnCourses` |
| **Learning > Enrollments** | `/Admin/learning/enrollments` (enroll, approve, reject, status, extend, complete an item, approve completion, clear review) | `ViewEnrollments`, else `ManageOwnCourses` for own courses |
| **Learning > Required Learning** | `/Admin/learning/required` | `ManageRequiredLearning` |
| **Learning > Team Learning** | `/Admin/learning/team` | `ViewTeamLearning` |
| **Learning > Authoring assistant** | `/Admin/learning/ai/authoring` | `UseLearningAuthoringAssistant` |
| Content > New > Learning Path | The path editor; **People** at `/Admin/learning/paths/{id}/people` | Edit the path |

Companion skills add **Question banks**, **Grading**, **Gradebook**, **Grade scales**, **Education Credits**, **External Learning**, **Credential Definitions**, **Credential Awards**, **External Tools** and **Learning Statements** under **Learning**.

| Learner page | URL |
|---|---|
| My learning | `/learning` (`status`, `q`, `view=list`) |
| Transcript | `/learning/transcript` |
| Catalog | `/learning/catalog` (`q`, `category`) |
| Player | `/learning/courses/{courseId}/play/{itemId}` |
| Completion panel | `/learning/courses/{courseId}/completed` |
| My required learning | `/learning/required` |

With the Users **My Account** feature (`CrestApps.OrchardCore.Users.Account`) on, the learner pages render inside the account frame and appear in its menu. **My courses** is added to Orchard Core's user menu for people with **Access learning**.

## Permissions

| Permission | Display name | Implied by | Default |
|---|---|---|---|
| `ManageCourses` | Manage courses | | Administrator |
| `ManageOwnCourses` | Manage the courses you teach | `ManageCourses` | Administrator |
| `ManageEnrollments` | Manage enrollments | | Administrator |
| `ViewEnrollments` | View enrollments | `ManageEnrollments` | Administrator |
| `ViewLearnerRecords` | View learner records | `ManageEnrollments` | Administrator |
| `ViewLearningReports` | View learning reports | | Administrator |
| `ExportLearnerRecords` | Export learner records | | Administrator |
| `AccessLearning` | Access learning | | Administrator, Authenticated |

Course Instructors and Assistants who hold **Manage the courses you teach** may edit, publish and preview their course and its lessons and manage its enrollments; Graders and Observers see their course's records.

## Recipes and Deployment

The `LearningEnrollments` step imports enrollments with progress. Users are matched by `UserName` (a missing user is reported and skipped); `ItemId` keeps the id so a second run updates. `CourseKeys` carries enrollment key hashes.

```json
{
  "steps": [
    {
      "name": "LearningEnrollments",
      "CourseKeys": [
        { "CourseId": "4safetycourse000000course1", "Hash": "<hash exported from the source site>" }
      ],
      "Enrollments": [
        {
          "ItemId": "4pj234zj1ypsnvynfa931j3gmx",
          "UserName": "learner1",
          "CourseId": "4safetycourse000000course1",
          "Status": "Active",
          "Number": "E000001",
          "Grants": [ { "Source": "Manual", "SourceId": "admin" } ]
        }
      ]
    }
  ]
}
```

- The **Learning enrollments (learner records)** deployment step (category **Learning**) has `CourseId` (empty for every course), `FromUtc`, `ToUtc` and `IncludeProgress`; it needs **Export learner records** and is written to the Audit Trail.
- `LearningRequirements` (required learning rules) is in [programs](references/learning-programs.md); coursework submissions (`LearningSubmissions`) are in [activities](references/learning-activities.md).
- Courses, lessons, categories, cohorts on a course (`LearningCohortsPart`) and learning paths move with the `Content` and `ContentDefinition` steps. With CrestApps Recipes on, every Learning step publishes a JSON schema.

## Workflows, Webhooks, Reports and Data

| Area | What Learning adds |
|---|---|
| Workflow events | Enrollment Requested, Learner Enrolled, Enrollment Status Changed, Course Started, Course Item Completed, Course Completed, Access Expiring, Learner Inactive. Each can be limited to courses and ignores changes made by a workflow unless told otherwise. |
| Workflow tasks | Enroll User, Approve Enrollment, Reject Enrollment, Change Enrollment Status, Extend Access or Due Date, Mark Course Item Complete. Writes made earlier in the run are committed with the enrollment. |
| Webhooks | `learning.enrollment.created`, `learning.course.completed` (assessments add `learning.attempt.submitted` and `learning.attempt.graded`). |
| Reports (Reports feature) | **Course completion** (`learning-course-completion`), **Learner transcript** (`learning-transcript`), **Inactive learners** (`learning-inactive-learners`); need **View learning reports**, export needs **Export learner records**. |
| Data Pipelines | The `Learning` data source (`LearningEnrollments`, `LearningItemProgress`, `LearningCompletions`, `LearningCourseStaff`) and the **Enroll learners** step (`LearningEnrollLearners`). |
| Notifications | Enrolled, Enrollment request declined, Course due soon, Course overdue, Completion needs review, Enrollment request to approve (types in `LearningConstants.NotificationTypes`). |
| Other | Audit Trail (manual changes and exports), Personal Data (export and erasure), Staff (Instructor capability, **Learning** section on the staff profile), Advanced SEO (schema.org `Course`). |

## Background Tasks and Settings

| Task | Schedule | Does |
|---|---|---|
| Learning upkeep | `15 * * * *` | Expires enrollments whose access ended; raises access ending, inactive, due soon and overdue once per enrollment |
| Learning cohort catch-up | `45 * * * *` | Enrolls linked cohort members the sync missed, ends what a cohort gave people who left |
| Required learning upkeep | `15 * * * *` | Runs each rule once a day and sends reminders and escalations |
| Learning class follow-ups | `*/2 * * * *` | Records attendance and shift changes that failed at the time |
| Learning purchase fulfilment | `*/2 * * * *` | Grants paid purchases and takes back refunded ones |

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Learning": {
        "Upkeep": {
          "AccessEndingNoticeDays": 7,
          "InactiveDays": 14,
          "DueSoonDays": 3
        },
        "Coursework": {
          "MaxFilesPerSubmission": 20,
          "MaxFileSizeMb": 50
        }
      }
    }
  }
}
```

Zero turns an upkeep notice off. Learning never changes Media limits: raise `OrchardCore:OrchardCore_Media:MaxFileSize` and `AllowedFileExtensions` (it replaces the list, keep the defaults you need plus `.vtt`, `.srt`) for training videos.

## Extension Points

| Registration | Use it to |
|---|---|
| `AddCourseItemKind<TPart>(name, configure)` | Add a kind of course item (`CourseItemKindDescriptor`: `DisplayName`, `ContentType`, `CompletionModes`, `ProducesGrade`, `PlayerShape`, `Icon`, `Inline`, `GradableCompletesWhenGraded`) |
| `AddEnrollmentSource(name, displayName)` | Name a grant source such as an order or a partner |
| `AddCourseItemReleaseRule<T>()` | `ICourseItemReleaseRule.EvaluateAsync(ReleaseRuleContext)` returns `ReleaseDecision.Released`, `Locked(reason, availableFromUtc)` or `WaitsFor(reason, blockingItemId)` |
| `AddCourseCompletionCriterion<T>()` | `ICourseCompletionCriterion.IsMetAsync(CompletionCriterionContext)` |
| `AddEnrollmentObserver<T>()` | `IEnrollmentObserver` (requested, enrolled, status changed, started, item completed, completed, extended, access ending, inactive, due soon, overdue, review needed, review cleared), after the commit |
| `AddCourseItemHandler<T>()` | `ICourseItemHandler.StartedAsync` / `CompletedAsync` |
| `AddCohortLearningTarget<T>()` | Something a cohort can be linked to (`ICohortLearningTarget`) |
| `services.AddScoped<ICourseCompletionLinkProvider, T>()` | Add links to the course completion panel |
| `ICoursePrerequisiteEvaluator`, `IEnrollmentReviewActionProvider`, `ILearnerRecordExportObserver` | Prerequisites, actions on the review card, hearing exports |

### Release an Item on Weekdays Only

```csharp
public sealed class WeekdayReleaseRule : ICourseItemReleaseRule
{
    public string Name => "Weekday";

    public Task<ReleaseDecision> EvaluateAsync(ReleaseRuleContext context)
    {
        var day = context.UtcNow.DayOfWeek;

        return Task.FromResult(day is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? ReleaseDecision.Locked(LearningRefusals.OpensLater)
            : ReleaseDecision.Released);
    }
}

// Startup: services.AddCourseItemReleaseRule<WeekdayReleaseRule>();
```

### React to Completions

```csharp
public sealed class CrmCompletionObserver : IEnrollmentObserver
{
    private readonly ILogger _logger;

    public CrmCompletionObserver(ILogger<CrmCompletionObserver> logger)
    {
        _logger = logger;
    }

    public Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        // Runs after the commit. Queue outside calls as deferred tasks and stay idempotent: an event can be lost.
        _logger.LogInformation("Enrollment {Number} completed with grade {Grade}.", enrollment.Number, enrollment.Grade);

        return Task.CompletedTask;
    }
}

// Startup: services.AddEnrollmentObserver<CrmCompletionObserver>();
```

## Troubleshooting

- **A learner gets 404 on a lesson**: the enrollment is not active or completed, the item is not released (release rules, `ItemsInOrder`, course start date), or the course item is not published. Course staff and free previews bypass release.
- **Lesson files open for everyone**: Secure Media is off, or a role holds **View media content in all folders**. Run **Learning public media** instead of granting that permission.
- **A service call returns `Busy`**: the enrollment lock was not obtained in time; nothing changed, retry.
- **Writes from my workflow were committed even though the run failed later**: enrollment calls commit the whole session; that is by design.
- **Self enrollment button missing**: enable Self Enrollment and set the course `EnrollmentMode` to `Open`, `Key` or `Approval`.
- **Course outline lists items as not placed**: they are contained in the course but not in a `CourseOutlinePart` section.
- **Cohort change not applied**: the person who published (or `LinkedBy`) lacks **Manage enrollments**; the change is logged and skipped.

Related skills: `orchardcore-learning-assessments`, `orchardcore-learning-certifications`, `orchardcore-learning-standards`, `orchardcore-crestapps-users` (audiences, cohorts, My Account), `orchardcore-scheduling`, `orchardcore-products`, `orchardcore-orders`, `orchardcore-ai-tools`, `orchardcore-reports`, `orchardcore-webhooks`.

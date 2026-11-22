# Learning Records, Access and Learner Pages

Supporting detail for `orchardcore-learning`.

## Records and Indexes

All records live in the `Learning` YesSql collection.

| Record | What it holds | Index |
|---|---|---|
| `Enrollment` | `UserId`, `CourseId`, `OfferingId`, `Status`, `Number` (series `LearningEnrollment`), `Cycle`, `PreviousEnrollmentId`, `StartedUtc`, `AccessEndsUtc`, `DueUtc`, `CompletedUtc`, `LastActivityUtc`, `ProgressPercent`, `Grade`, `Passed`, `Grants`, `History` (every status change with actor and reason), `Completion` (snapshot of course title, version and how each item was completed), `Review` | `EnrollmentIndex` |
| `EnrollmentGrant` | Why a person has access: `Source`, `SourceId` | `EnrollmentGrantIndex` |
| `EnrollmentProgress` | One per enrollment; per item: state, first and last access, seconds spent, share watched, score, attempts, completed when, by whom and why, acknowledged statement | `EnrollmentProgressIndex`, `CourseItemProgressIndex` |
| Course staff | From `CoursePart.Staff` | `CourseStaffIndex` |
| Imported sources | `CoursePart.SourceIdentifier`, `CourseItemPart.SourceIdentifier` (for example `moodle:<site>:course:12`) | `LearningSourceIndex` |

A person has at most one open enrollment per course and offering, so enrolling twice from the same source changes nothing.

## Writes, Locks and Commits

- Every change to an enrollment or its progress runs under one distributed lock per enrollment (`EnrollmentWriteGate`): the enrollment is read again inside the lock, the session is committed before the lock is released, and observers run after the commit, only for what changed.
- Enrolling takes a lock per learner and course and commits inside it, so two requests never create two enrollments. A call that cannot get the lock returns `Busy` and changes nothing.
- The commit is the whole session's. Workflow tasks commit earlier writes of the run; the **Enroll learners** pipeline step commits each row; the `LearningEnrollments` recipe step does not use the gate (the recipe's transaction applies); an order handler must enroll after the order is saved, never inside it.
- Observers that send something out (webhooks, notifications, workflow events) queue deferred tasks and swallow their own failures. Anything that must not miss an event (certificates, credentials) is idempotent and has a catch-up task.

## Progress Tracking

`ILearnerProgressService` records opening an item, time spent, video share watched, marking done and acknowledging. The player sends a heartbeat when an item opens, every 30 seconds while visible and once more with `navigator.sendBeacon` when the learner leaves; each call counts at most 60 seconds and never more than the time since the last access. Staff with **Manage enrollments** can complete an item by hand with a reason.

Learner endpoints (all need the learner's own enrollment): `POST learning/progress/{enrollmentId}/{itemId}/heartbeat`, `/viewed`, `/done`, `/acknowledge`, and `POST learning/courses/{courseId}/enroll`.

### Completion Criteria and Approval

`MinimumGrade`, `MinimumTime`, `ItemPassed`, `StaffApproval`. With a `Role` value, only course staff in that role may approve (`CompletionApprovers.MayApprove`; others get `NotApprover`), and the approver still needs **Manage enrollments** on the enrollment, which course staff get only as Instructors or Assistants holding **Manage the courses you teach**.

### Release Rules and Course Dates

- `CoursePart.ItemsInOrder` makes each item wait for every required, non-inline item before it; `CourseReleaseEvaluator` checks it before the rules, so the player, progress service, package launch, surveys, coursework and quiz start all refuse alike. `ReleaseDecision.BlockingItemId` drives the *Complete "..." first* button.
- `People` opens an item only to named users and the members of a User Audience; without the User Audiences feature an audience opens nothing.
- Before `StartsUtc`, learners may enroll but items are refused with `CourseNotStarted`. After `EndsUtc` nothing is locked (Moodle behavior): the course shows **Ended on**.
- `AccessDays` sets the access end of self enrollments only; other sources keep the access end they give.

### Reviews After Completion

A completed enrollment is a record and does not change. Work on items not yet completed is still recorded while the learner may review the course (a grant remains and access has not ended). When a result changes after completion, `RecordResultAsync` sets `Enrollment.Review`, observers hear `ReviewNeededAsync`, the enrollment list filters by **Needs review**, and `IEnrollmentService.ClearReviewAsync` closes it with a note. Clearing raises `ReviewClearedAsync`, and what is issued on completion (credentials, education credits, learning paths, certification paths) is checked again, idempotently.

## Who May Open What

- **Content access handler** (`ViewContent` for course items): allowed for course staff, course managers, free previews and learners with an active or completed enrollment whose item is released.
- **Record handler**: a learner sees their own enrollments and progress, a manager sees everyone's, course staff see their course's.

| Path that does not check each item | What a person who is not enrolled gets |
|---|---|
| Search results | Title and link only; course items render without body, files, video or link in `Summary`. The catalog index holds courses only. |
| List RSS feed | Nothing: Learning turns off the List feed of courses. |
| Queries API | Nothing unless you grant a query's permission; a query returning lessons hands their full documents to its grantees. |
| Sitemaps | Address and title, if a sitemap lists lesson types. |
| Liquid templates | Whatever the template reads; templates run with their author's trust. |

### Lesson Files

With Secure Media on, files attached through a media field are stored under `mediafields/{type}/{contentItemId}/` and each request asks `ViewContent` on the lesson (404 otherwise). Learners see original file names (`AttachedFileNames`, `LearningFileNames.GetDisplayName`) through the `MediaField-LearningFiles` template. Files outside lesson folders then need **View media content in the root folder** or a folder permission.

## Learner Pages

- **My learning** (`/learning`): one card per course with cover (`Cover` field or a generated pattern), first category, "3 of 7 complete", due date and next step. `status` is `inprogress`, `notstarted` or `completed`; `q` searches titles and codes; `view=list` shows rows.
- **Course page**: `CoursePart_Hero` (Detail, `Header:after`) with summary, facts and the one action (**Sign in to enroll**, **Enroll**, key, **Ask to enroll**, **Continue where you left off**, or the completion panel); `CoursePart_Learner` (`Content:20`) is the outline.
- **Player**: the `learning-player` script tracks time and video, refreshes parts marked `data-learning-refresh` (`index`, `outline`, `nav`, `condition`) when the `learning:item-completed` DOM event fires, and refuses progress for an enrollment that is not the signed-in person's (404). `_ItemCondition` shows *To do*, *Done* or *Not passed* from `CourseItemConditionText`.
- **Completion panel** (`/learning/courses/{courseId}/completed`, `_CourseCompletedPanel`): date, grade, links from every `ICourseCompletionLinkProvider` and **My transcript**.
- **User menu**: `LearningUserMenuDisplayDriver` adds **My courses** (`UserMenuItems__LearningCourses`, override `UserMenuItems-LearningCourses.cshtml`) with up to five active courses.
- **Catalog**: `/learning/catalog` (route `LearningCatalog`, needs **View content**), 24 per page, title search and category tree. Full-text search uses the **Learning course catalog** recipe's `courses` index.

```csharp
public sealed class SurveyCompletionLinkProvider : ICourseCompletionLinkProvider
{
    public Task<IEnumerable<CourseCompletionLink>> GetLinksAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        IEnumerable<CourseCompletionLink> links =
        [
            new CourseCompletionLink
            {
                Text = "Tell us how it went",
                Url = $"~/feedback?course={enrollment.CourseId}",
                Icon = "fa-solid fa-comment",
                Position = 10,
            },
        ];

        return Task.FromResult(links);
    }
}

// Startup: services.AddScoped<ICourseCompletionLinkProvider, SurveyCompletionLinkProvider>();
```

## Course Pickers

Admin course pickers (prerequisites, path sets, workflow events, product grants) use `ItemSelector` with `/Admin/learning/course-picker/search?query=&exceptId=` (route `LearningCourseSearch`): latest versions whose title contains the text, 50 at most. It needs **Manage the courses you teach**, **View enrollments** or **View learning reports**.

## Course Categories

The **Course Categories** feature adds a `Category` taxonomy field on the Course type, creates the **Course categories** taxonomy (term type `CourseCategory`, alias `course-categories`) and adds it to the admin content list's taxonomy filters. An existing taxonomy titled **Course categories** or an existing taxonomy field on the Course part is reused (`CourseCategorySetup`). `CourseCategoryService` (`Learning.Core`) reads, finds and creates categories. No new records: taxonomy, terms and field move with `Content` and `ContentDefinition`.

## Enrollment by Cohort

- The course editor (and the learning path editor with Learning Paths) gets a **Cohorts** card for people with **Manage enrollments**; choices are stored in `LearningCohortsPart.CohortIds` with `LinkedBy`.
- On publish, a deferred task compares the cohorts with those applied before (`CohortLearningLink`, `CohortLearningLinkIndex`): new cohort members are enrolled with grant source `Cohort` (`CohortEnrollmentGrants.SourceId`), members of an unlinked cohort lose only that grant, and `ICohortMembershipObserver` handles later joins and leaves.
- A change is applied only when the publisher (or `LinkedBy`) holds **Manage enrollments**; a publish without a person (recipe, setup) is the site's own.
- A linked cohort cannot be deleted (`ICohortUsage`). To target cohorts for required learning, use an audience with the `CohortMember` rule.
- Add another target with `services.AddCohortLearningTarget<T>()` (`ICohortLearningTarget`, including `ListJoinedUserIdsAsync` for the hourly catch-up).

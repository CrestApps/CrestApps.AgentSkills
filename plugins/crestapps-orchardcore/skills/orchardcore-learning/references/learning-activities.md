# Learning Activities: Classes, Coursework and Surveys

Supporting detail for `orchardcore-learning`. Each module adds a course item kind; add the item to a course outline like any lesson.

| Feature | ID | Depends on | Content types | Kind |
|---|---|---|---|---|
| Learning Classes | `CrestApps.OrchardCore.Learning.Classes` | Learning, Scheduling (`CrestApps.OrchardCore.Scheduling`) | `LearningClass` (`TitlePart`, `ClassPart`, `CourseItemPart`) | `Class` |
| Learning Coursework | `CrestApps.OrchardCore.Learning.Coursework` | Learning, Learning Gradebook | `CourseworkLesson` (`TitlePart`, `HtmlBodyPart`, `CourseworkPart`, `CourseItemPart`), `Rubric` (`TitlePart`, `RubricPart`) | `Coursework` |
| Learning Surveys | `CrestApps.OrchardCore.Learning.Surveys` | Learning | `FeedbackSurvey`, `Choice` (`TitlePart`, `HtmlBodyPart`, `SurveyPart`, `CourseItemPart`) | `Survey` |

None of these modules adds a Learning menu entry: their pages are linked from the item's editor.

## Classes

A class is attended in dated sessions that belong to Scheduling; Learning keeps no seats or waitlist of its own.

| `ClassPart` | Meaning |
|---|---|
| `AppointmentTypeId` | Scheduling appointment type whose rules registrations follow (required for sessions) |
| `ResourceIds` | Scheduling resources for new sessions; the first one's calendar holds the session event |
| `Capacity`, `Location`, `MeetingUrl`, `WaitlistMode` | Defaults of new sessions |
| `MinimumMinutes` | Minutes a partial attendance needs to complete the class; empty means only full attendance |

- Completion mode defaults to `Attended` (`MarkedDone` is the alternative).
- A session is a Scheduling `SchedulingSession` with source `Learning.Class` and the class item id as source id. Meetings also show on each confirmed learner's own calendar through the `LearningClassSessions` calendar event source.
- `IClassRegistrationService.RegisterAsync` registers a learner with an active enrollment (one place per class). Learner forms post to `/learning/classes/{classItemId}/register`, `/accept` and `/cancel`. An enrollment that ends without completion gives its places up.
- Attendance is recorded on the roster through `ISchedulingSessionService.RecordAttendanceAsync`; an attendance that counts completes the item through `IAppointmentObserver`.
- With Workforce on, `ClassShiftSegmentObserver` places a **Training** segment on a staff learner's published shift for each confirmed meeting and removes it when the place is cancelled.
- Notifications: **Class session cancelled** (`LearningClassSessionCancelled`), **Class place free** (`LearningClassPlaceOffered`), **Class place given** (`LearningClassRegistered`), **Class session moved** (`LearningClassSessionMoved`).

| Page | Address | Permission |
|---|---|---|
| Sessions of a class | `/Admin/learning/classes/{classItemId}/sessions` | `ManageLearningClasses` or course staff |
| Session editor | `/Admin/learning/classes/{classItemId}/sessions/new`, `/Admin/learning/class-sessions/{id}/edit` | same |
| Cancel a session | `/Admin/learning/class-sessions/{id}/cancel` | same |
| Roster and attendance | `/Admin/learning/class-sessions/{id}/roster` | same |

`ManageLearningClasses` (**Manage learning classes**) is implied by **Manage courses**; Instructors and Assistants with **Manage the courses you teach** manage their own classes. Course offerings are not built yet.

## Coursework

Work learners hand in as text and/or files, graded against a rubric or out of points; the grade counts in the course grade and waits in **Learning > Grading** beside quiz attempts.

| `CourseworkPart` | Meaning |
|---|---|
| `DueUtc` | Due date (entered in the editor's time zone) |
| `LatePenaltyPercentPerDay`, `MaxLatePenaltyPercent`, `CloseAfterDays` | Late policy: days late (part of a day counts whole) times penalty, capped; final score `raw x (1 - penalty / 100)` (`CourseworkLatePolicy`) |
| `SubmissionType` | `Text`, `Files`, `TextAndFiles` (default) or `SignOff` (a grader signs off, nothing handed in) |
| `AllowedExtensions`, `MaxFileSizeMb`, `MaxFiles` | File rules, up to `CourseworkOptions` (50 MB, 20 files by default) |
| `AllowResubmission` | Hand in again until graded |
| `RubricId`, `MaxPoints` | Grade with a published rubric, or out of points |

- New items are gradable with completion mode `Graded`; `Submitted` completes on hand-in and a later grade still counts (a changed outcome flags the completed enrollment for review).
- Rubric scoring: one level per criterion, total of chosen points over the sum of best levels, as a percentage (`RubricScoring.Score`).
- `ISubmissionService.SubmitAsync` accepts work only under the signed-in learner's active enrollment; files go to the protected file store first. Form: `POST /learning/coursework/{courseworkId}/submit`; files open at `/learning/coursework/submissions/{id}/files/{fileId}`.
- `GradeAsync` reports a staff result through `ILearnerProgressService.RecordResultAsync`. Regrading or returning graded work needs **Override grades** and a reason; nobody grades their own work. Grading rights follow `GradingScope` (**Grade assessments**, or course Graders, Instructors and Assistants with **Manage the courses you teach**).
- Coursework adds no permission. Submissions page: `/Admin/learning/coursework/{courseworkId}/submissions`; grading page: `/Admin/learning/coursework/submissions/{id}`.
- Behind IIS raise `requestLimits maxAllowedContentLength` to the largest hand-in (all files at the max size plus 16 MB).

| Area | What it adds |
|---|---|
| Workflows | **Coursework Submitted** and **Coursework Graded** events (input `SubmissionId`, `UserId`, `CourseworkId`, `CourseId`, `EnrollmentId`, `Attempt`, `Status`, `Late`, `Score`, `Points`, `MaxPoints`, `SubmittedUtc`, `Actor`; graded adds `Regraded`, `PreviousScore`, `Reason`); **Return Coursework For Resubmission** task (`SubmissionId`, `Feedback`, Liquid; outcomes Done and Failed) |
| Notifications | **Coursework graded** (`LearningCourseworkGraded`), **Coursework returned** (`LearningCourseworkReturned`) |
| Data source | `LearningCoursework` with a **Submissions** data set (no text, files, feedback or e-mail), needs **View learning reports** |
| Recipe and deployment | `LearningSubmissions` step with a `Submissions` array (people as `UserName`, `GraderUserName`, `ReturnedByUserName`); the **Learning coursework submissions (learner records)** deployment step is opt-in, can be limited to one coursework item and needs **Export learner records**. Files never travel. |
| Extension | `services.AddSubmissionObserver<T>()`; submissions reach the grading list through `IGradingQueueSource` |

## Surveys

Feedback surveys and choices (Moodle Feedback and Choice) are never scored. Kind `Survey`, completion modes `Submitted` (default), `Viewed`, `MarkedDone`.

| `SurveyPart` | Meaning |
|---|---|
| `Questions` | `Id` (stable), `Text`, `Type` (`SingleChoice`, `MultipleChoice`, `Rating` 1 to 5, `Text`), `Options` (up to 50), `Required`; a `Choice` (`SurveyPartSettings.SingleQuestion`) has exactly one choice question |
| `Anonymous` | Totals list written answers without names |
| `AllowChangeAnswer` | Answer again until close |
| `CloseUtc` | Stops taking answers |
| `ResultsVisibility` | `Never`, `AfterAnswer`, `AfterClose`, `Always` |

- `ISurveyResponseService.AnswerAsync` writes one `SurveyResponse` per learner and survey (collection `LearningSurveyResponses`, `SurveyResponseIndex`) under a lock, then reports `Submitted`. Refusals are `SurveyErrors` (`NotSurvey`, `NotEnrolled`, `AccessEnded`, `NotAvailableYet`, `Closed`, `AlreadyAnswered`, `Required`, `InvalidAnswer`, `Busy`).
- The player posts to `learning/surveys/{surveyId}/answer` (route `LearningSurveyAnswer`), one field per question named `q.<questionId>`; needs **Access learning**.
- Totals page: `/Admin/learning/surveys/{surveyId}/results` (route `LearningSurveyResults`) for **Manage courses** or course staff with **Manage the courses you teach**.
- No recipe step, data source or workflow events for responses; personal data erasure deletes responses.

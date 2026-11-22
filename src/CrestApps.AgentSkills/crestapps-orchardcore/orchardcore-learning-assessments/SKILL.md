---
name: orchardcore-learning-assessments
description: Skill for CrestApps Orchard Core Learning Assessments (CrestApps.OrchardCore.Learning.Assessments). Covers question banks and the thirteen question types, the Quiz course item with fixed questions and random draw rules, attempts kept and scored on the server, time limits and windows, review options, immediate feedback, access codes, overrides, the grading list, regrades and voids, grade scales and the course gradebook with weighted grade categories, QTI 3.0 and 2.1 import, Moodle backup import, the LearningGradeScales and LearningAttempts recipe steps, quiz workflow activities and webhooks. Use this skill when requests mention quizzes, exams, question banks, attempts, grading, gradebook, grade categories, QTI or Moodle import in CrestApps Learning. Strong matches include QuizPart, QuestionPart, IAssessmentAttemptService, IQuestionScorer, AddQuestionType, IAttemptObserver, IGradingQueueSource, GradingScope, CourseGradeCalculator and AttemptReviewPolicy.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Learning Assessments

## Quizzes, Question Banks and Grading for Learning Courses

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for quizzes in CrestApps Learning courses. Questions live in banks as content items; an attempt copies every question it asks (prompt, choices, key, points, feedback), so editing or deleting a question never changes a submitted attempt. Courses, enrollments and progress are covered by `orchardcore-learning`.

### Guidelines

- Install `CrestApps.OrchardCore.Learning.Assessments` (or `CrestApps.OrchardCore.Cms.Core.Targets`) in the web/startup project. Reference `CrestApps.OrchardCore.Learning.Assessments.Abstractions` or `...Assessments.Core` from your module.
- Questions and banks are private: only people who manage or view question banks pass `ViewContent`, so the display route, content API, GraphQL and data sources never return a key. Keep question types out of AI and search indexes.
- Every write to an attempt runs under one lock per attempt, commits before release, and raises `IAttemptObserver` after the commit. Quiz results reach the enrollment through `ILearnerProgressService.RecordResultAsync`.
- Nobody grades, regrades or voids their own attempt.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Learning Assessments | `CrestApps.OrchardCore.Learning.Assessments` | Learning |
| Learning Gradebook | `CrestApps.OrchardCore.Learning.Assessments.Gradebook` | Learning Assessments |
| Learning QTI Import | `CrestApps.OrchardCore.Learning.Assessments.QtiImport` | Learning Assessments, Content Transfer (`CrestApps.OrchardCore.ContentTransfer`), `OrchardCore.Media` |
| Learning Moodle Import | `CrestApps.OrchardCore.Learning.Assessments.MoodleImport` | Learning Assessments, Learning SCORM Packages, Course Categories, Content Transfer, `OrchardCore.Media` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Learning.Assessments",
        "CrestApps.OrchardCore.Learning.Assessments.Gradebook"
      ],
      "disable": []
    }
  ]
}
```

## Content Types

| Type | Parts | Question type name |
|---|---|---|
| `QuestionBank` | List of questions | |
| `SingleChoiceQuestion`, `MultipleChoiceQuestion` | `QuestionPart`, `ChoiceQuestionPart` (`Options` of `{ Id, Text, Correct, Feedback }`, `ShuffleOptions`, `PartialCredit`) | `SingleChoice`, `MultipleChoice` |
| `TrueFalseQuestion` | `QuestionPart`, `TrueFalseQuestionPart` (`Answer`) | `TrueFalse` |
| `ShortTextQuestion`, `NumericQuestion` | `QuestionPart` and the type's part | `ShortText`, `Numeric` |
| `MatchingQuestion`, `OrderingQuestion`, `FillInBlanksQuestion` | `QuestionPart` and the type's part | `Matching`, `Ordering`, `FillInBlanks` |
| `EssayQuestion` | `QuestionPart`, `EssayQuestionPart` (manually graded) | `Essay` |
| `SurveyQuestion` | `QuestionPart`, `SurveyQuestionPart` (every answer accepted; 0 points means ungraded) | `Survey` |
| `DragIntoTextQuestion`, `GapSelectQuestion` | `QuestionPart`, `ChoiceBlanksQuestionPart` (`[[word]]` blanks, distractors) | `DragIntoText`, `GapSelect` |
| `DescriptionQuestion` | `QuestionPart` only (text between questions, no points) | `Description` |
| `QuizLesson` | `QuizPart`, `CourseItemPart` (kind `Quiz`) | |

`QuestionPart`: `Prompt`, `Points` (default 1), `Difficulty` (1 to 5, default 3), `Tags`, `GeneralFeedback`, `SourceIdentifier`, `SourceFile`.

| `QuizPart` | Meaning |
|---|---|
| `QuestionIds` | Fixed questions in order (picked through `/Admin/learning/questions/search`) |
| `DrawRules` | `{ BankId, Tag, Difficulty, Count }` random draws (`QuizDrawRuleMatcher`) |
| `ShuffleQuestions`, `QuestionsPerPage` | Order and paging (`AttemptPaging`) |
| `TimeLimitMinutes`, `OpensUtc`, `ClosesUtc` | Deadline kept on the server, capped by the window |
| `MaxAttempts` (default 1), `DelayMinutes`, `GradingMethod` | `Highest`, `Latest`, `Average`, `First` |
| `PassMark` | Percent to pass |
| `Navigation` | `Free` or `Sequential` |
| `FeedbackMode` | `Deferred` or `Immediate` (a **Check** button per question) |
| `ReviewPolicy`, `ReviewOptions` | `ScoreOnly`, `AfterGraded` (default), `AfterClose`, `Hidden`; `QuizReviewOptions` sets what shows during, right after, later and after close |
| `HonorStatement` | Must be accepted to start |

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4safetyquiz00000000000quiz",
          "ContentType": "QuizLesson",
          "DisplayText": "Safety check",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Safety check" },
          "CourseItemPart": { "Kind": "Quiz", "CompletionMode": "Passed", "Gradable": true, "Weight": 1, "PassMark": 70 },
          "QuizPart": {
            "DrawRules": [ { "BankId": "<question bank id>", "Tag": "ppe", "Count": 5 } ],
            "TimeLimitMinutes": 15,
            "MaxAttempts": 3,
            "DelayMinutes": 10,
            "PassMark": 70,
            "GradingMethod": "Highest",
            "Navigation": "Free",
            "FeedbackMode": "Deferred",
            "ReviewPolicy": "AfterGraded"
          },
          "ContainedPart": { "ListContentItemId": "<course id>", "Order": 1 }
        }
      ]
    }
  ]
}
```

A quiz access code is stored as a salted hash in `LearningSecretStore` (kind `QuizAccessCode`), never in the content item; five wrong codes in 15 minutes lock the learner out for 15 minutes.

## Attempts

`IAssessmentAttemptService` runs every attempt on the server: **Start** checks enrollment, release, window, overrides, attempts left, delay, access code and honor statement, then draws and copies the questions; answers are saved one question at a time (`AttemptNavigation.CanAnswer` locks pages left behind on a sequential quiz); **Check** (`learning/attempts/{attemptId}/check/{index}`) scores one answer under immediate feedback; **Submit** scores everything, and an essay leaves the attempt `NeedsGrading`.

Statuses (`AttemptStatus`): `InProgress`, `Submitted`, `NeedsGrading`, `Graded`, `Abandoned`, `Voided`. The **Learning attempt deadlines** background task (`* * * * *`) submits attempts whose time is up. When a published quiz's close date moves, `QuizCloseDateHandler` moves the close date kept by its attempts.

Learner routes: `POST learning/quizzes/{quizId}/start`, `GET learning/attempts/{attemptId}`, `POST learning/attempts/{attemptId}/answers/{index}`, `/check/{index}`, `/submit`.

`AttemptReviewPolicy` (`Shows`, `ShowsFor`, `CanReview`, `CanSeeScore`) decides what the review shows. The attempt page never carries the key while open, except for a checked question whose review options show right answers during the attempt.

## Grading and the Gradebook

- **Grading** (`/Admin/learning/grading`) lists everything waiting for a grader in the courses the person grades, oldest first, merged from every `IGradingQueueSource`. Grade, regrade (scores again with the kept key) and void keep who, when and why in `ScoreHistory`.
- `GradingScope` (in `Learning.Assessments.Core`): **Grade assessments** grades every course; course Graders, Instructors and Assistants grade their course only with **Manage the courses you teach**.
- **Overrides** per quiz (`/Admin/learning/quizzes/{quizId}/overrides`): extra time, attempts or another window, with **Manage assessment overrides**.
- **Gradebook** (`/Admin/learning/gradebook`, Learning Gradebook) shows each learner's item scores, category scores and course grade, as percentages or on a grade scale.
- `CourseGradeCalculator` (in `Learning.Core`): only gradable items with weight above zero count; a missing score counts as zero. Without categories the grade is the weighted mean of items. With `CoursePart.GradeCategories` (`Id`, `Name`, `Weight`, `DropLowest`), each category is the weighted mean of its items after dropping the lowest, and the course grade is the weighted mean of categories; uncategorized items form **No category** with the sum of their weights. `ComputeBreakdown` returns `CourseGradeBreakdown`.

## Admin Menu

| Menu | Page | Permission |
|---|---|---|
| **Learning > Question banks** | Content list filtered to `QuestionBank` | `ManageQuestionBanks` |
| **Learning > Grading** | `/Admin/learning/grading` | `GradeAssessments`, else `ManageOwnCourses` |
| **Learning > Gradebook** | `/Admin/learning/gradebook` | `ViewLearnerRecords`, else `ManageOwnCourses` |
| **Learning > Grade scales** | `/Admin/learning/grade-scales` | `ManageGradeScales` |

## Permissions

| Permission | Display name | Implied by |
|---|---|---|
| `ManageQuestionBanks` | Manage question banks | |
| `ViewQuestionBanks` | View question banks and answer keys | `ManageQuestionBanks` |
| `GradeAssessments` | Grade assessments | |
| `OverrideGrades` | Override grades | |
| `ManageAssessmentOverrides` | Manage assessment overrides | |
| `ManageGradeScales` | Manage grade scales | |

## Recipes and Deployment

```json
{
  "steps": [
    {
      "name": "LearningGradeScales",
      "GradeScales": [
        {
          "ItemId": "4lettergrades0000000000001",
          "Name": "Letter grades",
          "Bands": [
            { "MinPercent": 90, "Label": "A", "Passing": true },
            { "MinPercent": 80, "Label": "B", "Passing": true },
            { "MinPercent": 70, "Label": "C", "Passing": true },
            { "MinPercent": 0, "Label": "F", "Passing": false }
          ]
        }
      ]
    }
  ]
}
```

- `LearningGradeScales` updates by `ItemId`; a scale without a name or bands is skipped. Deployment step **Learning grade scales**.
- `LearningAttempts` carries `Attempts` (learner by `UserName`, grader by `GradedByUserName`, `ScoreHistory[].ActorUserName`), `Overrides` (`ActorUserName`) and `AccessCodes` (`QuizId`, `Hash`). The **Learning quiz attempts (learner records)** deployment step needs **Export learner records**.
- Questions, banks and quizzes move with the `Content` step. Both steps publish CrestApps Recipes schemas.

## Workflows, Webhooks and Integrations

| Area | What it adds |
|---|---|
| Events | Quiz Attempt Started, Quiz Attempt Submitted, Quiz Attempt Needs Grading, Quiz Attempt Graded, Quiz Score Changed; each limited to chosen quizzes and, by default, not started by a workflow's own change |
| Tasks | Void Quiz Attempt, Regrade Quiz Attempt (required reason, actor `Workflow`) |
| Webhooks | `learning.attempt.submitted`, `learning.attempt.graded` (scores, never answers) |
| Notifications | **Quiz graded** (`LearningAttemptGraded`) when a person grades an attempt |
| Data source | `LearningAssessments` with `LearningAttempts`, `LearningAttemptAnswers`, `LearningAttemptScoreChanges`, `LearningQuizOverrides`; never keys or feedback; needs **View learning reports** |
| Report | **Quiz results and item analysis** (`learning-quiz-item-analysis`): pass rates, difficulty, discrimination (top vs bottom 27%) |
| Audit Trail, Personal Data | Manual grading, score changes and overrides; attempts and overrides exported, anonymizing keeps only attempts a completed enrollment rests on |
| Content Transfer | Questions in spreadsheets (`Options` with `*` for right answers, `Pairs`, `Items`, `Text` with `[[answer]]` blanks whose alternatives are separated by a pipe), and the QTI and Moodle formats; see [QTI and Moodle import](references/qti-and-moodle-import.md) |

## Extension Points

| Registration | Use it to |
|---|---|
| `services.AddQuestionType<TScorer>(name, d => { d.ContentType = ...; d.DisplayName = ...; d.ManuallyGraded = ...; d.MayBeUngraded = ...; d.Informational = ...; })` | Add a question type scored by `IQuestionScorer` (`Snapshot(question, item, random)`, `Score(item)`) |
| `services.AddAttemptObserver<T>()` | `IAttemptObserver` (started, submitted, needs grading, graded, score changed, override granted) after the commit |
| `services.AddGradingQueueSource<T>()` | Put your own waiting work in **Learning > Grading** (`IGradingQueueSource.ListWaitingAsync` returns `GradingQueuePage` of `GradingQueueItem`) |

### A Keyword Question Type

```csharp
public sealed class KeywordQuestionPart : ContentPart
{
    public IList<string> Keywords { get; set; } = [];
}

public sealed class KeywordScorer : IQuestionScorer
{
    public string QuestionType => "Keyword";

    public void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var keywords = question.As<KeywordQuestionPart>()?.Keywords ?? [];

        // The attempt keeps its own copy of the key; it never reads the question again.
        item.Key.Texts = [.. keywords];
    }

    public decimal? Score(AttemptItem item)
    {
        var answer = item.Response?.Texts.FirstOrDefault() ?? string.Empty;
        var keywords = item.Key.Texts;

        if (keywords.Count == 0)
        {
            return 0;
        }

        var found = keywords.Count(keyword => answer.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        return Math.Round(item.Points * found / keywords.Count, 2);
    }
}

// Startup:
// services.AddContentPart<KeywordQuestionPart>();
// services.AddQuestionType<KeywordScorer>("Keyword", descriptor =>
// {
//     descriptor.ContentType = "KeywordQuestion";
//     descriptor.DisplayName = LocalizationSource.Create("Keyword", typeof(Startup));
// });
```

Create the `KeywordQuestion` type with `QuestionPart` and `KeywordQuestionPart` in a migration. The attempt page renders a question type it does not know as one text box that fills `Response.Texts`; override `Views/Quiz/_AttemptQuestion.cshtml` in a theme for another input.

### Hear Graded Attempts

```csharp
public sealed class GradedAttemptObserver : IAttemptObserver
{
    private readonly ILogger _logger;

    public GradedAttemptObserver(ILogger<GradedAttemptObserver> logger)
    {
        _logger = logger;
    }

    public Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempt {Number} of quiz {QuizId} scored {Percent}%.", attempt.Number, attempt.QuizId, attempt.Percent);

        return Task.CompletedTask;
    }
}

// Startup: services.AddAttemptObserver<GradedAttemptObserver>();
```

## Troubleshooting

- **A learner cannot start a quiz**: no active enrollment, item not released, outside the window, no attempts left, the delay has not passed, wrong access code, or the honor statement not accepted.
- **Saving the quiz fails on a draw rule**: the rule has no bank, a count below one, or asks for more matching published questions than the bank has.
- **Instructors do not see Grading**: they need **Grade assessments**, or to be Grader, Instructor or Assistant on the course and hold **Manage the courses you teach**.
- **Course grade looks wrong after changing weights**: the stored enrollment grade is recomputed on the learner's next result.
- **Completed enrollment flagged after a regrade**: a lower score or a void after completion sets a review; see `orchardcore-learning`.
- **Grader cannot change a graded score**: **Override grades** is required.

Related skills: `orchardcore-learning`, `orchardcore-learning-certifications`, `orchardcore-learning-standards`, `orchardcore-content-transfer`.

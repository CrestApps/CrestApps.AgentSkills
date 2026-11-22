# Learning AI: Tutor, Tools, Authoring and Scoring Help

Supporting detail for `orchardcore-learning`. Module `CrestApps.OrchardCore.Learning.AI` (constants in `LearningAIConstants`, permissions in `LearningAIPermissions`, both in `Learning.AI.Abstractions`). It reuses AI profiles, profile templates, prompt templates, the AI Chat widget, memory, data sources and usage metering (usage purpose `Learning`); it adds no chat UI of its own.

| Feature | ID | Depends on |
|---|---|---|
| Learning Assistant | `CrestApps.OrchardCore.Learning.AI` | Learning, AI (`CrestApps.OrchardCore.AI`) |
| Learning Authoring Assistant | `CrestApps.OrchardCore.Learning.AI.Authoring` | Learning Assistant |

| Permission | Display name |
|---|---|
| `UseLearningAuthoringAssistant` | Use the learning authoring assistant |
| `UseLearningScoringHelp` | Use scoring help while grading |

## The Tutor

1. Create an AI profile from the **Learning tutor** profile template (id `learning-tutor`) under **Artificial Intelligence > Profiles**. `LearningTutorTemplateApplicationHandler` marks it with `LearningTutorProfileMetadata { IsTutor = true }`; the **Learning tutor** checkbox on the profile's **Knowledge** tab sets the same mark. In a recipe, the `CreateAIProfileFromTemplate` step runs the handler; otherwise set `Properties.LearningTutorProfileMetadata.IsTutor`.
2. Give the profile an AI data source over a content index holding `PageLesson`, `VideoLesson` and `FileLesson`.
3. Turn on the learning assistant switch (`LearningAssistantPart.Enabled`) on each course; it is off by default and a tutor retrieves nothing from a course while it is off.
4. Show the profile with the AI Chat widget, for example in a layer on the learner pages.

`LearningRetrievalFilter` (registered with `services.AddAIDataSourceResultFilter<T>()`) trims every data source search:

- Questions, question banks and quizzes are dropped for every profile and person, including readers of answer keys.
- A tutor keeps only course items whose kind is in `LearningAIOptions.TutorLessonKinds` (page, video, file by default), whose course has the switch on, and that the person may open through an **active enrollment**.
- Other profiles keep course items only when the person may view them and the course switch is on.

`LearningAIOptions` lives in the module project (namespace `CrestApps.OrchardCore.Learning.AI.Services`); kind names are `LearningConstants.CourseItemKinds` values.

```csharp
public sealed class TutorKindsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<LearningAIOptions>(options => options.TutorLessonKinds.Add("Link"));
    }
}
```

## Learning AI Tools

Registered with `AddCoreAITool`, category **Learning**, selectable on any AI profile. Each acts as the signed-in person and refuses anonymous callers.

| Tool | Scope | Returns |
|---|---|---|
| `getMyLearningProgress` | Access learning; own enrollments | Status, progress, required items done, due date, access end, grade. Optional `courseId`. |
| `getMyNextLesson` | Access learning; own enrollments | First open, uncompleted item of an active course with its player link. Optional `courseId`. |
| `explainMyQuizResult` | Access learning; own attempts (registered only with Learning Assessments) | Score and review details only as the quiz review policy allows. `quiz`, optional `attempt`. |
| `listOverdueLearners` | Staff scope | Active enrollments past due. `take` 1 to 50. |
| `listLearnersExpiringSoon` | Staff scope | Active enrollments whose access ends within `days` (1 to 90, default 14). `take`. |

Staff scope: everyone for **View learner records**; otherwise learners of the person's courses (with **Manage the courses you teach**) and, with Staff on, the people they manage. At most 1,000 enrollments are read.

## Author Prompts

**Learning > Authoring assistant** (`/Admin/learning/ai/authoring`, feature Learning Authoring Assistant) saves drafts only, never publishes.

| Prompt | Template | Needs | Saves |
|---|---|---|---|
| Draft a course outline | `learning-draft-outline` | Edit a new course | Draft course with sections and a draft page lesson per item |
| Draft questions from a lesson | `learning-draft-questions` | Manage question banks, view the lesson, edit the bank | Draft single choice, multiple choice, true/false, short text and essay questions |
| Draft a rubric | `learning-draft-rubric` | Learning Coursework; edit a new rubric | Draft rubric |
| Translate a lesson | `learning-translate` | Edit the lesson | Draft clone with translated title and body (not added to the outline) |

`AuthoringDraftParser` reads the JSON strictly (12 sections of 10 lessons, 20 questions of 8 options, 10 criteria of 6 levels) and HTML-encodes model text. Without an AI deployment the page disables its buttons. Override the prompt templates like any other.

## Scoring Help

With the Authoring feature, grading pages of quiz essays and coursework submissions show **Suggest a score** to people with `UseLearningScoringHelp` who may grade the work. It posts to `/Admin/learning/ai/scoring-help/attempts/{id}/{index}` or `/Admin/learning/ai/scoring-help/submissions/{id}`, renders `learning-score-essay` with the grader guidance or rubric and the learner's text (never the name), and fills the form; it saves nothing. A grade saved by the grader who got a suggestion is marked **AI-assisted** (`AttemptItem.AIAssisted`, `Submission.AIAssisted`), decided on the server. AI never saves a grade or issues a certificate.

Other modules add help to grading pages with a display driver for `GradingAssistContext` (`IDisplayManager<GradingAssistContext>`, display type `Detail`).

## AI Data Sources Result Filter

`IAIDataSourceResultFilter.FilterAsync(AIDataSourceResultFilterContext, IReadOnlyList<DataSourceSearchResult>)` (in `CrestApps.OrchardCore.AI.Core.DataSources`) lets any module keep only the chunks a request may receive. Register with `services.AddAIDataSourceResultFilter<T>()`; a module registering its own provider's content manager later calls `services.DecorateDataSourceContentManagers()`. With filters, searches over-fetch (`AIDataSourceResultFilterOptions.OverfetchFactor` 4, `MaxCandidates` 200).

# QTI and Moodle Import

Supporting detail for `orchardcore-learning-assessments`. Both are Content Transfer file formats: files go through **Content > Bulk Import** (or a content type's import page) with the same background import, progress, failed rows and permissions as a spreadsheet. There is no QTI or Moodle export.

## QTI Import

Feature `CrestApps.OrchardCore.Learning.Assessments.QtiImport` (Learning Assessments, Content Transfer, Media). Open the import page of any question type and choose a **Question bank**; each item becomes the question type that fits it.

- **Package**: a `.zip` with `imsmanifest.xml` at its root; resources of type `imsqti_item_xmlv3p0` or `imsqti_item_xmlv2p*` are read in manifest order. **Single item**: an `.xml` file (no images).
- **Safety**: `PackageArchiveReader` (`Learning.Core`) checks every entry before reading (no absolute paths, `..`, colons, control characters, device names, symbolic links or duplicates; at most 10,000 entries) and counts bytes against 200 MB. XML is read with DTDs refused and no resolver. Prompts and feedback are written from an HTML allow-list and sanitized.

| QTI | Question |
|---|---|
| `choiceInteraction`, one right choice, `maxChoices` 1 | Single choice (`shuffle` kept) |
| `choiceInteraction` with multiple cardinality or several right choices | Multiple choice (partial credit with `map_response`) |
| Two choices *True* and *False* | True or false |
| One `textEntryInteraction`, string | Short text |
| One `textEntryInteraction`, `float` or `integer` | Numeric (tolerance from an `equal` rule) |
| Inline or several `textEntryInteraction`s | Fill in the blanks |
| `extendedTextInteraction` | Essay (scorer rubric block becomes grader guidance) |
| `matchInteraction`, `associateInteraction` | Matching |
| `orderInteraction` | Ordering |
| `gapMatchInteraction` | Drag into text |
| `inlineChoiceInteraction`s | Select missing words |
| Content without interaction | Description |

- Points come from `MAXSCORE`, else `SCORE` `normalMaximum`, else the mapping, else 1. Response processing must be `match_correct` or `map_response` (or inline rules made of their elements).
- Images (PNG, JPEG, GIF, WebP, up to 10 MB) go to the media library under `qti-imports/{import id}/`. With Secure Media on, give learner roles **View media content in folder 'qti-imports'**. Images only general feedback shows are kept in the protected area `LearningQtiFeedbackImages`, served at `/learning/assessments/feedback-images/{file id}` once the review shows the feedback.
- Re-importing a row with the same `SourceIdentifier` and `SourceFile` into the same chosen bank updates that question (`QuestionSourceRowFilter`, `QuestionSourceIndex`), when the importer may edit it.
- Not imported (failed rows with a reason): hotspot, slider, drawing, upload, media, hottext, PCI and other interactions; adaptive items and templates; mixed interactions; other response processing; penalties and differently weighted mapped answers (`UnsupportedScoring`); markup in choices; matching distractors; math, audio, video and images outside the package or SVG.

## Moodle Import

Feature `CrestApps.OrchardCore.Learning.Assessments.MoodleImport` (Learning Assessments, Learning SCORM Packages, Course Categories, Content Transfer, Media). It is offered on the import page of the **Course** type; one `.mbz` backup (zip or Moodle 2.6+ tar.gz) is one course. Rows may be Course, Learning package, Quiz, File lesson, Link lesson, Page lesson and Text, plus Coursework, Feedback survey and Choice while their features are on.

Limits (`MoodleImportOptions`, shell configuration section `CrestApps:Learning:MoodleImport`, so `OrchardCore:CrestApps:Learning:MoodleImport` in appsettings): `MaxEntries` 100,000, `MaxExpandedBytes` 5 GB, `MaxXmlCharacters` 100 million.

| Moodle | Becomes |
|---|---|
| Course | Title, summary, code, dates, category (found by name or created), enrolment and completion |
| Course picture | `Cover` media field |
| Sections | Outline sections `moodle-<section id>` |
| `scorm` | Learning package through `LearningPackageImporter` (new window, attempt limit and grading method kept) |
| `imscp` | Learning package that completes when viewed |
| `quiz` | Quiz with time limit, attempts, grading method, navigation, dates, pass mark, questions per page, feedback mode and review options; random slots become draw rules |
| Questions | `MoodleQuestionMapper` maps to the QTI question model, into a bank per Moodle question category |
| `resource`, `url`, `page`, `label` | File lesson, Link lesson, Page lesson, Text item |
| `assign` | Coursework (sign-off when neither text nor files) |
| `feedback`, `choice` | Feedback survey, Choice |

- **Enrolment**: self with a key becomes `Key` (the key is never imported; set one), self without key `Open`, manual only `Closed`, `apply` plugin `Approval`; a self enrolment duration becomes `AccessDays`.
- **Completion**: activities in the course completion criteria are required, others optional; a grade to pass becomes `MinimumGrade`; manual completion by a role becomes `StaffApproval` (`editingteacher` Instructor, `teacher` Assistant); no criteria means `NeverCompletes`. Cohort sync links site cohorts (`MoodleCohorts.Match`) only for an importer who may manage enrollments.
- **Restrictions**: e-mail or user name conditions become the `People` release rule, opening dates the `Date` rule; others are flagged.
- **Reported rows** fall in **Not imported from Moodle** or **Needs attention after the Moodle import** (forums, glossaries, books, wikis, unsupported question types, keys and passwords to set, hidden items, scale grading, unmatched roles or cohorts).
- **Importing again**: `MoodleSourceRowFilter` matches earlier imports through `LearningSourceIndex` so the course and items are updated.
- Not built: web services import, pictures in questions, users, enrolments and attempts, competencies and groups.

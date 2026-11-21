---
name: orchardcore-learning-standards
description: Skill for the e-learning standards of CrestApps Orchard Core Learning. Covers Learning Packages (SCORM 1.2, SCORM 2004 3rd and 4th Edition, IMS content packages and cmi5) with safe import, the sandboxed player, the package content host and run-time data checked on the server; the xAPI 1.0.3 Learning Record Store with OpenID application clients and launch tokens; and LTI 1.3 External Tools with the site as platform, Deep Linking 2.0, Names and Role Provisioning Services 2.0 and Assignment and Grade Services 2.0, with the LearningPackageRuntimeStates, LearningStatements and LearningExternalTools recipe steps. Use this skill when requests mention SCORM, cmi5, xAPI, Tin Can, LRS, LTI, external tools, Articulate packages or learning statements in CrestApps Learning. Strong matches include LearningPackagePart, LearningPackageContentHostSettings, XapiStatementService, IXapiStatementFilter, AddXapiStatementObserver, ExternalTool, ExternalToolItemPart and ILtiScoreObserver.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Learning Standards

## SCORM, cmi5, xAPI and LTI 1.3 for Learning Courses

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the standards support of CrestApps Learning. Each standard adds a course item kind or an endpoint on top of `orchardcore-learning`; results reach the learner's progress through `ILearnerProgressService.RecordResultAsync`. None of these modules is certified by ADL or 1EdTech and none makes a conformance claim.

### Guidelines

- Install `CrestApps.OrchardCore.Learning.Packages`, `...Learning.Xapi` and/or `...Learning.Lti` (or `CrestApps.OrchardCore.Cms.Core.Targets`) in the web/startup project. Reference `*.Abstractions` / `*.Core` projects from your module.
- Package content is untrusted script: it runs in a sandboxed frame on an opaque origin (or a separate content host), never with the learner's cookies. Import packages only from authors you trust with script.
- Package, cmi5 and browser-sent results are marked **reported by the browser**; they do not count toward certifications unless the item allows it (`CourseItemPart.AllowSelfReported`). LTI `FullyGraded` scores are server results.
- The sign-in cookie never opens the LRS or the LTI services; they take only OpenID application credentials, bearer tokens or launch tokens.
- Package files and xAPI attachment contents never travel in recipes or deployments.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Learning SCORM Packages | `CrestApps.OrchardCore.Learning.Packages.Scorm` | Learning |
| Learning cmi5 Packages | `CrestApps.OrchardCore.Learning.Packages.Cmi5` | Learning SCORM Packages, Learning Record Store |
| Learning Record Store | `CrestApps.OrchardCore.Learning.Xapi` | Learning, `OrchardCore.OpenId`, `OrchardCore.OpenId.Management` |
| External Tools | `CrestApps.OrchardCore.Learning.Lti` | Learning, `OrchardCore.OpenId.Server`, `OrchardCore.OpenId.Validation`, `OrchardCore.Secrets` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Learning.Packages.Scorm",
        "CrestApps.OrchardCore.Learning.Packages.Cmi5",
        "CrestApps.OrchardCore.Learning.Xapi",
        "CrestApps.OrchardCore.Learning.Lti"
      ],
      "disable": []
    }
  ]
}
```

## Learning Packages (SCORM 1.2, SCORM 2004, IMS CP)

A `LearningPackage` content item (`TitlePart`, `HtmlBodyPart`, `LearningPackagePart`, `CourseItemPart`, kind `LearningPackage`). Save the item, then **Import a package** in its editor (`/Admin/learning/packages/{contentItemId}/upload`, needs **Import learning packages** and edit rights). The import goes into a draft, so learners keep the current package until it is published.

| Completion mode | The item completes when |
|---|---|
| `PackageReported` (default) | SCORM 1.2 `lesson_status` is `completed` or `passed`; SCORM 2004 evaluated `completion_status` is `completed` |
| `Passed` | The package passes the learner (SCORM 2004 evaluated `success_status`); `failed` fails the item |
| `MinimumScore` | The item's score reaches its pass mark, whatever the package reports |
| `MarkedDone` | The learner marks it done |
| `Viewed` | The learner opens it (set automatically for IMS content packages, which have no run-time API) |

- Upload limits (`LearningPackageOptions`, configure in code): `MaxUploadBytes` 500 MB, `MaxEntries` 10,000, `MaxExpandedBytes` 1 GB. Every entry path is checked before anything is written; a DTD in the manifest is refused.
- Only the first SCO of the default organization runs: no SCORM 2004 sequencing, rollup or navigation between SCOs (the editor says when the manifest has sequencing). Choose **Marked done** for a SCO that never reports `completion_status`.
- The **How learners run it** card sets `OpenInNewWindow`, `MaxAttempts` and `GradingMethod` (`Highest`, `Average`, `First`, `Latest`).
- Programs, scripts, server pages, macro Office files and `web.config` are refused at import.
- The package is kept as a clean archive in the protected file store, never in the media library.

### Package Content Host

Multi-frame packages (Articulate Rise and Storyline 360 with `scormdriver/scormdriver.js`) fail in strict opaque-origin mode ("LMS Initialize Failed"). Give them a content host: a separate origin that serves only package files, set under **Settings > Learning packages** (`ManageLearningPackageSettings`). Add the host to the tenant's host names, give it TLS, and prefer another registrable domain so it never shares cookies with the site.

```json
{
  "steps": [
    {
      "name": "Settings",
      "LearningPackageContentHostSettings": {
        "BaseUrl": "https://example-packages.com"
      }
    }
  ]
}
```

On the content host every path except `/learning/packages/run/{token}/{path}` and `/learning/packages/frame/{token}` returns 404, and `Cookie` and `Authorization` headers are stripped. Run-time details, launch tokens, the SCORM data models and cmi5 are in [SCORM and cmi5 runtime](references/scorm-cmi5-runtime.md).

### cmi5

With **Learning cmi5 Packages**, the import page also takes a `.zip` with `cmi5.xml` at its root (AUs that are package files or absolute `https` URLs). Launches write `LMS.LaunchData`, the `launched` statement and an `abandoned` statement for a session left open, and hand the AU an `endpoint`, `fetch` URL (`POST /learning/cmi5/fetch/<token>`), `registration`, `activityId` and an account `actor` (never an e-mail). The LRS enforces cmi5 statement rules for that registration; `satisfied` moves the item's result into Learning. `waived`, Browse mode and AU sequencing are not built.

## Learning Record Store (xAPI 1.0.3)

The LRS is at `/learning/xapi/` (`statements`, `activities/state`, `activities/profile`, `agents/profile`, `activities`, `agents`, `about`). Requests need `X-Experience-API-Version` 1.0.x (2.0.0 is refused with 400).

- **Clients** are OpenID applications created under **Access Control > OpenID Connect > Applications** with a role holding the LRS permissions: send a bearer token of Orchard Core's `Api` scheme (Authorization Server and Token Validation on, client credentials flow), or HTTP Basic `client id:client secret` for a confidential application. Launch tokens use Basic with user name `xapi-launch`.
- Statements are validated against xAPI 1.0.3, immutable (same id and content is a no-op, different content 409), voidable by the rules of the client's rights, and queryable (`limit` up to 100). Limits (`XapiOptions`): 20 MB request, 500 statements of 256 KB, 16 MB attachments, 1 MB documents.
- **Learning > Learning Statements** lists statements (needs **Read all learning statements**).
- Rate limit group `LearningXapi`: 1,200 requests a minute per address when Rate Limits is on.

Details in [xAPI Learning Record Store](references/xapi-lrs.md).

## External Tools (LTI 1.3 Platform)

1. Under **Settings > OpenID Connect > Authorization Server**, turn on the token endpoint, allow the client credentials flow and use the `JWT` access token format. Under **Settings > OpenID Connect > Token Validation**, choose this tenant.
2. Register the tool under **Learning > External Tools** (`ManageExternalTools`): client id, deployment id, login initiation URL, redirect URIs, target link URI, Deep Linking URL, JWKS URL or key set, custom parameters, privacy level (`Anonymous`, `Name`, `NameAndEmail`), roster and grade access, **Can be launched**. Saving creates or updates the tool's OpenID application (confidential, `private_key_jwt`).
3. Add an **External tool** course item (`ExternalToolItem`, `ExternalToolItemPart`: `ToolId`, `TargetLinkUri`, `CustomParameters`, `ScoreMaximum`, `LineItemLabel`), or let course staff use **Content from external tools** (Deep Linking) on the course editor.

Platform endpoints to give the tool: issuer (site Base URL), `/learning/lti/authorize`, `/learning/lti/jwks`, `/connect/token`, `/learning/lti/deep-linking/return`. Launches open in a new window from `GET /learning/lti/launch/{itemId}`. A tool never gets a user id: `sub` is an HMAC of the user id with the tool's `SubjectSalt` (`LtiSubjects.For`). The platform key is the `Learning.Lti.Platform` key set in Secrets, rotated every 90 days. Details in [LTI platform](references/lti-platform.md).

## Admin Menu and Permissions

| Menu | Page | Permission |
|---|---|---|
| **Settings > Learning packages** | Site settings group `learning-packages` | `ManageLearningPackageSettings` |
| **Learning > Learning Statements** | `/Admin/learning/statements` | `ReadAllLearningStatements` |
| **Learning > External Tools** | `/Admin/learning/external-tools` | `ManageExternalTools` |
| Package item editor | **Import a package** at `/Admin/learning/packages/{contentItemId}/upload` | `ImportLearningPackages` + edit the item |

| Permission | Display name | Implied by | Notes |
|---|---|---|---|
| `ImportLearningPackages` | Import learning packages | `ManageCourses` | |
| `ManageLearningPackageSettings` | Manage learning package settings | | |
| `WriteLearningStatements` | Write learning statements to the Learning Record Store | | For LRS clients |
| `ReadAllLearningStatements` | Read all learning statements of the Learning Record Store | | Also opens the admin list |
| `ReadOwnLearningStatements` | Read the learning statements the client stored | `ReadAllLearningStatements` | |
| `ManageLearningDocuments` | Manage the documents of the Learning Record Store | | State and profile documents, activities and agents |
| `ManageExternalTools` | Manage external tools | | Security critical |

## Recipes and Deployment

```json
{
  "steps": [
    {
      "name": "LearningExternalTools",
      "Tools": [
        {
          "ItemId": "4videotool0000000000000001",
          "Name": "Video platform",
          "ClientId": "video-platform",
          "DeploymentId": "1",
          "LoginInitiationUrl": "https://tool.example.com/lti/login",
          "TargetLinkUri": "https://tool.example.com/lti/launch",
          "DeepLinkingUrl": "https://tool.example.com/lti/deep-link",
          "JwksUrl": "https://tool.example.com/.well-known/jwks.json",
          "PrivacyLevel": "Name",
          "AllowNamesAndRoles": true,
          "AllowGrades": true,
          "Enabled": true,
          "SubjectSalt": "<salt exported from the source site>"
        }
      ]
    }
  ]
}
```

| Step | Carries | Deployment step |
|---|---|---|
| `LearningExternalTools` | `Tools` by `ItemId`, including `SubjectSalt` so tool-side ids stay stable; the OpenID applications move with Orchard Core's OpenID application step | **External tools** (category Learning) |
| `LearningPackageRuntimeStates` | `States` (learner by `UserName`, `EnrollmentId`, `PackageItemId`, `CourseId`, `Standard`, SCORM 2004 `Attempts` and `Cmi2004`); imported only into an enrollment of the named person in the named course, with values valid for the standard | **Learning package runtime states (learner records)**, opt-in, **Export learner records** |
| `LearningStatements` | `Statements` of `{ Statement, Voided, ClientId, AuthorityKey }`; keeps id, `stored` and `authority`, skips identical statements, reports different ones | **Learning statements (learner records)**, opt-in, **Export learner records** |

Package items export as content, but the package file does not: import it again on the target site.

## Workflows and Data

| Area | What it adds |
|---|---|
| Workflows | **Learning Statement Stored** (filter by verbs; input `StatementId`, `Verb`, `ObjectType`, `ObjectId`, `Registration`, `ActorAccountName`, `ActorAccountHomePage`, `ClientId`, `Voided`, `StoredUtc`); **External Score Received** (filter by grading progress; input includes `ToolId`, `CourseId`, `CourseItemId`, `LineItemId`, `UserId`, `ScoreGiven`, `ScoreMaximum`, `Percent`, `GradingProgress`, `RecordedInProgress`) |
| Data source | `Learning.Xapi` with `LearningStatements` (no e-mail addresses), needs **View learning reports** |
| Personal data | Package runtime states, statements and documents of the person's agents, and LTI scores are exported and erased (states of completed enrollments are stripped, not deleted) |

## Extension Points

| Registration | Use it to |
|---|---|
| `services.AddXapiStatementFilter<T>()` | `IXapiStatementFilter.OnWritingAsync(context, next)`: refuse a request (`context.Reject(status, message)`), hold a lock over the write, act on `context.Stored` |
| `services.AddXapiStatementObserver<T>()` | `IXapiStatementObserver.StoredAsync(records, credential)` after statements are stored |
| `XapiStatementService.WriteTrustedAsync` | Store statements your module writes with its own authority (no filters) |
| `XapiLaunchTokens`, `XapiDocumentStore` | Make launch tokens for content you launch; read and write documents |
| `services.AddLtiScoreObserver<T>()` | `ILtiScoreObserver.ScoreReceivedAsync(LtiScoreReceivedContext)` with tool, line item, score, percent |
| `IExternalToolManager`, `IExternalToolStore`, `ILtiPlatform`, `LtiSubjects.For` | Manage tools, describe the platform, compute a tool's subject id |

```csharp
public sealed class CompletedStatementObserver : IXapiStatementObserver
{
    private readonly ILogger _logger;

    public CompletedStatementObserver(ILogger<CompletedStatementObserver> logger)
    {
        _logger = logger;
    }

    public Task StoredAsync(IReadOnlyList<XapiStatementRecord> records, XapiCredential credential)
    {
        _logger.LogInformation("{Count} statements stored.", records.Count);

        return Task.CompletedTask;
    }
}

public sealed class LtiScoreLogger : ILtiScoreObserver
{
    private readonly ILogger _logger;

    public LtiScoreLogger(ILogger<LtiScoreLogger> logger)
    {
        _logger = logger;
    }

    public Task ScoreReceivedAsync(LtiScoreReceivedContext context)
    {
        _logger.LogInformation("Tool {Tool} posted {Percent}% (recorded in progress: {Recorded}).", context.Tool.Name, context.Percent, context.RecordedInProgress);

        return Task.CompletedTask;
    }
}

// Startup:
// services.AddXapiStatementObserver<CompletedStatementObserver>();
// services.AddLtiScoreObserver<LtiScoreLogger>();
```

## Troubleshooting

- **Articulate package is blank with "Error grabbing 1.2 API"**: set a package content host; strict mode gives each frame its own opaque origin.
- **Import refused**: an entry path is unsafe, the archive is too big or has too many entries, the manifest has a DTD, a file kind is forbidden, or a cmi5 package was uploaded without **Learning cmi5 Packages**.
- **Package completes but no certificate**: package results are reported by the browser; tick **AllowSelfReported** on the item if that is acceptable.
- **Runtime calls refused**: the call is from an older launch (another tab), the session finished, or the state would exceed 1 MB (`StateTooLarge`).
- **LRS answers 401 for an administrator**: the sign-in cookie never authenticates the LRS; use an OpenID application.
- **LTI launch refused inside a frame**: launches must open in a new window, because the `SameSite=Lax` cookie is not sent in a cross-site frame.
- **LTI client id refused when saving**: another OpenID application not made by External Tools already uses it.
- **Tool scores do not reach the gradebook**: only `FullyGraded` scores on a line item bound to an External tool item count.

Related skills: `orchardcore-learning`, `orchardcore-learning-certifications`, `orchardcore-openid`, `orchardcore-secrets`, `orchardcore-rate-limits`.

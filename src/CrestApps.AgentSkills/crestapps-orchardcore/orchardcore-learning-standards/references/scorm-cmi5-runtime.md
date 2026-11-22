# SCORM and cmi5 Runtime

Supporting detail for `orchardcore-learning-standards`. Module `CrestApps.OrchardCore.Learning.Packages` (contracts in `Learning.Packages.Abstractions` and `Learning.Packages.Core`; the kind and its five completion modes are registered by `AddLearningPackagesCore()`).

## What an Import Keeps

`LearningPackagePart` holds only what the import found:

| Property | Meaning |
|---|---|
| `PackageFileId`, `FileName` | Package in the protected file store, uploaded name |
| `Standard` | `SCORM 1.2`, `SCORM 2004 3rd Edition`, `SCORM 2004 4th Edition`, `cmi5` or `IMS Content Package` (`PackagesConstants.Standards`) |
| `ManifestIdentifier`, `Title` | Manifest identifier, default organization title |
| `LaunchItemIdentifier`, `LaunchItemTitle`, `LaunchPath`, `LaunchQuery` | The launched item |
| `MasteryScore`, `MaxTimeAllowed`, `TimeLimitAction`, `LaunchData` | SCORM 1.2 `adlcp:*` values; SCORM 2004 equivalents from `imsss:limitConditions` |
| `CompletionThreshold`, `ScaledPassingScore`, `ObjectiveIds`, `HasSequencing` | SCORM 2004 only (sequencing recorded, not applied) |
| `ScoCount`, `FileCount`, `ExpandedSize`, `ImportedUtc`, `ImportedBy` | Facts shown in the editor |
| `OpenInNewWindow`, `MaxAttempts`, `GradingMethod` | Set in **How learners run it** (not for cmi5) |

The version comes from the manifest's `schemaversion` (`1.2`, `2004 3rd Edition`, `2004 4th Edition`; `2004 2nd Edition` and `CAM 1.3` follow the 3rd), else the SCORM 2004 namespaces, else SCORM 1.2. A manifest with no ADL namespace, no `adlcp:scormtype` and no `SCORM` schema is an IMS content package and plays without the run-time API.

Allowed file kinds: pages, scripts and styles (`.html`, `.htm`, `.xhtml`, `.js`, `.mjs`, `.css`, `.map`, `.wasm`), data (`.json`, `.xml`, `.xsd`, `.dtd`, `.xsl`, `.txt`, `.csv`, `.md`), captions, images, fonts, audio and video, 3D models (`.glb`, `.gltf`, `.bin`) and Office documents without macros.

## The Sandbox

Without a content host (strict mode):

- The player embeds `<iframe sandbox="allow-scripts allow-forms allow-popups">` and every package file carries `Content-Security-Policy: sandbox allow-scripts allow-forms allow-popups` (no `allow-same-origin`), `nosniff`, `Referrer-Policy: no-referrer` and `Access-Control-Allow-Origin: *`.
- The frame cannot read cookies, script the player or call the site as the learner.

With a content host (`LearningPackageContentHostSettings.BaseUrl`, an origin only, never the site's host):

- `PackageContentHostMiddleware` runs before authentication, answers only `/learning/packages/run/{token}/{path}` and `/learning/packages/frame/{token}` on that host, strips `Cookie` and `Authorization`, and 404s everything else. On the site's own hosts those two paths return 404.
- Files and the frame page carry `sandbox allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox; frame-ancestors 'self' <player origin>`.
- The frame page defines `window.API` or `window.API_1484_11` and loads the launch file in a child frame on the same origin, so drivers that walk `window.parent` find it. Messages go to the player by `postMessage` only.
- Local testing: browse the site at `http://localhost:<port>` and use `http://127.0.0.1:<port>` as the content host.

## Launch Tokens and Runtime Calls

- **Launch** (`POST /learning/packages/{itemId}/launch`) checks the learner's own enrollment, access window and release rules, starts a session and returns a token made with Data Protection, bound to the launch, enrollment, item, package file, learner and player origin, valid 8 hours (`PackageLaunchTokens.Lifetime`). `OpenInNewWindow` launches through `learning/packages/{itemId}/window`.
- The frame loads `/learning/packages/run/{token}/{path}`; only files of the token's package are served at normalized paths. A token stops working when the enrollment is withdrawn or another package is published into the item.
- The player sends values to `POST /learning/packages/runtime/{enrollmentId}/{itemId}` on commit, finish, every 30 seconds and on leave, one call at a time with an increasing `sequence`; the server ignores anything at or below `PackageRuntimeState.LastSequence`.
- One runtime state per enrollment and item (collection `LearningPackageRuntime`), written under a lock. Lists are capped at SCORM 2004 smallest maximums (`ScormLimits`: 250 interactions, 100 objectives, 250 comments); a state above 1 MB is refused (`StateTooLarge`). When a result cannot be recorded because the enrollment is busy, `ResultNotRecorded` is kept and the next launch records it first.

## SCORM 1.2 and 2004 Data Models

- SCORM 1.2: `window.API` with `LMSInitialize`, `LMSFinish`, `LMSGetValue`, `LMSSetValue`, `LMSCommit`, `LMSGetLastError`, `LMSGetErrorString`, `LMSGetDiagnostic`. Every value is replayed on the server through `Scorm12DataModel` (access, vocabularies, CMIDecimal scores 0 to 100, CMITimespan, string lengths, `_children`, `_count`, `_version`). The mastery score decides `passed` or `failed` for a raw score when the status is `not attempted`, `completed` or `browsed` (credit mode only).
- SCORM 2004: `window.API_1484_11` with `Initialize`, `Terminate`, `GetValue`, `SetValue`, `Commit`, `GetLastError`, `GetErrorString`, `GetDiagnostic` and the API state error codes (103, 104, 112, 113, 122, 123, 132, 133, 142, 143). `Scorm2004DataModel` validates every RTE element and data type. Reads of `completion_status` and `success_status` are evaluated against the completion threshold and scaled passing score. A `suspend` exit resumes; any other exit starts a new attempt and resets attempt data. `adl.nav.request` is stored, not acted on; `adl.data._count` is 0.
- Results go to `RecordResultAsync` as the learner's own work, marked self-reported: score as a percentage of `score.min` to `score.max` (or `score.scaled` x 100), passed or failed, attempts. A completed enrollment opens the package in review mode (`no-credit`), and nothing is kept.

## cmi5

- Import reads `cmi5.xml`: course id and title, blocks, AUs with `url`, `moveOn` (`Passed`, `Completed`, `CompletedAndPassed`, `CompletedOrPassed`, `NotApplicable`), `masteryScore`, `launchMethod` (`AnyWindow`, `OwnWindow`), `launchParameters`, `entitlementKey`, `activityType`. AU URLs are package files or absolute `https` URLs.
- Launch (`POST /learning/packages/{itemId}/cmi5/launch`) gives the enrollment a registration, writes `abandoned` for a left-open session, writes `LMS.LaunchData` (read-only) and `cmi5LearnerPreferences`, writes `launched`, and returns the URL with `endpoint`, `fetch`, `registration`, `activityId` (`<home page>/learning/cmi5/<item>/au/<hash>`) and an account `actor`. Completed enrollments launch in Review mode. AUs return to `/learning/cmi5/return`.
- Fetch (`POST /learning/cmi5/fetch/<token>`, any origin) returns `{"auth-token": "..."}` once; later calls return `error-code` 1, 2 or 3 with HTTP 200. The auth token (Basic) opens the LRS only for the actor and registration and expires with the session (8 hours).
- The LRS enforces cmi5 section 9 rules (session ids, `initialized` first, nothing after `terminated`, verb and result rules, mastery score) and refuses a breaking request with 403.
- When an AU's moveOn is met, the LMS writes `satisfied` for the AU, then the block, then the course; the item is complete when the course is satisfied, passed when every AU needing a pass passed, and scored as the average of the AUs' last scaled scores x 100.

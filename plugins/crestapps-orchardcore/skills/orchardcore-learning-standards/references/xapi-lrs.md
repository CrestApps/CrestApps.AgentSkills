# xAPI Learning Record Store

Supporting detail for `orchardcore-learning-standards`. Module `CrestApps.OrchardCore.Learning.Xapi` (contracts in `Learning.Xapi.Abstractions`, services in `Learning.Xapi.Core`). It implements xAPI 1.0.3 and refuses 2.0.0.

## Resources

| Resource under `/learning/xapi/` | Methods | Purpose |
|---|---|---|
| `statements` | PUT, POST, GET, HEAD | Store and query statements |
| `activities/state` | PUT, POST, GET, HEAD, DELETE | State documents per activity, agent and optional registration |
| `activities/profile` | PUT, POST, GET, HEAD, DELETE | Activity profile documents |
| `agents/profile` | PUT, POST, GET, HEAD, DELETE | Agent profile documents |
| `activities` | GET, HEAD | Canonical activity definition joined from statements |
| `agents` | GET, HEAD | The Person object of an agent (agents are not joined into persons) |
| `about` | GET, HEAD | Accepted versions; no credential needed |

- Unknown parameters are refused with 400. The alternate request syntax (POST with `method`) works for writes only.
- Every response carries `Access-Control-Allow-Origin: *` and exposes `ETag`, `Last-Modified`, `X-Experience-API-Version` and `X-Experience-API-Consistent-Through`; credentials are never allowed cross-origin. This is safe only because the cookie never authenticates the LRS.

## Credentials

| Credential | How |
|---|---|
| OpenID application, bearer token | Enable the OpenID Authorization Server and Token Validation, allow client credentials on the application, send `Authorization: Bearer <token>` (Orchard Core `Api` scheme) |
| OpenID application, HTTP Basic | Confidential application; `Authorization: Basic base64(client id:client secret)`, checked by OpenIddict's application manager |
| Launch token | Made by a module that launches content (cmi5) with `XapiLaunchTokens`; Basic with user name `xapi-launch`; scoped to one actor, registration and activity until it expires |

The `authority` of a client's statements is an account agent with the site's home page and the client id. Give the application a role holding the LRS permissions; the Administrator role holds all four.

## Statements

- **Validation**: full xAPI 1.0.3 Part Two rules (no unknown or mis-cased properties, no `null`, one agent identifier, group rules, interaction types, results, contexts, timestamps without `-00:00`, version 1.0.x, language maps, extension IRIs, attachments). A batch is stored whole or not at all.
- **Set by the LRS**: `stored`, `authority`, `version` (`1.0.0` when missing), `timestamp` (stored time when missing).
- **Voiding**: verb `http://adlnet.gov/expapi/verbs/voided` with a StatementRef. A reader of all statements may void any; another client only its own; a launch token only its actor and registration's launch statements. Voided statements disappear from queries and are returned by `voidedStatementId`.
- **Queries**: `agent`, `verb`, `activity`, `registration`, `related_activities`, `related_agents`, `since`, `until`, `limit` (max 100, 0 means 100), `format` (`ids`, `exact`, `canonical`), `attachments`, `ascending`; `more` links are stable while new statements arrive.
- **Attachments**: `multipart/mixed` with `X-Experience-API-Hash` (SHA-256, 384 or 512; no SHA-224), stored in the protected area `LearningXapiAttachments`.
- **Signed statements**: compact JWS with RS256, RS384 or RS512; verified against an `x5c` certificate when present.
- **Limits** (`XapiOptions`): `MaxRequestBytes` 20 MB, `MaxStatementsPerRequest` 500, `MaxStatementBytes` 256 KB, `MaxAttachmentBytes` 16 MB, `MaxDocumentBytes` 1 MB, `MaxQueryLimit` 100; over a limit returns 413.

## Documents

PUT stores, POST merges top-level JSON properties, GET reads one (with `ETag`; non-JSON returned as an `application/octet-stream` attachment with `Content-Security-Policy: sandbox`) or lists ids (`since`). `If-Match` and `If-None-Match` are honored (412); a PUT to an existing profile without either is 409. Writes run under a distributed lock per document.

## Recipe Sample

```json
{
  "steps": [
    {
      "name": "LearningStatements",
      "Statements": [
        {
          "Statement": {
            "id": "5f3c2a7e-8b1d-4d6e-9f0a-1b2c3d4e5f60",
            "actor": { "objectType": "Agent", "account": { "homePage": "https://example.com", "name": "4learneruser000000000000001" } },
            "verb": { "id": "http://adlnet.gov/expapi/verbs/completed", "display": { "en-US": "completed" } },
            "object": { "objectType": "Activity", "id": "https://example.com/activities/safety-video" },
            "timestamp": "2026-11-01T10:00:00.000Z",
            "stored": "2026-11-01T10:00:00.120Z",
            "version": "1.0.0"
          },
          "Voided": false,
          "ClientId": "lrs-client"
        }
      ]
    }
  ]
}
```

Statements cannot change, so account actors keep the user id of the site they came from; attachment contents stay on the source site.

## Admin and Learner Records

- **Learning > Learning Statements** (`/Admin/learning/statements`): search by actor (user name, e-mail or account name), filter by verb, activity IRI, registration and stored dates; a statement page shows it as stored.
- Data source `Learning.Xapi`, data set `LearningStatements` (actor as account or `mbox_sha1sum`, never an e-mail address).
- Personal data: statements whose actor is the person's account or mailbox, and the documents of those agents, are exported and erased with their attachments.

## Conformance

Not run: ADL's LRS Conformance Test Suite needs a download. To run it, enable the LRS on a test site, create an OpenID application with a role holding the four LRS permissions, and point the suite at `https://<site>/learning/xapi` with that client's id and secret.

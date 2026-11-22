# LTI 1.3 Platform (External Tools)

Supporting detail for `orchardcore-learning-standards`. Module `CrestApps.OrchardCore.Learning.Lti` (contracts in `Learning.Lti.Abstractions`, services in `Learning.Lti.Core`). The site is the LTI platform for LTI 1.3 Core, Deep Linking 2.0, Names and Role Provisioning Services 2.0 and Assignment and Grade Services 2.0. The migration adds the five LTI service scopes to the OpenID scopes.

## Tool Registration

| `ExternalTool` | Meaning |
|---|---|
| `Name`, `Description` | How authors pick the tool |
| `ClientId` | Saving creates or updates an OpenID application with this id (confidential, client credentials, redirect URIs, scopes, the tool's key set). An id used by an application not made by External Tools is refused. |
| `DeploymentId` | Sent with every launch |
| `LoginInitiationUrl`, `TargetLinkUri`, `DeepLinkingUrl`, `JwksUrl` | Must be https (http only to this machine in Development). The JWKS URL is read through the egress guard (no redirects, no internal network). Redirect URIs and a pasted key set live on the OpenID application. |
| `CustomParameters` | `name=value` lines; a course item's win over the tool's |
| `PrivacyLevel` | `Anonymous`, `Name` or `NameAndEmail`; applies to launches, custom parameters and the roster |
| `AllowNamesAndRoles`, `AllowGrades` | Enable NRPS and AGS |
| `Enabled` | Off stops launches and service requests |
| `SubjectSalt` | Random salt for the subject ids this tool sees |

Removing a tool removes its OpenID application; its course items stop launching.

## The Launch

1. `GET /learning/lti/launch/{itemId}` checks the person may open the item (active enrollment, release rules, published item and course, enabled tool) and redirects to the login initiation URL with `iss`, `login_hint`, `lti_message_hint` (Data Protection, 5 minutes), `target_link_uri`, `client_id` and `lti_deployment_id`.
2. The tool's authentication request reaches `/learning/lti/authorize` (GET or POST): `scope=openid`, `response_type=id_token`, `response_mode=form_post`, `prompt=none`, a registered redirect URI, an unseen nonce, a valid message hint for the signed-in person. A cross-site POST without the cookie is answered with 303 to the same request as a GET.
3. The platform posts an RS256 id token (version `1.3.0`, message type, deployment id, resource link, roles, context, launch presentation, custom parameters, names and e-mail within the privacy level, and the AGS and NRPS claims when allowed).

Roles: learners `Learner`; course staff `Instructor` plus `TeachingAssistant` or `Grader`; observers `Mentor`; people who manage every course `Administrator` and `Instructor`. Substitution variables: `$Context.id`, `$Context.title`, `$ResourceLink.id`, `$ResourceLink.title`, `$User.id`, `$Person.name.full`, `$Person.name.given`, `$Person.name.family`, `$Person.email.primary`, `$Membership.role`.

The platform key set `Learning.Lti.Platform` (an `RsaKeySecret` in Secrets) is created on enable, rotated every 90 days, and the previous key stays published at `/learning/lti/jwks` for 30 days.

## Deep Linking 2.0

Course staff with **Manage the courses you teach** (or **Manage courses**) use **Content from external tools** on the course editor. **Pick from** sends an `LtiDeepLinkingRequest` (accept type `ltiResourceLink`, signed `data` valid one hour and used once). The tool posts its `LtiDeepLinkingResponse` JWT to `/learning/lti/deep-linking/return`; after signature, issuer, audience, time, nonce and data checks, each `ltiResourceLink` becomes an External tool item at the end of the course's last section (the course gets a draft when needed). Other content item types are listed, not added.

## Services

Service tokens come from Orchard Core's `/connect/token` with the client credentials grant and a `private_key_jwt` client assertion (1EdTech Security Framework: `iss` and `sub` the client id, `aud` the token endpoint, at most five minutes, unseen `jti`). An OpenIddict handler accepts the LTI assertion form for registered tools. The services take only a bearer token of the `Api` scheme whose client is an enabled tool used in the course.

| Scope | Service |
|---|---|
| `https://purl.imsglobal.org/spec/lti-nrps/scope/contextmembership.readonly` | Roster |
| `https://purl.imsglobal.org/spec/lti-ags/scope/lineitem` | Line items, read and write |
| `https://purl.imsglobal.org/spec/lti-ags/scope/lineitem.readonly` | Line items, read |
| `https://purl.imsglobal.org/spec/lti-ags/scope/score` | Post scores |
| `https://purl.imsglobal.org/spec/lti-ags/scope/result.readonly` | Read results |

| Endpoint | Notes |
|---|---|
| `GET /learning/lti/nrps/{courseId}/memberships` | `role`, `rlid`, `limit` with `Link: rel="next"`; learners are `Active` while enrolled or completed |
| `GET, POST /learning/lti/ags/{courseId}/lineitems` | List (`resource_link_id`, `resource_id`, `tag`, `limit`) and create (201) |
| `GET, PUT, DELETE /learning/lti/ags/{courseId}/lineitems/{lineItemId}` | One line item |
| `POST .../lineitems/{lineItemId}/scores` | `application/vnd.ims.lis.v1.score+json`; requires `userId`, `activityProgress`, `gradingProgress`, ISO `timestamp`, `scoreMaximum` with `scoreGiven`; an older timestamp is 409; answers 204 |
| `GET .../lineitems/{lineItemId}/results` | `user_id`, `limit` |

A `FullyGraded` score on a line item bound to an External tool item becomes that item's result (percentage of the maximum, marked as sent by the server) and counts in the gradebook and completion rules.

## Data and Not Built

- `LearningExternalTools` recipe and deployment step (with `SubjectSalt`); scores are not exported. Personal data lists each tool's subject id and posted scores and erases the scores (tools keep their own records).
- Not built: LTI 1.1, the site as a tool, Dynamic Registration, other Deep Linking content types, Submission Review, Proctoring, Caliper, Context Groups, launches inside a frame, unbound line items reaching progress, Audit Trail events.

---
name: orchardcore-ai-conversations
description: Skill for AI conversations across web chat, SMS and AI phone calls in CrestApps.OrchardCore 3.0. Covers AIConversationScope, conversation variables and Liquid in the system message, the AI Conversations workflow activities (conversation starting, turns, AI Tool Called, flows and stages, Use AI Profile, Decline Conversation, Split Traffic), Workflow tool instances, profile versions, evaluations and simulations, the AI Quality feature (test cases, QA cohorts, the QA dashboard, alert rules), contact memory, speaking styles, guardrails and data privacy storage modes, AI Webhooks and Send Signed Webhook, and the AI API under /api/ai/v1 with server and browser API keys and ai.* scopes. Use this skill when requests mention AI conversation workflows, conversation variables, AI tests or QA, profile versions, guardrails, speaking styles, conversation privacy or retention, contact memory, AI webhooks, AI API keys, or starting AI calls over an API.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Conversations

Website chats, automated text threads and AI phone calls are all **AI conversations**. AI Services
(`CrestApps.OrchardCore.AI`) and AI Chat add one model for all of them: a conversation scope that tools,
prompts and workflows read; workflows before, during and after a conversation; quality testing and
monitoring; guardrails, speaking styles and privacy on each profile; and a REST API.

## Features

| Feature ID | Adds |
| --- | --- |
| `CrestApps.OrchardCore.AI` + `OrchardCore.Workflows` | The **AI Conversations** workflow activities, profile versions, evaluations, simulations, contact memory, speaking styles, guardrails, privacy |
| `CrestApps.OrchardCore.AI.ToolInstances` + `OrchardCore.Workflows` | The `Workflow` tool instance source |
| `CrestApps.OrchardCore.AI.Chat` | The profile editor cards **Guardrails**, **Data Privacy** and **Contact memory** |
| `CrestApps.OrchardCore.AI.Quality` (AI Quality) | **Artificial Intelligence > Tests**, **QA** (cohorts, dashboard), **Alert rules**; `ManageAIQuality` |
| `CrestApps.OrchardCore.AI.Webhooks` (AI Webhooks) | AI events offered to `CrestApps.OrchardCore.Webhooks`; subscriptions need **Manage webhooks** (`ManageCommerceWebhooks`) |
| `CrestApps.OrchardCore.AI.Chat.Api` (AI Chat WebAPI) | The AI API under `/api/ai/v1` and **Artificial Intelligence > API keys** (`ManageAIApiKeys`) |
| `CrestApps.OrchardCore.AI.Chat.Analytics` | Conversation metrics for alerts and the `ConversationEvaluations` report data set |

Install the CrestApps packages in the web/startup project.

## The conversation scope

`AIConversationScope` (namespace `CrestApps.OrchardCore.AI.Core.Conversations`) holds the
`AIConversationContext` the current code runs for, flowing with the async call, so a tool the model calls,
the system message being rendered and a workflow a tool starts all see the same conversation.

| Property | Meaning |
| --- | --- |
| `Channel` | `Chat`, `Sms`, `Phone` or `Email` (`AIConversationChannels`) |
| `Direction` | `Inbound` or `Outbound` (`AIConversationDirections`) |
| `Session`, `Profile` | The `AIChatSession` (when it exists) and the answering `AIProfile` |
| `ActivityId`, `Contact` | The omnichannel activity and the contact content item |
| `CustomerAddress`, `BusinessAddress` | The customer's number and ours |
| `Values` | Extra template values: `Activity`, `Subject`, `Campaign`, `Endpoint` |

Texts and calls begin a scope per turn or call with `IOmnichannelConversationContextFactory`; a web chat
has none and `AIConversationScope.Resolve()` describes it from the session in `AIInvocationScope`.

### Conversation variables

Text values kept on the session as `AIConversationVariablesMetadata` (on the activity before a session
exists):

```csharp
session.SetConversationVariable("tier", "gold");
var tier = session.GetConversationVariables()["tier"];
```

They come from **Set Conversation Variables**, starting handlers, **Create AI Activity**, data extraction
(each extracted field) and, on Telnyx calls, custom `X-` SIP headers (`X-Account-Id` becomes
`sip_account_id`; up to 20 headers of 256 characters).

### Liquid in the system message

When the composed system message contains Liquid, `AIConversationLiquidCompletionContextBuilderHandler`
renders it with `Variables`, `Conversation` (`AIConversationTemplateModel`), `Session`, `Profile`, `Contact`
and each `Values` entry. A template that fails is logged and used as written.

```liquid
You are talking to {{ Variables.first_name }}, a {{ Variables.tier }} customer on {{ Conversation.Channel }}.
{% if Variables.balance_due %}They owe {{ Variables.balance_due }}.{% endif %}
```

## Workflows in conversations

| Moment | How |
| --- | --- |
| Before the first message | **AI Conversation Starting** event. `IAIConversationStartingService` runs every `IAIConversationStartingHandler` (budget 3 s inbound, 30 s outbound; failures and timeouts start the conversation anyway). Tasks: **Set Conversation Variables** (`name: value` per line), **Use AI Profile** (another profile, or a saved **Version** of one), **Decline Conversation** (cancels an outbound call or text, refuses an inbound call; not a web chat) |
| During it | A **Workflow** tool instance runs a workflow that starts with **AI Tool Called** (`Workflow.Input.Arguments`, `ToolName`) and returns its `Output`. **AI Conversation Turn** runs after each saved turn (`Text`, `Role`) |
| In stages | **AI Conversation Flow Started** plus **AI Conversation Stage** tasks (stage name, Liquid instructions, ways out `Name: when to take it`). The AI gets a `moveToStage` tool; pick the flow on the profile's **Instructions** tab (**Conversation flow**, optional **Flexible**). Progress is kept in `AIConversationFlowState` |
| After it | **AI Chat Session Field Extracted**, **Closed**, **Post-Processed** (with `Variables`, `Transcript`, `Turns`); see `orchardcore-ai-workflows` |

Every conversation workflow gets `AIConversationWorkflowInput.Build(conversation)`: `Channel`, `Direction`,
`SessionId`, `ProfileId`, `ActivityId`, `ContactId`, `ContactName`, `Contact`, `CustomerAddress`,
`BusinessAddress`, `Variables`, `Simulation`, plus `StartingResult` for **AI Conversation Starting**.
Set **Set Conversation Variables** inside a tool workflow to change the live conversation.

### Workflow tool instances

The `Workflow` source (`WorkflowToolConstants.SourceName`, settings `WorkflowToolSettings`: `WorkflowTypeId`,
`TimeoutSeconds`) binds declared parameters with `AIToolParameterBinder` (an instance without parameters
takes one `request` string), starts the workflow correlated to the session, and returns the workflow's
`Output` as JSON, `{"status":"accepted"}` when it halted on a blocking activity, or
`{"status":"error","message":...}` when it faulted. Parameters can be bound to `conversation.channel`,
`conversation.direction`, `conversation.session_id`, `conversation.customer_address`,
`conversation.business_address`, `activity.id`, `contact.id`, `contact.name` and `variables.<name>`.

On turn-based calls every non-system tool is wrapped in a `TimeLimitedAIFunction`: 8 seconds
(`AIConversationTools.DefaultCallToolTimeout`) unless the function sets `CrestApps.TimeoutSeconds` (from the
instance's **Time limit on calls**; `0` is no limit). After 1.5 seconds the caller hears one filler line.

The Contact Center adds **AI Call Action** (message the AI, hand to an agent, transfer to a number, switch
profile, end) and **Transfer AI Call** (cold, warm or screened), and Omnichannel Management adds **Create
AI Activity**. See `orchardcore-contact-center` and `orchardcore-omnichannel-management`.

## Quality

### Profile versions

Each profile save is numbered by `AIProfileVersionHandler` (`AIProfileVersionMetadata.Version`) and kept as
an `AIProfileVersion` (indexed by `AIProfileVersionIndex`, read through `IAIProfileVersionStore`). Set
`AIProfileVersionMetadata.PendingNote` before saving to note the version. The profile's **Versions** page
compares and restores versions; a restore is saved as a new version and keeps the `ItemId`, `Name`, `Type`,
`OwnerId` and `CreatedUtc`. Each session records the version that answered it (`ProfileVersion` in the
**Chat sessions** data set). **Use AI Profile** with a **Version** copies that version once to a profile
named such as *Support (version 3)*, reported as that version of the original.

### Evaluations and simulations

- `IAIConversationEvaluator` scores `AIConversationTurn`s against criteria with the utility deployment:
  score 0 to 100, criteria met or not, resolved, sentiment, issues (`AIConversationIssueKinds`). `Passed` needs
  the passing score and every criterion. `AIConversationEvaluationService` saves an
  `AIConversationEvaluation` through `IAIConversationEvaluationStore` (index `AIConversationEvaluationIndex`).
- `AIConversationSimulator` plays a profile against a customer played by the utility deployment (scenario
  as its system message, `[END]` to finish), with `IsSimulation` on the scope and `Simulation` in workflow
  input so tool workflows can return test data.
- Tasks: **Evaluate AI Conversation** (Passed, Failed, Not evaluated), **Simulate AI Conversation** (Passed,
  Failed, Error), **Rerun AI Conversation Analysis**, **Split Traffic** (weighted `Name: weight` outcomes with a
  **Keep the same outcome for** sticky key, for A/B tests). Every evaluation raises **AI Conversation Evaluated**.

### AI Quality feature

- **Tests**: test cases (profile and optional version, simulated customer, success criteria, passing score,
  variables, mocked tools `tool name: result`, most customer turns, customer and judge models). **Run** or
  **Run 5 times** queues runs; **Create test** on a chat session replays that customer.
- **QA**: cohorts score a sample of real conversations of chosen profiles every ten minutes (label
  `QA: <cohort name>`), optionally checking answers against the knowledge base for hallucinations. The **AI
  QA Dashboard** report (needs Reports) shows scores, issues, sentiment and the lowest-scored sessions.
- **Alert rules**: watch a metric with a threshold, window, minimum samples and check interval; notify by
  email, the `alert.triggered` / `alert.resolved` webhooks and the **AI Alert Triggered** / **AI Alert Resolved**
  events.
- **Check AI Conversation Metric** does the same in a workflow (Alert started, Still alerting, Resolved,
  Normal). Metrics (`AIConversationMetric`): `Conversations`, `ResolutionRate`, `AverageHandleTimeSeconds`,
  `AverageResponseLatencyMs`, `NegativeFeedbackRate` (need AI Chat Session Analytics), `AverageQAScore`,
  `QAFailureRate`, `ToolFailureRate`, `HallucinationRate`, `AICalls`, `AICallHandoffRate`, `AICallFailureRate`,
  `AverageTimeToFirstAudioMs`.

## Contact memory

`AIContactMemory` keeps one note per contact (at most 2,000 characters) in its own record (index
`AIContactMemoryIndex`, store `IAIContactMemoryStore`), so rewrites create no content item versions. On a
profile, `AIProfileContactMemorySettings.UseMemory` appends the note to the system message when the
conversation has a contact, and `SaveMemory` rewrites it with the utility deployment when the session closes
(`AIContactMemoryService.RememberSessionAsync`). The rewrite instructions are on the **Contact memory** card
of **Settings > Artificial Intelligence** (`AIContactMemorySettings.Instructions`). The **Update Contact
Memory** task and `PUT /api/ai/v1/contacts/{contactId}/memory` set it explicitly; contacts with
`OmnichannelContactPart` show it in their editor.

## Speaking styles, guardrails and privacy

The runtime is CrestApps.Core's (`AddCoreAISpeakingStyles`, `AddCoreAIGuardrails`,
`AddCoreAIConversationPrivacy`); Orchard adds the editors and storage.

| Profile card | Settings | Notes |
| --- | --- | --- |
| **Speaking Style** | `AIProfileSpeakingStyleSettings.Styles` | Styles are System Prompt templates in the category `AISpeakingStyles.Category`, listed under **Artificial Intelligence > Speaking Styles**. Eight defaults (AI disclosure, scope limits, high empathy, echo verification, NATO spelling, speech normalization, fuzzy name matching, filler words) are seeded once per tenant |
| **Guardrails** | `AIProfileGuardrailSettings`: `CheckCustomer`, `CheckReplies`, `Topics`, `OtherTopics`, `PlaceholderReply` | Topics are templates under **Guardrail Topics**. Checked on web chat, SMS and turn-based calls for both sides; realtime calls check only the caller. A refusal raises **AI Guardrail Triggered** |
| **Data Privacy** | `AIProfileDataPrivacySettings`: `StorageMode` (`Everything`, `RedactPersonalData`, `MetadataOnly`), `RetentionDays`, `NamesToKeep` | `AIConversationPrivacyBackgroundTask` applies the mode to ended conversations and deletes them past retention every 10 minutes. Add `IAIConversationPrivacyHandler`s for data you own |

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "support-assistant",
          "Settings": {
            "AIProfileGuardrailSettings": {
              "CheckCustomer": true,
              "CheckReplies": true,
              "OtherTopics": "Medical or legal advice",
              "PlaceholderReply": "I can't help with that, but I can help with your order."
            },
            "AIProfileDataPrivacySettings": {
              "StorageMode": "RedactPersonalData",
              "RetentionDays": 90
            }
          }
        }
      ]
    }
  ]
}
```

## AI webhooks and signed requests

With **AI Webhooks** on, AI events are declared by `AIWebhookEventProvider` and published through the
Webhooks building block's `IWebhookPublisher`: `conversation.started`, `conversation.ended`,
`conversation.analyzed`, `conversation.transferred`, `conversation.evaluated`, `alert.triggered`,
`alert.resolved` (`AIWebhookEvents`). Subscriptions, the outbox and the delivery log belong to
`CrestApps.OrchardCore.Webhooks`; AI deliveries are kept and never purged by default. On upgrade,
`AIWebhookIndexMigrations` moves the old AI subscriptions and pending deliveries, and copies the delivered and
failed history into the shared log. See `orchardcore-webhooks`.

**Send Signed Webhook** (category **HTTP**) posts a rendered body with `X-Webhook-Id`, `X-Webhook-Event`,
`X-Webhook-Timestamp` and `X-Webhook-Signature` (`v1=` plus the hex HMAC-SHA256 of `{timestamp}.{body}`),
retries 408, 429, 5xx and network errors up to `MaxAttempts` (1 to 5), follows no redirects and refuses hosts
on the site's own network. The secret is protected per tenant and cannot travel in a recipe.

## The AI API

**AI Chat WebAPI** maps minimal API endpoints under `/api/ai/v1`, described in the tenant's OpenAPI document
(tags **AI Profiles**, **AI Conversations**, **AI Calls and Texts**, **AI Knowledge**, **Phone Numbers**, **AI
Tests**, **AI Chat**). Callers use an OpenID bearer token (client credentials; add `ai.*` scopes on the OpenID
server to narrow a client) or an API key; no cookies or antiforgery. Errors are RFC 9457 problem details.

- **Server key** (`cak_...`, secret, shown once): acts with chosen roles in the ticked scopes, can expire.
- **Browser key** (`cpk_...`, public): may only chat with chosen profiles through
  `POST /api/ai/completion/chat` from listed origins (pair with an Orchard Core CORS policy).
- Send it as `X-Api-Key: cak_...` or `Authorization: Bearer cak_...`.

| Scope | Endpoints |
| --- | --- |
| `ai.profiles` | `profiles` (CRUD in the `AIProfile` recipe shape; every save is a version), `profiles/{id}/versions`, `.../restore` |
| `ai.conversations` | `conversations` (list, get with transcript, variables, extracted fields, evaluations, flow; delete), `conversations/{id}/evaluations`, `conversations/{id}/analysis`, `profiles/{id}/simulations` |
| `ai.tests` | `tests`, `tests/batches` |
| `ai.calls` | `calls`, `texts`, `batches` (up to 1,000), `activities/{id}`, `activities/{id}/cancel`, `live-calls` (notes, transfer, profile, end) |
| `ai.knowledge` | `profiles/{id}/documents`, `data-sources`, `file-sources`, `web-crawlers` |
| `ai.numbers` | `numbers`, Telnyx `carrier-numbers` |
| `ai.contacts` | `contacts/{id}/memory` |
| `ai.chat` | `api/ai/completion/chat`, `api/ai/completion/utility`, `api/ai/chat/session` |
| `ai.recordings` | `recordings`, `recordings/{id}/links` |

Each endpoint also checks its Orchard permission (for example `ManageAIProfiles`, `ManageAIQuality`, or the
activity permissions for calls). Calls and texts are automated activities, so opt-out, do-not-call and
calling-window checks still apply.

```http
POST /api/ai/v1/profiles/{profileId}/simulations
X-Api-Key: cak_...
Content-Type: application/json

{ "scenario": "A customer whose order 4471 is late wants a delivery date.", "criteria": ["Looks up the order", "Gives the delivery date"], "passingScore": 80, "label": "late order" }
```

## Related skills

- `orchardcore-ai-workflows` for the completion tasks and chat session events.
- `orchardcore-ai-tool-instances` and `orchardcore-ai-tools` for tool instances and hidden call tools.
- `orchardcore-ai-realtime-voice` for live AI calls and switching profiles during a call.
- `orchardcore-ai-usage-analytics` and `orchardcore-ai-chat-analytics` for usage and session reports.
- `orchardcore-contact-center` and `orchardcore-omnichannel-voice` for the call side.
- `orchardcore-webhooks` for subscriptions and the delivery log.

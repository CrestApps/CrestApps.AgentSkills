---
name: orchardcore-ai-workflows
description: Skill for using CrestApps AI Services in Orchard Core Workflows. Covers the AI Completion using Profile task (AICompletionFromProfileTask) and the AI Completion using Direct Config task (AICompletionWithConfigTask, which needs CrestApps.OrchardCore.AI.Chat.Interactions), the fields other AI features contribute, Liquid prompt rendering, chat slot resolution, the Done, Drew Blank and Failed outcomes, AIResponseMessage output, Workflow usage metering, the AI chat session events (field extracted, all fields extracted, closed, post-processed) with Variables, Transcript and Turns, and an overview of the AI Conversations activities (conversation starting, turns, tool-called workflows, flows, evaluation, simulation, alerts, signed webhooks). Use this skill when requests mention Orchard Core AI workflows, AI completion tasks, AI Conversations workflow activities, reacting to chat session events, or an AI workflow task that fails or draws blank.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Workflows

With `OrchardCore.Workflows` enabled, CrestApps AI Services adds two completion tasks and four chat session events.

## Guidelines

- **AI Completion using Profile** needs `CrestApps.OrchardCore.AI` and `OrchardCore.Workflows`.
- **AI Completion using Direct Config** additionally needs `CrestApps.OrchardCore.AI.Chat.Interactions`, because it reuses the chat interaction configuration model and completion pipeline.
- The chat session events need an enabled chat feature (which brings `CrestApps.OrchardCore.AI.Chat.Core`) plus `OrchardCore.Workflows`. Do not enable `CrestApps.OrchardCore.AI.Chat.Core` directly; enable a public chat feature such as `CrestApps.OrchardCore.AI.Chat`.
- Both tasks render their prompt with Liquid, save a non-empty reply as an `AIResponseMessage` in `Workflow.Output[ResultPropertyName]` (default `ChatResponse`), and end on **Done**, **Drew Blank**, or **Failed**.
- Prefix result names with `AI-` to avoid collisions, and read the text with `.Content`.
- Prefer the profile task in production so deployment, instructions, and capabilities are governed in one place.
- Install CrestApps packages in the web/startup project.

## Enable

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "OrchardCore.Workflows"
      ],
      "disable": []
    }
  ]
}
```

Drop `CrestApps.OrchardCore.AI.Chat.Interactions` if you only use the profile task, and `CrestApps.OrchardCore.AI.Chat` if you do not react to chat sessions.

## AI Completion using Profile

1. Create an AI profile with a chat deployment and the capabilities the workflow needs.
2. Add **AI Completion using Profile**, select the profile, write a Liquid **Prompt template**, and set a **Result Property Name** such as `AI-order-summary`.
3. Wire all three outcomes.

The template is rendered with the selected profile available as `Profile`. The task builds the completion context from the profile, resolves the chat deployment through the `chat` slot (`IAIDeploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, ...)`), and sends the prompt as a user message.

```liquid
Summarize this order for the support team.
Order number: {{ Workflow.Input.OrderNumber }}
Customer note: {{ Workflow.Input.CustomerNote }}
```

```liquid
{{ Workflow.Output["AI-order-summary"].Content }}
```

| Outcome | When |
|---|---|
| **Failed** | Unknown profile, empty rendered prompt, no resolvable chat deployment, or an exception |
| **Drew Blank** | The model returned no text |
| **Done** | A reply was stored |

## AI Completion using Direct Config

The task owns its configuration. It stores it as an embedded `ChatInteraction` (the task's `Interaction` property), builds the completion context with `IAICompletionContextBuilder`, and resolves the interaction's chat deployment through the `chat` slot. The editor uses the chat interaction tabs, and each enabled feature contributes its own fields:

| Tab | Fields | Contributed by |
|---|---|---|
| **Content** | Result property name, prompt template, orchestrator, chat and utility deployments, system instructions, max tokens, temperature, top P, penalties | AI Services |
| **Content** | Prompt template selection | `CrestApps.OrchardCore.AI.Prompting` |
| **Content** | Persona | `CrestApps.OrchardCore.AI.Personas` |
| **Knowledge** | Data source, restrict to retrieved data, strictness, retrieved documents, OData filter. A blank strictness or retrieved documents uses the **Default strictness** / **Default top documents** of the **Data Sources** settings card (built-in default 5 documents) | `CrestApps.OrchardCore.AI.DataSources` |
| **Knowledge** | Document retrieval mode and uploaded knowledge files | `CrestApps.OrchardCore.AI.Documents` module |
| **Capabilities** | Tools | AI Services |
| **Capabilities** | Tool instances | `CrestApps.OrchardCore.AI.ToolInstances` |
| **Capabilities** | MCP connections | `CrestApps.OrchardCore.AI.Mcp` |
| **Capabilities** | A2A connections | `CrestApps.OrchardCore.AI.A2A` |

The deployment pickers list deployments by model capability (see `orchardcore-ai-model-capabilities`), not by the removed deployment purpose. Uploaded documents are chunked and indexed against the activity's embedded interaction, so they are retrieved whenever the task runs.

| Outcome | When |
|---|---|
| **Failed** | Empty rendered prompt, no chat deployment with a valid connection, or an exception |
| **Drew Blank** | The model returned no text |
| **Done** | A reply was stored |

A workflow that runs in the background (timer, deferred task) has no caller, so tool access checks are skipped. One triggered inside a user's request runs as that user, and a direct-config task can then drop tools that user may not access. Select only the tools the workflow genuinely needs.

## Usage metering

Both tasks run inside an `AIUsageScope` with the **Workflow** category, so their tokens appear under that category in **AI Usage Analytics** (`orchardcore-ai-usage-analytics`).

## Chat session events

All events use the chat session ID as the workflow correlation ID and accept an optional **Profile** filter (`ProfileId`). Every event input contains `SessionId`, `ProfileId`, `Session` (`AIChatSession`), `Profile` (`AIProfile`). **Field Extracted**, **Closed** and **Post-Processed** also carry the conversation `Variables`; **Closed** and **Post-Processed** also carry `Transcript` (one `Customer:` or `Assistant:` line per turn) and `Turns` (`AIConversationTurn` records). An extracted field is also stored as a conversation variable.

| Event | Triggers | Extra input |
|---|---|---|
| **AI Chat Session Field Extracted** (`AIChatSessionFieldExtractedEvent`) | Each newly extracted data field | `FieldName`, `Value`, `IsMultiple`, `Timestamp` |
| **AI Chat Session All Fields Extracted** (`AIChatSessionAllFieldsExtractedEvent`) | Once every configured extraction field has a value | `ExtractedData`, `Timestamp` |
| **AI Chat Session Closed** (`AIChatSessionClosedEvent`) | A session closes during extraction processing or by the inactivity close task | `ClosedAtUtc` |
| **AI Chat Session Post-Processed** (`AIChatSessionPostProcessedEvent`) | Post-session tasks complete for the first time | `Results` |

`DataExtractionChatSessionHandler` raises the extraction and closed events; `PostSessionProcessingChatSessionHandler` raises the post-processed event only when the tasks completed in that run, so it is not delivered twice. The background `AIChatSessionCloseBackgroundTask` raises the closed event for inactive sessions.

### Field-extraction workflow

1. Configure data extraction on a chat profile (`orchardcore-ai-chat`).
2. Start a workflow with **AI Chat Session Field Extracted** and optionally select the profile.
3. Use `{{ Workflow.Input.FieldName }}` and `{{ Workflow.Input.Value }}` in later activities.
4. Make the workflow idempotent; several fields can fire separate executions for one session.

### Post-session workflow

1. Configure post-session processing on the profile.
2. Start a workflow with **AI Chat Session Post-Processed**.
3. Read `Workflow.Input.Results` and route or store them.

## AI Conversations activities

With `OrchardCore.Workflows` and AI Services enabled, the **AI Conversations** category adds activities for
website chats, text threads and AI phone calls. Their input is `AIConversationWorkflowInput` (`Channel`,
`Direction`, `SessionId`, `ProfileId`, `ActivityId`, `ContactId`, `ContactName`, `Contact`, `CustomerAddress`,
`BusinessAddress`, `Variables`, `Simulation`).

| Activity | Kind | Outcomes |
|---|---|---|
| **AI Conversation Starting** | Event, before the first message (3 s budget inbound, 30 s outbound) | Done |
| **AI Conversation Turn** | Event, after each saved turn (`Text`, `Role`) | Done |
| **AI Tool Called** | Event, run by a **Workflow** tool instance (`Arguments`, `ToolName`) | Done |
| **AI Conversation Flow Started** / **AI Conversation Stage** | Event / task, a staged conversation flow with a `moveToStage` tool | one outcome per way out, **Not in a conversation** |
| **Set Conversation Variables** | Task, `name: value` per line | Done, No conversation |
| **Use AI Profile** | Task, answer with another profile or a profile version | Done, Not starting |
| **Decline Conversation** | Task, cancel or refuse (not a web chat) | Done, Not starting |
| **Evaluate AI Conversation** | Task | Passed, Failed, Not evaluated |
| **Simulate AI Conversation** | Task | Passed, Failed, Error |
| **Rerun AI Conversation Analysis** | Task | Done, Nothing to analyze, Not found |
| **Split Traffic** | Task, weighted outcomes with a sticky key | your outcomes |
| **Check AI Conversation Metric** | Task | Alert started, Still alerting, Resolved, Normal |
| **Update Contact Memory** | Task | Updated, Nothing to remember |
| **AI Conversation Evaluated**, **AI Guardrail Triggered**, **AI Alert Triggered**, **AI Alert Resolved** | Events | Done |
| **Send Signed Webhook** (category **HTTP**) | Task, HMAC-signed POST with retries | Delivered, Failed |

The Contact Center adds **AI Call Action** and **Transfer AI Call**, and Omnichannel Management adds **Create
AI Activity**. Behaviour, services and recipes are in `orchardcore-ai-conversations`.

## Upgrading from 2.x

- Deployments are resolved through the `chat` slot by model capability; the deployment purpose is gone. Workflows that named a deployment keep using it; an empty selection resolves through the `chat` slot's defaults (`orchardcore-ai-model-capabilities`).
- Both tasks are now metered per request under the **Workflow** usage category.
- If the workflow uses a persona, enable `CrestApps.OrchardCore.AI.Personas` to get the persona picker on the direct-config task (`orchardcore-ai-personas`).

## Troubleshooting

| Symptom | Check |
|---|---|
| Direct-config task missing from the activity picker | Enable `CrestApps.OrchardCore.AI.Chat.Interactions`. |
| Task ends on **Failed** at once | The prompt rendered empty, the profile was deleted, or no chat deployment has a connection. Check the log. |
| Task ends on **Drew Blank** | The model replied with no text; check the deployment and the prompt. |
| Chat events never fire | No chat feature is enabled, data extraction or post-processing is not configured, or the profile filter does not match. |
| Output is empty in a later activity | The result property name differs, or the read omits `.Content`. |

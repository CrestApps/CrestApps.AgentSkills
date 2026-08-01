---
name: orchardcore-ai-workflows
description: Skill for integrating CrestApps AI Services with Orchard Core Workflows. Covers AI completion tasks using profiles or direct configuration, Liquid prompt rendering, AI response workflow output, selectable workflow tools, and AI chat session lifecycle events for field extraction, session closure, and post-session processing. Use this skill when requests mention Orchard Core AI Workflows, AI Completion using Profile, AI Completion using Direct Config, AIChatSessionClosedEvent, workflow AI profile, AI chat session events, or closely related CrestApps implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.AI, AICompletionFromProfileTask, AICompletionWithConfigTask, DataExtractionChatSessionHandler, PostSessionProcessingChatSessionHandler, AIChatSessionFieldExtractedEvent, AIChatSessionPostProcessedEvent.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Workflows - Prompt Templates

## Use AI Services in Orchard Core Workflows

You are an Orchard Core expert. Generate workflow configurations and extension code that use CrestApps AI Services. The AI workflow activities are registered by the AI Services module only when `OrchardCore.Workflows` is enabled.

### Guidelines
- Enable `CrestApps.OrchardCore.AI` and `OrchardCore.Workflows` before adding AI activities to a workflow.
- Use **AI Completion using Profile** when a reusable AI Profile defines orchestration, deployments, system instructions, and selected capabilities.
- Use **AI Completion using Direct Config** only when the workflow must own its deployment, system message, generation settings, and tool selection.
- Both completion tasks render their prompt template with Orchard Liquid before sending a user chat message.
- Both tasks save a non-empty response as an `AIResponseMessage` in `Workflow.Output`.
- Use a unique result property name, preferably prefixed with `AI-`, to avoid collisions with other workflow output.
- Read the text output through `.Content`, for example `Workflow.Output["AI-summary"].Content`.
- Both task activities have `Done`, `Drew Blank`, and `Failed` outcomes. Branch workflows deliberately for all three.
- The profile task fails if the selected profile, generated prompt, or chat deployment cannot be resolved.
- The direct-config task fails if the deployment lacks a valid connection, the prompt is empty, or the completion throws.
- Direct-config tasks may select registered non-system tools. The task configures automatic tool use and honors the configured maximum function-iteration limit.
- AI workflow event activities filter by optional `ProfileId` and use the chat session ID as their workflow correlation ID.
- The field-extraction, all-fields-extracted, closed, and post-processed events require AI chat session processing plus `OrchardCore.Workflows`.
- Do not place secrets, access tokens, or untrusted raw input in workflow prompt templates.
- Install CrestApps packages in the web/startup project.

### Feature Overview

| Feature | Feature ID | Purpose |
|---|---|---|
| AI Services | `CrestApps.OrchardCore.AI` | AI profiles, deployments, completion tasks, and workflow event activities |
| AI Chat Services | `CrestApps.OrchardCore.AI.Chat.Core` | Chat-session services required by chat lifecycle processing |
| Orchard Core Workflows | `OrchardCore.Workflows` | Workflow runtime and activity registration |

### Install and Enable

Install AI Services in the web/startup project:

```shell
dotnet add package CrestApps.OrchardCore.AI
```

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Core",
        "OrchardCore.Workflows"
      ],
      "disable": []
    }
  ]
}
```

`CrestApps.OrchardCore.AI.Chat.Core` is an enabled-by-dependency feature. Enable an appropriate chat feature that depends on it, such as `CrestApps.OrchardCore.AI.Chat`, if the workflow also reacts to live profile chat sessions.

### AI Completion Using Profile

Use **AI Completion using Profile** for governed, reusable behavior:

1. Create an AI Profile with a valid chat deployment and any required tools.
2. Add **AI Completion using Profile** to the workflow.
3. Select the profile.
4. Write a Liquid prompt template.
5. Set a unique result property, such as `AI-order-summary`.
6. Connect the `Done`, `Drew Blank`, and `Failed` outcomes.

The task renders the Liquid template with the selected profile available as `Profile`, builds an `AICompletionContext`, resolves the profile chat deployment, and sends the rendered prompt.

Example prompt template:

```liquid
Summarize this order for the support team.
Order number: {{ Workflow.Input.OrderNumber }}
Customer note: {{ Workflow.Input.CustomerNote }}
```

Read the response in a later workflow activity:

```liquid
{{ Workflow.Output["AI-order-summary"].Content }}
```

### AI Completion Using Direct Config

Use **AI Completion using Direct Config** for a specific workflow-owned completion:

1. Add the task.
2. Select a chat deployment.
3. Enter a Liquid prompt template.
4. Optionally enter a system message.
5. Set temperature, top P, penalties, and maximum output tokens.
6. Select approved local tools only when the task needs them.
7. Set a unique result property.

The task creates a chat client through `IAIClientFactory`, applies the selected deployment, then sends the optional system message and rendered user prompt.

### Use Tools in Direct Config Tasks

The task editor lists `AIToolDefinitionOptions.GetSelectableTools()`. System tools are not shown. Selected names become `AICompletionWithConfigTask.ToolNames`.

At runtime the task:

1. Enables `ChatToolMode.Auto`.
2. Builds function invocation middleware.
3. Resolves each name through `IAIToolsService`.
4. Adds found tools to `ChatOptions.Tools`.
5. Executes the completion.

Give workflow identities only the dynamic per-tool permissions they require. Do not select management or destructive tools simply because a workflow is administrator-triggered.

### Chat Lifecycle Workflow Events

| Activity | When it triggers | Key input |
|---|---|---|
| `AIChatSessionFieldExtractedEvent` | A newly extracted field is recorded | `FieldName`, `Value`, `IsMultiple`, `Session`, `Profile` |
| `AIChatSessionAllFieldsExtractedEvent` | Extraction processing completes with the session data | `ExtractedData`, `Session`, `Profile` |
| `AIChatSessionClosedEvent` | A session is closed during extraction processing | `ClosedAtUtc`, `Session`, `Profile` |
| `AIChatSessionPostProcessedEvent` | Configured post-session tasks complete | `Results`, `Session`, `Profile` |

Every event supplies `SessionId`, `ProfileId`, `Session`, `Profile`, and a timestamp as applicable. Configure an optional `ProfileId` on the event activity to scope a workflow to one profile.

### Build a Field-Extraction Workflow

1. Create a chat profile with data extraction configured.
2. Create a workflow starting with **AI Chat Session Field Extracted**.
3. Optionally choose the profile ID.
4. Use `Workflow.Input.FieldName` and `Workflow.Input.Value` in later activities.
5. Add an idempotency check because multiple fields can trigger independent executions.

The `DataExtractionChatSessionHandler` triggers the event after the shared extraction handler records a new field change. The correlation ID is the chat session ID.

### Build a Post-Session Workflow

1. Configure post-session processing for the profile.
2. Create a workflow beginning with **AI Chat Session Post-Processed**.
3. Use `Workflow.Input.Results` for task results.
4. Record or route the results without repeating the session's post-processing action.

`PostSessionProcessingChatSessionHandler` triggers this only when `PostSessionTasksCompletedNow` is true, preventing repeat event delivery for already completed work.

### Outcomes and Error Handling

- Route `Done` to activities that consume `AIResponseMessage.Content`.
- Route `Drew Blank` to a fallback, retry queue, or human review path.
- Route `Failed` to error handling with non-sensitive diagnostic context.
- Keep prompts concise and validate workflow input before it reaches Liquid rendering.
- Prefer profiles for production workflows so deployment and capability policy are centrally maintained.

### Security Checklist

- Use the least-privileged AI Profile and tool set for each workflow.
- Limit workflow access and event triggers to trusted roles and profiles.
- Treat AI output as untrusted data before using it in content updates, notifications, or external calls.
- Avoid embedding secrets in Liquid prompts or workflow output.
- Test all three completion outcomes and each chat event with representative data.

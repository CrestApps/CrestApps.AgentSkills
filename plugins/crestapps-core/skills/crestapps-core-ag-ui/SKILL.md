---
name: crestapps-core-ag-ui
description: Skill for running the CrestApps.Core 2.0 AI chat as an AG-UI agent. Covers the CrestApps.Core.AI.AGUI package (AGUIRunEventWriter, AGUIRunRequest, AGUIJson), AIChatHubCore.RunAgent over SignalR and MapAGUIChat with Server-Sent Events, the RunAgentInput members the chat reads and the events it streams, tool approvals with RequireApproval, ApprovalRequiredAIFunction, interrupts and resume, page context with crestAppsAGUI.registerContextProvider, shared state with registerStateProvider and update_shared_state, frontend tools, stored tool calls (ChatTurnToolActivity), runs that continue on the next page, AGUIChatOptions.DisconnectedRunTimeout, the protocol and sendPageContext chat options, and the AICompletionContextKeys and ChatResponseHandlerContext members. Use it when showing tool calls in the chat, asking a person before a tool runs, sharing page state with the agent, connecting CopilotKit or another AG-UI client, or troubleshooting approvals and continued runs.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AG-UI

[AG-UI](https://docs.ag-ui.com) is an event protocol between an agent and the app a person uses it in. A client starts a *run* with a `RunAgentInput` (thread, newest message, the tools it runs itself, shared state, page context) and the agent streams events back: `RUN_STARTED`, text, reasoning, tool calls and results, state changes, then `RUN_FINISHED` or `RUN_ERROR`. CrestApps runs the whole AI chat pipeline (profiles, orchestration, retrieval, tools, guardrails, session history) behind it.

| Transport | Used by |
|---|---|
| The chat hub's `RunAgent(JsonElement input, CancellationToken)` method (SignalR, returns a `ChannelReader<JsonElement>` of events) | The CrestApps chat UI and widget (`ai-chat.js`), by default |
| `MapAGUIChat<THub>()` (HTTP `POST`, Server-Sent Events) | Any AG-UI client: CopilotKit, the AG-UI client SDKs, your own app |

Both go through the same hub, so the hub's profile and session authorization, rate limits and guardrails apply to both.

## Guidelines

- AG-UI has no feature to enable. Every hub that derives from `AIChatHubCore<TClient>` has `RunAgent`, and `AddCoreAIOrchestration()` (called by `AddAISuite()`) registers the shared state tool, the frontend tool provider and the page context handler.
- Map `MapAGUIChat` only when other AG-UI clients should reach your profiles over HTTP, and add authorization and rate limiting to the builder it returns.
- Mark tools that change data with `.RequireApproval()`. They only run after a person approves the call, and are never offered where nobody can answer.
- Page context and shared state are data the browser sends. The model receives them as data, never as instructions; do not put secrets in them.
- Chat Interactions (`ChatInteractionHubBase`) and realtime voice conversations do not speak AG-UI yet; they keep the text-only stream.

## Packages

| Package | What it has |
|---|---|
| `CrestApps.Core.AI.AGUI` | `AGUIRunEventWriter` (chat updates to AG-UI events), `AGUIRunRequest` (what a run input asks for: prompt, `Resume`, `ClientToolResults`, `IsResume`, `TryGetApproval`) and `AGUIJson` (`SerializerOptions`, `SerializeEvent`, `DeserializeInput`), on the MIT-licensed AG-UI .NET SDK (`AGUI.Abstractions`, `AGUI.Formatting` 1.0.1) |
| `CrestApps.Core.AI.Chat` | `AIChatHubCore.RunAgent`, resumable runs, `AGUIChatOptions` and `MapAGUIChat` (namespace `CrestApps.Core.AI.Chat.AGUI`) |
| `CrestApps.AI.Resources` (`@crestapps/ai-chat-ui`) | The chat UI's AG-UI client: tool cards, reasoning, approvals, page context, shared state (`agui-activity.css` styles) |

## Setup

```csharp
using CrestApps.Core.AI.Chat.AGUI;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
    )
);

// Optional: how long a run keeps going after its connection drops (two minutes by default; TimeSpan.Zero ends it with the connection).
builder.Services.Configure<AGUIChatOptions>(options => options.DisconnectedRunTimeout = TimeSpan.FromMinutes(1));

var app = builder.Build();

app.MapHub<AIChatHub>("/hubs/ai-chat");

// Optional: POST api/ai/agui (AGUIEndpointRouteBuilderExtensions.DefaultPattern) with a RunAgentInput.
app.MapAGUIChat<AIChatHub>()
    .RequireAuthorization();
```

- Without the builder: `services.AddCoreServices().AddCoreAIServices().AddCoreAIOrchestration();`.
- A hub whose client contract extends `IAIChatHubClient` uses `MapAGUIChat<THub, TClient>(pattern)`.
- The endpoint reads only `Content-Type: application/json` (a cross-site page cannot send that without a CORS preflight), so it calls `DisableAntiforgery()` and needs no token.
- The chat UI uses `RunAgent` by default (`protocol: 'agui'`). Set `protocol: 'legacy'` in the chat configuration to keep the text-only `SendMessage` stream; a hub without `RunAgent` makes the UI fall back by itself.

| Want | Add |
|---|---|
| Tool calls, reasoning, citations and shared state in the chat | Nothing: the chat UI and widget use AG-UI by default |
| Other AG-UI clients over HTTP | `app.MapAGUIChat<AIChatHub>()` |
| A person approves a tool before it runs | `.RequireApproval()` on the tool |
| The agent works on the page and keeps going on pages it opens | WebMCP page tools (`crestapps-core-webmcp`) |
| No page context sent to the model | `sendPageContext: false` in the chat configuration |

## The Run Input

| `RunAgentInput` member | What the chat does |
|---|---|
| `threadId` | The chat session id. Empty starts a new session; its id comes back in `RUN_STARTED` |
| `runId` | Echoed in `RUN_STARTED` and `RUN_FINISHED` |
| `messages` | Only the newest user message is read (the server keeps the history), plus `role: "tool"` results of calls that waited for the client |
| `forwardedProps.profileId` | The AI profile (required for a new message). `forwardedProps.sessionProfileId` names a session-level prompt profile |
| `context` | Page context entries |
| `state` | The shared state |
| `tools` | Frontend tools the client runs itself |
| `resume` | Answers to the previous run's interrupts |

## The Events

| Event | When |
|---|---|
| `RUN_STARTED` / `RUN_FINISHED` / `RUN_ERROR` | Every run. `RUN_FINISHED` has a `success` outcome (with `pendingToolCallIds` when frontend tools wait) or an `interrupt` outcome. Hub errors (unknown profile, refused session, rate limit) end with `RUN_ERROR`; a refused session start has the code `session_start_rejected` |
| `TEXT_MESSAGE_START` / `CONTENT` / `END` | The answer. Its message id is the stored assistant message id; text written again after a tool call is a new message whose id ends with `-1`, `-2`, ... |
| `REASONING_START` ... `REASONING_END` | Visible reasoning, when the model streams it |
| `TOOL_CALL_START` / `ARGS` / `END` / `RESULT` | Every tool call and result (profile, MCP, agent, page and shared state tools). Results are cut to 4,000 characters |
| `STATE_SNAPSHOT` / `STATE_DELTA` | The shared state and each change, as JSON Patch |
| `CUSTOM` | `crestapps.assistant_message`, `crestapps.references`, `crestapps.message_replaced` (output checks replaced what streamed), `crestapps.tool_calls_resolved`, `crestapps.continues_on_next_page` |

Tool calls are stored with the assistant message as a `ChatTurnToolActivity` (`ToolCalls` of `ChatTurnToolCall`: `CallId`, `Name`, `Arguments`, `Result`, `Status`, `ApprovalRequestId`; `ContinuesOnNextPage`; `HasPendingCalls`). `ChatTurnToolCallStatus` is `Requested`, `Completed`, `AwaitingApproval`, `Approved`, `Rejected` or `AwaitingClient`. The session payload returns them (`AIChatResponseMessageDetailed.ToolCalls`) and the history replays calls and results to the model in later turns.

## Tool Approvals

```csharp
using CrestApps.Core.AI.Tooling;

builder.Services
    .AddCoreAITool<DeleteArticleTool>(DeleteArticleTool.TheName)
        .WithDescription("Deletes an article.")
        .Selectable()
        .RequireApproval();
```

1. In an AG-UI run, `AIChatResponseHandler` sets `AICompletionContextKeys.ToolApprovalSupported`, `ToolMaterializationOptions.SupportsToolApproval` is `true`, and the tool is wrapped in `ApprovalRequiredAIFunction`.
2. When the model calls it, the tool does not run: the run ends with an `interrupt` outcome naming each call and asking for `{ "approved": true | false }`, and the paused message is stored with the calls `AwaitingApproval`. When the model calls several tools at once and one needs approval, the whole batch waits.
3. The chat UI shows **Allow** and **Deny**. It accepts the click only from the `<button>` it rendered in that call's `.ai-chat-agui-approval` row while the call is waiting; the answer sanitizer strips every `data-agui-*` attribute from model output, so an answer cannot forge an approval.
4. The answer starts a new run with `resume`. Approved calls run, rejected ones never run and the model is told, and an interrupt the client did not answer counts as rejected.

```json
{
  "threadId": "<session id>",
  "runId": "<new run id>",
  "messages": [],
  "resume": [{ "interruptId": "<interrupt id>", "status": "resolved", "payload": { "approved": true } }],
  "forwardedProps": { "profileId": "<profile id>" }
}
```

Requests that cannot ask (the `SendMessage` stream, agents inside a tool call, realtime sessions, the Claude orchestrator, background work, other channels) are not offered the tool at all; each skip counts on `crestapps.core.ai.tool.denied` with `reason=approval`. A custom `IToolRegistryProvider` sets `ToolRegistryEntry.RequiresApproval` on its own entries. WebMCP tools marked `consequentialHint` require approval in an AG-UI run unless the profile has `AllowConsequentialPageTools`. Resuming a run counts against the per-message rate limit (`EvaluateMessageRateLimitAsync`), once per resume.

## Page Context

The chat sends the page title, address and the user's last text selection with each message. Scripts add their own entries:

```javascript
window.crestAppsAGUI.registerContextProvider(() => ({
    description: 'Record being edited',
    value: `Article #${articleId}: ${document.querySelector('#Title').value}`,
}));
```

At most 20 entries of 2,000 characters reach the model (and at most 32,000 characters of shared state), in the system message as data (`ClientContextOrchestrationHandler`). On the server they are `ChatResponseHandlerContext.ClientContext` (`ChatClientContextEntry`: `Description`, `Value`) and `AICompletionContextKeys.ClientContext`. A script that runs before `ai-chat.js` can preset `window.crestAppsAGUI = { _contextProviders: [...], _stateProvider: {...} }`.

## Shared State

```javascript
window.crestAppsAGUI.registerStateProvider({
    get: () => ({ orderDetails: { giftMessage: gift.value, deliveryDate: date.value } }),
    apply: (state, delta) => {
        gift.value = state.orderDetails?.giftMessage ?? '';
        date.value = state.orderDetails?.deliveryDate ?? '';
    },
});
```

When a run carries state, the model sees it and gets the `update_shared_state` tool (`UpdateSharedStateFunction.FunctionName`), which applies JSON Patch `add`, `replace` and `remove` operations through `JsonPatchApplier` to a `ChatSharedState` (`Value`, `OnChangedAsync`). Each change streams as `STATE_DELTA`; the chat calls `apply` and fires a `crestapps:agui-state` event on `document`. The server does not store the state; it uses what each run sends (`ChatResponseHandlerContext.SharedState`, `AICompletionContextKeys.SharedState`).

## Frontend Tools

Tools in `RunAgentInput.tools` are offered to the model as declarations (`ClientToolRegistryProvider`, `ChatResponseHandlerContext.ClientTools`, `AICompletionContextKeys.ClientTools`). A call ends the run with `RUN_FINISHED` whose `pendingToolCallIds` name the calls; the client runs them and sends `role: "tool"` messages with the `toolCallId` in its next run:

```json
{
  "threadId": "<session id>",
  "runId": "<new run id>",
  "messages": [{ "id": "t1", "role": "tool", "toolCallId": "call_123", "content": "Sunny, 21 °C" }],
  "tools": [{ "name": "get_weather", "description": "Gets the weather in a city.", "parameters": { "type": "object", "properties": { "city": { "type": "string" } } } }],
  "forwardedProps": { "profileId": "<profile id>" }
}
```

The CrestApps chat UI does not use frontend tools; page tools reach it through WebMCP instead.

## Runs That Continue on the Next Page

With WebMCP page tools (`AddWebMcp(webMcp => webMcp.AddPageTools<AIChatHub>())`, **Allow page tools** on the profile, WebMCP scripts before the chat scripts), `page_navigate` answers first and opens the page 400 ms later. In a run on the chat hub that ends the turn:

1. The turn is saved with `ChatTurnToolActivity.ContinuesOnNextPage` and the run sends `crestapps.continues_on_next_page`.
2. The chat on the new page loads the session; when the same tab opened it and the last message is marked, it starts a run with `forwardedProps.continueRun = true` and `forwardedProps.continuePage` (title and address).
3. `ContinueAGUIRunAsync` clears the mark and tells the model which page loaded (`GetContinueOnNextPagePrompt`, overridable). At most five pages chain in a row; the user's next message starts a new chain. HTTP runs have no page to continue on.

A tool that ends the turn this way sets `AICompletionContextKeys.EndTurnAfterToolCalls` in `AIInvocationScope.Current.Items` when the completion context carries `AICompletionContextKeys.ContinuesOnNextPage`. A run whose connection dropped keeps going for `AGUIChatOptions.DisconnectedRunTimeout` so its answer is saved; a user Stop ends it at once, and the hub then sends the session to its group.

## Hub Extension Points

`AIChatHubCore<TClient>` virtuals for AG-UI: `RunAgent`, `HandleRunAgentAsync(AGUIChatTurn, IServiceProvider, CancellationToken)`, `ResumeAGUIRunAsync`, `ContinueAGUIRunAsync`, `GetContinueOnNextPagePrompt(string loadedPage)`, `GetInvalidRunInputMessage()`, `GetNothingToResumeMessage()`, `GetNothingToContinueMessage()`. `ProcessGeneratedPromptAsync` (template prompts) streams an AG-UI run chunk by chunk too.

## Providers

Clients from `IAIClientFactory` stream tool calls through `FunctionInvokingChatClient`; the Azure OpenAI client streams `FunctionCallContent` and `FunctionResultContent` on Chat Completions and Responses, pauses for approval-required tools and replays stored calls. `NamedAICompletionClient` passes tool result messages to the provider and drops results whose calls a past-message limit cut off.

## Try It

Both sample hosts map `MapAGUIChat<AIChatHub>()`. On the **WebMCP Playground** with a profile that allows page tools, "Place the order" waits for **Allow** or **Deny** (`store.place_order` is consequential), and "Write a gift message for my sister" fills the shared **Order details** card.

```bash
curl -N -X POST https://localhost:5100/api/ai/agui -H "Content-Type: application/json" \
  -d '{"threadId":"","runId":"r1","messages":[{"id":"m1","role":"user","content":"Hello"}],"tools":[],"context":[],"forwardedProps":{"profileId":"<profile id>"}}'
```

## Troubleshooting

- **No tool cards or approvals**: the chat config sets `protocol: 'legacy'`, or the page loads an older `ai-chat.js` that has no AG-UI client.
- **An approval tool never appears**: the request cannot ask (agent, realtime, background, `SendMessage`); check `crestapps.core.ai.tool.denied` with `reason=approval`.
- **`RUN_ERROR` with `session_start_rejected`**: the session-start rate limit refused a new session.
- **The turn does not continue on the next page**: the new page lacks the WebMCP or chat scripts, another tab opened it, five pages already chained, or the run came over HTTP.
- **`415`/`400` from `MapAGUIChat`**: send `Content-Type: application/json` with a valid `RunAgentInput`.

See `crestapps-core-chat-interactions` for the hubs and chat UI, `crestapps-core-custom-tools` for tool registration, and `crestapps-core-webmcp` for page tools.

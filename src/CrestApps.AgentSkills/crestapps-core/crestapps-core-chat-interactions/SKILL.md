---
name: crestapps-core-chat-interactions
description: Skill for Chat Interactions and the shared chat hubs and chat UI in CrestApps.Core 2.0. Covers AddChatInteractions with AddEntityCoreStores or AddYesSqlStores, ConfigureVisitorIdentity, ConfigureChatRateLimiting and ConfigureChatHubOptions, subclassing ChatInteractionHubBase and AIChatHubCore with AuthorizeAsync, AuthorizeProfileAsync, owner-only AuthorizeSessionAsync, EvaluateMessageRateLimitAsync and the other hooks, the ChatInteraction model and IChatInteractionSettingsHandler, send-time tool access checks, speech limits, document endpoints with antiforgery, ownership and MaxDocumentsPerSession, the IChatInteractionHubClient contract, the @crestapps/ai-chat-ui scripts with chat-markers, allowedImageHosts, the answer sanitizer and CoreAIChatSurface, assistant name and avatar, and upgrading from 1.x. Use it to stand up a chat playground for a provider, build a chat page or widget, extend interaction settings, or troubleshoot chat hubs and chat UI.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Chat Interactions

A **Chat Interaction** is an operator-controlled conversation that needs no AI Profile: the interaction itself stores its deployments, system message, sampling, tools, agents, MCP connections, data source and documents. It is the fastest way to validate a provider and deployment. **AI Chat sessions** (`AIChatSession`) are the profile-based equivalent used by end-user chat pages and widgets. Both ship in `CrestApps.Core.AI.Chat` and share the same chat UI scripts.

## Guidelines

- Start with Chat Interactions to validate a connection and deployment, then move reusable behavior into an AI Profile (`crestapps-core-ai-profiles`).
- Register stores for `ChatInteraction` and `IChatInteractionPromptStore` on the chat interactions builder.
- The framework does not ship a mapped hub. Subclass `ChatInteractionHubBase` (and `AIChatHubCore<TClient>` for sessions), map it, and call `ConfigureChatHubOptions<THub>()` for each concrete hub.
- The hubs' authorization hooks return `true` by default, except `AuthorizeSessionAsync` on `AIChatHubCore<TClient>`, which only lets a session's owner in. Override them or put `[Authorize]` on the hub.
- Chat Interactions do not get the AI Profile prompt-security preamble or injection blocking (the operator controls the prompt), but they do get input-boundary delimiters and a per-user tool access check at send time.
- Use `crestapps-core-chat` for server-side completion code, `crestapps-core-signalr` for hub routes and authentication, `crestapps-core-realtime-voice` for voice sessions, and `crestapps-core-ag-ui` for the AG-UI runs (`RunAgent`, tool cards, approvals, shared state) the session chat uses by default.
- Cookie-authenticated hosts put the antiforgery token on the chat page, or document uploads and removals answer `400`.

## Registration

```csharp
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.OpenAI;
using CrestApps.Core.AI.Security;
using CrestApps.Core.Data.EntityCore;
using CrestApps.Core.SignalR;
using MyApp.Hubs;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddEntityCoreStores()
        .AddOpenAI()
        .AddSignalR(addStoreCommitterFilter: true)
        .AddChatInteractions(chatInteractions => chatInteractions
            .AddEntityCoreStores()
            .ConfigureChatHubOptions<ChatInteractionHub>()
            .ConfigureVisitorIdentity(options =>
            {
                options.RemoteAddressMode = AIVisitorRemoteAddressMode.Hashed;
            })
            .ConfigureChatRateLimiting(options =>
            {
                options.AnonymousSessionStartPartitions =
                    ChatRateLimitPartition.Visitor |
                    ChatRateLimitPartition.NetworkAddress;
            })
        )
    )
    .AddEntityCoreSqliteDataStore("Data Source=app.db")
);

// HubOptions<THub> is per concrete hub type; configure every chat hub you map.
builder.Services.ConfigureCrestAppsChatHubOptions<AIChatHub>();

var app = builder.Build();

app.MapHub<ChatInteractionHub>("/hubs/chat-interaction");
app.MapHub<AIChatHub>("/hubs/ai-chat");
```

Use `.AddYesSqlStores()` instead of `.AddEntityCoreStores()` for YesSql; YesSql hosts create the index tables with `CreateChatInteractionIndexSchemaAsync` and `CreateChatInteractionPromptIndexSchemaAsync` (see `crestapps-core-yessql-stores`).

`AddCoreAIChatInteractions()` (called by `AddChatInteractions`) registers visitor identity, chat notifications, the session-processing pipeline (`AddCoreAIChatSessionProcessing`: data extraction, post-session processing, the `AIChatSessionCloseBackgroundService` that closes inactive sessions every 5 minutes), `CitationReferenceCollector`, `CompositeAIReferenceLinkResolver`, `ChatInteractionCompletionContextBuilderHandler`, the built-in settings handlers (data source, prompt template, persona), and `OptionsRealtimeIceServerProvider`.

`ConfigureChatHubOptions<THub>()` uses `AddHubOptions<THub>` (a plain `services.Configure<HubOptions<THub>>` is silently ignored by SignalR) and sets a 10-minute client timeout, 15-second keep-alive, 10 MB maximum message size, 8 parallel invocations per client, and a stream buffer of 64.

## Host the Hub

`ChatInteractionHub` and `AIChatHub` above are host classes. The interaction hub looks like this; the session hub is in [chat-interactions-examples.md](references/chat-interactions-examples.md).

```csharp
using CrestApps.Core.AI.Chat.Hubs;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Authorization;

namespace MyApp.Hubs;

[Authorize]
public sealed class ChatInteractionHub : ChatInteractionHubBase
{
    public ChatInteractionHub(
        IServiceProvider services,
        TimeProvider timeProvider,
        ILogger<ChatInteractionHub> logger)
        : base(services, timeProvider, logger)
    {
    }

    protected override Task<bool> AuthorizeAsync(IServiceProvider services, ChatInteraction interaction)
        => Task.FromResult(string.Equals(interaction.OwnerId, Context.UserIdentifier, StringComparison.Ordinal));

    protected override Task<ChatMode> GetChatModeAsync(IServiceProvider services)
        => Task.FromResult(ChatMode.Conversation);
}
```

Other virtual hooks include `GetDeploymentSettingsAsync` (returns the `DefaultAIDeploymentSettings` used for slot defaults), `IsTextToSpeechPlaybackEnabledAsync`, `CollectStreamingReferences` (merge citations with `CitationReferenceCollector`), `OnAssistantPromptCreatedAsync`, `ValidateSettings`, and the localizable message methods (`GetNotAuthorizedMessage`, `GetSettingsValidationMessage`, `GetNoRealtimeDeploymentMessage`, `GetConversationDeploymentNotRealtimeMessage`, and others). `AIChatHubCore<TClient>` exposes `AuthorizeProfileAsync(IServiceProvider, AIProfile)` and `GetRateLimitedMessage(PromptSecurityResult)`, which returns a "slow down" message with the retry delay when the message rate limit is hit.

- `EvaluateMessageRateLimitAsync(services, profile, sessionId, cancellationToken)` applies `IChatRateLimiter` to input that does not pass through `ValidatePromptSecurityAsync`: `SendAudioStream`, `SynthesizeSpeech`, `StartConversation`, `StartRealtimeConversation`, `StartRealtimeWebRtc`, template prompts, blank messages, and resuming or continuing an AG-UI run. It runs after the profile is authorized and before a provider is called.
- A `TemplatePrompt` profile sent through `SendMessage` needs the `sessionProfileId` of the chat profile the caller is in; both profiles are authorized and the message is rate-limited before `ProcessGeneratedPromptAsync` runs. A missing or unknown session profile gets the same "not authorized" answer.
- `SynthesizeSpeech` refuses text over `MaxSpeechTextLength` (virtual, 10,000) with `GetSpeechTextTooLongMessage()` on both hubs; `AIChatHubCore` also refuses a profile without text-to-speech playback or Conversation mode (`IsSpeechSynthesisEnabledAsync`, virtual) with `GetTtsNotEnabledMessage()`.
- `ResolveVisitorIdentity(IServiceProvider)` is `protected static`, so a derived hub builds the same visitor identity.
- Over long polling the hub sets the connection's own `HttpContext` on `IHttpContextAccessor` for the invocation, so visitor identity and rate-limit keys work on every transport.

`AIChatHubCore<TClient>` also exposes `AuthorizeSessionAsync(IServiceProvider, AIChatSession)` and `ValidateOutputAsync(...)` (the output security filter, then the profile's guardrails; returns the replacement text for a blocked reply or `null`).

### Session Ownership

Loading, continuing, rating and acting on a session all call `AuthorizeSessionAsync` first. By default it allows only the signed-in user whose id (`ClaimTypes.NameIdentifier`, else the name) equals `AIChatSession.UserId`, or, for a visitor session, the same `ClientId` from `IAIVisitorIdentityResolver`. A session with neither is refused. A refused id is answered with `GetSessionNotFoundMessage()`, and a message sent to it starts a new session for the caller. Let staff open other users' conversations by widening it:

```csharp
protected override async Task<bool> AuthorizeSessionAsync(IServiceProvider services, AIChatSession chatSession)
{
    if (await base.AuthorizeSessionAsync(services, chatSession))
    {
        return true;
    }

    var authorizationService = services.GetRequiredService<IAuthorizationService>();

    return (await authorizationService.AuthorizeAsync(Context.User, "Admin")).Succeeded;
}
```

### Stop After Streaming

When the user presses Stop after the answer has streamed, the hubs still validate, save and finish the turn: the remaining work ignores the cancelled token, and `SendMessage` runs in a child scope the hub owns because SignalR disposes the invocation scope on cancel. A host that overrides `ExecuteInScopeAsync` with its own scope keeps that scope.

Hub methods called by the client (interaction hub; the session hub takes `profileId` and `sessionId` instead of `itemId`):

| Method | Purpose |
|---|---|
| `SendMessage(itemId, prompt)` | Streams `CompletionPartialMessage` items through a `ChannelReader` |
| `RunAgent(input)` | Session hub only: an AG-UI run that streams AG-UI events (see `crestapps-core-ag-ui`) |
| `LoadInteraction(itemId)` / `SaveSettings(itemId, settings)` / `ClearHistory(itemId)` | Load, update and reset an interaction |
| `StartConversation`, `SendAudioStream`, `SynthesizeSpeech` | Speech-to-text and text-to-speech conversation mode |
| `StartRealtimeConversation`, `StartRealtimeWebRtc`, `AddRealtimeIceCandidate`, `GetRealtimeIceServers`, `UpdateRealtimeSettings`, `UpdateRealtimeSpeechSpeed`, `StopConversation` | Realtime voice (see `crestapps-core-realtime-voice`) |
| `HandleNotificationAction(sessionId, notificationType, actionName)` | Chat notification buttons |
| `SetPageTools(tools)` / `ClearPageTools()` | Session hub only: the WebMCP tools the tab exposes (see `crestapps-core-webmcp`) |

`IChatInteractionHubClient` callbacks: `ReceiveError`, `LoadInteraction`, `SettingsSaved`, `HistoryCleared`, `ReceiveTranscript`, `ReceiveAudioChunk`, `ReceiveAudioComplete`, `ReceiveRealtimeAnswer`, `ReceiveRealtimeIceCandidate`, `ReceiveRealtimeEvent`, `ReceiveConversationUserMessage`, `ReceiveConversationAssistantToken`, `ReceiveConversationAssistantComplete`, `ReceiveNotification`, `UpdateNotification`, `RemoveNotification`.

## The Interaction and Its Settings

`SaveSettings` reads camelCase keys from the settings JSON: `title`, `orchestratorName`, `connectionName`, `deploymentName` (stored as `ChatDeploymentName`), `utilityDeploymentName`, `conversationDeploymentName`, `realtimeVoiceName`, `systemMessage`, `temperature`, `topP`, `frequencyPenalty`, `presencePenalty`, `maxTokens`, `pastMessagesCount`, `toolNames`, `agentNames`, `mcpConnectionIds`, plus whatever the registered `IChatInteractionSettingsHandler` implementations read (for example `filter`, `personaId`).

- `ChatDeploymentName` is the text model; `UtilityDeploymentName` runs background work; `ConversationDeploymentName` names the realtime model and, because a chat interaction has no chat mode of its own, naming it is the opt-in to speaking. A conversation deployment that is not realtime-capable is reported with the deployment name rather than silently replaced.
- The `filter` setting is validated with `IODataValidator` before any settings handler runs. An invalid filter rejects the save with `ReceiveError` ("Invalid filter value. It must be a valid OData filter.") and the interaction keeps its settings.
- Add your own settings with a handler; see [chat-interactions-examples.md](references/chat-interactions-examples.md).

## Security Behavior

- **Tool access at send time.** An interaction stores its own tool names, which are client-controllable, so every listable tool is re-checked with `IAIToolAccessEvaluator.IsAuthorizedAsync(user, toolName)` and unauthorized tools are dropped with one warning. System and hidden tools are not checked; a `null` caller (trusted server code) skips the check. AI Profile sessions do not apply this gate; the profile is their authorization boundary.
- **Input delimiters.** The user's message is wrapped in input-boundary delimiters, controlled by `PromptSecurityOptions.EnableChatInteractionInputDelimiters` (default `true`).
- **Anonymous visitors.** `AIVisitorIdentityOptions.AllowCrossSiteEmbedding` (off by default) writes the visitor cookie `SameSite=None; Secure; Partitioned` so a chat embedded in another site's frame keeps its visitor. Rate limiting for anonymous traffic uses tiers on `PromptSecurityOptions` (see `crestapps-core-prompt-security`).

## Documents

Upload and remove endpoints live in `CrestApps.Core.AI.Documents.Endpoints` and need document processing (`crestapps-core-document-processing`):

```csharp
app.AddUploadChatInteractionDocumentEndpoint();
app.AddRemoveChatInteractionDocumentEndpoint();
app.AddUploadChatSessionDocumentEndpoint();
app.AddRemoveChatSessionDocumentEndpoint();
app.AddDownloadAIDocumentEndpoint();
```

Each `Add*Endpoint()` returns the endpoint's `RouteHandlerBuilder` (add filters or metadata to it), so call them on `app` one by one; they no longer chain. Authorize them by registering `IAuthorizationHandler` implementations for `AIChatDocumentOperations.ManageDocuments` on `ChatInteraction` and on `AIChatSessionDocumentAuthorizationContext`.

- **Session uploads** authorize before creating or saving a session, treat an unknown or foreign session id as no session, count a session-starting upload on `IChatSessionStartRateLimiter` (`429` when throttled), and cap a session at `ChatDocumentsOptions.MaxDocumentsPerSession` (namespace `CrestApps.Core.AI.Ingestion`, default `20`, `0` disables; extra files come back in `failed`). Uploads to one session run one at a time per process; across instances use a store that checks concurrency, or sticky routing.
- **Ownership**: remove and download endpoints answer `404` for a session the caller does not own. A signed-in caller matches on `NameIdentifier` (on `Name` only when there is none); a visitor on the visitor id.
- **Interaction removal** only removes a document whose reference is that interaction (`chat-interaction`); other ids answer `404`.
- **Antiforgery**: the four upload and remove endpoints run `CookieAuthenticationAntiforgeryEndpointFilter`. It validates the `RequestVerificationToken` header or form field unless every identity came from a type in `AntiforgeryEndpointFilterOptions.NonCookieAuthenticationTypes` (namespace `CrestApps.Core.Filters`; `Bearer`, `Api`, `ApiKey`, `OpenIddict.Validation.AspNetCore` by default; add the authentication type your API scheme produces, which for the JWT bearer handler is `AuthenticationTypes.Federation` unless the host sets another). An `Authorization` header exempts nothing, and an anonymous request carrying a cookie in `AmbientCookieNames` (the visitor cookie) is validated too. `AddCoreAIDocumentProcessing` registers `AddAntiforgery()`.

```cshtml
@inject Microsoft.AspNetCore.Antiforgery.IAntiforgery Antiforgery

<meta name="RequestVerificationToken" content="@Antiforgery.GetAndStoreTokens(Context).RequestToken" />
```

The vendored scripts send the token (through `window.CoreAIAntiforgery` in `chat-markers.js`) from the chat config's `antiforgeryToken`, else that meta tag, else a hidden `__RequestVerificationToken` field, and only to same-origin URLs.

## Chat UI Assets

The browser scripts are published as `@crestapps/ai-chat-ui` (npm or jsDelivr). Load Vue 3, marked, DOMPurify and a pinned SignalR client first, then the CrestApps scripts:

```html
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui/dist/chat-widget.min.css" />
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui/dist/chat-image-carousel.min.css" />

<script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@10.0.11/dist/browser/signalr.min.js"
        integrity="sha384-N+pL/4qXZHZcvsa6hVMd5/7cdF3lC59ZMNHeQyRo5q9jmC7yw0hcJILPjUDSU7ZS"
        crossorigin="anonymous"></script>
<script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/vue@3/dist/vue.global.prod.js"></script>
<script src="https://cdn.jsdelivr.net/npm/dompurify@3/dist/purify.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui/dist/chat-markers.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui/dist/chat-image-carousel.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui/dist/ai-chat.min.js"></script>
```

- `chat-markers.js` (global `CoreAIChatMarkers`) holds the shared `[doc:n]`, `[fig:n]` and `[chart:{...}]` rules and must load before `ai-chat.js`. It also splits combined citations such as `[doc:1, doc:2]` and merges references that print the same source.
- `chat-image-carousel.js` is optional. With it, two or more adjacent pictures (and a single picture on its own line) become one swipeable stack with an S / M / L size control. A custom UI calls `CoreAIChatImageCarousel.stackAdjacentImages(html)` on sanitized HTML.
- `realtime-audio.js` (global `CoreAIRealtime`) is the shared voice client; `chat-interaction.js` and `ai-chat-widget.js` are the other entry points.
- Assistant name and avatar: `data-coreai-chat-assistant-label` and `data-coreai-chat-assistant-avatar-url` (Chat Interactions take `assistantLabel` and `assistantAvatarUrl` in `data-chat-interaction-config`). Resolve them from the linked persona with `await services.ResolveChatAssistantDisplayAsync(profile)` (namespace `CrestApps.Core.AI.Profiles`; a host without personas gets the default look).
- Pin package versions in production (`@crestapps/ai-chat-ui@<version>`), and pin SignalR with an `integrity` hash.
- AG-UI options in the chat config: `protocol` (`'agui'` by default, `'legacy'` for the text-only stream) and `sendPageContext` (default `true`). See `crestapps-core-ag-ui`.

### What an Answer May Render

`CoreAIImages.sanitize` (in `chat-markers.js`, used by both chat scripts and the MVC chat-interaction view) cleans every answer:

- Images load only from the page's origin, `data:`/same-origin `blob:`, or a host in the `allowedImageHosts` chat option (`'images.example.com'`, `'*.example.com'` for subdomains; empty means same-origin only). Other images become plain links. `<video>`, `<audio>`, `<source>`, `<track>`, SVG, MathML, `poster`, `background`, `srcset` and every `<input>` except the disabled task-list checkbox are removed.
- `style` keeps only size, spacing (no negative margins), text and color properties; `id` (except chart canvases `chat_chart_N`/`ci_chart_N`), `data-coreai-*`, `data-chat-interaction-*`, `data-agui-*`, `for`, `form`, `formaction`, `list`, `popovertarget`, `popovertargetaction`, `commandfor`, `command` and form-control `name` are removed, and every `<button>` becomes `type="button"`.
- Auto-start skips any chat configuration inside a rendered message or a mounted chat. A custom client calls `window.CoreAIChatSurface.isInsideRenderedMessage(element)` before starting a chat and `registerRoot(element)` for its mount; a custom settings script reads its controls with `CoreAIChatSurface.queryPageElements(selector)` / `queryPageElement(selector)`.

```js
window.coreAIChatManager().initialize({
    // ...
    allowedImageHosts: ['images.example.com', '*.cdn.example.com']
});
```

## Upgrading Within 2.0 Previews

- **The session hub only opens a session for its owner.** Hubs whose staff review other users' conversations through the hub override `AuthorizeSessionAsync`, as the sample hosts do for the `Admin` policy. Sessions with no `UserId` and no `ClientId` (for example ones started by another channel) are no longer opened by anyone unless the override allows them.
- A Stop click after the answer streamed no longer loses the turn; remove any client workaround that re-sent the message.
- The session chat runs over AG-UI (`RunAgent`) by default; set `protocol: 'legacy'` to keep `SendMessage`.
- Document `Add*Endpoint()` methods return `RouteHandlerBuilder`; split chained calls into separate statements.
- Uploads and removals need the antiforgery token on cookie-authenticated pages. Hosts with `AllowCrossSiteEmbedding` also write the antiforgery cookie `SameSite=None; Secure; Partitioned` and set `AntiforgeryOptions.SuppressXFrameOptionsHeader` (see `crestapps-core-prompt-security`).
- Template prompts need a `sessionProfileId`; blank messages, speech methods and AG-UI resumes now count against the message rate limit.
- Images from other hosts render as links until listed in `allowedImageHosts`; inline styles other than size, spacing, text and color are dropped from answers, and answers keep no `id`.
- A session the upload endpoint of an earlier version stamped with the user's `Name` (where it differs from `NameIdentifier`) no longer reads as that user's; update its `UserId`.

## Upgrading from 1.x

- The chat scripts are built with Vite for current browsers (Chrome/Edge 111, Firefox 114, Safari 16.4). URLs and globals are unchanged, but ES5 browsers no longer run them, the `*.css.map` files are gone, and Babel helpers are no longer page globals.
- `ChatMode.Realtime` and `AIProfile.RealtimeDeploymentName` are removed. Use `ConversationDeploymentName` on the interaction (or `ChatModeProfileSettings.ConversationDeploymentName` on a profile).
- AI Profile sessions no longer apply `IAIToolAccessEvaluator`; Chat Interactions now do, at send time. Keep a custom evaluator if you enforce per-user tool permissions.
- Load `chat-markers.js` before the chat scripts if you host them yourself.
- Configure hub options per concrete hub with `ConfigureChatHubOptions<THub>()` / `ConfigureCrestAppsChatHubOptions<THub>()`; a hub mapped without it runs on SignalR defaults.

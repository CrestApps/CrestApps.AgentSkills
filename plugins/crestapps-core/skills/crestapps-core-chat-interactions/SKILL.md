---
name: crestapps-core-chat-interactions
description: Skill for Chat Interactions and the shared chat hubs and chat UI in CrestApps.Core 2.0. Covers AddChatInteractions with AddEntityCoreStores or AddYesSqlStores, ConfigureVisitorIdentity, ConfigureChatRateLimiting and ConfigureChatHubOptions, subclassing ChatInteractionHubBase and AIChatHubCore with AuthorizeAsync, AuthorizeProfileAsync and the other override hooks, the ChatInteraction model with chat, utility and conversation deployment names, IChatInteractionSettingsHandler, SaveSettings filter validation, send-time tool access checks through IAIToolAccessEvaluator, input-boundary delimiters, document upload endpoints, the IChatInteractionHubClient contract, the @crestapps/ai-chat-ui scripts with chat-markers, the stacked image carousel, assistant name and avatar, and upgrading from 1.x. Use it to stand up the fastest chat playground for a provider, build a chat page or widget, extend interaction settings, or troubleshoot chat hubs and chat UI.
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
- The hubs' authorization hooks return `true` by default. Override them or put `[Authorize]` on the hub.
- Chat Interactions do not get the AI Profile prompt-security preamble or injection blocking (the operator controls the prompt), but they do get input-boundary delimiters and a per-user tool access check at send time.
- Use `crestapps-core-chat` for server-side completion code, `crestapps-core-signalr` for hub routes and authentication, and `crestapps-core-realtime-voice` for voice sessions.

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

Hub methods called by the client (interaction hub; the session hub takes `profileId` and `sessionId` instead of `itemId`):

| Method | Purpose |
|---|---|
| `SendMessage(itemId, prompt)` | Streams `CompletionPartialMessage` items through a `ChannelReader` |
| `LoadInteraction(itemId)` / `SaveSettings(itemId, settings)` / `ClearHistory(itemId)` | Load, update and reset an interaction |
| `StartConversation`, `SendAudioStream`, `SynthesizeSpeech` | Speech-to-text and text-to-speech conversation mode |
| `StartRealtimeConversation`, `StartRealtimeWebRtc`, `AddRealtimeIceCandidate`, `GetRealtimeIceServers`, `UpdateRealtimeSettings`, `UpdateRealtimeSpeechSpeed`, `StopConversation` | Realtime voice (see `crestapps-core-realtime-voice`) |
| `HandleNotificationAction(sessionId, notificationType, actionName)` | Chat notification buttons |

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
app.AddUploadChatInteractionDocumentEndpoint()
    .AddRemoveChatInteractionDocumentEndpoint()
    .AddUploadChatSessionDocumentEndpoint()
    .AddRemoveChatSessionDocumentEndpoint()
    .AddDownloadAIDocumentEndpoint();
```

Authorize them by registering `IAuthorizationHandler` implementations for `AIChatDocumentOperations.ManageDocuments` on `ChatInteraction` and on `AIChatSessionDocumentAuthorizationContext`.

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

## Upgrading from 1.x

- The chat scripts are built with Vite for current browsers (Chrome/Edge 111, Firefox 114, Safari 16.4). URLs and globals are unchanged, but ES5 browsers no longer run them, the `*.css.map` files are gone, and Babel helpers are no longer page globals.
- `ChatMode.Realtime` and `AIProfile.RealtimeDeploymentName` are removed. Use `ConversationDeploymentName` on the interaction (or `ChatModeProfileSettings.ConversationDeploymentName` on a profile).
- AI Profile sessions no longer apply `IAIToolAccessEvaluator`; Chat Interactions now do, at send time. Keep a custom evaluator if you enforce per-user tool permissions.
- Load `chat-markers.js` before the chat scripts if you host them yourself.
- Configure hub options per concrete hub with `ConfigureChatHubOptions<THub>()` / `ConfigureCrestAppsChatHubOptions<THub>()`; a hub mapped without it runs on SignalR defaults.

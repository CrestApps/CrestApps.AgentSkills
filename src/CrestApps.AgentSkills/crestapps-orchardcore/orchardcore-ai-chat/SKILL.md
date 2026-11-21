---
name: orchardcore-ai-chat
description: Skill for setting up and extending AI Chat in Orchard Core with the CrestApps AI Chat module. Covers the AI Chat and AI Chat Admin Widget features, chat profiles on the admin menu, chat session and history pages, Invoke Profile, the frontend AIChat widget with AIProfilePart, chat modes with ChatModeProfileSettings, session uploads and citations, AG-UI tool cards and tool approvals, admin widget page context with IAIChatPageContextProvider and SendPageContext, the AG-UI endpoint, PromptSecurityOptions rate-limit tiers, AIVisitorIdentityOptions cookies, AIChatHub methods, and upgrading from deployment purposes and ChatMode.Realtime. Use this skill when requests mention Orchard Core AI Chat, chat profiles, chat widgets, the admin chat widget, anonymous chat visitors, chat rate limits, or prompt security. Strong matches include AIChatHub, AG-UI, RequireApproval, IAIChatPageContextProvider, AIChatProfileSettings, IsOnAdminMenu, AIProfilePart, QueryAIProfile, and AIChatApp.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Chat - Prompt Templates

## Configure AI Chat

You are an Orchard Core expert. Generate code, configuration, and recipes for adding profile-driven AI chat to an Orchard Core application with the CrestApps AI Chat module.

### Guidelines

- Install `CrestApps.OrchardCore.AI.Chat` and at least one provider package (OpenAI, Azure OpenAI, Azure AI Inference or Ollama) in the web/startup project.
- `CrestApps.OrchardCore.AI.Chat` depends on `OrchardCore.SignalR` (the Orchard Core module, not the deprecated CrestApps one), `OrchardCore.Liquid`, `CrestApps.OrchardCore.Resources`, `CrestApps.OrchardCore.AI` and the dependency-only `CrestApps.OrchardCore.AI.Chat.Core` (**AI Chat Services**).
- AI Chat ships no completion client. Enable a provider feature and create a connection and deployments for it.
- A deployment declares **model capabilities** in `Properties.AIDeploymentMetadata.Features`. The deployment purpose (`Purpose`, `Type`) is gone in 3.0. Which deployment serves chat, utility, speech and the other slots is chosen under **Settings → Artificial Intelligence → Default Deployments** or on the profile. See `orchardcore-ai-model-capabilities`.
- A chat-type profile with **Show On Admin Menu** (`AIChatProfileSettings.IsOnAdminMenu`) appears under **Artificial Intelligence**. The menu entry needs `QueryAnyAIProfile` or the per-profile `QueryAIProfile_{profileName}` permission.
- **Add Profile** opens the **New AI Profile** picker. **Blank profile** opens the editor; every other card creates the profile from a profile template. See `orchardcore-ai-profile-templates`.
- The **Chat deployment** is the text model only. A spoken conversation is carried by the **Conversation deployment**. See `orchardcore-ai-realtime-voice`.
- Read chat-mode settings with `profile.TryGetSettings<ChatModeProfileSettings>()` and write them with `profile.AlterSettings<ChatModeProfileSettings>(...)`. They live in `AIProfile.Settings`, not in `Properties`.
- Never put API keys in recipes or committed configuration. Use user secrets, environment variables, or a key vault.
- All recipe JSON uses the root `{ "steps": [...] }` format and C# classes are `sealed`, except view models.

### Features

| Feature | ID | Notes |
|---|---|---|
| AI Chat | `CrestApps.OrchardCore.AI.Chat` | Admin chat UI, frontend widget, chat modes, prompt security and visitor identity settings |
| AI Chat Admin Widget | `CrestApps.OrchardCore.AI.Chat.AdminWidget` | Floating chat on every admin page. Depends on AI Chat and `CrestApps.OrchardCore.AI.Agent` |
| AI Chat Session Analytics | `CrestApps.OrchardCore.AI.Chat.Analytics` | See `orchardcore-ai-chat-analytics` |
| AI Chat Services | `CrestApps.OrchardCore.AI.Chat.Core` | Dependency only; hub, session manager and handlers |
| AI Chat WebAPI | `CrestApps.OrchardCore.AI.Chat.Api` | REST chat API and the AG-UI endpoint `POST /api/ai/v1/agui`, shipped by the AI module |

Provider features: `CrestApps.OrchardCore.OpenAI`, `CrestApps.OrchardCore.OpenAI.Azure`, `CrestApps.OrchardCore.AzureAIInference`, `CrestApps.OrchardCore.Ollama`. See `orchardcore-ai-providers`.

### Enable AI Chat

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

### Connection, Deployments and a Chat Profile

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Source": "OpenAI",
          "Name": "openai",
          "DisplayText": "OpenAI",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.openai.com/v1",
              "ApiKey": "{{YourApiKey}}"
            }
          }
        }
      ]
    },
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "gpt-4o",
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textGeneration", "toolCalling", "streaming" ]
            }
          }
        },
        {
          "Name": "gpt-4o-mini",
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textGeneration", "toolCalling", "streaming" ]
            }
          }
        }
      ]
    },
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "customer-support-chat",
          "DisplayText": "Customer Support Chat",
          "WelcomeMessage": "Hello! How can I help you today?",
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "ChatDeploymentName": "gpt-4o",
          "UtilityDeploymentName": "gpt-4o-mini",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a helpful customer support assistant. Be friendly and concise.",
              "Temperature": 0.3,
              "MaxTokens": 2048,
              "PastMessagesCount": 10
            }
          },
          "Settings": {
            "AIChatProfileSettings": {
              "IsOnAdminMenu": true
            },
            "ChatModeProfileSettings": {
              "ChatMode": "TextInput",
              "EnableTextToSpeechPlayback": false
            }
          }
        }
      ]
    }
  ]
}
```

- Profiles are source-agnostic. The selected chat and utility deployments decide the client and model, so `Source` is not needed.
- Leaving `ChatDeploymentName` empty uses the connection default, then **Default Chat Deployment** in site settings.
- Link a persona with a top-level `"PersonaId"` on the profile. See `orchardcore-ai-personas`.

### Define a Chat Profile in Code

```csharp
public sealed class SiteAssistantMigrations : DataMigration
{
    private readonly IAIProfileManager _profileManager;

    public SiteAssistantMigrations(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _profileManager.NewAsync();

        profile.Name = "site-assistant";
        profile.DisplayText = "Site Assistant";
        profile.Type = AIProfileType.Chat;

        profile.WithSettings(new AIProfileSettings
        {
            LockSystemMessage = true,
            IsRemovable = false,
            IsListable = true,
        });

        profile.WithSettings(new AIChatProfileSettings
        {
            IsOnAdminMenu = true,
        });

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "You are a site assistant. Help users navigate the site and answer questions.",
            Temperature = 0.3f,
            MaxTokens = 4096,
            PastMessagesCount = 10,
        });

        var result = await _profileManager.ValidateAsync(profile);

        if (result.Succeeded)
        {
            await _profileManager.CreateAsync(profile);
        }

        return 1;
    }
}
```

`IAIProfileManager` is a catalog manager: `NewAsync`, `ValidateAsync`, `CreateAsync`, `UpdateAsync`, `FindByNameAsync`, `DeleteAsync`. There is no `SaveAsync`.

For agent profiles, profile types and tools, see `orchardcore-ai`, `orchardcore-ai-agent` and `orchardcore-ai-tools`. The framework profile API is described in `crestapps-core-ai-profiles`.

### Profile Editor Layout

- The first tab holds the **General**, **Deployments & Interactions**, **Instructions**, **Parameters** and **Prompt Security** cards (plus **Speaking Style** and **Guardrails**). **Knowledge**, **Capabilities** and **Data Processing & Metrics** tabs come from enabled features; **Data Processing & Metrics** also holds **Contact memory** and **Data Privacy** (see `orchardcore-ai-conversations`).
- Every save of a profile is kept as a version (**Versions** in the row menu: compare and restore).
- With the WebMCP module, the **Capabilities** tab has **Allow page tools** and **Allow consequential page tools** (`WebMcpProfileSettings`): the chat discovers and runs the WebMCP tools of the page the user has open, through the hub's `SetPageTools` and `ClearPageTools`. See [orchardcore-ai-webmcp](../orchardcore-ai-webmcp/SKILL.md).
- **Deployments & Interactions** reads in one pass: chat deployment, its model parameters, **Chat mode**, **Conversation deployment**, **Voice**, **Enable text-to-speech playback**, then the utility deployment and its parameters.
- **Start the conversation automatically** (formerly **Add initial prompt**) with its **Opening message** (formerly **Initial prompt**) still stores `AIProfileMetadata.InitialPrompt`. The session is created lazily: it is persisted only when the visitor sends the first message, and the opening message is inserted ahead of it.
- A **Welcome Message** is placeholder text for new sessions and is never added to the model history.

### Chat Modes

| Mode | Behavior | Requirement |
|---|---|---|
| `TextInput` | Typed chat (default) | None |
| `AudioInput` | Microphone dictation; transcript is placed in the input for review | A speech-to-text deployment in the `speechToText` slot |
| `Conversation` | Voice toggle beside a working message box; both kinds of turn land in one thread | A realtime deployment that resolves, or both speech-to-text and text-to-speech deployments |

```csharp
profile.AlterSettings<ChatModeProfileSettings>(settings =>
{
    settings.ChatMode = ChatMode.Conversation;

    // Empty means the site's default realtime deployment, then the first realtime-capable one.
    settings.ConversationDeploymentName = "gpt-realtime";
    settings.VoiceName = "alloy";
    settings.EnableTextToSpeechPlayback = false;
});
```

- The **Chat mode** field only appears on **Chat** profiles, and only when a speech-to-text deployment or a realtime deployment exists.
- `VoiceName` is kept whatever the mode, because phone calls answered or placed with the profile use it. Without one, the persona's voice, then the site default voice, then the provider default is used.
- **Enable text-to-speech playback** adds a read-aloud button to each answer and needs a text-to-speech deployment. It is hidden in Conversation mode, which already speaks.
- Resolution order, cascaded realtime and transport configuration are in `orchardcore-ai-realtime-voice`.

### Admin Chat Pages

| Route | Purpose | Permission |
|---|---|---|
| `ai/chat/session/{profileId}/{sessionId?}` | Admin chat session | `QueryAnyAIProfile` or `QueryAIProfile_{name}` |
| `ai/chat/history/{profileId}` | Chat history (`AIChatSessions` admin list) | Same |
| `ai/chat/test/{profileId}` | **Invoke Profile** for Utility and Agent profiles; no session, history or metrics | `QueryAnyAIProfile` |
| `ai/chat/chat-session/delete/{sessionId}` | Delete one session | `DeleteChatSession` |
| `ai/chat/history/{profileId}/delete-all` | Delete all sessions of a profile | `DeleteAllChatSessions` |

The history page renders through Orchard Core's `AdminList` shape (`AIChatSessions`, rows are `AIChatSessionEntry_SummaryAdmin`, grid columns `Name`, `Status`, `LastActivity`, `Actions`). Add columns with an `IAdminListColumnsProvider` for `AIChatSessions`; see `orchardcore-admin-lists`.

### Admin Widget

Enable `CrestApps.OrchardCore.AI.Chat.AdminWidget`, then pick the profile, the history size (1 to 50, default 10), the primary color (default `#41b670`) and **Tell the AI which page the user is on** (`SendPageContext`, on by default) in the **Admin Widget** section of **Settings → Artificial Intelligence** (permission `ManageAIProfiles`). It renders for signed-in users allowed to query that profile and keeps its position, size and session in browser local storage.

```json
{
  "steps": [
    {
      "name": "Settings",
      "AIChatAdminWidgetSettings": {
        "ProfileId": "<chat-profile-id>",
        "MaxSessions": 10,
        "PrimaryColor": "#41b670",
        "SendPageContext": true
      }
    }
  ]
}
```

### AG-UI: the Assistant Works on the Page

The chat page, the admin widget, the frontend widget and the profile test page run the chat as an [AG-UI](https://docs.ag-ui.com) agent over the AI chat hub (`RunAgent`, from CrestApps.Core; see `crestapps-core-ag-ui`). Nothing needs to be enabled.

- **Tool cards.** Each tool call shows as a card in the message with the tool's title (`.WithTitle(...)`), input and result, also after the chat reloads. The UI texts are localized by `AIChatUIConfiguration`.
- **Tool approvals.** A tool registered with `.RequireApproval()` waits for the user: the run ends with an interrupt, the card shows **Allow** and **Deny**, and the answer starts a new run. The tool's own permission checks still run. Chats that cannot ask (SMS, voice) run such tools as before. The **Orchard Core AI Agent** tools that delete, disable, import, set up tenants or send email/SMS ask first.
- **Page context.** The admin widget sends the page title and address, the selected text and what the page is about (at most 10 entries of 1,000 characters, read by the model as data). Built-in providers: `ContentItemPageContextProvider` (content item being edited or viewed, or the type being created; needs `OrchardCore.Contents`) and `SitePageContextProvider` (site name and tenant). `SendPageContext = false` turns all of it off.
- **Page tools.** With WebMCP and a profile that has **Allow page tools**, the assistant reads the page (`page_read_content`), fills forms without submitting them and opens other site pages; a `page_navigate` run continues on the next page (up to five pages in a row). See `orchardcore-ai-webmcp`.

```csharp
services.AddCoreAITool<ArchiveOrdersTool>(ArchiveOrdersTool.TheName)
    .WithTitle(S["Archive Orders"])
    .RequireApproval()
    .Selectable();
```

A module adds context for the records it owns with `IAIChatPageContextProvider` (`CrestApps.OrchardCore.AI.Chat.Core.Services`). Providers run when the widget renders on each admin page, so read only what is already loaded or cached:

```csharp
public sealed class OrderPageContextProvider : IAIChatPageContextProvider
{
    public Task PopulateAsync(AIChatPageContext context)
    {
        if (context.GetRouteValue("area") == "MyCompany.Orders" && context.GetRouteValue("orderId") is { } orderId)
        {
            context.Add("Order being edited", $"Order {orderId}");
        }

        return Task.CompletedTask;
    }
}

// In the startup of the feature that owns orders:
services.AddScoped<IAIChatPageContextProvider, OrderPageContextProvider>();
```

**AG-UI endpoint.** With **AI Chat WebAPI** (`CrestApps.OrchardCore.AI.Chat.Api`), any AG-UI client (such as a CopilotKit front end) runs chat profiles at `POST /api/ai/v1/agui` with a `RunAgentInput`, answered as Server-Sent Events. Put the profile id in `forwardedProps.profileId` and the session id in `threadId` (empty starts a session). Authenticate with an AI API key whose areas include `ai.chat` (`X-Api-Key` header or bearer) or an OpenID bearer token; others get `401`. The hub then authorizes the profile and session. JSON only, no antiforgery token.

### Frontend Widget

With `OrchardCore.Widgets` enabled, the module creates the `AIChat` widget type (**Artificial Intelligence Chat**) with `AIProfilePart` (`ProfileId`, `TotalHistory`). The widget renders only for visitors allowed to query the profile, so grant the `Anonymous` role `QueryAIProfile_{profileName}` for a public assistant. History is listed for the visitor's own identity: a signed-in user's sessions, or the sessions owned by an anonymous visitor's cookie.

```json
{
  "steps": [
    {
      "name": "Roles",
      "Roles": [
        {
          "Name": "Anonymous",
          "PermissionBehavior": "Add",
          "Permissions": [ "QueryAIProfile_customer-support-chat" ]
        }
      ]
    }
  ]
}
```

### Prompt Security and Visitor Identity

Both are sections of **Settings → Artificial Intelligence** (permission `ManageAIProfiles`) and both are site settings that can be set with the `Settings` recipe step. Saving either still releases the tenant shell, because their readers in CrestApps.Core use `IOptions<T>`.

```json
{
  "steps": [
    {
      "name": "Settings",
      "PromptSecurityOptions": {
        "EnableInjectionDetection": true,
        "MaxPromptLength": 8000,
        "BlockingThreshold": "High",
        "MaxMessagesPerWindow": 20,
        "RateLimitWindow": "00:01:00",
        "AnonymousMessageRateLimitTiers": [
          { "Limit": 5, "Window": "00:00:30" },
          { "Limit": 150, "Window": "01:00:00" }
        ],
        "AnonymousSessionStartRateLimitTiers": [
          { "Limit": 10, "Window": "00:05:00" }
        ]
      },
      "AIVisitorIdentityOptions": {
        "CookieName": "crestapps-ai-visitor",
        "CookieLifetime": "180.00:00:00",
        "AllowCrossSiteEmbedding": true,
        "UsePartitionedCookie": true,
        "RemoteAddressMode": "Hashed"
      }
    }
  ]
}
```

- Tiers apply to anonymous callers only and take precedence over the single-window values. Lowering `MaxAnonymousSessionsPerWindow` has no effect while tiers are set; clear the tier list or lower a tier instead.
- The default for **Maximum anonymous sessions per window** is now `20`.
- A profile can override every throttle value on its **Prompt Security** card (first tab of the profile editor), or opt out of tiers with **Do not use tiered ... limits for this profile**. Injection detection, output filtering and prompt length stay site-wide.
- A throttled session start is sent to the client as `ReceiveSessionStartRejected` with a generic, localizable message.
- Turn on `AllowCrossSiteEmbedding` only when the chat runs in a frame on another site. It writes the cookie `SameSite=None; Secure` (HTTPS requests only) and `UsePartitionedCookie` adds `Partitioned`.
- `RemoteAddressMode` values are `Disabled`, `Hashed` (recommended), `PlainText` and `Encrypted`. Configure forwarded headers behind a reverse proxy.

### Session Documents and Citations

- Enable `CrestApps.OrchardCore.AI.Documents.ChatSessions` and turn on **Allow session document uploads** and **Allow session image uploads** per profile (`AIProfileSessionDocumentsMetadata`). Image uploads also need a vision deployment. See `orchardcore-ai-documents`.
- `[doc:N]` markers render as superscript citations with a linked reference list in the admin chat, the admin widget and the frontend widget.
- Pictures returned together, such as document previews, stack into the `chat-image-carousel`.
- Deleting a chat session deletes its uploaded files from the document file store, not only their rows and chunks.

### AIChatHub Methods

The hub is mapped with `SignalRHubRoutes.GetHubPath<AIChatHub>()`, and `ConfigureCrestAppsChatHubOptions<AIChatHub>()` sets its SignalR options.

| Method | Purpose |
|---|---|
| `StartSession` | Start a session for a profile |
| `LoadSession` | Load a session and join its group |
| `SendMessage` | Stream a reply to a typed prompt |
| `SendAudioStream` | Stream audio for dictation |
| `StartConversation` / `StopConversation` | Speech-to-text plus text-to-speech conversation |
| `StartRealtimeConversation` / `StartRealtimeWebRtc` | Realtime voice over WebSocket or WebRTC |
| `GetRealtimeIceServers`, `AddRealtimeIceCandidate`, `UpdateRealtimeSettings`, `UpdateRealtimeSpeechSpeed` | Realtime session control |
| `SynthesizeSpeech` | Read an answer aloud |
| `RateMessage` | Thumbs up or down |
| `HandleNotificationAction` | Run a notification action, see `orchardcore-ai-chat-notifications` |
| `RunAgent` | Run one AG-UI turn (the chat UI and widgets use it; tool cards, approvals and page context ride on it) |
| `SetPageTools` / `ClearPageTools` | Register or clear the WebMCP tools of the user's current page (WebMCP bridge) |

### Resources

The chat UI comes from the `@crestapps/ai-chat-ui` package. Scripts `AIChatApp`, `AIChatWidgetApp`, `realtime-audio` and `chat-image-carousel`, and styles `AIChatApp`, `AIChatWidget` and `chat-image-carousel`, are registered by the modules. Assistant messages show the persona's name and avatar when a persona is linked.

### Upgrading from 2.x

- Replace `"Purpose": "Chat"` and similar on deployments with `Properties.AIDeploymentMetadata.Features`. Old recipes and stored records still import because the purpose is projected onto capabilities on read.
- `ChatMode.Realtime` and the separate realtime deployment field are gone. Put the speech-to-speech model in `ChatModeProfileSettings.ConversationDeploymentName` and set `ChatMode` to `Conversation`. Stored profiles are folded on read.
- The chat deployment picker now lists text-capable deployments only.
- The CrestApps SignalR module is deprecated. AI Chat depends on `OrchardCore.SignalR` directly, and `HubRouteManager` is replaced by `SignalRHubRoutes.GetHubPath<T>()` and `Html.SignalRHubUrl<T>()`.
- The chat history list uses the `AdminList` shape. A theme override of the whole row only applies to the List layout.
- The **Apply Templates** card is gone from the profile editor; use the **New AI Profile** picker, or link to `Create?templateId=`.
- The CSS source maps `ai-chat.css.map` and `chat-widget.css.map` are no longer shipped.

See [ai-chat-examples.md](references/ai-chat-examples.md) for more recipes.

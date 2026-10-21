---
name: crestapps-core-signalr
description: Skill for SignalR in CrestApps.Core 2.0. Covers AddSignalR on the AI suite builder and AddCoreSignalR with a tenant path prefix and the store-committer hub filter, the global hub defaults it sets (camelCase JSON, eight parallel invocations per client, a stream buffer of 64) that realtime WebRTC signaling depends on, per-hub chat options with ConfigureCrestAppsChatHubOptions, HubRouteManager path and URL generation, mapping hubs in minimal hosting, connection-level and per-invocation authorization for AIChatHubCore and ChatInteractionHubBase, access tokens for headless clients, reading the caller through Context.User and IUserAccessor, pinning the browser SignalR client with an integrity hash, and Redis or Azure SignalR scale-out. Use it when mapping CrestApps chat hubs, generating hub URLs for JavaScript clients, securing hubs, or troubleshooting stalled realtime handshakes, queued hub calls, or cross-tenant hub URLs.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core SignalR

The `CrestApps.Core.SignalR` package registers SignalR with CrestApps defaults and a `HubRouteManager` that builds hub paths and absolute URLs (with an optional tenant prefix). The chat hubs themselves (`AIChatHubCore<TClient>`, `ChatInteractionHubBase`) live in `CrestApps.Core.AI.Chat`; see `crestapps-core-chat-interactions`.

## Register SignalR

Through the AI suite builder, optionally with the store-committer hub filter so staged store writes are committed after each hub invocation:

```csharp
using CrestApps.Core.SignalR;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddSignalR(pathPrefix: "", addStoreCommitterFilter: true)
    )
);
```

Without the builder, `AddCoreSignalR(pathPrefix)` returns the `ISignalRServerBuilder`:

```csharp
builder.Services.AddCoreSignalR("/tenant-a")
    .AddCrestAppsStoreCommitterFilter();
```

`AddCoreSignalR` registers `HubRouteManager` as a singleton and sets host-wide defaults:

| Setting | Value | Why |
|---|---|---|
| JSON protocol naming | camelCase | Matches the bundled chat scripts |
| `MaximumParallelInvocationsPerClient` | `8` | `StartRealtimeWebRtc` holds its invocation open while trickled `AddRealtimeIceCandidate` calls arrive; with SignalR's default of one, ICE never completes on a remote host |
| `StreamBufferCapacity` | `64` | Realtime audio is uploaded as a stream of small frames; the default of 10 blocks the dispatch loop while the provider session opens |

The chat hubs need more than that (10-minute client timeout, 15-second keep-alive, 10 MB messages). Configure each concrete hub type:

```csharp
using CrestApps.Core.AI.Chat;

builder.Services.ConfigureCrestAppsChatHubOptions<AIChatHub>();
builder.Services.ConfigureCrestAppsChatHubOptions<ChatInteractionHub>();
```

`ConfigureCrestAppsChatHubOptions<THub>()` uses `AddHubOptions<THub>`. Setting `services.Configure<HubOptions<THub>>(...)` directly is silently ignored by SignalR, and a hub mapped without per-hub options runs on the global defaults.

## Map Hubs with Minimal Hosting

Use the registered `HubRouteManager` instance so the configured prefix is applied:

```csharp
using CrestApps.Core.SignalR.Services;

var app = builder.Build();

var hubRouteManager = app.Services.GetRequiredService<HubRouteManager>();

app.MapHub<ChatInteractionHub>(hubRouteManager.GetPathByHub<ChatInteractionHub>());
app.MapHub<AIChatHub>(hubRouteManager.GetPathByHub<AIChatHub>());
```

The default path is `/Communication/Hub/{HubTypeName}`, prefixed when a path prefix is configured (`/tenant-a/Communication/Hub/AIChatHub`). The static `HubRouteManager.MapHub<T>(routes)` maps the default path without any prefix and is meant for module startups that are already tenant-scoped. A host may also map a literal path (the sample hosts use `/hubs/ai-chat` and `/hubs/chat-interaction`) as long as the client is given the same URL.

## Generate Client URLs

```csharp
using CrestApps.Core.SignalR.Services;

namespace MyApp.Services;

public sealed class ChatHubUrlProvider
{
    private readonly HubRouteManager _hubRouteManager;

    public ChatHubUrlProvider(HubRouteManager hubRouteManager)
    {
        _hubRouteManager = hubRouteManager;
    }

    public string GetChatHubUrl(HttpContext httpContext)
        => _hubRouteManager.GetUriByHub<AIChatHub>(httpContext);
}
```

| Method | Returns |
|---|---|
| `GetPathByHub<T>()` | Prefixed relative path for `MapHub` |
| `GetPathByRoute(pattern)` | Prefixed relative path for a custom pattern |
| `GetUriByHub<T>(httpContext)` | Absolute URL from the current request scheme and host |
| `GetUriByRoute(httpContext, pattern)` | Absolute URL for a custom pattern |

A `HubRouteManager` constructed with a `siteBaseUrlResolver` builds absolute URLs from that base URL instead of the request, which helps behind reverse proxies. `AddCoreSignalR` registers one without a resolver; register your own instance to supply one.

## Authorization

`AddCoreSignalR()` does not configure authentication; `app.UseAuthentication()` and `app.UseAuthorization()` must run before endpoints.

- **Connection level**: put `[Authorize]` on the hub class to reject the negotiate request.
- **Per invocation**: the chat hubs are not decorated and their hooks allow everything by default. Override `AuthorizeProfileAsync(IServiceProvider, AIProfile)` on `AIChatHubCore<TClient>` and `AuthorizeAsync(IServiceProvider, ChatInteraction)` on `ChatInteractionHubBase`. A `false` result sends `ReceiveError` with `GetNotAuthorizedMessage()` and keeps the connection open.

Headless clients send a bearer token as the `access_token` query parameter during the WebSocket handshake. Accept it only on hub paths:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/Communication/Hub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });
```

Inside a hub use `Context.User` and `Context.GetHttpContext()`, never `IHttpContextAccessor`, which is unreliable during hub invocations. Services that need the caller should take the principal as a parameter or use `IUserAccessor`; the built-in chat hubs publish the connection's principal to it for every invocation. Do not attach a policy that lists schemes to a hub that must also accept anonymous connections; a failed policy replaces `HttpContext.User` with an empty principal.

## Browser Client

Pin the SignalR browser client and check its bytes, as the sample hosts do:

```html
<script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@10.0.11/dist/browser/signalr.min.js"
        integrity="sha384-N+pL/4qXZHZcvsa6hVMd5/7cdF3lC59ZMNHeQyRo5q9jmC7yw0hcJILPjUDSU7ZS"
        crossorigin="anonymous"></script>
```

A floating `@latest` URL cannot carry an integrity hash.

## Scale-Out

SignalR keeps connection state in memory. Behind a load balancer with several instances, add a backplane (Redis via `AddStackExchangeRedis(...)` on the `ISignalRServerBuilder`, or Azure SignalR Service). A realtime voice session is bound to one server for its whole life, so use sticky sessions when WebSockets are not guaranteed.

## Troubleshooting

- **Realtime WebRTC never connects on a remote host but works on localhost**: the hub runs with one parallel invocation per client. Register through `AddCoreSignalR` and call `ConfigureCrestAppsChatHubOptions<THub>()` for that hub.
- **Voice sessions stall at start or other hub calls queue behind a call**: same cause; check per-hub options.
- **Clients connect to another tenant's hub**: generate URLs with the tenant's `HubRouteManager`, not hard-coded strings.
- **Every caller can open every profile or interaction**: the authorization hooks were not overridden.

## Upgrading from 1.x

- `AddCoreSignalR` now sets `MaximumParallelInvocationsPerClient = 8` and `StreamBufferCapacity = 64` for every hub, and the chat hub options are configured per hub type with `AddHubOptions<THub>`. Remove any `services.Configure<HubOptions<THub>>` workarounds.
- The sample hosts pin `@microsoft/signalr@10.0.11` with an integrity hash instead of `@latest`.

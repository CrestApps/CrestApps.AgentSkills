---
name: crestapps-core-copilot-orchestrator
description: Skill for the GitHub Copilot orchestrator in CrestApps.Core 2.0, a named IOrchestrator ("copilot") built on the GitHub Copilot SDK. Covers AddCopilotOrchestrator and AddCoreAICopilotOrchestrator registration, the automatic Copilot CLI acquisition with its shared cache and npm registry MSBuild properties, binding CopilotOptions yourself, the NotConfigured, GitHubOAuth and ApiKey (BYOK) authentication modes and what each requires, implementing ICopilotCredentialStore or using JsonFileCopilotCredentialStore, GitHubOAuthService authorization, callback state validation, token refresh and model listing, CopilotSessionMetadata model, permission and reasoning-effort overrides, how tools reach the SDK, and its limits around MCP tools, deployments and usage metering. Use it when running profiles on a Copilot subscription or an OpenAI-compatible BYOK endpoint through Copilot, or when Copilot reports it is not configured.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Copilot Orchestrator

`CrestApps.Core.AI.Copilot` adds `CopilotOrchestrator`, an `IOrchestrator` named `"copilot"` (title **Copilot**), which runs a turn through the GitHub Copilot SDK and its CLI instead of the default pipeline.

## Guidelines

- Choose GitHub OAuth when each user brings a Copilot subscription. Choose `ApiKey` (BYOK) for one shared OpenAI-compatible, Azure or Anthropic endpoint.
- Treat `NotConfigured` as intentionally disabled; editors should show a warning instead of Copilot fields while it is selected.
- Bind `CopilotOptions` yourself; the package reads no configuration section.
- GitHub OAuth needs an `ICopilotCredentialStore`. BYOK does not.
- Copilot does not use AI connections, deployments, deployment capabilities or deployment model parameters, and its SDK-managed requests are not recorded by usage metering.

## Registration

```csharp
using CrestApps.Core.AI.Copilot;
using CrestApps.Core.AI.Copilot.Models;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddCopilotOrchestrator()
    )
);

builder.Services.Configure<CopilotOptions>(builder.Configuration.GetSection("Copilot"));
```

Raw form: `builder.Services.AddCoreAICopilotOrchestrator();` after `AddCoreAIServices()` and `AddCoreAIOrchestration()`. It registers a named `HttpClient` with the standard resilience handler, the orchestrator, `GitHubOAuthService`, `CopilotChatInteractionSettingsHandler`, `CopilotOrchestrationContextHandler` and `CopilotOptionsValidator`.

Referencing the package downloads the Copilot CLI during build into `runtimes/<rid>/native/`. It caches the CLI machine-wide under the NuGet packages folder and resolves the npm registry from `NPM_CONFIG_REGISTRY` or `npm config get registry`. Override only when needed with the MSBuild properties `CopilotUseSharedCliCache`, `CopilotCliCacheDir`, `CopilotResolveNpmRegistry` and `CopilotNpmRegistryUrl`; the SDK's `CopilotCliBinaryPath` and `CopilotSkipCliDownload` still win.

## Authentication Modes

| `CopilotAuthenticationType` | Required for the orchestrator to run |
|---|---|
| `NotConfigured` | Never runs |
| `GitHubOAuth` | `ClientId`, `ClientSecret` (`Scopes` default `user:email`, `read:org`) and a signed-in user with a stored credential |
| `ApiKey` | `ProviderType`, `BaseUrl`, `DefaultModel`; for `azure` also `ApiKey` and `AzureApiVersion` |

`CopilotOptionsValidator` fails when `ClientId` or `ClientSecret` is missing in OAuth mode, or `ApiKey` is missing in BYOK mode. When the runtime check fails, a turn answers "Copilot is not configured and cannot be used until it has been configured."

```json
{
  "Copilot": {
    "AuthenticationType": "ApiKey",
    "ProviderType": "openai",
    "BaseUrl": "https://api.openai.com/v1",
    "ApiKey": "sk-...",
    "DefaultModel": "gpt-4o",
    "WireApi": "completions"
  }
}
```

```json
{
  "Copilot": {
    "AuthenticationType": "GitHubOAuth",
    "ClientId": "Iv1.abc123",
    "ClientSecret": "your-client-secret",
    "Scopes": [ "user:email", "read:org" ]
  }
}
```

## Credential Store

```csharp
public interface ICopilotCredentialStore
{
    Task<CopilotProtectedCredential> GetProtectedCredentialAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveProtectedCredentialAsync(string userId, CopilotProtectedCredential credential, CancellationToken cancellationToken = default);
    Task ClearCredentialAsync(string userId, CancellationToken cancellationToken = default);
}
```

`CopilotProtectedCredential` carries `GitHubUsername`, `ProtectedAccessToken`, `ProtectedRefreshToken`, `ExpiresAt` and `UpdatedUtc`; the tokens arrive already protected. Scope storage by user (and tenant). For development, the shipped `JsonFileCopilotCredentialStore` writes one file per user under `App_Data/CopilotCredentials`:

```csharp
builder.Services.AddScoped<ICopilotCredentialStore, JsonFileCopilotCredentialStore>();
```

Use a database-backed store in production.

## OAuth Flow

```csharp
using System.Security.Claims;
using CrestApps.Core.AI.Copilot.Services;
using Microsoft.AspNetCore.Mvc;

[Route("copilot")]
public sealed class CopilotAuthController : Controller
{
    private readonly GitHubOAuthService _oauthService;

    public CopilotAuthController(GitHubOAuthService oauthService)
    {
        _oauthService = oauthService;
    }

    [HttpGet("connect")]
    public IActionResult Connect(string returnUrl = "/")
    {
        var callbackUrl = Url.Action(nameof(Callback), null, null, Request.Scheme);

        return Redirect(_oauthService.GetAuthorizationUrl(callbackUrl, returnUrl));
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string code, string state, CancellationToken cancellationToken)
    {
        if (!_oauthService.TryValidateCallbackState(state, out var returnUrl))
        {
            return BadRequest();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _oauthService.ExchangeCodeForTokenAsync(code, userId, cancellationToken);

        return LocalRedirect(returnUrl);
    }
}
```

`GetAuthorizationUrl` writes a protected, ten-minute state cookie and sends a nonce as `state`; `TryValidateCallbackState` checks it and returns only a local return URL (or `__popup__`). Other members: `GetCredentialAsync`, `GetProtectedCredentialsAsync`, `GetValidAccessTokenAsync` (refreshes when expired), `IsAuthenticatedAsync`, `ListModelsAsync` and `DisconnectAsync`, all keyed by user id.

## Session Metadata

`CopilotSessionMetadata` on a profile or chat interaction overrides `CopilotModel`, `IsAllowAll` (approve all SDK permission requests, default `true`), and `ReasoningEffort` (`CopilotReasoningEffort.None`, `Low`, `Medium`, `High`), and records `GitHubUsername` plus protected tokens in OAuth mode.

```csharp
profile.OrchestratorName = "copilot";
profile.Put(new CopilotSessionMetadata { CopilotModel = "gpt-4o", ReasoningEffort = CopilotReasoningEffort.Low });
```

## How a Turn Runs

1. Tools come from `IToolRegistry` (system, profile, agent and tool instance tools); MCP tools are excluded from it.
2. Each tool is wrapped so dependency injection works when the SDK invokes it.
3. A session config is built with streaming, the model (`CopilotModel` or `DefaultModel`), the tools and the system message.
4. BYOK sets the provider type, base URL, key, `WireApi` and Azure API version; OAuth uses `GitHubOAuthService.GetValidAccessTokenAsync`.
5. MCP connections in the context are only described in the system message under `[Available MCP Servers]`. The current SDK `SessionConfig` has no MCP server support, and MCP tools are excluded from the tool list, so the model cannot call MCP tools through this orchestrator. Use the default orchestrator for profiles that depend on MCP tools.

Tool access follows the profile's configuration, the same boundary as the default orchestrator.

## Troubleshooting

- **"Copilot is not configured..."**: the mode's required values are missing (see the table), usually because nothing bound `CopilotOptions`.
- **Build fails downloading the CLI behind a proxy**: set `CopilotNpmRegistryUrl` or `NPM_CONFIG_REGISTRY`, or point `CopilotCliCacheDir` at a pre-seeded cache.
- **OAuth callback rejected**: the state cookie expired or the callback ran on a different host than the connect request.

## Upgrading from 1.x

- Match the current `ICopilotCredentialStore` signatures, which take a `CancellationToken`; stores written against older samples without it no longer compile.
- Validate the OAuth `state` with `TryValidateCallbackState`; it is a nonce backed by a protected cookie, not the return URL that older samples redirected to.
- The removed deployment purpose and the new deployment capabilities do not affect Copilot.

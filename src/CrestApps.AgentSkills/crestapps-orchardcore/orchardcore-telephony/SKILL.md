---
name: orchardcore-telephony
description: Skill for configuring provider-agnostic telephony and the soft phone in CrestApps Orchard Core. Covers provider selection, call control, OAuth user connections, call history, SignalR hub integration, and soft phone placement. Use this skill when requests mention CrestApps Orchard Core Telephony, soft phone, click-to-dial, call history, or custom telephony providers. Strong matches include work with CrestApps.OrchardCore.Telephony, ITelephonyProvider, ITelephonyService, ITelephonyProviderResolver, TelephonyHub, TelephonySettings, and TelephonyCapabilities.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Telephony - Prompt Templates

## Configure Provider-Agnostic Telephony

You are an Orchard Core expert. Generate accurate code, configuration, recipes, and administration guidance for the CrestApps Telephony module. It supplies a provider-neutral call-control layer and optional floating soft phone. It does not provide an SMS abstraction or SMS provider; use Orchard Core SMS or Omnichannel features for SMS.

### Guidelines

- Install `CrestApps.OrchardCore.Telephony` in the web/startup project.
- Enable `CrestApps.OrchardCore.Telephony` for services, settings, the hub, and call-history persistence.
- The core feature depends on `OrchardCore.Users` and `CrestApps.OrchardCore.SignalR`.
- Enable `CrestApps.OrchardCore.Telephony.SoftPhone` only when the floating widget is needed.
- Select a default provider only after that provider feature is enabled and configured.
- Use `ITelephonyService` from application code; do not invoke a provider directly unless implementing framework infrastructure.
- Implement `ITelephonyProvider` in provider packages and declare only the capabilities actually supported.
- Use `ITelephonyAuthenticationProvider` only when a provider requires per-user authentication.
- Keep provider credentials and user tokens server-side; never send an API key or OAuth refresh token to browser code.
- Register a provider settings display driver in `TelephonyConstants.SettingsGroupId` so it appears on the Telephony settings screen.
- Register hubs through `HubRouteManager`, not a fixed route, to preserve tenant prefixes and site base URLs.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Feature Overview

| Feature | Feature ID | Purpose |
|---|---|---|
| Telephony | `CrestApps.OrchardCore.Telephony` | Provider resolver, services, settings, SignalR hub, OAuth routes, and interaction history |
| Telephony Soft Phone | `CrestApps.OrchardCore.Telephony.SoftPhone` | Floating soft phone on the admin, front end, or both |

### Enable the Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.Telephony.SoftPhone"
      ],
      "disable": []
    }
  ]
}
```

The soft-phone feature depends on the core Telephony feature. Omit it when only server-side call services are required.

## Architecture

The browser never calls a telephony vendor directly:

```text
Soft phone or custom client
        -> TelephonyHub
        -> ITelephonyService
        -> ITelephonyProviderResolver
        -> selected ITelephonyProvider
```

`TelephonyHub` returns `TelephonyResult` values for commands and pushes `CallStateChanged`, `IncomingCall`, and `ReceiveError` events through `ITelephonyClient`. The hub uses a tenant-aware route created by `HubRouteManager`.

The core registrations include `DefaultTelephonyService`, `DefaultTelephonyProviderResolver`, `DefaultTelephonyAuthenticationService`, `DefaultTelephonyUserTokenStore`, and `DefaultTelephonyInteractionStore`. Interaction records are indexed through `TelephonyInteractionIndexProvider`.

### Call Operations

`ITelephonyProvider` contains these operations:

| Intent | Contract method |
|---|---|
| Start a call | `DialAsync(DialRequest)` |
| End a call | `HangupAsync(CallReference)` |
| Pause and continue | `HoldAsync(CallReference)` and `ResumeAsync(CallReference)` |
| Control local audio | `MuteAsync(CallReference)` and `UnmuteAsync(CallReference)` |
| Transfer or conference | `TransferAsync(TransferRequest)` and `MergeAsync(MergeRequest)` |
| Send DTMF | `SendDigitsAsync(SendDigitsRequest)` |
| Handle an incoming call | `AnswerAsync(CallReference)` and `RejectAsync(CallReference)` |
| Initialize a client | `GetClientCredentialsAsync()` |

Expose supported operations in `TelephonyCapabilities`. The widget hides controls that are not represented by the provider capability flags.

## Configure a Provider

Enable a provider feature and configure it under **Settings → Communication → Telephony**. The core Telephony tab selects the default enabled provider and configures soft-phone placement. A provider module adds its own tab through a site display driver.

The default provider lives in `TelephonySettings.DefaultProviderName`. When the sole configured provider is enabled it becomes the default automatically. Disabling the selected provider clears the default.

### API-Key and Per-User Authentication

Use shared credentials when one tenant-level account makes calls. Do not add an authentication provider for that case.

For OAuth-like per-user providers:

1. Implement `ITelephonyAuthenticationProvider`.
2. Return the appropriate `AuthenticationScheme`; OAuth providers use `TelephonyConstants.AuthenticationSchemes.OAuth2`.
3. Build authorization and token-exchange requests in the provider.
4. Let `ITelephonyAuthenticationService` and `ITelephonyUserTokenStore` persist encrypted user tokens.
5. Register `{scheme}://{host}/Telephony/Connect/Callback`, including any tenant URL prefix, with the remote provider.

The core module maps the `TelephonyOAuthConnect`, `TelephonyOAuthCallback`, and `TelephonyOAuthDisconnect` routes. Do not duplicate these routes in a provider module.

## Soft Phone

Enable `CrestApps.OrchardCore.Telephony.SoftPhone`, then configure the **Soft Phone** tab:

- Show on the admin dashboard.
- Show on the front end.
- Choose an accent color.

The widget is shown only where enabled and only to users with the `Use the telephony soft phone` permission. Its position and expanded state are persisted in browser local storage.

For manual placement, register the resources and render `SoftPhoneWidget`:

```cshtml
<style asp-name="telephony-soft-phone" at="Head"></style>
<script asp-name="telephony-soft-phone" at="Foot"></script>
<script asp-name="telephony-phone-field" at="Foot"></script>

@await DisplayAsync(await New.SoftPhoneWidget())
```

The `telephony-soft-phone` resource depends on the SignalR resource. The widget also augments phone-field `data-phone-dial` placeholders to support click-to-dial.

## Call History

The core service persists calls through `ITelephonyInteractionStore`. `TelephonyInteractionIndex` records provider interaction id, provider name, user, direction, outcome, call timestamps, and duration. This history remains available after a provider feature is removed.

Use the hub’s `GetInteractions` support for the widget’s recent-calls view. Provider webhooks may report inbound calls, but outbound calls placed through the hub are recorded by the core flow.

## Creating a Provider

Reference `CrestApps.OrchardCore.Telephony.Abstractions` from the provider project. Install any resulting CrestApps package in the consuming web/startup project.

```csharp
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace MyCompany.OrchardCore.MyTelephony;

public sealed class MyTelephonyProvider : ITelephonyProvider
{
    public LocalizedString Name => new("MyTelephony", "My Telephony");

    public TelephonyCapabilities Capabilities => TelephonyCapabilities.Dial | TelephonyCapabilities.Hangup;

    public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        // Translate the normalized request into the vendor API request.
        throw new NotImplementedException();
    }

    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
```

Register provider availability through `TelephonyProviderOptions`. Read the tenant settings in an `IConfigureOptions<TelephonyProviderOptions>` implementation and add a `TelephonyProviderTypeOptions` only when the provider is enabled:

```csharp
services.AddTelephonyProviderOptionsConfiguration<MyTelephonyProviderOptionsConfiguration>();
services.AddSiteDisplayDriver<MyTelephonySettingsDisplayDriver>();
```

Use a stable provider technical name. The resolver uses the configured default provider name and should not rely on the display name.

## Permissions and Operations

Telephony contributes administration permissions for managing Telephony settings and a user-facing permission for using the soft phone. Grant the latter narrowly. Provider configuration, OAuth callbacks, and saved user tokens must be protected by the tenant’s data-protection configuration.

Prefer explicit error results from provider operations. A `TelephonyResult` allows the hub to notify the client without leaking provider secrets or raw API payloads.

## Troubleshooting

| Symptom | Check |
|---|---|
| Widget says Not Ready | Ensure a provider is enabled and selected as `DefaultProviderName` |
| Widget never renders | Enable `CrestApps.OrchardCore.Telephony.SoftPhone`, select an admin or front-end surface, and grant use permission |
| OAuth callback fails | Verify the callback URL, tenant prefix, client credentials, and provider OAuth settings |
| Controls are missing | Confirm the provider reports the matching `TelephonyCapabilities` flags |
| Hub URL is wrong behind a prefix | Map and generate routes through `HubRouteManager` |
| Call history is empty | Ensure calls pass through `ITelephonyService` or report interactions through the core interaction store |

---
name: orchardcore-dialpad
description: Skill for configuring Dialpad as a CrestApps Orchard Core Telephony provider. Covers Dialpad site settings, API-key and OAuth authentication, provider selection, OAuth redirect URLs, and resilient API clients. Use this skill when requests mention Orchard Core Dialpad, Dialpad soft phone, Dialpad OAuth, or Dialpad call control. Strong matches include work with CrestApps.OrchardCore.DialPad, DialPadTelephonyProvider, DialPadSettings, DialPadProviderOptionsConfigurations, DialPadConstants, and ITelephonyProvider.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Dialpad - Prompt Templates

## Configure Dialpad Telephony

You are an Orchard Core expert. Generate accurate configuration and integration guidance for the CrestApps Dialpad module. The module is a Telephony provider that performs call control through the Dialpad REST API from the server.

### Guidelines

- Install `CrestApps.OrchardCore.DialPad` in the web/startup project.
- Enable `CrestApps.OrchardCore.DialPad`; it depends on `CrestApps.OrchardCore.Telephony`.
- Configure Dialpad under **Settings → Communication → Telephony** on the Dialpad tab.
- Enable the provider in its tenant settings before selecting it as the Telephony default provider.
- Use API-key authentication for one shared tenant Dialpad account.
- Use OAuth 2.0 when each Orchard user must connect their own Dialpad account.
- Register the core callback URL with Dialpad exactly, including any tenant URL prefix.
- Never put Dialpad credentials, access tokens, or refresh tokens in browser JavaScript.
- Keep the module-managed named HTTP client and its resilience pipeline; do not replace it with ad-hoc `HttpClient` creation.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Feature and Package

| Item | Value |
|---|---|
| Package | `CrestApps.OrchardCore.DialPad` |
| Feature ID | `CrestApps.OrchardCore.DialPad` |
| Dependency | `CrestApps.OrchardCore.Telephony` |
| Provider technical name | `DialPad` |
| Provider implementation | `DialPadTelephonyProvider` |

### Enable Dialpad

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.DialPad",
        "CrestApps.OrchardCore.Telephony.SoftPhone"
      ],
      "disable": []
    }
  ]
}
```

The Soft Phone feature is optional. Enable it only for the floating browser dialer; server-side code can use the Dialpad provider without it.

## Provider Model

`DialPadTelephonyProvider` implements the provider-agnostic `ITelephonyProvider` contract. The Telephony hub resolves it through `ITelephonyProviderResolver` when `TelephonySettings.DefaultProviderName` is `DialPad`.

Dialpad declares support for dial, hang up, hold, resume, mute, transfer, merge, DTMF, and inbound-call operations. The Telephony UI uses those `TelephonyCapabilities` values to decide which controls to show.

Call requests reach Dialpad only from the server:

```text
Browser soft phone -> TelephonyHub -> DialPadTelephonyProvider -> Dialpad REST API
```

This separation keeps Dialpad credentials out of the browser.

## Configure Tenant Settings

On **Settings → Communication → Telephony → Dialpad**:

1. Enable the Dialpad provider.
2. Select **Production** or **Sandbox**.
3. Select an authentication type.
4. Save the credentials required by that type.
5. Select **DialPad** as the default provider on the Telephony Soft Phone tab.

| Setting | API key mode | OAuth 2.0 mode |
|---|---|---|
| Enable Dialpad provider | Required | Required |
| Environment | Production or Sandbox | Production or Sandbox |
| API token | Required | Not used |
| User id | Required | Not used |
| Outbound caller id | Optional | Optional |
| Client id | Not used | Required |
| Client secret | Not used | Required |
| OAuth scopes | Not used | Optional |

The initial authentication-type selection deliberately leaves the provider unconfigured. API-key mode validates both the API token and user id. OAuth mode validates both client id and client secret.

Saved API tokens and client secrets are protected with the tenant data-protection provider. An empty password field after saving means the secret remains stored; enter a value only to replace it.

### Environment Endpoints

| Environment | Base URL |
|---|---|
| Production | `https://dialpad.com/api/v2/` |
| Sandbox | `https://sandbox.dialpad.com/api/v2/` |

The selected environment supplies the default REST API base. `DialPadSettings.ApiBaseUrl` is an optional tenant-level override; when it is empty, the provider uses the selected environment's default.

## API-Key Authentication

Choose API-key authentication when the tenant uses one Dialpad identity to place calls:

1. Create a Dialpad API token.
2. Obtain the Dialpad user id that will place outbound calls.
3. Set an optional caller id with country code.
4. Save settings and choose DialPad as the default provider.

Every outgoing call uses the configured Dialpad user. End users do not need to connect an account.

## OAuth 2.0 Authentication

Choose OAuth when each soft-phone user should authorize their own Dialpad account:

1. Create a Dialpad OAuth application.
2. Add `{scheme}://{host}/Telephony/Connect/Callback` as an allowed redirect URI.
3. Include the tenant request URL prefix in the callback if the tenant uses one.
4. Save the client id, client secret, and optional space-separated scopes.
5. Have each user click **Connect to provider** from the soft phone.

The provider uses authorization-code OAuth with PKCE. It always requests `offline_access`, allowing expired access tokens to be refreshed. Token storage and refresh are handled through the Telephony authentication services and encrypted user-token store.

On disconnect, Dialpad is deauthorized before the locally stored user tokens are removed.

## Provider Registration

The module startup is already responsible for registration:

```csharp
services.AddHttpClient(DialPadConstants.ProviderTechnicalName)
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(2);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.CircuitBreaker.FailureRatio = 0.1;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.MinimumThroughput = 100;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
    });

services.AddTelephonyProviderOptionsConfiguration<DialPadProviderOptionsConfigurations>();
services.AddSiteDisplayDriver<DialPadSettingsDisplayDriver>();
```

`DialPadProviderOptionsConfigurations` reads `DialPadSettings` from site settings and adds a `TelephonyProviderTypeOptions` entry for `DialPadTelephonyProvider` only when the provider is enabled. Do not register a second provider implementation with the same technical name.

## Use Telephony Instead of a Vendor-Specific Client

Application code should resolve the Telephony abstraction:

```csharp
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;

namespace MyCompany.OrchardCore.Calls;

public sealed class CustomerCallService
{
    private readonly ITelephonyService _telephonyService;

    public CustomerCallService(ITelephonyService telephonyService)
    {
        _telephonyService = telephonyService;
    }

    public Task<TelephonyResult> CallAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        return _telephonyService.DialAsync(new DialRequest
        {
            Destination = phoneNumber,
        }, cancellationToken);
    }
}
```

This lets the selected tenant provider change without changing application code. Use a correctly populated `DialRequest`; do not assume that every provider supports every optional request field.

## Soft Phone Use

After Dialpad is configured:

1. Enable `CrestApps.OrchardCore.Telephony.SoftPhone`.
2. Configure the admin and/or front-end surface.
3. Grant the `Use the telephony soft phone` permission.
4. Have OAuth users connect their Dialpad accounts, if applicable.

The widget uses the core `TelephonyHub`, not a Dialpad browser SDK. Phone fields can display click-to-dial buttons when the widget is present.

## Troubleshooting

| Symptom | Resolution |
|---|---|
| Dialpad is not in the default-provider list | Enable the Dialpad provider in its site settings and save valid credentials |
| Browser asks to connect repeatedly | Check the callback URL, OAuth client credentials, and tenant prefix |
| Sandbox calls target production | Set the Dialpad environment to Sandbox and reauthenticate users |
| Calls fail after transient API errors | Preserve the named `DialPad` HTTP client and inspect the server logs after retry/circuit-breaker handling |
| API key appears blank after saving | This is expected protected-secret behavior; blank does not erase the saved value |
| Soft phone shows Not Ready | Configure Dialpad and set `TelephonySettings.DefaultProviderName` to `DialPad` |

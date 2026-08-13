---
name: orchardcore-https
description: Skill for enforcing HTTPS and HSTS per Orchard Core tenant. Covers HttpsSettings, IHttpsService, HTTPS redirection, permanent redirect status, SSL port selection, HSTS modes, admin settings, and deployment. Use this skill when requests mention Orchard Core HTTPS, HttpsSettings, RequireHttps, HSTS, HTTPS redirection, strict transport security, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Https, HttpsSettings, IHttpsService, HttpsService, HttpStrictTransportSecurityMode, HttpsSettingsDisplayDriver, HttpsRedirectionOptions, and AddHsts. It also helps with feature recipes, proxy considerations, migrations, and the code patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core HTTPS - Prompt Templates

## Enforce HTTPS and HSTS

You are an Orchard Core expert. Generate safe tenant-specific HTTPS configuration for Orchard Core.

### Guidelines

- Enable the `OrchardCore.Https` feature. The module has no declared feature dependencies.
- Configure the setting only over an HTTPS request. The settings driver prevents changes over HTTP for safety.
- `RequireHttps` enables ASP.NET Core `UseHttpsRedirection`.
- `RequireHttpsPermanent` changes the redirect result to `308 Permanent Redirect`; otherwise ASP.NET Core uses its normal redirect behavior.
- `SslPort` supplies the HTTPS port when it cannot be inferred by the host.
- `StrictTransportSecurityMode` controls `UseHsts`: `Disabled`, `Enabled`, or `FromConfiguration`.
- `FromConfiguration` enables HSTS only in the Production host environment.
- The module configures HSTS for a 365-day maximum age, including subdomains, with preload disabled.
- Configure forwarded headers and proxy TLS termination correctly before requiring HTTPS. Otherwise an HTTPS request may be seen as HTTP and cause redirect loops.
- Enable `OrchardCore.Deployment` when HTTPS site settings must move between tenants.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling HTTPS

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Https"
      ],
      "disable": []
    }
  ]
}
```

### Configuring HTTPS in the Admin

Navigate to **Configuration → Settings → Security → HTTPS** using an HTTPS connection. Configure:

| Setting | Behavior |
|---|---|
| Require HTTPS | Redirects HTTP requests to HTTPS. |
| Permanent redirect | Uses HTTP 308 for the redirect. |
| SSL port | Overrides automatic HTTPS port detection when necessary. |
| Strict transport security | Controls HSTS mode for this tenant. |

Settings changes request a tenant shell release so the pipeline reads the new values.

### HttpsSettings

```csharp
using OrchardCore.Https.Settings;

var settings = new HttpsSettings
{
    RequireHttps = true,
    RequireHttpsPermanent = true,
    SslPort = 443,
    StrictTransportSecurityMode = HttpStrictTransportSecurityMode.FromConfiguration,
};
```

`EnableStrictTransportSecurity` is a legacy obsolete property. Do not set it in new code; use `StrictTransportSecurityMode`.

### Reading Tenant HTTPS Settings

Inject `IHttpsService` when a component needs to inspect the current tenant configuration.

```csharp
using OrchardCore.Https.Services;
using OrchardCore.Https.Settings;

namespace MyModule.Services;

public sealed class HttpsStatusProvider
{
    private readonly IHttpsService _httpsService;

    public HttpsStatusProvider(IHttpsService httpsService)
    {
        _httpsService = httpsService;
    }

    public async Task<bool> IsRequiredAsync()
    {
        HttpsSettings settings = await _httpsService.GetSettingsAsync();
        return settings.RequireHttps;
    }
}
```

### Runtime Pipeline Behavior

During tenant pipeline setup, the module:

1. Reads `HttpsSettings` through `IHttpsService`.
2. Adds HTTPS redirection if `RequireHttps` is true.
3. Adds HSTS if the mode is `Enabled`.
4. Adds HSTS in Production only if the mode is `FromConfiguration`.

It configures `HttpsRedirectionOptions.HttpsPort` from `SslPort` and changes the redirect status to 308 when permanent redirects are selected.

### Reverse Proxy Configuration

At a TLS-terminating proxy, preserve the original scheme with the appropriate forwarded headers middleware and trusted proxy configuration. Configure the hosting environment before the Orchard Core pipeline runs. Do not use `RequireHttps` until the application recognizes proxied HTTPS requests correctly.

### Deployment

With `OrchardCore.Deployment` enabled, the module registers the **Https settings** site-settings deployment step.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Https",
        "OrchardCore.Deployment"
      ],
      "disable": []
    }
  ]
}
```

Use deployment carefully across environments. An imported production HTTPS setting can be inappropriate for a local HTTP-only development tenant.

### HSTS Safety

- Enable HSTS only after every intended hostname supports HTTPS.
- HSTS applies to subdomains because the module sets `IncludeSubDomains` to true.
- Browser HSTS state is persistent, so test with a non-production hostname before enabling it broadly.
- Keep preload disabled unless an external preload submission and its long-term operational requirements are intentional.

### Troubleshooting

| Symptom | Check |
|---|---|
| Redirect loop behind a proxy | Ensure forwarded HTTPS scheme headers are trusted and processed. |
| Cannot save HTTPS settings | Open the admin settings page over HTTPS. |
| HSTS is absent in development | `FromConfiguration` deliberately enables it only in Production. |
| Wrong redirect port | Set `SslPort` when the server cannot infer the target HTTPS port. |

---
name: orchardcore-reverse-proxy
description: Skill for configuring reverse proxy support in Orchard Core. Covers the forwarded headers middleware (X-Forwarded-For, X-Forwarded-Proto, X-Forwarded-Host, X-Forwarded-Prefix), ReverseProxySettings in the admin UI and the settings recipe step, the OrchardCore ReverseProxy configuration section with ConfigureReverseProxySettings, KnownProxies and KnownNetworks, multi-tenant configuration, deployment, and the effect on HTTPS redirection, rate limiting and OpenID URLs. Use this skill when requests mention Orchard Core Reverse Proxy, forwarded headers, load balancers, nginx, Azure Front Door, client IP behind a proxy, ReverseProxySettings, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.ReverseProxy, ConfigureReverseProxySettings, ForwardedHeadersOptions, ManageReverseProxySettings, OrchardCoreBuilder, and appsettings.json.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Reverse Proxy - Prompt Templates

## Configure Reverse Proxy Support

You are an Orchard Core expert. Generate configuration and code for reverse proxy scenarios including forwarded headers, trusted proxy networks, and multi-tenant deployments behind load balancers.

### Guidelines

- Enable `OrchardCore.ReverseProxy` per tenant. It calls `UseForwardedHeaders()` early in the tenant pipeline using `ReverseProxySettings`.
- Manage the settings at **Settings → Reverse Proxy** (permission `ManageReverseProxySettings`), with the `settings` recipe step, or from configuration.
- Call `ConfigureReverseProxySettings()` on the `OrchardCoreBuilder` to read the `OrchardCore:ReverseProxy` section. Configuration values override the admin values property by property, and the admin page shows a warning.
- `ForwardedHeaders` accepts `None`, `XForwardedFor`, `XForwardedHost`, `XForwardedProto`, `XForwardedPrefix`, `All`, or a comma-separated combination.
- `KnownNetworks` uses CIDR notation; `KnownProxies` uses IP addresses. The module replaces the ASP.NET Core defaults with these lists, and when both are empty, forwarded headers are accepted from any sender. Always set them in production.
- The forwarded client IP feeds `orchardcore-rate-limits` partitions, audit trail, and login logging; the forwarded scheme and host affect `orchardcore-https` redirection and OpenID issuer and redirect URIs.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling and Configuring via Recipe

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.ReverseProxy"
      ]
    },
    {
      "name": "settings",
      "ReverseProxySettings": {
        "ForwardedHeaders": "XForwardedFor, XForwardedProto",
        "KnownNetworks": [ "10.0.0.0/8" ],
        "KnownProxies": [ "10.0.0.4" ]
      }
    }
  ]
}
```

With `OrchardCore.Deployment` enabled, the **Reverse Proxy settings** deployment step exports the same `ReverseProxySettings` object.

### Configuration Options

| Setting | Description | Example Values |
|---------|-------------|----------------|
| `ForwardedHeaders` | Headers to process | `XForwardedFor, XForwardedHost, XForwardedProto`, `All` |
| `KnownNetworks` | Trusted proxy networks in CIDR notation | `["192.168.1.0/24", "10.0.0.0/8"]` |
| `KnownProxies` | Trusted proxy IP addresses | `["192.168.1.200", "192.168.1.201"]` |

### File-Based Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOrchardCms()
    .ConfigureReverseProxySettings();

var app = builder.Build();

app.UseOrchardCore();

app.Run();
```

```json
{
  "OrchardCore": {
    "ReverseProxy": {
      "ForwardedHeaders": "XForwardedFor, XForwardedHost, XForwardedProto",
      "KnownNetworks": [ "10.0.0.0/8" ],
      "KnownProxies": [ "10.0.0.1" ]
    }
  }
}
```

Environment variables:

```text
OrchardCore__ReverseProxy__ForwardedHeaders=XForwardedFor, XForwardedProto
OrchardCore__ReverseProxy__KnownProxies__0=10.0.0.1
```

### Partial Override

Only the properties present in configuration override the admin values:

```json
{
  "OrchardCore": {
    "ReverseProxy": {
      "KnownProxies": [ "10.0.0.1" ]
    }
  }
}
```

Here `KnownProxies` comes from configuration while `ForwardedHeaders` and `KnownNetworks` come from the admin UI.

### Multi-Tenant Considerations

- Admin and recipe settings are tenant-specific.
- The section in the application `appsettings.json` applies to every tenant. Put tenant-specific values in `App_Data/Sites/{TenantName}/appsettings.json` or use `OrchardCore__{TenantName}__ReverseProxy__...` environment variables.
- Tenant `appsettings.json` files are not wrapped in an `OrchardCore` section:

```json
{
  "ReverseProxy": {
    "ForwardedHeaders": "XForwardedFor, XForwardedProto",
    "KnownProxies": [ "10.0.1.100" ]
  }
}
```

### Security Considerations

1. Always configure `KnownProxies` and/or `KnownNetworks`; empty lists trust forwarded headers from any client, which allows IP spoofing that bypasses rate limits and pollutes audit data.
2. Forward only the headers the proxy actually sets. `XForwardedHost` lets callers choose the host used to resolve tenants and build absolute URLs.
3. Keep CIDR ranges narrow; do not trust whole public ranges.
4. Verify with a request that `HttpContext.Connection.RemoteIpAddress` and `Request.Scheme` show the client IP and `https`.

### Upgrading from 3.x

- Rename the `OrchardCore_ReverseProxy` section to `OrchardCore:ReverseProxy` (environment variable prefix `OrchardCore__ReverseProxy__`). The old name still works but is deprecated; when both exist, the new name wins.
- In the application `appsettings.json` the section is nested under `OrchardCore`; in a tenant `App_Data/Sites/{TenantName}/appsettings.json` it sits at the root as `ReverseProxy`.

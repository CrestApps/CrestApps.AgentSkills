---
name: orchardcore-reverse-proxy
description: Skill for configuring reverse proxy support in Orchard Core. Covers forwarded headers middleware (X-Forwarded-For, X-Forwarded-Proto, X-Forwarded-Host), configuration sources (admin UI vs file-based), security considerations for trusted proxies, and multi-tenancy forwarding. Use this skill when requests mention Orchard Core Reverse Proxy, Configure Reverse Proxy Support, Enabling the Reverse Proxy Feature, Configuration Options, Admin UI Configuration (Scenario 1), File-Based Configuration (Scenario 2), or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.ReverseProxy, ConfigureReverseProxySettings, OrchardCoreBuilder, CreateBuilder, appsettings.json. It also helps with Admin UI Configuration (Scenario 1), File-Based Configuration (Scenario 2), Partial Override Configuration (Scenario 3), plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Reverse Proxy - Prompt Templates

## Configure Reverse Proxy Support

You are an Orchard Core expert. Generate configuration and code for reverse proxy scenarios including forwarded headers, trusted proxy networks, and multi-tenant deployments behind load balancers or reverse proxies.

### Guidelines

- Enable `OrchardCore.ReverseProxy` to configure forwarded headers middleware.
- Forwarded headers include `X-Forwarded-For` (client IP), `X-Forwarded-Host` (original host), and `X-Forwarded-Proto` (original protocol).
- Configuration can be managed via the admin UI at **Settings → Reverse Proxy** or through `appsettings.json`.
- Call `ConfigureReverseProxySettings()` on `OrchardCoreBuilder` to enable file-based configuration.
- Always configure `KnownProxies` and `KnownNetworks` in production to prevent IP spoofing.
- `KnownNetworks` values must use CIDR notation (e.g., `192.168.1.0/24`).
- Configuration file values take precedence over admin UI settings when `ConfigureReverseProxySettings()` is called.
- Admin UI settings are tenant-specific; file-based settings apply to all tenants unless placed in tenant-specific `appsettings.json`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling the Reverse Proxy Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.ReverseProxy"
      ],
      "disable": []
    }
  ]
}
```

### Configuration Options

| Setting | Description | Example Values |
|---------|-------------|----------------|
| `ForwardedHeaders` | Specifies which headers to forward | `None`, `XForwardedFor`, `XForwardedHost`, `XForwardedProto`, `XForwardedPrefix`, `All`, or comma-separated combination |
| `KnownNetworks` | Array of known proxy networks in CIDR notation | `["192.168.1.0/24", "10.0.0.0/8"]` |
| `KnownProxies` | Array of known proxy IP addresses | `["192.168.1.200", "192.168.1.201"]` |

### Admin UI Configuration (Scenario 1)

When `ConfigureReverseProxySettings()` is **not** called, all settings are managed through the admin UI at **Settings → Reverse Proxy**. The available options are:

- **X-Forwarded-For**: Enables forwarding of the client IP address.
- **X-Forwarded-Host**: Enables forwarding of the original host requested by the client.
- **X-Forwarded-Proto**: Enables forwarding of the protocol (HTTP or HTTPS) used by the client.

These settings are stored in the site configuration and are managed per tenant.

### File-Based Configuration (Scenario 2)

To use configuration from `appsettings.json`, call `ConfigureReverseProxySettings()` in `Program.cs`:

```csharp
builder.Services.AddOrchardCms()
    .ConfigureReverseProxySettings();
```

Then add the configuration section to `appsettings.json`:

```json
{
  "OrchardCore_ReverseProxy": {
    "ForwardedHeaders": "XForwardedFor, XForwardedHost, XForwardedProto",
    "KnownNetworks": ["192.168.1.0/24"],
    "KnownProxies": ["192.168.1.200", "192.168.1.201"]
  }
}
```

### Partial Override Configuration (Scenario 3)

When `ConfigureReverseProxySettings()` is called and only some settings are specified in `appsettings.json`, the configuration file values are **merged** with admin UI values. Only the specified config file properties override the admin values:

```json
{
  "OrchardCore_ReverseProxy": {
    "KnownProxies": ["10.0.0.1"]
  }
}
```

In this example, `KnownProxies` comes from the config file while `ForwardedHeaders` and `KnownNetworks` come from the admin UI.

### Forward All Headers with Full Proxy Configuration

```json
{
  "OrchardCore_ReverseProxy": {
    "ForwardedHeaders": "All",
    "KnownNetworks": ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"],
    "KnownProxies": ["10.0.0.1", "10.0.0.2"]
  }
}
```

### Security Considerations

Improperly configured reverse proxy settings can expose the application to IP spoofing and header injection attacks. Follow these rules:

1. **Always configure `KnownProxies` and `KnownNetworks`** when using forwarded headers in production.
2. **Only trust headers from known proxies** — do not use `All` without specifying known networks.
3. **Use CIDR notation carefully** to avoid trusting untrusted networks.
4. **Test your configuration** to verify that client IPs and protocols are forwarded correctly.

### Multi-Tenant Considerations

- Admin UI settings are **tenant-specific** — each tenant can have its own reverse proxy configuration.
- File-based settings in the main `appsettings.json` apply to **all tenants**.
- For tenant-specific file-based settings, use the tenant's configuration file:

```
App_Data/Sites/{TenantName}/appsettings.json
```

Example tenant-specific override:

```json
{
  "OrchardCore_ReverseProxy": {
    "ForwardedHeaders": "XForwardedFor, XForwardedProto",
    "KnownProxies": ["10.0.1.100"]
  }
}
```

### Complete Program.cs Example Behind a Load Balancer

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrchardCms()
    .ConfigureReverseProxySettings();

var app = builder.Build();

app.UseOrchardCore();

app.Run();
```

With the corresponding `appsettings.json`:

```json
{
  "OrchardCore_ReverseProxy": {
    "ForwardedHeaders": "XForwardedFor, XForwardedHost, XForwardedProto",
    "KnownNetworks": ["10.0.0.0/8"],
    "KnownProxies": ["10.0.0.1"]
  }
}
```

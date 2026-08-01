---
name: orchardcore-cors
description: Skill for administering tenant-specific cross-origin resource sharing in Orchard Core. Covers CorsSettings policies, CorsPolicySetting options, CorsService persistence, CorsOptionsConfiguration, admin configuration, deployment, and secure origin restrictions. Use this skill when requests mention Orchard Core CORS, CorsSettings, cross-origin requests, allowed origins, CORS policies, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Cors, CorsSettings, CorsPolicySetting, CorsService, CorsOptionsConfiguration, CorsOptions, and ManageCorsSettings. It also helps with feature recipes, policy deployment, security validation, and the code patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core CORS - Prompt Templates

## Configure Tenant CORS Policies

You are an Orchard Core expert. Generate secure configuration and code for the Orchard Core CORS module.

### Guidelines

- Enable the `OrchardCore.Cors` feature. The module has no declared feature dependencies.
- The module calls `app.UseCors()` at Orchard Core's CORS pipeline order and configures ASP.NET Core `CorsOptions` from tenant site settings.
- Configure policies from **Configuration → Settings → Security → CORS**. The feature requires the `ManageCorsSettings` permission.
- Each `CorsPolicySetting` has a name, origin, header, method, credentials, default-policy, and exposed-header settings.
- `CorsOptionsConfiguration` rejects a policy that combines `AllowAnyOrigin` and `AllowCredentials`; this is unsafe and invalid in ASP.NET Core.
- Use exact trusted origins when credentials are allowed. Do not use wildcard origins for authenticated browser requests.
- The first policy becomes the default if no policy is explicitly marked as default.
- Setting changes release the tenant shell, so the CORS middleware reads the new policy configuration.
- Enable `OrchardCore.Deployment` when CORS site settings must be exported by a deployment plan.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling CORS

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Cors"
      ],
      "disable": []
    }
  ]
}
```

### Configuring Policies in the Admin

Create a policy under **Settings → Security → CORS** and configure:

| Setting | Guidance |
|---|---|
| Name | Use a stable descriptive name such as `PublicApi`. |
| Allowed origins | Use complete origins such as `https://app.example.com`. |
| Allowed methods | Limit to the verbs required by the browser client. |
| Allowed headers | Allow only client headers the API actually accepts. |
| Allow credentials | Enable only for trusted, authenticated origins. |
| Exposed headers | List response headers browser JavaScript may read. |
| Default policy | Mark one policy when all endpoints should use it. |

The policy editor refuses wildcard-origin policies that also allow credentials and shows a security warning.

### CorsSettings Model

The settings are stored on the tenant site as `CorsSettings`:

```csharp
using OrchardCore.Cors.Settings;

var settings = new CorsSettings
{
    Policies =
    [
        new CorsPolicySetting
        {
            Name = "PublicApi",
            AllowedOrigins = ["https://app.example.com"],
            AllowedMethods = ["GET", "POST"],
            AllowedHeaders = ["Content-Type", "Authorization"],
            ExposedHeaders = ["X-Request-Id"],
            AllowAnyOrigin = false,
            AllowAnyMethod = false,
            AllowAnyHeader = false,
            AllowCredentials = true,
            IsDefaultPolicy = true,
        },
    ],
};
```

Do not write this model directly from an arbitrary controller. Use the admin UI or a service that deliberately updates tenant site settings and releases the shell.

### Reading CORS Settings

`CorsService` is registered as a singleton and exposes `GetSettingsAsync`.

```csharp
using OrchardCore.Cors.Services;
using OrchardCore.Cors.Settings;

namespace MyModule.Services;

public sealed class CorsPolicyReporter
{
    private readonly CorsService _corsService;

    public CorsPolicyReporter(CorsService corsService)
    {
        _corsService = corsService;
    }

    public async Task<IEnumerable<CorsPolicySetting>> GetPoliciesAsync()
    {
        var settings = await _corsService.GetSettingsAsync();
        return settings?.Policies ?? [];
    }
}
```

### Endpoint-Specific Policies

The module establishes the configured ASP.NET Core policies. Apply a named policy to an endpoint only when it should not use the default policy.

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace MyModule.Controllers;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController : ControllerBase
{
    [HttpGet]
    [EnableCors("PublicApi")]
    [AllowAnonymous]
    public IActionResult Get()
    {
        return Ok();
    }
}
```

The policy name must exactly match a policy configured in `CorsSettings`.

### Deployment

When `OrchardCore.Deployment` is enabled, the module registers a site-settings deployment step labelled **Cors settings**. Add that step to a deployment plan to export tenant CORS policies, then import it into the destination tenant.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Cors",
        "OrchardCore.Deployment"
      ],
      "disable": []
    }
  ]
}
```

### Security Checklist

- Never combine `AllowAnyOrigin` and `AllowCredentials`.
- Do not use an origin path, trailing route, or wildcard where an origin is expected.
- Restrict unsafe methods and non-standard headers.
- Treat CORS as a browser policy, not as authentication or authorization.
- Validate requests with Orchard Core permissions and application authorization even when the origin is trusted.

### Troubleshooting

| Symptom | Check |
|---|---|
| No CORS headers | Verify the feature is enabled and the endpoint selects a valid named or default policy. |
| Browser preflight fails | Add only the requested method and headers to the selected policy. |
| Policy is missing after save | Correct the invalid wildcard-origin plus credentials combination. |
| Setting change has no effect | Confirm the tenant shell was released after updating settings. |

---
name: orchardcore-security
description: Skill for configuring security and authorization in Orchard Core. Covers declaring permissions with LocalizationSource descriptions, permission providers and stereotypes, IAuthorizationService checks, content authorization handlers, security headers through SecuritySettings and the OrchardCore Security configuration section, the Api authentication scheme, RFC 9457 Problem Details API error responses, anti-forgery, and the 4.0 removal of obsolete permission members. Use this skill when requests mention Orchard Core Security, permissions, authorization, security headers, CSP, Permissions-Policy, Api authentication scheme, Problem Details, 401 or 403 API responses, SkipStatusCodePages, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Security, IPermissionProvider, Permission, PermissionStereotype, LocalizationSource, CommonPermissions, ConfigureSecuritySettings, and OrchardCoreConstants.AuthenticationSchemes.Api.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Security - Prompt Templates

## Configure Security and Authorization

You are an Orchard Core expert. Generate security and authorization configurations for Orchard Core.

### Guidelines

- Orchard Core authorizes with permissions granted to roles. Check them with `IAuthorizationService.AuthorizeAsync(User, permission[, resource])`.
- Declare permissions as static fields on a `*Permissions` class (usually in an `Abstractions` or `Core` project, so other modules can reference them without depending on your module) and return them from an `IPermissionProvider`.
- `Permission.Description` is a `LocalizationSource` in 4.0. Create it with `LocalizationSource.Create("text", typeof(DeclaringType))` or `LocalizationSource.Create<DeclaringType>("text")` so the PO-file context is preserved. String descriptions still compile but are context-free.
- Mark dangerous permissions with `isSecurityCritical: true`.
- Content permissions (`ViewContent`, `EditContent`, `PublishContent`, `DeleteContent`, and their `Own` variants) live in `OrchardCore.Contents.CommonPermissions`; pass the content item as the resource so dynamic per-type permissions apply.
- `OrchardCore.Security` adds security headers configured from **Settings → Security → Security Headers** (permission `ManageSecurityHeadersSettings`).
- API endpoints authenticate with the shared `Api` scheme (`OrchardCoreConstants.AuthenticationSchemes.Api`), which accepts bearer tokens (for example from `OrchardCore.OpenId.Validation`) and never cookies.
- Related skills: `orchardcore-cors`, `orchardcore-https`, `orchardcore-reverse-proxy`, `orchardcore-openid`, `orchardcore-openapi`, `orchardcore-rate-limits`, `orchardcore-antivirus`, `orchardcore-users-roles`.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Security Features

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.Security",
        "OrchardCore.Https",
        "OrchardCore.Cors",
        "OrchardCore.ReverseProxy",
        "OrchardCore.RateLimits"
      ]
    }
  ]
}
```

### Security Headers via Recipe

```json
{
  "steps": [
    {
      "name": "settings",
      "SecuritySettings": {
        "ContentTypeOptions": "nosniff",
        "ReferrerPolicy": "strict-origin-when-cross-origin",
        "ContentSecurityPolicy": {
          "default-src": "'self'",
          "script-src": "'self'",
          "style-src": "'self' 'unsafe-inline'",
          "frame-ancestors": "'self'"
        },
        "PermissionsPolicy": {
          "camera": "()",
          "microphone": "()",
          "geolocation": "()"
        }
      }
    }
  ]
}
```

`frame-ancestors` replaces the obsolete `X-Frame-Options` header. Admin scripts in 4.0 load as ES modules without jQuery; test a strict `script-src` against the admin before removing `'unsafe-inline'` for the whole site.

### Security Headers from Configuration

Call `ConfigureSecuritySettings()` to let configuration override the admin values. The settings page then shows that values come from configuration.

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureSecuritySettings();
```

```json
{
  "OrchardCore": {
    "Security": {
      "ContentSecurityPolicy": {
        "default-src": "'self'"
      },
      "PermissionsPolicy": {
        "fullscreen": "self"
      },
      "ReferrerPolicy": "no-referrer"
    }
  }
}
```

### Declaring Permissions

```csharp
using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace MyModule;

public static class InvoicePermissions
{
    public static readonly Permission ManageInvoices = new(
        "ManageInvoices",
        LocalizationSource.Create("Manage invoices", typeof(InvoicePermissions)),
        isSecurityCritical: true);

    public static readonly Permission ViewInvoices = new(
        "ViewInvoices",
        LocalizationSource.Create("View invoices", typeof(InvoicePermissions)),
        [ManageInvoices]);
}

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        InvoicePermissions.ManageInvoices,
        InvoicePermissions.ViewInvoices,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
```

Register it with `services.AddPermissionProvider<Permissions>();`. The third constructor argument lists permissions that imply this one. Display a description with `IStringLocalizerFactory.Localize(permission.Description)`; read the raw text with `permission.Description.Value`.

### Checking Permissions in a Controller

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace MyModule.Controllers;

public sealed class InvoiceController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentManager _contentManager;

    public InvoiceController(
        IAuthorizationService authorizationService,
        IContentManager contentManager)
    {
        _authorizationService = authorizationService;
        _contentManager = contentManager;
    }

    public async Task<IActionResult> Display(string contentItemId)
    {
        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem == null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, contentItem))
        {
            return Forbid();
        }

        return View(contentItem);
    }
}
```

### Custom Authorization Handler

```csharp
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.Security;

namespace MyModule.Security;

public sealed class InvoiceOwnerAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (requirement.Permission.Name == InvoicePermissions.ViewInvoices.Name &&
            context.Resource is ContentItem contentItem &&
            contentItem.Owner == context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

Register with `services.AddScoped<IAuthorizationHandler, InvoiceOwnerAuthorizationHandler>();`. `ContentItem.Owner` stores the user ID.

### API Endpoints and Error Responses

Secure API endpoints with the `Api` scheme and return Problem Details:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore;

namespace MyModule.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize(AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api)]
public sealed class InvoiceApiController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;

    public InvoiceApiController(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicePermissions.ViewInvoices))
        {
            return this.ApiForbidProblem();
        }

        return this.ApiNotFoundProblem();
    }
}
```

- In 4.0, the `401` and `403` responses produced by the `Api` scheme, and error responses of endpoints marked `[SkipStatusCodePages]`, carry an RFC 9457 `application/problem+json` body. Other error responses still go to the `OrchardCore.Diagnostics` error pages.
- Status codes and headers are unchanged; `WWW-Authenticate` (RFC 6750 error details) still comes from the authentication handler. The body is written through `IProblemDetailsService`, so `services.AddProblemDetails(options => ...)` customizations apply. `AddOrchardCms()` enables it; hosts built with `AddOrchardCore()` call `.AddApiProblemDetails()`.
- Helpers on `ControllerBase` and `HttpContext`: `ApiForbidProblem()`, `ApiBadRequestProblem()`, `ApiNotFoundProblem()`, `ApiValidationProblem()`. In minimal APIs, `httpContext.ChallengeOrForbid(OrchardCoreConstants.AuthenticationSchemes.Api)` returns the right challenge.
- Add `[SkipStatusCodePages]` (or `.WithMetadata(new SkipStatusCodePagesAttribute())`) to JSON endpoints that use cookie authentication so their errors are not replaced by HTML pages.

### Anti-Forgery

Orchard Core forms include anti-forgery tokens automatically. Only bypass validation for endpoints that authenticate with bearer tokens:

```csharp
[HttpPost]
[IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api)]
public IActionResult Import()
{
    return Ok();
}
```

Minimal APIs use `.DisableAntiforgery()` together with the `Api` scheme.

### Upgrading from 3.x

- Obsolete permission members were removed; role claims are unchanged. Replace, for example:
  - `OrchardCore.Users.CommonPermissions.*` → `OrchardCore.Users.UsersPermissions.*`
  - `OrchardCore.Deployment.CommonPermissions.*` → `OrchardCore.Deployment.DeploymentPermissions.*`
  - `OrchardCore.Apis.GraphQL.CommonPermissions.*` → `OrchardCore.Apis.GraphQL.GraphQLPermissions.*`
  - `OrchardCore.Media.Permissions.*` → `OrchardCore.Media.MediaPermissions.*`
  - `OrchardCore.OpenId.Permissions.*` → `OrchardCore.OpenId.OpenIdPermissions.*`
  - `OrchardCore.Settings.Permissions.ManageSettings` → `OrchardCore.Settings.SettingsPermissions.ManageSettings`
  - `OrchardCore.Security.StandardPermissions.SiteOwner` → removed; assign users to the `Administrator` role.
- Build against the latest 3.x first and fix every `CS0618` warning. Use the member with the same name, not the one named in the old obsolete message (`AdminMenu` `ManageAdminMenu`/`ViewAdminMenuAll` and `CommonPermissions.CreateAssignRoleToUsersPermission` returned the wrong permission in 3.x).
- `Permission.Description` changed from `string` to `LocalizationSource`; recompile modules. Translations of built-in descriptions now come from PO files, not Data Localization.
- The `OrchardCore_Security` and `OrchardCore_ReverseProxy` configuration sections are now `OrchardCore:Security` and `OrchardCore:ReverseProxy` (old names still work but are deprecated).
- API `401`/`403` responses now have a Problem Details body instead of an empty one.

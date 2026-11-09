---
name: orchardcore-openid
description: Skill for configuring and managing OpenID Connect in Orchard Core. Covers the authorization server, token validation, client integration and management features, the OpenIdServerSettings, OpenIdApplication, OpenIdScope, OpenIdValidationSettings and OpenIdClientSettings recipe steps, authorization code with PKCE, client credentials, refresh tokens, pushed authorization requests, RequireEndSessionConfirmation, userinfo claims, securing APIs with the Api scheme, token endpoint rate limiting, signing and encryption keys from the Secrets module with OrchardCore.OpenId.Secrets, and OpenIdPermissions. Use this skill when requests mention Orchard Core OpenID Connect, OAuth 2.0, OpenIddict, token endpoint, bearer tokens, PKCE, logout confirmation, external OIDC login, or related setup or troubleshooting work. Strong matches include OrchardCore.OpenId, OrchardCore.OpenId.Server, OrchardCore.OpenId.Validation, IOpenIdServerService, IOpenIdApplicationManager, and OpenIdPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core OpenID Connect - Prompt Templates

## Module Overview

The `OrchardCore.OpenId` module provides OpenID Connect and OAuth 2.0 on top of OpenIddict (7.7.1 in Orchard Core 4.0). Enable the features your scenario needs:

| Feature | ID | Purpose |
|---|---|---|
| OpenID Connect Core Services | `OrchardCore.OpenId` | Shared stores and managers. |
| OpenID Connect Management UI | `OrchardCore.OpenId.Management` | Applications and scopes admin. |
| OpenID Connect Authorization Server | `OrchardCore.OpenId.Server` | `/connect/authorize`, `/connect/token`, `/connect/logout`, `/connect/userinfo`, `/connect/par`, introspection, revocation. Depends on Management. |
| OpenID Connect Token Validation | `OrchardCore.OpenId.Validation` | Validates bearer tokens for the `Api` scheme (same tenant, another tenant, or a remote authority). |
| OpenID Connect Client Integration | `OrchardCore.OpenId.Client` | Sign in with an external OIDC provider. Depends on `OrchardCore.Users.ExternalAuthentication`. |
| OpenID Connect Secrets | `OrchardCore.OpenId.Secrets` | 4.0. Loads the server signing and encryption keys from the Secrets module. Depends on `OrchardCore.OpenId.Server` and `OrchardCore.Secrets`; must be enabled explicitly. |

Admin locations:

- **Settings → OpenID Connect → Authorization Server** (`ManageServerSettings`)
- **Settings → OpenID Connect → Token Validation** (`ManageValidationSettings`)
- **Settings → OpenID Connect → Authentication Client** (`ManageClientSettings`)
- **Access Control → OpenID Connect → Applications** and **Scopes** (`ManageApplications`, `ManageScopes`)

### Guidelines

- The authorization server validates tokens sent to its own `/connect/userinfo` endpoint; enable `OrchardCore.OpenId.Validation` to accept its tokens on API endpoints.
- Use authorization code + PKCE for browser and native apps, client credentials for services. Avoid the password and implicit flows for new clients.
- Always use HTTPS in production for every redirect URI and the authority.
- Secure API endpoints with `[Authorize(AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api)]` (the `"Api"` scheme) rather than the raw OpenIddict scheme; the token's roles then drive Orchard permission checks.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

## Authorization Server Settings

### OpenIdServerSettings Recipe Step

The dedicated step toggles endpoints with `Enable*Endpoint` booleans (the paths are always `/connect/*`). It replaces the server settings: every flow or endpoint left out becomes `false`, while `RequireEndSessionConfirmation` defaults to `true`.

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.OpenId.Server",
        "OrchardCore.OpenId.Validation"
      ]
    },
    {
      "name": "OpenIdServerSettings",
      "AccessTokenFormat": "JsonWebToken",
      "Authority": "https://auth.example.com",
      "EnableTokenEndpoint": true,
      "EnableAuthorizationEndpoint": true,
      "EnableLogoutEndpoint": true,
      "EnableUserInfoEndpoint": true,
      "EnablePushedAuthorizationEndpoint": true,
      "EnableIntrospectionEndpoint": false,
      "EnableRevocationEndpoint": false,
      "AllowAuthorizationCodeFlow": true,
      "AllowClientCredentialsFlow": true,
      "AllowRefreshTokenFlow": true,
      "AllowPasswordFlow": false,
      "AllowImplicitFlow": false,
      "AllowHybridFlow": false,
      "DisableRollingRefreshTokens": false,
      "RequireProofKeyForCodeExchange": true,
      "RequirePushedAuthorizationRequests": false,
      "RequireEndSessionConfirmation": true,
      "UseReferenceAccessTokens": false
    }
  ]
}
```

Other properties: `DisableAccessTokenEncryption` (lets third-party resource servers read JWTs), `SigningCertificateStoreLocation`/`SigningCertificateStoreName`/`SigningCertificateThumbprint`, and the matching `EncryptionCertificate*` properties. `AccessTokenFormat` is `DataProtection` (default) or `JsonWebToken`.

The generic `settings` step also accepts an `OpenIdServerSettings` object with the stored property names, including custom `*EndpointPath` values.

### Signing and Encryption Keys From Secrets (4.0)

Enable `OrchardCore.OpenId.Secrets` to pick the server keys from the Secrets module in the **RSA Keys from Secrets** card of the server settings. The choice is stored in the `OpenIdSecretSettings` site settings (`SigningKeySecretName`, `EncryptionKeySecretName`), and each secret can be an `RsaKeySecret` with its private key (portable, tried first) or an `X509Secret` (certificate store reference or PEM certificate with private key). Secret keys are added before the configured or generated ones, so they are used first.

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [ "OrchardCore.Secrets", "OrchardCore.OpenId.Server", "OrchardCore.OpenId.Secrets" ]
    },
    {
      "name": "settings",
      "OpenIdSecretSettings": {
        "SigningKeySecretName": "OpenId.SigningKey",
        "EncryptionKeySecretName": "OpenId.EncryptionKey"
      }
    }
  ]
}
```

Create the RSA or X.509 secrets first (admin or `ISecretManager`; the `Secrets` recipe step only creates text secrets). When the feature is enabled, a migration turns configured `SigningCertificate*`/`EncryptionCertificate*` settings into `X509Secret` references named `OpenId.SigningCertificate` and `OpenId.EncryptionCertificate`; certificates and private keys are not copied, so keep them accessible on every instance, and auto-generated development certificates are not converted. See [orchardcore-secrets](../orchardcore-secrets/SKILL.md).

### Server Settings via Migration

```csharp
using OrchardCore.Data.Migration;
using OrchardCore.OpenId.Services;

namespace MyModule.Migrations;

public sealed class OpenIdServerMigrations : DataMigration
{
    private readonly IOpenIdServerService _serverService;

    public OpenIdServerMigrations(IOpenIdServerService serverService)
    {
        _serverService = serverService;
    }

    public async Task<int> CreateAsync()
    {
        var settings = await _serverService.LoadSettingsAsync();

        settings.AllowAuthorizationCodeFlow = true;
        settings.AllowClientCredentialsFlow = true;
        settings.AllowRefreshTokenFlow = true;
        settings.RequireProofKeyForCodeExchange = true;
        settings.AuthorizationEndpointPath = "/connect/authorize";
        settings.TokenEndpointPath = "/connect/token";
        settings.LogoutEndpointPath = "/connect/logout";
        settings.UserinfoEndpointPath = "/connect/userinfo";
        settings.PushedAuthorizationEndpointPath = "/connect/par";
        settings.RequireEndSessionConfirmation = true;

        await _serverService.UpdateSettingsAsync(settings);

        return 1;
    }
}
```

Use `LoadSettingsAsync()` (not the cached `GetSettingsAsync()`) before updating. Validate with `ValidateSettingsAsync(settings)`; for example, PAR requires the authorization endpoint, and the code flow requires both the authorization and token endpoints.

### Logout Without Confirmation

`RequireEndSessionConfirmation` (default `true`) shows a confirmation prompt on `/connect/logout`. Set it to `false` to sign users out immediately when the request carries a valid `id_token_hint` issued for the currently authenticated user; otherwise the prompt is still shown. When the prompt is skipped, the local authentication cookie is removed too. The setting is available in the admin, the `OpenIdServerSettings` recipe step, and deployment plans.

```json
{
  "steps": [
    {
      "name": "OpenIdServerSettings",
      "EnableTokenEndpoint": true,
      "EnableAuthorizationEndpoint": true,
      "EnableLogoutEndpoint": true,
      "EnableUserInfoEndpoint": true,
      "AllowAuthorizationCodeFlow": true,
      "AllowRefreshTokenFlow": true,
      "RequireProofKeyForCodeExchange": true,
      "RequireEndSessionConfirmation": false
    }
  ]
}
```

### Token Endpoint Rate Limiting

When `OrchardCore.RateLimits` is enabled, the server registers a built-in limit on `POST /connect/token` (route `Access.Token`) for the **password grant only**: 100 requests per minute per IP, sliding window, sharing the `password-authentication` budget with the Users login form. Authorization code, refresh token, and client credentials requests are not limited, since a single server often requests tokens for many users. A rejected token request gets an OAuth 2.0 error response with `error=slow_down` and a `Retry-After` header (through a route rejection handler) instead of an empty body, so token clients can back off. See `orchardcore-rate-limits`.

## Client Applications

### Guidelines

- Every client needs a unique `ClientId`. Confidential clients need a `ClientSecret`; public clients (SPAs, mobile) must not have one and should require PKCE.
- `ConsentType` is `explicit`, `implicit`, `external`, or `systematic`. Use `implicit` only for trusted first-party apps.
- Register only the redirect URIs the app uses. Multiple URIs are space- or comma-separated in recipes.
- Client credentials apps get their permissions from `RoleEntries`; user-based flows get them from the user's roles through the `roles` scope.

### OpenIdApplication Recipe Step

One application per step:

```json
{
  "steps": [
    {
      "name": "OpenIdApplication",
      "ClientId": "spa-client",
      "DisplayName": "Single Page Application",
      "Type": "public",
      "ConsentType": "implicit",
      "AllowAuthorizationCodeFlow": true,
      "AllowRefreshTokenFlow": true,
      "AllowLogoutEndpoint": true,
      "RequireProofKeyForCodeExchange": true,
      "RedirectUris": "https://spa.example.com/callback https://spa.example.com/silent-renew",
      "PostLogoutRedirectUris": "https://spa.example.com/logout-callback",
      "ScopeEntries": [
        { "Name": "profile" },
        { "Name": "email" },
        { "Name": "roles" },
        { "Name": "api" }
      ]
    },
    {
      "name": "OpenIdApplication",
      "ClientId": "background-worker",
      "DisplayName": "Background Worker",
      "Type": "confidential",
      "ClientSecret": "{{WorkerSecret}}",
      "AllowClientCredentialsFlow": true,
      "RoleEntries": [
        { "Name": "Editor" }
      ],
      "ScopeEntries": [
        { "Name": "api" }
      ]
    }
  ]
}
```

Other flags: `AllowPasswordFlow`, `AllowHybridFlow`, `AllowImplicitFlow`, `AllowIntrospectionEndpoint`, `AllowRevocationEndpoint`, `RequirePushedAuthorizationRequests`. The `openid` scope does not need a scope entry. A recipe run for an existing `ClientId` updates that application.

### Client Registration via Migration

```csharp
using OpenIddict.Abstractions;
using OrchardCore.Data.Migration;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;

namespace MyModule.Migrations;

public sealed class OpenIdClientMigrations : DataMigration
{
    private readonly IOpenIdApplicationManager _applicationManager;

    public OpenIdClientMigrations(IOpenIdApplicationManager applicationManager)
    {
        _applicationManager = applicationManager;
    }

    public async Task<int> CreateAsync()
    {
        await _applicationManager.CreateAsync(new OpenIdApplicationDescriptor
        {
            ClientId = "my-web-app",
            DisplayName = "My Web Application",
            ClientSecret = "your-client-secret-here",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + "api",
            },
            Requirements =
            {
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
            },
            RedirectUris = { new Uri("https://myapp.example.com/signin-oidc") },
            PostLogoutRedirectUris = { new Uri("https://myapp.example.com/signout-callback-oidc") },
        });

        return 1;
    }
}
```

Prefer a recipe for secrets that differ per environment, and never commit real secrets.

## Authorization Flows

| Flow | Server setting | Client requirements |
|---|---|---|
| Authorization code (+ PKCE) | `AllowAuthorizationCodeFlow` | `GrantTypes.AuthorizationCode`, `ResponseTypes.Code`; public clients require PKCE. |
| Client credentials | `AllowClientCredentialsFlow` | Confidential client with `GrantTypes.ClientCredentials`; roles from `RoleEntries`. |
| Refresh token | `AllowRefreshTokenFlow` | `GrantTypes.RefreshToken`; rolling refresh tokens unless `DisableRollingRefreshTokens`. |
| Pushed authorization requests | `EnablePushedAuthorizationEndpoint` (`/connect/par`) | Application `RequirePushedAuthorizationRequests` to enforce. |
| Implicit / hybrid / password | `AllowImplicitFlow`, `AllowHybridFlow`, `AllowPasswordFlow` | Legacy only. |

## Scopes and Claims

### OpenIdScope Recipe Step

```json
{
  "steps": [
    {
      "name": "OpenIdScope",
      "ScopeName": "api",
      "DisplayName": "API Access",
      "Description": "Grants access to protected API endpoints.",
      "Resources": "my-resource-server"
    }
  ]
}
```

`Resources` is space-separated and becomes the token audience. In the admin, a scope can also target tenants, which builds `oct:{TenantName}` audiences for cross-tenant validation.

### Standard Scopes

- `openid` — required; returns `sub`.
- `profile` — profile claims.
- `email` — `email` and `email_verified`.
- `phone` — phone number claims.
- `roles` — role claims; needed for Orchard permission checks on API calls.

### Custom Userinfo Claims

```csharp
using OrchardCore.OpenId.Abstractions.Handlers;

namespace MyModule.Services;

public sealed class TenantUserInfoClaimsProvider : IUserInfoClaimsProvider
{
    public Task GenerateAsync(UserInfoClaimsContext context)
    {
        context.Claims["tenant"] = "example";

        return Task.CompletedTask;
    }
}
```

Register with `services.AddScoped<IUserInfoClaimsProvider, TenantUserInfoClaimsProvider>();`. Standard scope claims are already written when `GenerateAsync` runs; inspect `context.Principal` for the user.

## Securing APIs with Token Validation

### OpenIdValidationSettings Recipe Step

Set exactly one of `Tenant` (validate tokens issued by an authorization server tenant in the same app) or `Authority` (remote server with discovery). With neither, the settings are invalid and every bearer call returns `401`.

```json
{
  "steps": [
    {
      "name": "OpenIdValidationSettings",
      "Tenant": "Default"
    }
  ]
}
```

```json
{
  "steps": [
    {
      "name": "OpenIdValidationSettings",
      "Authority": "https://idp.example.com",
      "Audience": "my-resource-server",
      "MetadataAddress": "",
      "DisableTokenTypeValidation": false
    }
  ]
}
```

### API Controller

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore;
using OrchardCore.Contents;

namespace MyModule.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api)]
public sealed class ProductsController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;

    public ProductsController(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent))
        {
            return this.ApiForbidProblem();
        }

        return Ok(new { UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) });
    }
}
```

- A missing or invalid token returns `401` with the RFC 6750 `WWW-Authenticate` header from OpenIddict and, in 4.0, an RFC 9457 Problem Details body written by the `Api` scheme's middleware through `IProblemDetailsService`. Insufficient permissions return `403` with Problem Details. The OAuth 2.0 error details stay in the `WWW-Authenticate` header; the body doesn't repeat them as `error`/`error_uri` extensions.
- `DisableAntiforgery()` / `[IgnoreAntiforgeryToken]` is appropriate only on bearer-authenticated endpoints.

## External OpenID Connect Login

The `OrchardCore.OpenId.Client` feature signs users in with an external provider and links or registers local users (an `email` claim links to an existing account after authentication).

```json
{
  "steps": [
    {
      "name": "OpenIdClientSettings",
      "DisplayName": "Corporate IdP",
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "ClientId": "azure-app-client-id",
      "ClientSecret": "azure-app-client-secret",
      "CallbackPath": "/signin-oidc",
      "SignedOutCallbackPath": "/signout-callback-oidc",
      "SignedOutRedirectUri": "https://www.example.com/",
      "ResponseType": "code",
      "ResponseMode": "form_post",
      "Scopes": "email profile",
      "StoreExternalTokens": false
    }
  ]
}
```

`Scopes` is a space- or comma-separated string of extra scopes.

In 4.0, `OpenIdClientSettings.ClientSecretSecretName` can name a `TextSecret` of the Secrets module; it takes precedence over `ClientSecret`. With `OrchardCore.Secrets` enabled, the **Authentication Client** settings offer **Use a secret** (suggested name `OpenIdClient.ClientSecret`), and a saved client secret can be moved without retyping it from **Tools → Security → Migrate Secrets**. The `OpenIdClientSettings` step has no secret-name property and leaves an existing reference unchanged; set it in the admin or with the generic `settings` step. See [orchardcore-secrets](../orchardcore-secrets/SKILL.md).

For Microsoft Entra ID, Google, GitHub, Facebook, or X, prefer the dedicated modules (`orchardcore-microsoft-auth`, `orchardcore-google`, `orchardcore-github-authentication`, `orchardcore-facebook`, `orchardcore-twitter`).

## Upgrading from 3.x

- `OrchardCore.OpenId.Permissions.ManageApplications`, `ManageClientSettings`, `ManageScopes`, `ManageServerSettings`, and `ManageValidationSettings` were removed; use the same members on `OrchardCore.OpenId.OpenIdPermissions`.
- Disabling `RequireEndSessionConfirmation` now really skips the prompt for a valid `id_token_hint` (the prompt was always shown in 3.x). It can now be set from the `OpenIdServerSettings` step and is exported by deployment plans; omitted, it is `true`.
- Recipes that used non-existent step properties (`UseRollingRefreshTokens`, `*EndpointPath` in the `OpenIdServerSettings` step, `ClientType` or `AllowedScopes` in `OpenIdApplication`) must use `DisableRollingRefreshTokens`, `Enable*Endpoint`, `Type`, and `ScopeEntries`.
- API `401`/`403` challenges now carry a Problem Details body; the status codes and `WWW-Authenticate` header are unchanged. The Token Validation feature no longer registers its own OpenIddict challenge handler or calls `AddProblemDetails()` (the host registers Problem Details), so customize the body through `IProblemDetailsService`.
- With Rate Limits enabled, password-grant requests to `POST /connect/token` are limited per IP and rejected with an OAuth 2.0 `slow_down` error and `Retry-After`.
- Saved client secrets and server certificates keep working. `OpenIdClientSettings` gains `ClientSecretSecretName`, and the optional `OrchardCore.OpenId.Secrets` feature, part of the `OrchardCore.OpenId` module (no separate package), loads server keys from secrets.

See [openid-examples.md](references/openid-examples.md) for complete setups.

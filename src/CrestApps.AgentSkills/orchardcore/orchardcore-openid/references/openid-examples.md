# OpenID Connect Examples

## Example 1: Authorization Server with a Confidential Web Client

### Recipe

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
      "EnableTokenEndpoint": true,
      "EnableAuthorizationEndpoint": true,
      "EnableLogoutEndpoint": true,
      "EnableUserInfoEndpoint": true,
      "EnablePushedAuthorizationEndpoint": true,
      "AllowAuthorizationCodeFlow": true,
      "AllowClientCredentialsFlow": true,
      "AllowRefreshTokenFlow": true,
      "RequireProofKeyForCodeExchange": true,
      "RequireEndSessionConfirmation": true
    },
    {
      "name": "OpenIdScope",
      "ScopeName": "portal_api",
      "DisplayName": "Portal API",
      "Description": "Grants access to the customer portal API.",
      "Resources": "portal-resource-server"
    },
    {
      "name": "OpenIdApplication",
      "ClientId": "customer-portal",
      "DisplayName": "Customer Portal",
      "Type": "confidential",
      "ClientSecret": "{{PortalSecret}}",
      "ConsentType": "implicit",
      "AllowAuthorizationCodeFlow": true,
      "AllowRefreshTokenFlow": true,
      "AllowLogoutEndpoint": true,
      "RequireProofKeyForCodeExchange": true,
      "RedirectUris": "https://portal.example.com/signin-oidc",
      "PostLogoutRedirectUris": "https://portal.example.com/signout-callback-oidc",
      "ScopeEntries": [
        { "Name": "profile" },
        { "Name": "email" },
        { "Name": "roles" },
        { "Name": "portal_api" }
      ]
    },
    {
      "name": "OpenIdValidationSettings",
      "Tenant": "Default"
    }
  ]
}
```

### Same Setup in a Migration

```csharp
using OpenIddict.Abstractions;
using OrchardCore.Data.Migration;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.OpenId.Services;

namespace MyModule.Migrations;

public sealed class PortalOpenIdMigrations : DataMigration
{
    private readonly IOpenIdServerService _serverService;
    private readonly IOpenIdApplicationManager _applicationManager;
    private readonly IOpenIdScopeManager _scopeManager;

    public PortalOpenIdMigrations(
        IOpenIdServerService serverService,
        IOpenIdApplicationManager applicationManager,
        IOpenIdScopeManager scopeManager)
    {
        _serverService = serverService;
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
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
        await _serverService.UpdateSettingsAsync(settings);

        await _scopeManager.CreateAsync(new OpenIdScopeDescriptor
        {
            Name = "portal_api",
            DisplayName = "Portal API",
            Description = "Grants access to the customer portal API.",
            Resources = { "portal-resource-server" },
        });

        await _applicationManager.CreateAsync(new OpenIdApplicationDescriptor
        {
            ClientId = "customer-portal",
            DisplayName = "Customer Portal",
            ClientSecret = "portal-secret-value",
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
                OpenIddictConstants.Permissions.Prefixes.Scope + "portal_api",
            },
            Requirements =
            {
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
            },
            RedirectUris = { new Uri("https://portal.example.com/signin-oidc") },
            PostLogoutRedirectUris = { new Uri("https://portal.example.com/signout-callback-oidc") },
        });

        return 1;
    }
}
```

## Example 2: Client Credentials for a Background Service

### Recipe

```json
{
  "steps": [
    {
      "name": "OpenIdApplication",
      "ClientId": "background-worker",
      "DisplayName": "Background Worker Service",
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

The client's `RoleEntries` become the roles of the token, so the `Editor` permissions apply to its API calls.

### Requesting and Using a Token

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MyWorker.Services;

public sealed class OrchardApiClient
{
    private readonly HttpClient _httpClient;

    public OrchardApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<JsonElement> GetContentItemAsync(string contentItemId, CancellationToken cancellationToken)
    {
        using var tokenResponse = await _httpClient.PostAsync(
            "https://cms.example.com/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = "background-worker",
                ["client_secret"] = "worker-service-secret",
                ["scope"] = "api",
            }),
            cancellationToken);

        tokenResponse.EnsureSuccessStatusCode();

        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://cms.example.com/api/content/{contentItemId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
```

Cache the token until it expires. With Rate Limits enabled, `/connect/token` allows 10 requests per minute per IP, and a `429` means the token is being requested too often.

## Example 3: Public SPA with PKCE and Silent Renew

```json
{
  "steps": [
    {
      "name": "OpenIdApplication",
      "ClientId": "react-dashboard",
      "DisplayName": "React Dashboard",
      "Type": "public",
      "ConsentType": "explicit",
      "AllowAuthorizationCodeFlow": true,
      "AllowRefreshTokenFlow": true,
      "AllowLogoutEndpoint": true,
      "RequireProofKeyForCodeExchange": true,
      "RedirectUris": "https://dashboard.example.com/callback https://dashboard.example.com/silent-renew",
      "PostLogoutRedirectUris": "https://dashboard.example.com/logout",
      "ScopeEntries": [
        { "Name": "profile" },
        { "Name": "email" },
        { "Name": "roles" },
        { "Name": "api" }
      ]
    }
  ]
}
```

Add a CORS policy for `https://dashboard.example.com` (see `orchardcore-cors`) when the SPA calls the API from another origin.

## Example 4: Logout Without a Confirmation Prompt

Turn off the prompt on the server, then send `id_token_hint` when signing out:

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

```text
GET https://cms.example.com/connect/logout?id_token_hint={id_token}&post_logout_redirect_uri=https%3A%2F%2Fdashboard.example.com%2Flogout
```

If the hint is missing, invalid, or was issued for another user, the confirmation prompt is still shown.

## Example 5: Resource Server on a Separate Tenant or App

Tenant `Api` validates tokens issued by tenant `Auth` in the same application:

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [ "OrchardCore.OpenId.Validation" ]
    },
    {
      "name": "OpenIdValidationSettings",
      "Tenant": "Auth"
    }
  ]
}
```

A separate application validates against a remote authority:

```json
{
  "steps": [
    {
      "name": "OpenIdValidationSettings",
      "Authority": "https://auth.example.com",
      "Audience": "portal-resource-server"
    }
  ]
}
```

For remote validation of JWTs, the server must issue `JsonWebToken` access tokens (and `DisableAccessTokenEncryption` when a third-party resource server reads them).

## Example 6: Adding Userinfo Claims

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

The standard `profile`, `email`, `phone`, and `roles` claims are populated before the provider runs.

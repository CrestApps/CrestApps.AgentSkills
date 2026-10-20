# Security Examples

## Example 1: Hardened Tenant Recipe

Enables security headers, HTTPS, CORS for a single front end, and rate limiting.

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.Security",
        "OrchardCore.Https",
        "OrchardCore.Cors",
        "OrchardCore.RateLimits"
      ]
    },
    {
      "name": "settings",
      "SecuritySettings": {
        "ContentTypeOptions": "nosniff",
        "ReferrerPolicy": "strict-origin-when-cross-origin",
        "ContentSecurityPolicy": {
          "default-src": "'self'",
          "frame-ancestors": "'self'"
        },
        "PermissionsPolicy": {
          "camera": "()",
          "microphone": "()"
        }
      },
      "HttpsSettings": {
        "RequireHttps": true,
        "RequireHttpsPermanent": true,
        "StrictTransportSecurityMode": "FromConfiguration"
      },
      "CorsSettings": {
        "Policies": [
          {
            "Name": "Frontend",
            "AllowAnyOrigin": false,
            "AllowedOrigins": [ "https://app.example.com" ],
            "AllowAnyMethod": false,
            "AllowedMethods": [ "GET", "POST" ],
            "AllowAnyHeader": false,
            "AllowedHeaders": [ "Content-Type", "Authorization" ],
            "AllowCredentials": false,
            "IsDefaultPolicy": true,
            "ExposedHeaders": []
          }
        ]
      }
    }
  ]
}
```

## Example 2: Public SPA and Service Clients

The `OpenIdApplication` step registers one application per step. See `orchardcore-openid` for the server settings.

```json
{
  "steps": [
    {
      "name": "OpenIdApplication",
      "ClientId": "my-spa-client",
      "DisplayName": "My SPA Application",
      "Type": "public",
      "ConsentType": "implicit",
      "AllowAuthorizationCodeFlow": true,
      "AllowRefreshTokenFlow": true,
      "AllowLogoutEndpoint": true,
      "RequireProofKeyForCodeExchange": true,
      "RedirectUris": "https://myapp.example.com/callback",
      "PostLogoutRedirectUris": "https://myapp.example.com/signout-callback",
      "ScopeEntries": [
        { "Name": "profile" },
        { "Name": "email" },
        { "Name": "roles" }
      ]
    },
    {
      "name": "OpenIdApplication",
      "ClientId": "my-api-service",
      "DisplayName": "My API Service",
      "Type": "confidential",
      "ClientSecret": "{{SecureSecret}}",
      "AllowClientCredentialsFlow": true,
      "RoleEntries": [
        { "Name": "Editor" }
      ]
    }
  ]
}
```

## Example 3: Minimal API with Problem Details

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapGet("api/invoices/{id}", GetInvoiceAsync)
            .AllowAnonymous()
            .DisableAntiforgery();
    }

    [Authorize(AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api)]
    private static async Task<IResult> GetInvoiceAsync(
        string id,
        IAuthorizationService authorizationService,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicePermissions.ViewInvoices))
        {
            return httpContext.ChallengeOrForbid(OrchardCoreConstants.AuthenticationSchemes.Api);
        }

        return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Invoice not found");
    }
}
```

An unauthenticated call returns `401` with a `WWW-Authenticate` header and an `application/problem+json` body:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Authentication required",
  "status": 401,
  "detail": "Authentication is required to access this resource. Additional details may be found in the WWW-Authenticate HTTP response header."
}
```

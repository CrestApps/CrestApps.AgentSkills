---
name: orchardcore-openapi
description: Skill for the Orchard Core 4.0 OpenAPI module. Covers OpenAPI and Swagger schema generation, the Swagger UI, ReDoc and Scalar documentation sub-features, silent authorization-code plus PKCE sign-in for Try it out requests, the OpenApiPkce and OpenApiGeneration recipes, the AllowAnonymousSchemaAccess setting, the ViewOpenApiContent and ManageOpenApi permissions, NSwag client generation, and documenting custom API endpoints. Use this skill when requests mention Orchard Core OpenAPI, Swagger, swagger.json, ReDoc, Scalar, NSwag, API documentation, API client generation, OpenApiSettings, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.OpenApi, OrchardCore.OpenApi.SwaggerUI, OrchardCore.OpenApi.ReDocUI, OrchardCore.OpenApi.ScalarUI, OpenApiPermissions, OpenApiSettings, ProducesResponseType, ApiExecuteQueryGet, and ApiExecuteQueryPost.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core OpenAPI - Prompt Templates

## Document and Explore Orchard Core APIs

You are an Orchard Core expert. Generate configuration, recipes, and code for the `OrchardCore.OpenApi` module, which ships with Orchard Core 4.0.

### Guidelines

- `OrchardCore.OpenApi` generates the OpenAPI document (Swashbuckle at `/swagger/v1/swagger.json`, plus the ASP.NET Core `/openapi/*.json` document) for every enabled API endpoint.
- The documentation UIs are separate sub-features that each depend on `OrchardCore.OpenApi`. A disabled UI returns `404`.
- Every documentation URL (`/swagger`, `/redoc`, `/scalar`, `/openapi`) requires an authenticated user with `ViewOpenApiContent`. Anonymous users are redirected to the admin login; authenticated users without the permission get `403`.
- The JSON schema endpoints also require `ViewOpenApiContent` (cookie or `Api` bearer token) unless **Allow anonymous access to the API schema** (`AllowAnonymousSchemaAccess`) is enabled. Unauthenticated schema requests get a `401` with a `WWW-Authenticate` header and a Problem Details body.
- `ManageOpenApi` implies `ViewOpenApiContent`. Grant only `ViewOpenApiContent` to roles that should read the docs without changing settings. Both are granted to Administrator by default.
- Orchard Core APIs authenticate with the `Api` scheme, which accepts bearer tokens only. Swagger UI and Scalar acquire a token silently (authorization code + PKCE, `prompt=none` in a hidden iframe) from the tenant's own OpenID server, using the admin cookie session. ReDoc is read-only and needs no token.
- Run the `OpenApiPkce` recipe once per tenant to provision that silent sign-in. Client credentials and password grants are intentionally not supported by the browser UIs.
- Tokens stay in memory, are attached only to same-origin requests whose path contains `/api/`, and a rejected token triggers one silent re-sign-in and retry.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Installing the Package

The module is part of the `OrchardCore.Application.Cms.Targets` bundle. For a custom host, reference it in the web (startup) project:

```xml
<PackageReference Include="OrchardCore.OpenApi" Version="4.*" />
```

### Features

| Feature | ID | URL |
|---|---|---|
| OpenApi | `OrchardCore.OpenApi` | `/swagger/v1/swagger.json`, `/openapi/v1.json` |
| OpenApi Swagger UI | `OrchardCore.OpenApi.SwaggerUI` | `/swagger` |
| OpenApi ReDoc UI | `OrchardCore.OpenApi.ReDocUI` | `/redoc` |
| OpenApi Scalar UI | `OrchardCore.OpenApi.ScalarUI` | `/scalar/v1` |

All URLs are relative to the tenant, so a tenant with the `customer-a` prefix serves `/customer-a/swagger`.

### Enabling the Documentation UIs

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.OpenApi",
        "OrchardCore.OpenApi.SwaggerUI",
        "OrchardCore.OpenApi.ScalarUI"
      ]
    }
  ]
}
```

### Settings

**Configuration → Settings → OpenApi** shows which UIs are enabled and exposes one setting, `OpenApiSettings.AllowAnonymousSchemaAccess` (default `false`):

```json
{
  "steps": [
    {
      "name": "settings",
      "OpenApiSettings": {
        "AllowAnonymousSchemaAccess": true
      }
    }
  ]
}
```

Enable anonymous schema access only on tenants used for client generation. The schema lists every API route and its parameters.

### Provisioning Silent Sign-In with the OpenApiPkce Recipe

Run **OpenAPI Documentation — Bearer/PKCE** (`OpenApiPkce`) from **Configuration → Recipes**. It enables `OrchardCore.OpenApi`, `OrchardCore.OpenId.Server`, `OrchardCore.OpenId.Validation`, and `OrchardCore.OpenId.Management`, turns on the authorization code flow with PKCE required, configures local token validation, and registers a public `openapi` client. The equivalent custom recipe looks like this:

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.OpenApi",
        "OrchardCore.OpenId.Management",
        "OrchardCore.OpenId.Server",
        "OrchardCore.OpenId.Validation"
      ]
    },
    {
      "name": "OpenIdServerSettings",
      "EnableTokenEndpoint": true,
      "EnableAuthorizationEndpoint": true,
      "EnableLogoutEndpoint": true,
      "EnableUserInfoEndpoint": true,
      "AllowAuthorizationCodeFlow": true,
      "RequireProofKeyForCodeExchange": true
    },
    {
      "name": "OpenIdValidationSettings",
      "Tenant": "Default"
    },
    {
      "name": "OpenIdApplication",
      "ClientId": "openapi",
      "DisplayName": "OpenAPI Interactive UI",
      "Type": "public",
      "ConsentType": "implicit",
      "AllowAuthorizationCodeFlow": true,
      "AllowLogoutEndpoint": true,
      "RequireProofKeyForCodeExchange": true,
      "RedirectUris": "https://cms.example.com/OrchardCore.OpenApi/openapi-oidc-silent.html",
      "PostLogoutRedirectUris": "https://cms.example.com/",
      "ScopeEntries": [
        { "Name": "email" },
        { "Name": "profile" },
        { "Name": "roles" }
      ]
    }
  ]
}
```

Adjust before running:

- The client ID must be `openapi`; the UIs request `openid email profile roles`.
- `RedirectUris` must be the tenant origin plus any tenant prefix plus `/OrchardCore.OpenApi/openapi-oidc-silent.html`, exactly.
- `OpenIdValidationSettings.Tenant` must name the tenant running the recipe. Leaving both `Tenant` and `Authority` empty makes validation invalid and every bearer call returns `401`.
- The `OpenIdServerSettings` step replaces the server settings; every flow or endpoint left out is turned off. Merge it with the flows the tenant already uses.
- Users who press **Try it out** need roles that grant the API permissions; the `roles` scope carries them into the token.
- With a non-implicit consent type, the UI falls back once to a full-page authorization redirect to record consent.

### Generating Clients with NSwag

The `OpenApiGeneration` recipe enables the API features (Contents, Media, Media TUS, Queries, Tenants, Lucene, Elasticsearch, OpenApi) and turns on `AllowAnonymousSchemaAccess`. Its `settings` step replaces the stored OpenApi settings, so run it on a throwaway generation tenant.

```powershell
dotnet tool install -g NSwag.ConsoleCore
# Against a running site with anonymous schema access enabled
nswag openapi2csclient /input:https://localhost:5001/swagger/v1/swagger.json /output:OrchardClient.cs /namespace:MyApp.OrchardClient
```

In the Orchard Core repository, `dotnet run --project tools/OpenApiClientGenerator -c Release` boots the CMS in-process with the `OpenApiGenerationSetup` recipe and regenerates the bundled clients headlessly. Operations are sorted by route and verb, so unchanged APIs regenerate byte-identical clients.

Without anonymous access, fetch the schema with an `Authorization: Bearer <token>` header for a user that has `ViewOpenApiContent`.

### Documenting a Custom API Endpoint

Endpoints appear in the document when they are API controllers or minimal APIs with an HTTP method. Secure them with the `Api` scheme and describe every response:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore;

namespace MyModule.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api)]
public sealed class InventoryController : ControllerBase
{
    /// <summary>
    /// Gets the stock level of a product.
    /// </summary>
    [HttpGet("{sku}", Name = "ApiGetInventory")]
    [ProducesResponseType(typeof(InventoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Get(string sku)
    {
        return Ok(new InventoryDto { Sku = sku, Quantity = 10 });
    }
}

public sealed class InventoryDto
{
    public string Sku { get; set; }

    public int Quantity { get; set; }
}
```

- Give each operation a unique route name or operation ID. OpenAPI forbids reusing one `operationId` across verbs.
- Return Problem Details for errors (`this.ApiNotFoundProblem()`, `this.ApiBadRequestProblem()`, `this.ApiForbidProblem()` on controllers, or `TypedResults.Problem()` in minimal APIs) so generated clients can parse failures.
- Restart the application or reload the tenant after adding endpoints.

### Upgrading from 3.x

- `OrchardCore.OpenApi` is new in 4.0. Remove community Swagger modules that also call `AddSwaggerGen` for the same tenant.
- `api/queries/{name}` was split into two operations, `ApiExecuteQueryGet` and `ApiExecuteQueryPost` (same route, verbs, and parameters). NSwag-generated method names change on the next regeneration.

### Troubleshooting

| Symptom | Check |
|---|---|
| `401` on Try it out | Run `OpenApiPkce`, confirm `OrchardCore.OpenId.Validation` is enabled and `OpenIdValidationSettings.Tenant` is set. |
| Silent sign-in fails | The redirect URI must match origin, tenant prefix, and `/OrchardCore.OpenApi/openapi-oidc-silent.html`; check the browser console for `[openapi-ui]` errors. |
| Token acquired but `401`/`403` from the API | The signed-in user's roles lack the endpoint permission. |
| NSwag gets `401` for `swagger.json` | Enable `AllowAnonymousSchemaAccess` on a generation tenant or send a bearer token. |
| `/swagger` returns `404` | Enable `OrchardCore.OpenApi.SwaggerUI`. |
| Endpoint missing from the document | Its feature is disabled, it lacks an HTTP method attribute, or the app was not restarted. |

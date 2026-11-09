---
name: orchardcore-rate-limits
description: Skill for the Orchard Core 4.0 Rate Limits module. Covers tenant rate-limit policies with Global, Endpoint and Group targets, fixed window, sliding window, concurrency and token bucket limiters, the seeded default global policy, the CreateOrUpdateRateLimitPolicies recipe step, deployment, built-in route and group limits registered through RateLimitsOptions, RateLimitPartitionHelpers with per-user or per-IP keys, route rejection handlers, global policy path exclusions, RateLimitGroupAttribute and WithRateLimitGroups, and the limits Users and OpenID apply to sign-in and token endpoints. Use this skill when requests mention Orchard Core rate limiting, throttling, 429 Too Many Requests, brute-force protection, login or API rate limits, users sharing an IP address, or related troubleshooting. Strong matches include OrchardCore.RateLimits, RateLimitsOptions, AddRouteRateLimit, AddGroupRateLimit, RateLimitPartitionHelpers, RateLimitGroupAttribute, ManageRateLimits, and UserRateLimiterPolicyNames.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Rate Limits - Prompt Templates

## Throttle Tenant Requests

You are an Orchard Core expert. Generate rate-limit policies, recipes, and module code for the `OrchardCore.RateLimits` module, which ships with Orchard Core 4.0.

### Guidelines

- Enable `OrchardCore.RateLimits` (category Security) per tenant. It registers ASP.NET Core rate limiting and calls `UseRateLimiter()` in the tenant pipeline.
- Rejected requests return `429 Too Many Requests`. With `OrchardCore.Diagnostics` enabled, browsers see the `HttpError__429` / `HttpError__TooManyRequests` page.
- Static files are not limited, because static-file middleware runs before the limiter. Media files are served after it, so the Media module excludes its request path (`/media` by default) from **Global** policies; an `Endpoint` policy can still target it.
- Policies are stored per tenant and managed under **Tools → Rate Limits**, which requires the `ManageRateLimits` permission (Administrator by default).
- Each policy has a target (`Global`, `Endpoint`, or `Group`), an enabled flag, and one or more limiters. Every limiter of every matching enabled policy is applied.
- A disabled policy can be edited freely without reloading the tenant. An enabled policy only allows name and description edits. Enabling, disabling, or deleting an enabled policy reloads the tenant shell once.
- The first time the feature runs, it seeds an enabled **Default Global Policy** with a fixed window of 1000 requests per 60 seconds per client and a queue limit of 0. Tenants seeded by an earlier 4.0 preview keep their stored policy (150 per IP); raise it if users share an IP address.
- Features register read-only built-in limits in code through `RateLimitsOptions`. They are always active and listed as read-only in the admin UI.
- Tenant policies count requests per client: each signed-in user has their own budget, and anonymous visitors share the budget of their remote IP address (`RateLimitPartitionHelpers.GetClientPartitionKey`). Users of a shared network (office gateway, carrier NAT, VPN) therefore only share a budget while anonymous or signing in.
- Behind a proxy, load balancer or CDN, configure `orchardcore-reverse-proxy` (or the forwarded headers middleware) and trust only your own proxies, otherwise every anonymous client shares the proxy's budget.
- A rejected request gets an empty `429`. A feature whose clients expect another error format registers `RateLimitsOptions.AddRouteRejectionHandler(routeName, handler)`.
- Rate limiting complements, and does not replace, authentication, lockout, captcha, or monitoring.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Rate Limits

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.RateLimits"
      ]
    }
  ]
}
```

### Policy Targets

| Target | Matches | Required property |
|---|---|---|
| `Global` | Every dynamic tenant request | none |
| `Endpoint` | Requests whose path starts with `Path` (for example `/api` matches `/api/orders/42`) | `Path`, must start with `/` |
| `Group` | Endpoints tagged with a rate-limit group | `GroupName` |

### Limiter Sources

| Source | Data property | Settings | Use for |
|---|---|---|---|
| `FixedWindow` | `FixedWindowRateLimiterData` | `PermitLimit`, `WindowSeconds`, `QueueLimit` | Simple "X requests every Y seconds" caps |
| `SlidingWindow` | `SlidingWindowRateLimiterData` | `PermitLimit`, `WindowSeconds`, `SegmentsPerWindow`, `QueueLimit` | Smoother caps without window-boundary bursts |
| `Concurrency` | `ConcurrencyRateLimiterData` | `PermitLimit`, `QueueLimit`, `QueueProcessingOrder` | Limiting expensive in-flight work |
| `TokenBucket` | `TokenBucketRateLimiterData` | `TokenLimit`, `TokensPerPeriod`, `ReplenishmentPeriodSeconds`, `QueueLimit`, `QueueProcessingOrder` | Burst-tolerant APIs with a steady refill |

Use a `QueueLimit` of `0` to reject excess requests immediately instead of queuing them.

### Recipe Step

`CreateOrUpdateRateLimitPolicies` imports a `policies` array. Existing policies are matched by `PolicyId` or by `Name` (case-insensitive) and updated; others are created.

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [ "OrchardCore.RateLimits" ]
    },
    {
      "name": "CreateOrUpdateRateLimitPolicies",
      "policies": [
        {
          "Name": "Protect login",
          "Description": "Slows down credential stuffing.",
          "IsEnabled": true,
          "Scope": "Endpoint",
          "Path": "/Login",
          "Limiters": [
            {
              "Id": "[js: uuid()]",
              "Source": "SlidingWindow",
              "Properties": {
                "SlidingWindowRateLimiterData": {
                  "PermitLimit": 10,
                  "WindowSeconds": 60,
                  "SegmentsPerWindow": 6,
                  "QueueLimit": 0
                }
              }
            }
          ]
        },
        {
          "Name": "Public API",
          "IsEnabled": true,
          "Scope": "Endpoint",
          "Path": "/api",
          "Limiters": [
            {
              "Id": "[js: uuid()]",
              "Source": "TokenBucket",
              "Properties": {
                "TokenBucketRateLimiterData": {
                  "TokenLimit": 20,
                  "TokensPerPeriod": 5,
                  "ReplenishmentPeriodSeconds": 10,
                  "QueueLimit": 0
                }
              }
            }
          ]
        },
        {
          "Name": "Authentication group",
          "IsEnabled": false,
          "Scope": "Group",
          "GroupName": "authentication",
          "Limiters": [
            {
              "Id": "[js: uuid()]",
              "Source": "FixedWindow",
              "Properties": {
                "FixedWindowRateLimiterData": {
                  "PermitLimit": 20,
                  "WindowSeconds": 300,
                  "QueueLimit": 0
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

Add **All Rate Limit Policies** to a deployment plan to export every stored policy. Setup-style exports stamp `OwnerId` and `Author` with the tenant admin parameters.

### Tagging Endpoints with a Rate-Limit Group

Group policies target endpoint metadata instead of paths. The metadata is harmless when no enabled policy targets the group, so add it freely.

```csharp
using Microsoft.AspNetCore.Mvc;
using OrchardCore.RateLimits;

namespace MyModule.Controllers;

public sealed class PartnerController : Controller
{
    [HttpPost]
    [RateLimitGroup("authentication", "public-api")]
    public IActionResult Exchange()
    {
        return Ok();
    }
}
```

```csharp
routes.MapPost("/api/partner/token", HandleTokenAsync)
    .WithName("Partner.Token")
    .WithRateLimitGroups("authentication", "public-api");
```

Use `WithRateLimitGroup("name")` for a single group. The attribute and extensions live in `OrchardCore.RateLimits.Core`.

### Registering Built-In Limits from a Module

Register limits only when the Rate Limits feature is enabled. Route limits match the endpoint route name and, optionally, the HTTP method, so `GET` and `POST` actions sharing a route name do not share a budget.

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace MyModule;

[RequireFeatures("OrchardCore.RateLimits")]
public sealed class RateLimitsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options =>
        {
            options.AddRouteRateLimit(
                routeName: "Partner.Token",
                httpMethod: HttpMethods.Post,
                partitioner: RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(
                    policyName: "partner-token",
                    permitLimit: 10));

            options.AddRouteRateLimit(
                routeName: "PartnerSignup",
                httpMethod: HttpMethods.Post,
                partitioner: RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(
                    policyName: "partner-signup",
                    permitLimit: 3,
                    window: TimeSpan.FromMinutes(1)));

            options.AddGroupRateLimit(
                groupName: "public-api",
                partitioner: RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(
                    policyName: "public-api",
                    permitLimit: 60));
        });
    }
}
```

- `CreateSlidingWindowPerIpPolicy` uses a 1-minute window with 6 segments; `CreateFixedWindowPerIpPolicy` takes an explicit window. Both use a queue limit of `0` and partition by `{policyName}:{remote IP}`.
- `CreateSlidingWindowPolicy(policyName, permitLimit, partitionKeySelector)` and `CreateFixedWindowPolicy(policyName, permitLimit, window, partitionKeySelector)` partition by the key you return. Prefer `RateLimitPartitionHelpers.GetClientPartitionKey` (signed-in user, otherwise IP) whenever the endpoint knows the user; `GetUserPartitionKey(userId)` returns the same key for a given user id, and `GetRemoteIpAddress` the IP. Keep per-IP limits for anonymous endpoints and set them high enough for shared networks.
- Any `Func<HttpContext, RateLimitPartition<string>>` works as a partitioner, for example `RateLimitPartition.GetTokenBucketLimiter(...)` keyed by user ID.
- A feature that serves files after the limiter excludes their path from global policies with `options.ExcludePathFromGlobalPolicies("/my-files")`.
- Write the response of rejected requests to a route in the format its clients expect (the status is already `429`; the handler can change it):

```csharp
services.Configure<RateLimitsOptions>(options =>
{
    options.AddRouteRateLimit(
        routeName: "ExportReport",
        httpMethod: HttpMethods.Post,
        partitioner: RateLimitPartitionHelpers.CreateFixedWindowPolicy(
            policyName: "report-export",
            permitLimit: 5,
            window: TimeSpan.FromMinutes(1),
            partitionKeySelector: RateLimitPartitionHelpers.GetClientPartitionKey));

    options.AddRouteRejectionHandler("ExportReport", async (context, cancellationToken) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "too_many_requests" }, cancellationToken);
    });
});
```

- Reference `OrchardCore.RateLimits.Core` from the module project. Give the protected route a stable name (`MapAreaControllerRoute(name: ...)`, `[HttpPost(Name = ...)]`, or `WithName(...)`).

### Built-In Limits Shipped by Orchard Core

| Feature | Endpoint (POST) | Limit | Counted against |
|---|---|---|---|
| Users | Login | 100 per minute, sliding (`password-authentication`) | IP, shared with external login linking and the OpenID password grant |
| Users | Resend confirmation email | 2 per minute, fixed (`email-confirmation`) | User who passed the password check |
| Users: External Authentication | `LinkExternalLogin` | 100 per minute, sliding (`password-authentication`) | IP, shared with login |
| Users: External Authentication | `RegisterExternalLogin` | 20 per minute, sliding (`user-registration`) | IP, shared with registration |
| Users: User Registration | Register | 20 per minute, sliding (`user-registration`) | IP |
| Users: Reset Password | Forgot password, reset password | 20 per minute, sliding (`password-recovery`) | IP, shared by both |
| Users: Two-Factor Authentication | Two-factor code / recovery code | 5 / 3 per minute, sliding (`two-factor-authentication` / `two-factor-recovery`) | User who passed the password check |
| Users: Email and SMS Authenticators | Send code | 2 per minute, fixed (`two-factor-code-send`) | User who passed the password check, shared by both |
| OpenID Authorization Server | `Access.Token` (`/connect/token`), **password grant only** | 100 per minute, sliding (`password-authentication`) | IP, shared with login. Rejections get an OAuth 2.0 `slow_down` error with `Retry-After` |

Policies that share a policy name, such as `password-authentication`, share the same budget. The names are in `OrchardCore.Users.UserRateLimiterPolicyNames`. Anonymous sign-in limits are generous on purpose: account lockout and the one-email-per-minute limits on password reset and confirmation emails protect each account. The other OpenID grants (codes, refresh tokens, client credentials) are not limited, since a single server often requests tokens for many users.

GraphQL (`/api/graphql`) and the health check URL are mapped as endpoints, so an `Endpoint` policy on their path limits them.

### Troubleshooting

| Symptom | Check |
|---|---|
| Every client is throttled together | The proxy IP is used as the partition key; configure forwarded headers and known proxies. |
| Users of an office are throttled while signed out | Anonymous visitors share their IP budget; raise the global or endpoint limit. |
| Policy edits are rejected | Enabled policies only accept name and description changes; disable the policy first. |
| Endpoint policy never matches | `Path` must start with `/` and is a prefix of the tenant-relative request path. |
| Group policy never matches | The endpoint lacks `[RateLimitGroup]` or `WithRateLimitGroups()` metadata with the same name. |
| Recipe import fails | Endpoint policies need `Path`, group policies need `GroupName`, and the step needs a `policies` array. |

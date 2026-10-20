---
name: orchardcore-rate-limits
description: Skill for the Orchard Core 4.0 Rate Limits module. Covers tenant rate-limit policies with Global, Endpoint and Group targets, fixed window, sliding window, concurrency and token bucket limiters, the seeded default global policy, the CreateOrUpdateRateLimitPolicies recipe step, deployment of all policies, built-in route and group limits registered in code through RateLimitsOptions, RateLimitPartitionHelpers, RateLimitGroupAttribute and WithRateLimitGroups, and the limits that Users and OpenID apply to sign-in and token endpoints. Use this skill when requests mention Orchard Core rate limiting, throttling, 429 Too Many Requests, brute-force protection, login rate limits, API rate limits, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.RateLimits, RateLimitsOptions, AddRouteRateLimit, AddGroupRateLimit, RateLimitPartitionHelpers, RateLimitGroupAttribute, ManageRateLimits, and UserRateLimiterPolicyNames.
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
- Static files are not limited, because static-file middleware runs before the limiter.
- Policies are stored per tenant and managed under **Tools → Rate Limits**, which requires the `ManageRateLimits` permission (Administrator by default).
- Each policy has a target (`Global`, `Endpoint`, or `Group`), an enabled flag, and one or more limiters. Every limiter of every matching enabled policy is applied.
- A disabled policy can be edited freely without reloading the tenant. An enabled policy only allows name and description edits. Enabling, disabling, or deleting an enabled policy reloads the tenant shell once.
- The first time the feature runs, it seeds an enabled **Default Global Policy** with a fixed window of 150 requests per 60 seconds and a queue limit of 0.
- Features register read-only built-in limits in code through `RateLimitsOptions`. They are always active and listed as read-only in the admin UI.
- Built-in and tenant limiters partition by `HttpContext.Connection.RemoteIpAddress`. Behind a proxy, configure `orchardcore-reverse-proxy` so the client IP is used, otherwise all clients share one budget.
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
- Any `Func<HttpContext, RateLimitPartition<string>>` works as a partitioner, for example `RateLimitPartition.GetTokenBucketLimiter(...)` keyed by user ID.
- Reference `OrchardCore.RateLimits.Core` from the module project. Give the protected route a stable name (`MapAreaControllerRoute(name: ...)`, `[HttpPost(Name = ...)]`, or `WithName(...)`).

### Built-In Limits Shipped by Orchard Core

| Feature | Route name | Limit per IP |
|---|---|---|
| Users | `Login` (POST) | 10 per minute, sliding (`password-authentication`) |
| Users | `ResendEmailConfirmation` (POST) | 2 per minute, fixed (`email-confirmation`) |
| Users external authentication | `RegisterExternalLogin` (POST) | 3 per minute, sliding (`user-registration`) |
| Users external authentication | `LinkExternalLogin` (POST) | 10 per minute, sliding (`password-authentication`) |
| OpenID Authorization Server | `Access.Token` (POST `/connect/token`) | 10 per minute, sliding (`password-authentication`) |

Policies that share a policy name, such as `password-authentication`, share the same per-IP budget. The names are in `OrchardCore.Users.UserRateLimiterPolicyNames`.

GraphQL (`/api/graphql`) and the health check URL are mapped as endpoints, so an `Endpoint` policy on their path limits them.

### Troubleshooting

| Symptom | Check |
|---|---|
| Every client is throttled together | The proxy IP is used as the partition key; configure forwarded headers and known proxies. |
| Policy edits are rejected | Enabled policies only accept name and description changes; disable the policy first. |
| Endpoint policy never matches | `Path` must start with `/` and is a prefix of the tenant-relative request path. |
| Group policy never matches | The endpoint lacks `[RateLimitGroup]` or `WithRateLimitGroups()` metadata with the same name. |
| Recipe import fails | Endpoint policies need `Path`, group policies need `GroupName`, and the step needs a `policies` array. |

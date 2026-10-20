---
name: orchardcore-diagnostics
description: Skill for configuring Orchard Core diagnostics error handling. Covers custom error pages, status code pages, DiagnosticsStartupFilter, IConfigureOptions integration, error shapes, alternate shape names, the 429 Too Many Requests page, Problem Details for Api and SkipStatusCodePages responses, and production exception handling. Use this skill when requests mention Orchard Core Diagnostics, custom error page, status code pages, Error controller, HttpError shape, DiagnosticsStartupFilter, exception handler, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.Diagnostics, IStartupFilter, UseExceptionHandler, UseStatusCodePagesWithReExecute, IConfigureOptions, HttpErrorShapeViewModel, HttpError__404, HttpError__NotFound, HttpError__TooManyRequests, SkipStatusCodePages, and IProblemDetailsService. It also helps with theme overrides, static-file behavior, and the source-backed patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Diagnostics

`OrchardCore.Diagnostics` wires tenant-aware error handling into the Orchard
Core pipeline. Its startup filter uses `/Error` for unhandled exceptions outside
development and re-executes status code responses at `/Error/{status}`. The
module maps the `Error/{status?}` route to `DiagnosticsController.Error`.

## Guidelines

- Enable the exact `OrchardCore.Diagnostics` feature before relying on its error route and shapes.
- In non-development environments it calls `UseExceptionHandler("/Error")`.
- It calls `UseStatusCodePagesWithReExecute("/Error/{0}")` for status responses.
- Do not expose exception details in theme templates or production error pages.
- Static-file paths are excluded from status page rendering when their extension maps to a content type.
- Override error rendering through theme shapes rather than changing the module controller.
- Use `IConfigureOptions` for your own options registration, not to replace the diagnostic middleware order.
- Error templates must tolerate an absent status code because `/Error` also handles exceptions.
- Responses that opt out of the HTML pages are not re-executed: `401`/`403` challenges from the `Api` authentication scheme and errors from endpoints marked `[SkipStatusCodePages]` get an RFC 9457 Problem Details JSON body instead (Orchard Core 4.0).
- `429 Too Many Requests` from `orchardcore-rate-limits` renders through `HttpError__429` / `HttpError__TooManyRequests`.
- All C# classes in examples are sealed except View Models.

## Enable Diagnostics

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Diagnostics"
      ],
      "disable": []
    }
  ]
}
```

## Pipeline Behavior

The module registers `DiagnosticsStartupFilter` as an `IStartupFilter`. This
ensures the error handling runs in the correct tenant pipeline without an
application manually adding duplicate middleware.

| Situation | Result |
|---|---|
| Unhandled exception in production | Re-executes `/Error`. |
| Response with a 4xx or 5xx status | Re-executes `/Error/{status}`. |
| Development environment exception | The exception handler is not registered by this filter. |
| Missing static asset | Status page rendering is disabled for known file extensions. |
| `Api` scheme `401`/`403`, or endpoint with `[SkipStatusCodePages]` | No HTML page; an `application/problem+json` body is written through `IProblemDetailsService`. |

Avoid calling `UseExceptionHandler` or `UseStatusCodePagesWithReExecute` again
for the same tenant unless you deliberately replace this behavior and have
verified pipeline order.

## Override Error Shapes

The controller produces an `HttpError` shape. It supplies alternates in this
order:

1. `HttpError__{numericStatus}` such as `HttpError__404`
2. `HttpError__{HttpStatusCodeName}` such as `HttpError__NotFound`

Later alternates win, so a theme's `HttpError-NotFound` template takes precedence over `HttpError-404` when both exist. The module ships templates for 400, 401, 403, 404, and 429 (`HttpError-BadRequest`, `HttpError-Unauthorized`, `HttpError-Forbidden`, `HttpError-NotFound`, `HttpError-TooManyRequests`), and `HttpError` as the fallback for every other status.

Add a default fallback in the active theme:

```cshtml
@* Views/HttpError.cshtml *@
@model OrchardCore.Diagnostics.ViewModels.HttpErrorShapeViewModel

<main class="error-page">
    <h1>Something went wrong</h1>
    <p>Please return to the home page or try again later.</p>
</main>
```

Add a more specific 404 template:

```cshtml
@* Views/HttpError-404.cshtml *@
@model OrchardCore.Diagnostics.ViewModels.HttpErrorShapeViewModel

<main class="error-page">
    <h1>Page not found</h1>
    <p>The requested address is unavailable.</p>
</main>
```

Liquid works too, for example `Views/HttpError-TooManyRequests.liquid`:

```liquid
<h1>{{ "Too many requests. Please wait a moment and try again." | t }}</h1>
```

With the Templates feature, create a template named `HttpError__Forbidden` (or another alternate) in the admin to override the page without a theme change. Editing Templates requires `ManageTemplates` and, in 4.0, `ManageLiquidTemplates`.

The physical file naming convention is normalized by display management. Use
the documented `HttpError__404` alternate when reasoning about selection and
the conventional `HttpError-404.cshtml` theme file when creating Razor views.

## JSON Endpoints and Problem Details

Status code pages turn an empty error response into an HTML page, which breaks JSON clients that authenticate with cookies. Opt such endpoints out:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MyModule.Controllers;

[SkipStatusCodePages]
public sealed class WidgetDataController : Controller
{
    [HttpGet]
    public IActionResult Get()
    {
        return NotFound();
    }
}
```

The empty `404` then receives a Problem Details body. Responses that already have a body or content type are left untouched, and status codes and headers never change. Register `services.AddProblemDetails(options => ...)` to customize the payload. `AddOrchardCms()` registers this behavior through `AddApiProblemDetails()`; a host built with `AddOrchardCore()` must call `.AddApiProblemDetails()` itself.

## Add Application-Specific Error Options

Diagnostics itself has no public settings class. If a module needs a feature
flag or support link for its own error display, model it with options:

```csharp
using Microsoft.Extensions.Options;

namespace MyModule;

public sealed class ErrorDisplayOptions
{
    public string SupportUrl { get; set; }
}

public sealed class ErrorDisplayOptionsConfiguration
    : IConfigureOptions<ErrorDisplayOptions>
{
    public void Configure(ErrorDisplayOptions options)
    {
        options.SupportUrl = "/contact";
    }
}
```

Register the configuration in your module startup and consume it only from
your own shape or view model. Do not insert exception details into the options
or render them to public users.

## Test Error Pages

Test an unknown route, a forbidden route, and a controlled exception in an
environment matching production. Verify the intended `HttpError` alternate
renders and that requests for missing CSS, JavaScript, and images do not
receive HTML error documents.

## Keep Error Responses Safe

An error page is part of the public attack surface. Show a stable support
message, preserve correlation identifiers supplied by the host when available,
and log exception details only on the server. Do not display stack traces,
connection strings, authentication claims, or route values that could contain
personal data.

The diagnostics module deliberately separates exception handling from status
code rendering. A 404 is a normal request outcome and should normally have a
helpful, cache-aware themed response. An exception is an operational failure
and should lead users to a generic page while server telemetry retains the
diagnostic context.

## Route Considerations

The built-in area route is `Error/{status?}`. Avoid defining an application
endpoint that shadows `/Error` in a tenant using this module. If a site needs a
different public support URL, link to it from the `HttpError` shape rather than
changing the error handler path.

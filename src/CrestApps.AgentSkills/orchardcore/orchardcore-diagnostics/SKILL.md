---
name: orchardcore-diagnostics
description: Skill for configuring Orchard Core diagnostics error handling. Covers custom error pages, status code pages, DiagnosticsStartupFilter, IConfigureOptions integration, error shapes, alternate shape names, and production exception handling. Use this skill when requests mention Orchard Core Diagnostics, custom error page, status code pages, Error controller, HttpError shape, DiagnosticsStartupFilter, exception handler, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.Diagnostics, IStartupFilter, UseExceptionHandler, UseStatusCodePagesWithReExecute, IConfigureOptions, HttpErrorShapeViewModel, HttpError__404, and HttpError__NotFound. It also helps with theme overrides, static-file behavior, and the source-backed patterns captured in this skill.
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

Avoid calling `UseExceptionHandler` or `UseStatusCodePagesWithReExecute` again
for the same tenant unless you deliberately replace this behavior and have
verified pipeline order.

## Override Error Shapes

The controller produces an `HttpError` shape. It supplies alternates in this
order:

1. `HttpError__{numericStatus}` such as `HttpError__404`
2. `HttpError__{HttpStatusCodeName}` such as `HttpError__NotFound`

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

The physical file naming convention is normalized by display management. Use
the documented `HttpError__404` alternate when reasoning about selection and
the conventional `HttpError-404.cshtml` theme file when creating Razor views.

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

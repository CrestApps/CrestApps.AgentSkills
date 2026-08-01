# Orchard Core Deferred Tasks and After-Request Jobs - Examples

Source-backed patterns for `HttpBackgroundJob.ExecuteAfterEndOfRequestAsync`
(namespace `OrchardCore.BackgroundJobs`) and `ShellScope.AddDeferredTask`
(namespace `OrchardCore.Environment.Shell.Scope`). Both are in the
`OrchardCore.Abstractions` package.

## Content handler: run work only after the item is committed

```csharp
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;

public sealed class ProductPublishedHandler : ContentHandlerBase
{
    public override Task PublishedAsync(PublishContentContext context)
    {
        if (!context.ContentItem.ContentType.Equals("Product"))
        {
            return Task.CompletedTask;
        }

        var contentItemId = context.ContentItem.ContentItemId;

        // Defer so the outer session commits first, then fire-and-forget.
        ShellScope.AddDeferredTask(scope =>
            HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
                "sync-product-to-catalog",
                contentItemId,
                static async (jobScope, id) =>
                {
                    var catalog = jobScope.ServiceProvider
                        .GetRequiredService<IExternalCatalogClient>();

                    await catalog.SyncAsync(id);
                }));

        return Task.CompletedTask;
    }
}
```

## Controller action: send an email without blocking the response

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;

public sealed class ContactController : Controller
{
    [HttpPost]
    public async Task<IActionResult> Submit(ContactViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Persist the message using request services here...

        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
            "notify-support",
            model.Email,
            static async (scope, email) =>
            {
                var notifier = scope.ServiceProvider
                    .GetRequiredService<ISupportNotifier>();

                await notifier.NotifyNewContactAsync(email);
            });

        return RedirectToAction(nameof(ThankYou));
    }
}
```

`ContactViewModel` is a view model, so it is not sealed:

```csharp
public class ContactViewModel
{
    public string Email { get; set; }

    public string Message { get; set; }
}
```

## Deferred task only: react to committed data in a new scope

Use `AddDeferredTask` alone (no HTTP job) when the follow-up work is short and
can run during scope teardown. It executes in a fresh scope with a new session.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

ShellScope.AddDeferredTask(async scope =>
{
    var session = scope.ServiceProvider.GetRequiredService<YesSql.ISession>();

    // Reads committed data written earlier in the request.
    var pending = await session
        .Query<Order, OrderIndex>(x => x.Status == "Pending")
        .ListAsync();

    // ... process, then this scope commits its own changes on dispose.
});
```

## Invalidate a cache after commit with a deferred signal

`AddDeferredSignal` fires an `ISignal` token during scope teardown, after the
commit and before deferred tasks. Pair it with cache entries keyed on that
signal token so consumers refresh only once the write is durable.

```csharp
using OrchardCore.Environment.Shell.Scope;

// After updating settings/data in the request:
ShellScope.AddDeferredSignal("MyModule:Settings");
```

## Run cleanup as the scope disposes (no new scope)

`RegisterBeforeDispose` runs inside the current scope while its services are
still available. Use it for disposing resources, not for reading committed data.

```csharp
using OrchardCore.Environment.Shell.Scope;

ShellScope.Current.RegisterBeforeDispose(scope =>
{
    // e.g. flush a buffer or dispose a rented resource.
    return Task.CompletedTask;
});
```

## Typed overloads

`ExecuteAfterEndOfRequestAsync` provides overloads that carry 1–5 captured
arguments into the job delegate, which lets you use a `static` lambda and avoid
capturing request-scoped state:

```csharp
// Two captured values.
await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
    "audit-change",
    userId,
    changeType,
    static async (scope, uid, type) =>
    {
        var audit = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        await audit.WriteAsync(uid, type);
    });
```

## Gotchas

- No HTTP context, no run: `ExecuteAfterEndOfRequestAsync` logs a warning and
  skips when there is no `HttpContext` (e.g. inside an `IBackgroundTask`,
  during CLI or non-request flows). Use a deferred task or run inline instead.
- Not durable: neither mechanism survives a process restart and neither retries.
  For guaranteed processing, enqueue to a persistent queue from the job.
- Fresh scope only: capturing the outer request's `IServiceProvider`,
  `DbConnection`, `ISession`, or `HttpContext` leads to disposed-object errors.
  Always resolve from the callback's `scope.ServiceProvider`.

---
name: orchardcore-deferred-tasks
description: Skill for running fire-and-forget work after a request in Orchard Core using HttpBackgroundJob.ExecuteAfterEndOfRequestAsync and ShellScope.AddDeferredTask. Covers deferring work until the current YesSql session is committed, running in a fresh isolated ShellScope, restoring the current user, and choosing between deferred tasks and after-request background jobs. Use this skill when requests mention Orchard Core HttpBackgroundJob, ExecuteAfterEndOfRequestAsync, ShellScope.AddDeferredTask, AddDeferredSignal, RegisterBeforeDispose, running code after the session is saved, fire-and-forget after a request, or closely related Orchard Core setup or troubleshooting work. Strong matches include OrchardCore.BackgroundJobs, OrchardCore.Environment.Shell.Scope, OrchardCore.Abstractions, ShellScope, IShellHost, IDocumentStore, IHttpContextAccessor, and combining a deferred task with a background job so work runs only after the session commit.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Deferred Tasks and After-Request Background Jobs

Orchard Core provides two complementary ways to run code *after* the main work
of a request, without adding a scheduled `IBackgroundTask`. Both live in the
`OrchardCore.Abstractions` package, so any module can use them.

- `ShellScope.AddDeferredTask` — runs a delegate in a **fresh isolated scope**
  once the current shell scope tears down, **after the YesSql session has been
  committed**. Use it to react to committed data (indexing, cache busting,
  sending a signal) and to see your own just-saved changes.
- `HttpBackgroundJob.ExecuteAfterEndOfRequestAsync` — truly **fire-and-forget**.
  It returns immediately, waits for the current `HttpContext` to be released
  (end of the HTTP response), reloads the shell, restores the current user, and
  runs the job in an isolated scope. Use it for longer work you do not want the
  client to wait for.

## When to use which

| Need | Use |
|---|---|
| Run right after commit, still part of request teardown, client waits | `ShellScope.AddDeferredTask` |
| Run after the response is sent, do not block the client | `HttpBackgroundJob.ExecuteAfterEndOfRequestAsync` |
| Only send an invalidation signal after commit | `ShellScope.AddDeferredSignal` |
| Run something as the scope disposes (no new scope) | `ShellScope.RegisterBeforeDispose` |

## Why "after the session is saved" works

The YesSql/document session commit is registered as a **before-dispose**
callback on the shell scope. During scope teardown Orchard Core runs, in order:

1. `RegisterBeforeDispose` callbacks — this is where `IDocumentStore.CommitAsync()`
   commits the session.
2. Deferred **signals** added via `AddDeferredSignal`.
3. Deferred **tasks** added via `AddDeferredTask`, each in its **own new scope**
   (with a fresh session) built from the reloaded shell.

Because the commit happens in step 1 and deferred tasks run in step 3, a
deferred task always observes committed data and never fights the request's
open transaction. Kicking off the background job **from inside a deferred task**
guarantees the job is only scheduled after a successful commit.

## Fire-and-forget after the request

```csharp
using OrchardCore.BackgroundJobs;

// Inside a controller action, driver, or handler:
await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
    "send-welcome-email",
    async scope =>
    {
        // 'scope' is a fresh, isolated ShellScope. Resolve services from it;
        // never capture request-scoped services from the outer scope.
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        await emailService.SendWelcomeAsync();
    });
```

Pass captured state through the typed overloads instead of closures over
request-scoped services:

```csharp
await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
    "reindex-item",
    contentItemId,
    static async (scope, id) =>
    {
        var manager = scope.ServiceProvider.GetRequiredService<IContentManager>();
        var item = await manager.GetAsync(id);
        // ... process the freshly loaded, committed item
    });
```

## Defer until after commit, then fire-and-forget

This is the recommended pattern when the background work must only happen if the
current unit of work is actually persisted. Register a deferred task; inside it,
schedule the after-request job.

```csharp
using OrchardCore.BackgroundJobs;
using OrchardCore.Environment.Shell.Scope;

// e.g. inside a content handler or controller, after mutating data:
ShellScope.AddDeferredTask(async scope =>
{
    // Runs in a NEW scope AFTER the session has been committed.
    await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
        "process-order",
        orderId,
        static async (jobScope, id) =>
        {
            var orders = jobScope.ServiceProvider.GetRequiredService<IOrderService>();
            await orders.ProcessAsync(id);
        });
});
```

## Guidelines

- Both APIs run outside the original request-scoped services. **Resolve every
  dependency from the provided `scope.ServiceProvider`**; do not close over the
  ambient `IServiceProvider`, `HttpContext`, or request-scoped services.
- Pass identifiers or immutable values, then **re-load entities** inside the job
  so you read committed state.
- `ExecuteAfterEndOfRequestAsync` requires an active `HttpContext`; if there is
  no HTTP context it logs a warning and does nothing. For non-HTTP flows
  (background tasks, CLI, setup) use a deferred task or run the work directly.
- The after-request job **waits up to 60 seconds** for the current `HttpContext`
  to be released before running; keep controller work bounded.
- The after-request job restores the **current user principal**, so authorization
  checks inside the job reflect the requesting user.
- Make the work **idempotent**. Fire-and-forget jobs are not retried and are not
  durable across an app restart; for guaranteed delivery use a real queue.
- Exceptions inside either callback are caught and logged, not surfaced to the
  client; add your own logging and compensation.
- No feature needs to be enabled — these are framework primitives in
  `OrchardCore.Abstractions`, referenced transitively by modules.
- All C# classes in examples are sealed except View Models.

See `references/deferred-tasks-examples.md` for content-handler, controller, and
signal-invalidation examples.

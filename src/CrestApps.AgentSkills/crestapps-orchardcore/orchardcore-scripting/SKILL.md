---
name: orchardcore-scripting
description: Skill for extending Orchard Core scripting from CrestApps modules. Covers IGlobalMethodProvider, GlobalMethod registration, singleton-safe service resolution, sync and async scripting methods, and feature-scoped Startup registration. Use this skill when requests mention Orchard Core scripting, IGlobalMethodProvider, custom scripting methods, GlobalMethod, IScriptingManager lifetime issues, recipe scripting helpers, workflow scripting helpers, or closely related Orchard Core implementation and troubleshooting work. Strong matches include OrchardCore.Scripting, IGlobalMethodProvider, GlobalMethod, IScriptingManager, StartupBase, and the code patterns and examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Scripting

## Overview

`IGlobalMethodProvider` adds custom global functions to Orchard Core scripting. Orchard uses these functions from scripting-enabled features such as recipes, workflows, and other expression-based execution paths that resolve methods through `IScriptingManager`.

Use `IGlobalMethodProvider` when you want Orchard scripting to expose a reusable function such as:

- loading configuration or feature-aware values
- rendering a reusable template
- querying a scoped Orchard service during script execution
- exposing a safe helper to recipe or workflow authors

## Core Lifetime Rule

`IScriptingManager` is a singleton. Treat every `IGlobalMethodProvider` as part of a singleton activation path.

That means:

- register the provider as `Singleton`
- do **not** constructor-inject scoped services into the provider
- resolve scoped services from the `serviceProvider` passed to `GlobalMethod.Method` and `GlobalMethod.AsyncMethod`

If a provider constructor takes a scoped dependency, Orchard can fail at runtime with errors like:

- `Cannot consume scoped service 'X' from singleton 'OrchardCore.Scripting.IScriptingManager'`

## Recommended Implementation Pattern

Create one cached `GlobalMethod` instance in the constructor and return it from `GetMethods()`.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Scripting;

namespace MyModule.Scripting;

internal sealed class MyMethodProvider : IGlobalMethodProvider
{
    private readonly GlobalMethod _globalMethod;

    public MyMethodProvider()
    {
        _globalMethod = new GlobalMethod
        {
            Name = "myHelper",
            Method = serviceProvider => (Func<string, object>)((value) =>
                ResolveAsync(serviceProvider, value).GetAwaiter().GetResult()),
            AsyncMethod = serviceProvider => (value) => ResolveAsync(serviceProvider, value),
        };
    }

    public IEnumerable<GlobalMethod> GetMethods()
    {
        yield return _globalMethod;
    }

    private static async Task<object> ResolveAsync(
        IServiceProvider serviceProvider,
        string value)
    {
        var scopedService = serviceProvider.GetRequiredService<IMyScopedService>();

        return await scopedService.BuildValueAsync(value);
    }
}
```

## Sync and Async Methods

`GlobalMethod` supports both:

- `Method` for synchronous scripting callers
- `AsyncMethod` for asynchronous scripting callers

Recommended pattern:

1. Put the real logic in a private async method.
2. Resolve scoped services inside that method from the invocation `serviceProvider`.
3. Let `Method` call the async implementation with `GetAwaiter().GetResult()`.
4. Let `AsyncMethod` return the async delegate directly.

This keeps the logic in one place and prevents the sync and async paths from drifting apart.

## Registration Pattern

Register the provider in the feature startup that owns the scripting functionality.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Scripting;

namespace MyModule;

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGlobalMethodProvider, MyMethodProvider>();
    }
}
```

## What the Method Name Becomes

`GlobalMethod.Name` is the function name script authors call.

If the provider registers:

```csharp
Name = "renderAITemplate"
```

then Orchard scripting can call:

```javascript
renderAITemplate("customer-support-intro")
```

and, for multi-argument methods:

```javascript
renderAITemplate("customer-support-intro", {
  siteName: "Contoso"
})
```

## Multi-Argument Pattern

Use the delegate signature that matches the scripting arguments you want to expose.

```csharp
_globalMethod = new GlobalMethod
{
    Name = "renderThing",
    Method = serviceProvider => (Func<string, object, object>)((name, args) =>
        ResolveThingAsync(serviceProvider, name, args).GetAwaiter().GetResult()),
    AsyncMethod = serviceProvider => (name, args) =>
        ResolveThingAsync(serviceProvider, name, args),
};
```

## Constructor Guidance

Safe constructor dependencies:

- `ILogger<T>`
- other singleton-safe services
- cached metadata or configuration already safe for singleton use

Avoid in the constructor:

- scoped services
- services that internally depend on scoped services

If you need a scoped service, resolve it from the invocation `serviceProvider` inside the method delegate.

## When to Use IGlobalMethodProvider

Use `IGlobalMethodProvider` when the behavior should be callable from Orchard scripting expressions rather than from Liquid tags, MVC endpoints, or admin UI components.

Good fits:

- recipe and workflow helper functions
- scripting access to tenant-aware services
- expression helpers that wrap Orchard APIs

Not the right fit:

- UI-only behavior
- Liquid-only rendering helpers
- long-running background operations that should not execute inline from a script

## Common Mistakes

- registering the provider as scoped
- injecting `ITemplateService`, `IShellFeaturesManager`, or other scoped services into the provider constructor
- resolving services from static or ambient scope helpers instead of the invocation `serviceProvider`
- implementing only the sync path and forgetting async callers
- duplicating sync and async logic in separate code paths

## Real Orchard Pattern

Follow Orchard Core's own method-provider pattern: the `serviceProvider` parameter supplied to `Method` and `AsyncMethod` is the correct place to resolve scoped services for the current invocation.

See also:

- `references/global-method-provider-examples.md`

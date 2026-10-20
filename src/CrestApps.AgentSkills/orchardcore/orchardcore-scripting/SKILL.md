---
name: orchardcore-scripting
description: Skill for using Orchard Core scripting and creating custom IGlobalMethodProvider implementations. Covers JavaScript evaluation with Jint, IScriptingManager and IScriptingEngine, disposing scripting scopes, built-in global methods, custom scripting providers that stay correct under lazy globals and engine reuse, service resolution from IServiceProvider, configuring Jint.Options execution limits (MaxStatements, TimeoutInterval, LimitMemory, constraint factories), StackOverflowGuard, cancellation tokens that stop scripts, JavaScriptEngineOptions.EnginePoolSize, Jint 4.16 and 4.17 behavior changes, recipe expressions, layer rules, and file scripting. Use it for OrchardCore.Scripting, OrchardCore.Scripting.JavaScript, IScriptingManager, GlobalMethod, JavaScriptEngine, JavaScriptScope, OperationDeadlineConstraint, and related Orchard Core setup, extension, upgrade, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Scripting - Prompt Templates

## Use the Orchard Core Scripting Engine

You are an Orchard Core expert. Generate code and configuration for scripting, JavaScript evaluation, global methods, custom scripting providers, JavaScript engine limits, and recipe scripting expressions.

### Guidelines

- The scripting module provides `IScriptingManager` for evaluating scripts in different languages.
- The default JavaScript engine uses the `js:` prefix and is powered by Jint. The file engine uses `file:`.
- A *directive* passed to `IScriptingManager` carries the prefix (`js: uuid()`). A script passed directly to `IScriptingEngine.Evaluate()` does not.
- Prefer `IScriptingManager.EvaluateAsync(...)` with a `CancellationToken`; it creates the scope, evaluates, and disposes the scope for you.
- If you create a scope yourself with `IScriptingEngine.CreateScope()`, dispose it in a `finally` (`(scope as IDisposable)?.Dispose()`) so the engine returns to the tenant's pool.
- Use `IGlobalMethodProvider` to register custom global methods as singletons.
- `GlobalMethod.Method` (and `AsyncMethod`) is `Func<IServiceProvider, Delegate>`: a factory that receives the evaluation's service provider and returns a strongly-typed delegate (`Func<string, string>`, `Action<string>`, ...).
- Global method factories run lazily, once per evaluation that reads the name, and may receive a `null` service provider when a script runs outside a tenant. Keep them side-effect free.
- Resolve scoped or transient services from the `serviceProvider` passed to the factory, never from the provider's constructor and never cache them in fields.
- Configure the JavaScript engine through `IOptions<Jint.Options>`. Sites that let non-administrators author scripts should set at least one execution limit.
- Recipe expressions use the `[js: expression]` syntax for dynamic values. Layer rules use JavaScript expressions evaluated by the scripting engine.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling Scripting Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Scripting",
        "OrchardCore.Scripting.JavaScript"
      ],
      "disable": []
    }
  ]
}
```

### Evaluating JavaScript from Code

Prefer `IScriptingManager`, which handles the scope lifetime and accepts a cancellation token that actually stops the script:

```csharp
using OrchardCore.Scripting;

namespace MyModule.Services;

public sealed class ScriptEvaluationService
{
    private readonly IScriptingManager _scriptingManager;

    public ScriptEvaluationService(IScriptingManager scriptingManager)
    {
        _scriptingManager = scriptingManager;
    }

    public Task<object> EvaluateAsync(string script, CancellationToken cancellationToken)
        // The directive carries the engine prefix.
        => _scriptingManager.EvaluateAsync($"js: {script}", null, null, [], cancellationToken);
}
```

When you need to evaluate several scripts against the same scope, talk to the engine directly and dispose the scope when you are done:

```csharp
using OrchardCore.Scripting;

namespace MyModule.Services;

public sealed class BatchScriptEvaluator
{
    private readonly IScriptingManager _scriptingManager;
    private readonly IServiceProvider _serviceProvider;

    public BatchScriptEvaluator(IScriptingManager scriptingManager, IServiceProvider serviceProvider)
    {
        _scriptingManager = scriptingManager;
        _serviceProvider = serviceProvider;
    }

    public IReadOnlyList<object> Evaluate(IEnumerable<string> scripts)
    {
        var engine = _scriptingManager.GetScriptingEngine("js");

        var globalMethods = _scriptingManager
            .GlobalMethodProviders
            .SelectMany(x => x.GetMethods());

        var scope = engine.CreateScope(globalMethods, _serviceProvider, null, null);

        try
        {
            // No "js:" prefix here: the engine is already the JavaScript one.
            return scripts.Select(script => engine.Evaluate(scope, script)).ToList();
        }
        finally
        {
            // IScriptingScope is a marker interface; JavaScriptScope implements IDisposable.
            // Disposing resets the engine and returns it to the pool. Do it in a finally,
            // because a script that throws still leaves its declarations behind.
            (scope as IDisposable)?.Dispose();
        }
    }
}
```

- Every engine exposes all registered `IGlobalMethodProvider` globals, whatever you pass as `methods`. Passing a restricted set no longer hides a global. Methods you pass take precedence over a registered global of the same name.
- Do not return a JavaScript *function* from a script and invoke it after the scope is disposed. It is bound to an engine that has since been reset or rented by another evaluation. Objects, arrays, and dates are converted to CLR values and are safe.

### Configuring the JavaScript Engine Safely

The engine is configured with Jint's own options type, registered as `IOptions<Jint.Options>`. **One `Jint.Options` instance is shared by every engine of the tenant** (recipes, workflow scripts, layer rules), so configure only tenant-wide policy.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Scripting.JavaScript;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<Jint.Options>(options =>
        {
            // Each helper registers a constraint *factory*: every engine gets its own counter/deadline.
            options.MaxStatements(10_000);
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
            options.LimitMemory(4_000_000);
            options.LimitRecursion(256);

            // Custom constraint: register a factory, never a shared instance.
            options.Constraint(() => new MaxLoopChecksConstraint(1_000_000));
        });

        // Optional: size the per-tenant engine pool (default 8, 0 disables reuse).
        services.Configure<JavaScriptEngineOptions>(options => options.EnginePoolSize = 16);
    }
}
```

```csharp
using Jint;

namespace MyModule;

// Per-execution state lives in the instance, so each engine must get its own instance.
public sealed class MaxLoopChecksConstraint : Constraint
{
    private readonly long _maxChecks;
    private long _checks;

    public MaxLoopChecksConstraint(long maxChecks)
    {
        _maxChecks = maxChecks;
    }

    public override void Check()
    {
        if (++_checks > _maxChecks)
        {
            throw new TimeoutException("The script exceeded its check budget.");
        }
    }

    // Jint calls Reset() at the start of every evaluation, on a reused engine too.
    public override void Reset() => _checks = 0;
}
```

Rules for `Jint.Options`:

- **Register constraints with the factory overload** `options.Constraint(() => new MyConstraint())`. `options.Constraint(new MyConstraint())` shares one instance, and its per-execution state, across every concurrently running engine of the tenant.
- **Always pass the budget you mean.** `MaxStatements(int.MaxValue)`, `LimitMemory(long.MaxValue)`, `TimeoutInterval(TimeSpan.MaxValue)`, and `MaxStatements()` with no argument register nothing and also remove any earlier limit of that kind. Omit the call instead of passing a maximum.
- **No execution budget is configured by default.** `while (true) {}` runs until the process is recycled unless you set a limit.
- **Do not register an `IObjectConverter` that handles `Delegate`.** Global methods are delegates; such a converter would change the shape of lazily created globals only.
- **Do not write to `Engine.Advanced.HostDefined`** on an engine obtained through `JavaScriptScope.Engine`. Orchard Core carries the evaluation's services in that slot to build lazy globals.
- **`Constraints.StackOverflowGuard` is on by default.** Unbounded recursion (`function f() { return 1 + f(); }`, a self-reentrant getter or `valueOf`, a self-evaluating `eval`) raises a catchable `RangeError` instead of killing the process. Budgets do not substitute for it. It costs about 2% on deeply recursive code; turn it off only after measuring:

```csharp
services.Configure<Jint.Options>(options =>
{
    options.Constraints.StackOverflowGuard = false;
});
```

### Cancellation Stops the Script

`AddJavaScriptEngine()` registers a `Jint.Constraints.OperationDeadlineConstraint` factory, and `JavaScriptEngine.EvaluateAsync()` arms it with the call's `CancellationToken`.

- Cancelling the token stops the interpreter (not only promise settlement) and surfaces as `OperationCanceledException` carrying the token, not as a scripting error.
- A token already cancelled before the call stops the script before it starts. One cancelled mid-run is observed on Jint's amortized constraint cadence, within a bounded number of statements.
- No time budget is added; the constraint is armed with `Timeout.InfiniteTimeSpan`. Time limits stay a site policy set on `Jint.Options`.
- Do not use `options.CancellationToken(token)` for per-evaluation tokens. It captures one token at configuration time for the tenant's lifetime.
- Built-in callers now pass their natural token: workflow scripts use `WorkflowExecutionContext.CancellationToken`, JavaScript rule conditions (layers) use `HttpContext.RequestAborted`, recipe `[js: ...]` expressions use the token given to `IRecipeExecutor.ExecuteAsync()` (a cancelled step fails with `RecipeExecutionException`). A recipe's `variables()` and the Users external login scripts are still not bounded by cancellation.

### Engines Are Reused Between Evaluations

Each tenant keeps a small pool of idle Jint engines. Disposing a scope restores the engine's global bindings and returns it to the pool. The reset undoes declared variables and functions, top-level `let`/`const`/`class`, overwritten globals, scope-only methods (such as a recipe's `variables()` or a workflow's `workflow()`), interop wrapper caches, and pending promises. Registered globals go back to their not-yet-created state so the next evaluation builds them from its own services, and the `HostDefined` services slot is cleared.

- **Not an isolation boundary.** Changes to built-in prototypes (`Object.prototype.x = 1`), `Symbol.for`, or object graphs reachable from untouched globals survive. Engines are never shared between tenants, but do not use the pool to run scripts that distrust each other.
- **Pool size** (`JavaScriptEngineOptions.EnginePoolSize`, default `8`) is not a concurrency limit; an evaluation that finds no idle engine builds one. It bounds how many engines retain state: a reused engine remembers the last object each call site resolved, including a global built from a finished request's services. That state is discarded once it is about a minute old, so a request's services stay reachable for at most about two minutes.
- Raise the pool size for tenants with many concurrent JavaScript layer rules. Set it to `0` when global methods project large object graphs into script that must not outlive the request.

### Jint 4.16 and 4.17 Behavior Changes for Script Authors

- `JSON.stringify()` and `Date.prototype.toISOString()` always format invariantly (no Unicode minus on Swedish, Finnish, Norwegian, Lithuanian, or Estonian threads).
- `JSON.parse('[1,2,]')` now throws. Remove trailing commas.
- An inconsistent `sort()` comparator finishes with an unspecified order instead of throwing an uncatchable CLR exception.
- Reading a member straight off an awaited value, such as `(await executeQueryAsync('recent', {}))[0]`, no longer re-runs the earlier non-awaited statements of the async function (which used to create duplicate content items or duplicate output).
- Long prototype chains, long `.bind()` chains used with `new`, long proxy chains, `ShadowRealm` wrapped-function chains, and self-evaluating `eval` either complete or raise a catchable error instead of overflowing the native stack. The depth limit depends on the server's stack size.
- `s = s + x` in a loop is linear now, like `s += x`. `LimitMemory()` still counts the string.
- `['a'].concat(list)` spreads arrays and lists returned by .NET methods.

### Built-In Global Methods

#### Generic Functions

| Function | Description |
|---|---|
| `log(level, text, param)` | Writes a log message at the specified log level |
| `uuid()` | Generates a unique content item identifier |
| `base64(string)` | Decodes a string from Base64 encoding |
| `html(string)` | Decodes a string from HTML encoding |
| `gzip(string)` | Decodes a string from gzip/base64 encoding |
| `protect(purpose, value)` | Protects a value using ASP.NET Core Data Protection with the given purpose string (development and testing only, do not store production secrets in recipes) |
| `encrypt(value)` | Encrypts a value and returns Base64 ciphertext |
| `decrypt(value)` | Decrypts Base64 ciphertext and returns an empty string if it cannot be decrypted |

#### Content Functions (`OrchardCore.Contents`)

| Function | Description |
|---|---|
| `newContentItemAsync(contentTypeName)` | Creates a new ContentItem instance (not persisted) |
| `createContentItemAsync(contentTypeName, publish, properties)` | Creates, persists, and optionally publishes a ContentItem |
| `updateContentItemAsync(contentItem, properties)` | Updates an existing ContentItem with properties |
| `deleteContentItemAsync(contentItem)` | Deletes an existing ContentItem |
| `getUrlPrefix(path)` | Prefixes a path with the tenant URL prefix |

#### Layer Rule Functions (`OrchardCore.Layers`)

| Function | Description |
|---|---|
| `isHomepage()` | Returns `true` if the current URL is the homepage |
| `isAnonymous()` | Returns `true` if no user is authenticated |
| `isAuthenticated()` | Returns `true` if a user is authenticated |
| `url(url)` | Returns `true` if the current URL matches (supports `*` wildcard) |
| `culture(name)` | Returns `true` if the current culture or its parent matches |

#### Query Functions (`OrchardCore.Queries`)

| Function | Description |
|---|---|
| `executeQueryAsync(name, parameters)` | Executes a named query and returns results |

#### HTTP Functions (`OrchardCore.Workflows.Http`)

| Function | Description |
|---|---|
| `httpContext()` | Returns the current `HttpContext` |
| `queryString(name)` | Returns the query string value as a string, a list of strings, or `null`; with no argument returns the whole query string |
| `responseWriteAsync(text)` | Writes text directly to the HTTP response |
| `absoluteUrl(relativePath)` | Converts a relative path to an absolute URL |
| `readBodyAsync()` | Returns the raw HTTP request body |
| `requestForm(name)` | Returns form field value(s) as a string, a list of strings, or `null` |
| `deserializeRequestDataAsync()` | Deserializes JSON or form request data to a dictionary; query and form values are strings or lists of strings |

Values read directly from ASP.NET Core, such as `httpContext().Request.Query['name']`, are still objects (they compare with `==` and concatenate without throwing). Use `queryString('name')` or `requestForm('name')` when you need a JavaScript string.

#### Recipe Functions (`OrchardCore.Recipes`)

| Function | Description |
|---|---|
| `variables()` | Declares and retrieves recipe variables |
| `parameters()` | Retrieves setup parameters (e.g., `AdminUserId`) |
| `configuration(key, defaultValue)` | Reads `IShellConfiguration` values with optional default |

#### Workflow Functions (`OrchardCore.Workflows`)

| Function | Description |
|---|---|
| `workflow()` | Returns the `WorkflowExecutionContext` |
| `workflowId()` | Returns the unique workflow ID |
| `input(name)` | Returns a workflow input parameter |
| `output(name, value)` | Sets a workflow output parameter |
| `property(name)` / `setProperty(name, value)` | Reads or writes a workflow property |
| `lastResult()` | Returns the previous activity's result |
| `correlationId()` / `setCorrelationId(id)` | Reads or sets the workflow correlation ID |
| `signalUrl(signal)` | Generates a protected signal trigger URL |
| `setOutcome(outcome)` | Adds an outcome to the current activity |
| `createWorkflowToken(workflowTypeId, activityId, expiresInDays)` | Generates a workflow SAS token |

See `orchardcore-workflows` for how workflow scripts fault the workflow when an execution limit stops them.

### File Scripting Engine

The `file:` prefix reads file contents relative to the application root:

| Function | Example | Description |
|---|---|---|
| `text` | `file:text('../wwwroot/template.html')` | Returns file content as text |
| `base64` | `file:base64('../wwwroot/image.jpg')` | Returns file content as Base64 |

### Using Scripts in Recipes

Recipe steps support `[js: expression]` syntax for dynamic values:

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js: uuid()]",
          "ContentType": "BlogPost",
          "DisplayText": "Welcome Post",
          "Latest": true,
          "Published": true,
          "Owner": "[js: parameters('AdminUserId')]",
          "TitlePart": {
            "Title": "Welcome Post"
          }
        }
      ]
    }
  ]
}
```

### Using Recipe Variables

Declare variables at the root of a recipe and reference them in steps:

```json
{
  "variables": {
    "blogContentItemId": "[js: uuid()]",
    "homePageId": "[js: uuid()]"
  },
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js: variables('blogContentItemId')]",
          "ContentType": "Blog",
          "DisplayText": "My Blog"
        }
      ]
    }
  ]
}
```

### Reading Configuration in Recipes

Use the `configuration()` function to read `IShellConfiguration` values. Use the Orchard Core 4.0 section names (`Admin`, not `OrchardCore_Admin`):

```json
{
  "steps": [
    {
      "name": "Settings",
      "AdminUrlPrefix": "[js: configuration('Admin:AdminUrlPrefix', 'Admin')]"
    }
  ]
}
```

## IGlobalMethodProvider — When and Why

### Purpose

`IGlobalMethodProvider` is the extension point for exposing custom functions to the Orchard Core scripting engine. Any function registered through this interface becomes available to **all** scripting contexts — recipes, layer rules, workflow expressions, and programmatic `IScriptingManager` calls.

### When to Use IGlobalMethodProvider

- **Expose a helper function to recipe files** — encoding, encryption, ID generation, or configuration lookups that recipe authors can call with `[js: myFunction(...)]`.
- **Provide utility functions for layer rules** — custom conditions like checking user roles, tenant state, or feature flags.
- **Make services accessible in workflow script expressions** — wrap a service so workflow activities can call it from JavaScript.

### When NOT to Use IGlobalMethodProvider

- **For C#-only logic** — use a normal service interface and DI.
- **For Liquid filters** — Liquid has its own filter registration; `IGlobalMethodProvider` is for the scripting engines.
- **For one-off recipe steps** — implement a custom recipe step handler instead.
- **For values that belong to one evaluation only** — pass a scoped `IGlobalMethodProvider` to `IScriptingManager.EvaluateAsync(..., scopedMethodProviders, ...)` instead of registering it globally (this is how a workflow's `workflow()` and a recipe's `variables()` are supplied).

### Key Types

```
IGlobalMethodProvider   — Interface with a single method: GetMethods()
GlobalMethod            — Name (string), Method and AsyncMethod (Func<IServiceProvider, Delegate>)
IScriptingManager       — Aggregates all registered IGlobalMethodProvider instances
JavaScriptEngine        — Declares registered globals lazily on the shared Jint.Options
```

### How Globals Are Created (Lazy Globals and Engine Reuse)

- Registered globals are declared on every engine but built only when a script reads the name. `[js: uuid()]` only invokes the `uuid` factory. The properties still exist (`in`, `hasOwnProperty()`), are non-enumerable, and keep a stable identity for the evaluation.
- The factory runs **once per evaluation that uses the name**, with **that evaluation's** service provider. After the scope is disposed the global is reset and the next evaluation builds it again from its own services.
- When a script is evaluated outside a tenant (no shell scope), the factory receives a **`null` service provider**. Handle it instead of throwing a `NullReferenceException`.
- Eager exceptions: methods passed directly to `CreateScope()`, a name contributed by more than one provider, and a method with an `AsyncMethod` whose `<name>Async` global is also claimed by a method literally named `<name>Async` are created eagerly, once per engine.

### The GlobalMethod Delegate Pattern

`Method` and `AsyncMethod` have the type `Func<IServiceProvider, Delegate>`. Each is a **factory** that receives the evaluation's `IServiceProvider` (or `null`) and returns a **strongly-typed delegate**:

- Cast to a concrete delegate type (`Func<string, string>`, `Action<string>`, `Func<string, Task<string>>`). A lambda without a natural type, like `args => ...`, does not compile into a usable global.
- Resolve scoped services from the factory's `serviceProvider`, ideally inside the returned delegate so they are only resolved when called.
- Inject only singletons (`ILogger<T>`, `IShellConfiguration`) into the provider's constructor, because the provider itself is a singleton.
- `AsyncMethod` is exposed as `<Name>Async` and returns a Promise. Use it for I/O-bound helpers.

### Writing a Provider That Is Correct Under Engine Reuse

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Scripting;

namespace MyModule.Scripting;

public sealed class ContentHelpersMethodProvider : IGlobalMethodProvider
{
    // Static, immutable GlobalMethod definitions: no request state is captured here.
    private static readonly GlobalMethod _utcNow = new()
    {
        Name = "utcNowIso",
        Method = serviceProvider => (Func<string>)(() =>
        {
            // serviceProvider is null when the script runs outside a tenant.
            var clock = serviceProvider?.GetService<IClock>();
            var now = clock?.UtcNow ?? DateTime.UtcNow;

            return now.ToString("O");
        }),
    };

    private static readonly GlobalMethod _displayText = new()
    {
        // Exposed to scripts as displayTextAsync(contentItemId), which returns a Promise.
        Name = "displayText",
        AsyncMethod = serviceProvider => (Func<string, Task<string>>)(async contentItemId =>
        {
            if (serviceProvider is null)
            {
                throw new InvalidOperationException("displayTextAsync() requires a tenant scope.");
            }

            // Scoped service resolved from the evaluation's own provider, at call time.
            var contentManager = serviceProvider.GetRequiredService<IContentManager>();
            var contentItem = await contentManager.GetAsync(contentItemId);

            // Return a plain CLR value, not the ContentItem graph, so nothing large is
            // retained by a pooled engine's call-site caches after the request ends.
            return contentItem?.DisplayText;
        }),
    };

    public IEnumerable<GlobalMethod> GetMethods()
        => [_utcNow, _displayText];
}
```

Rules this example follows:

- **No per-request state on the provider.** Never store the `serviceProvider`, a resolved scoped service, or a built delegate in a field or `Lazy<T>`. The engine rebuilds globals per evaluation precisely so one request's services never reach the next.
- **Side-effect free factories.** The factory may run zero, one, or several times depending on which scripts use the name. Put work in the returned delegate, not in the factory body.
- **Return CLR values.** Prefer strings, numbers, and small DTOs over live object graphs; set `EnginePoolSize = 0` if you must project large graphs.
- **Never return a function** that you expect to call after the scope is disposed.
- **Null-safe service provider** for scripts evaluated outside a tenant.

### Other Provider Patterns

#### Static Methods (No Service Dependencies)

**Reference:** `CommonGeneratorMethods` in `OrchardCore.Infrastructure` — provides `base64()`, `html()`, and `gzip()`.

```csharp
using System.Net;
using OrchardCore.Scripting;

namespace MyModule;

public sealed class EncodingMethodProvider : IGlobalMethodProvider
{
    private static readonly GlobalMethod _urlEncode = new()
    {
        Name = "urlEncode",
        Method = serviceProvider => (Func<string, string>)(value => WebUtility.UrlEncode(value)),
    };

    private static readonly GlobalMethod _urlDecode = new()
    {
        Name = "urlDecode",
        Method = serviceProvider => (Func<string, string>)(value => WebUtility.UrlDecode(value)),
    };

    public IEnumerable<GlobalMethod> GetMethods()
        => [_urlEncode, _urlDecode];
}
```

#### Resolving Services from IServiceProvider

**Reference:** `ProtectDataProvider` in `OrchardCore.Scripting` resolves `IDataProtectionProvider`; `IdGeneratorMethod` resolves `IIdGenerator`.

```csharp
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Scripting;

namespace MyModule;

public sealed class ProtectDataProvider : IGlobalMethodProvider
{
    private static readonly GlobalMethod _protect = new()
    {
        Name = "protect",
        Method = serviceProvider => (Func<string, string, string>)((purpose, value) =>
        {
            ArgumentException.ThrowIfNullOrEmpty(purpose);
            ArgumentException.ThrowIfNullOrEmpty(value);

            var dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();

            return dataProtectionProvider.CreateProtector(purpose).Protect(value);
        }),
    };

    public IEnumerable<GlobalMethod> GetMethods()
        => [_protect];
}
```

#### Constructor Injection for Singleton Services

**Reference:** `LogProvider` in `OrchardCore.Scripting` injects `ILogger<LogProvider>`; `ConfigurationMethodProvider` injects `IShellConfiguration`.

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.Scripting;

namespace MyModule;

public sealed class AuditLogMethodProvider : IGlobalMethodProvider
{
    private readonly GlobalMethod _audit;

    public AuditLogMethodProvider(ILogger<AuditLogMethodProvider> logger)
    {
        _audit = new GlobalMethod
        {
            Name = "audit",
            Method = serviceProvider => (Action<string>)(message =>
                logger.LogInformation("Script audit: {Message}", message)),
        };
    }

    public IEnumerable<GlobalMethod> GetMethods()
        => [_audit];
}
```

### Registering a Custom Global Method Provider

Register the provider as a **singleton** in your module's `Startup.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Scripting;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGlobalMethodProvider, EncodingMethodProvider>();
        services.AddSingleton<IGlobalMethodProvider, ContentHelpersMethodProvider>();
    }
}
```

### Common Delegate Signatures

| Signature | Use Case |
|---|---|
| `Func<string>` | No parameters, returns a string (e.g., `uuid()`) |
| `Func<string, string>` | One string parameter, returns a string (e.g., `base64(encoded)`) |
| `Func<string, string, string>` | Two string parameters, returns a string (e.g., `protect(purpose, value)`) |
| `Func<string, bool>` | One string parameter, returns a boolean (e.g., `url(pattern)`) |
| `Action<string, string, object>` | Multiple parameters, no return value (e.g., `log(level, text, param)`) |
| `Func<string, Task<string>>` | `AsyncMethod`, exposed as `<name>Async()` returning a Promise |

### Anti-Patterns to Avoid

```csharp
// ❌ WRONG — no natural delegate type, cannot become a global
Method = serviceProvider => (args) => $"Hello, {args[0]}!",

// ✅ CORRECT — strongly-typed Func delegate
Method = serviceProvider => (Func<string, string>)(name => $"Hello, {name}!"),
```

```csharp
// ❌ WRONG — ISession is scoped, but IGlobalMethodProvider is a singleton (captive dependency)
public sealed class BadProvider : IGlobalMethodProvider
{
    private readonly ISession _session;

    public BadProvider(ISession session) => _session = session;

    public IEnumerable<GlobalMethod> GetMethods() => [];
}

// ❌ WRONG — caches the delegate built for the first request's services
private Delegate _cached;
Method = serviceProvider => _cached ??= (Func<string>)(() => serviceProvider.GetRequiredService<IFoo>().Value),

// ✅ CORRECT — resolve scoped services from the evaluation's provider every time
Method = serviceProvider => (Func<string, object>)(contentType =>
{
    var session = serviceProvider.GetRequiredService<ISession>();
    return null;
}),
```

### Layer Rules with Scripting

Layer rules use JavaScript expressions to determine visibility. A JavaScript rule condition is bounded by `HttpContext.RequestAborted`, and saving a condition with a syntax error now shows a validation message (`Jint.ScriptPreparationException`) instead of an unhandled exception.

```
// Show only on the homepage
isHomepage()

// Show to authenticated users only
isAuthenticated()

// Show on URLs starting with /blog
url("~/blog*")

// Combine conditions
isAuthenticated() && !isHomepage()

// Culture-specific content
culture("en")
```

## Upgrading from 3.x

- `JavaScriptEngine` is public and its constructor takes a third parameter, `IEnumerable<IGlobalMethodProvider>`. Code (typically tests) that constructs it directly must pass the providers, or an empty sequence for the old behavior.
- Every engine exposes all registered globals regardless of the `methods` passed to `CreateScope()`. Do not rely on a restricted `methods` list to hide a global.
- Global method factories are invoked lazily, possibly not at all, and with a `null` service provider outside a tenant. Remove side effects from factories and stop caching delegates or services.
- `JavaScriptScope` implements `IDisposable`. Dispose scopes you create (in a `finally`) so engines are reused; `IScriptingManager` already does.
- Before: `options.Constraint(new MyConstraint())`. After: `options.Constraint(() => new MyConstraint())`.
- `IScriptingManager.EvaluateAsync()` / `IScriptingEngine.EvaluateAsync()` tokens now stop the running script with `OperationCanceledException`. Callers that passed a token now get real cancellation.
- `Constraints.StackOverflowGuard` is on by default.
- Workflow request helpers (`deserializeRequestData()`, `queryStringAsJson()`, `requestFormAsJson()`) return strings or lists of strings instead of objects; `===`, `typeof`, and string methods now behave as expected.
- `JSON.parse()` rejects trailing commas; `JSON.stringify()` and `toISOString()` are culture-invariant.
- The prepared script cache uses a dedicated key type in `IMemoryCache`, so other components' cache keys no longer collide with it.
- Use the new configuration section names in `configuration()` lookups, for example `Admin:AdminUrlPrefix` instead of `OrchardCore_Admin:AdminUrlPrefix` (old names are deprecated but still read).

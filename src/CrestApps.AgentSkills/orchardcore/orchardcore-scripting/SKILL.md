---
name: orchardcore-scripting
description: Skill for using Orchard Core scripting and creating custom IGlobalMethodProvider implementations. Covers JavaScript evaluation, built-in global methods, custom scripting providers, service resolution from IServiceProvider, recipe expressions, layer rules, and file scripting. Use it for OrchardCore.Scripting, OrchardCore.Scripting.JavaScript, IScriptingManager, GlobalMethod, and related Orchard Core setup, extension, or troubleshooting work.
---

# Orchard Core Scripting - Prompt Templates

## Use the Orchard Core Scripting Engine

You are an Orchard Core expert. Generate code and configuration for scripting, JavaScript evaluation, global methods, custom scripting providers, and recipe scripting expressions.

### Guidelines

- The scripting module provides `IScriptingManager` for evaluating scripts in different languages.
- The default JavaScript engine uses the `js:` prefix and is powered by Jint (Esprima.NET).
- Scripts are prefixed with the engine identifier (e.g., `js:`, `file:`).
- Global methods are available across all scripting engines unless engine-specific.
- Use `IGlobalMethodProvider` to register custom global methods as singletons.
- The `GlobalMethod.Method` property is `Func<IServiceProvider, Delegate>` — it receives an `IServiceProvider` and must return a strongly-typed delegate (e.g., `Func<string, string>`, `Action<string>`).
- **Never** use loosely-typed delegates or `args[]` arrays; always cast to a concrete delegate type matching the function's parameter and return types.
- Resolve scoped or transient services from the `serviceProvider` parameter inside the `Method` delegate, not from the constructor.
- Use constructor injection only for singleton services (e.g., `ILogger<T>`).
- The File scripting engine (`file:`) reads file contents at recipe execution time.
- Recipe expressions use the `[js: expression]` syntax for dynamic values.
- Layer rules use JavaScript expressions evaluated by the scripting engine.
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

Use `IScriptingManager` to evaluate scripts programmatically:

```csharp
using OrchardCore.Scripting;

public sealed class ScriptEvaluationService
{
    private readonly IScriptingManager _scriptingManager;
    private readonly IServiceProvider _serviceProvider;

    public ScriptEvaluationService(
        IScriptingManager scriptingManager,
        IServiceProvider serviceProvider)
    {
        _scriptingManager = scriptingManager;
        _serviceProvider = serviceProvider;
    }

    public object EvaluateJavaScript(string script)
    {
        var engine = _scriptingManager.GetScriptingEngine("js");

        var globalMethods = _scriptingManager
            .GlobalMethodProviders
            .SelectMany(x => x.GetMethods());

        var scope = engine.CreateScope(globalMethods, _serviceProvider, null, null);

        return engine.Evaluate(scope, $"js: {script}");
    }
}
```

### Built-In Global Methods

#### Generic Functions

| Function | Description |
|---|---|
| `log(level, text, param)` | Writes a log message at the specified log level |
| `uuid()` | Generates a unique content item identifier |
| `base64(string)` | Decodes a string from Base64 encoding |
| `html(string)` | Decodes a string from HTML encoding |
| `gzip(string)` | Decodes a string from gzip/base64 encoding |
| `protect(purpose, value)` | Protects a value using ASP.NET Core Data Protection with the given purpose string |

#### Content Functions (`OrchardCore.Contents`)

| Function | Description |
|---|---|
| `newContentItem(contentTypeName)` | Creates a new ContentItem instance (not persisted) |
| `createContentItem(contentTypeName, publish, properties)` | Creates, persists, and optionally publishes a ContentItem |
| `updateContentItem(contentItem, properties)` | Updates an existing ContentItem with properties |
| `deleteContentItem(contentItem)` | Deletes an existing ContentItem |
| `getUrlPrefix(path)` | Prefixes a path with the tenant URL prefix |

#### Layer Rule Functions (`OrchardCore.Layers`)

| Function | Description |
|---|---|
| `isHomepage()` | Returns `true` if the current URL is the homepage |
| `isAnonymous()` | Returns `true` if no user is authenticated |
| `isAuthenticated()` | Returns `true` if a user is authenticated |
| `url(url)` | Returns `true` if the current URL matches (supports `*` wildcard) |
| `culture(name)` | Returns `true` if the current culture matches |

#### Query Functions (`OrchardCore.Queries`)

| Function | Description |
|---|---|
| `executeQuery(name, parameters)` | Executes a named query and returns results |

#### HTTP Functions (`OrchardCore.Workflows.Http`)

| Function | Description |
|---|---|
| `httpContext()` | Returns the current `HttpContext` |
| `queryString(name)` | Returns query string value(s) by name |
| `responseWrite(text)` | Writes text directly to the HTTP response |
| `absoluteUrl(relativePath)` | Converts a relative path to an absolute URL |
| `readBody()` | Returns the raw HTTP request body |
| `requestForm(name)` | Returns form field value(s) by name |
| `deserializeRequestData()` | Deserializes JSON or form request data to a dictionary |

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
| `property(name)` | Returns a workflow property value |
| `lastResult()` | Returns the previous activity's result |
| `correlationId()` | Returns the workflow correlation ID |
| `signalUrl(signal)` | Generates a protected signal trigger URL |
| `setOutcome(outcome)` | Adds an outcome to the current activity |

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

Use the `configuration()` function to read `IShellConfiguration` values:

```json
{
  "steps": [
    {
      "name": "Settings",
      "AdminUrlPrefix": "[js: configuration('OrchardCore_Admin:AdminUrlPrefix', 'Admin')]"
    }
  ]
}
```

## IGlobalMethodProvider — When and Why

### Purpose

`IGlobalMethodProvider` is the extension point for exposing custom functions to the Orchard Core scripting engine. Any function registered through this interface becomes available to **all** scripting contexts — recipes, layer rules, workflow expressions, and programmatic `IScriptingManager` calls.

### When to Use IGlobalMethodProvider

Use `IGlobalMethodProvider` when you need to:

- **Expose a helper function to recipe files** — e.g., encoding, encryption, ID generation, or configuration lookups that recipe authors can call with `[js: myFunction(...)]`.
- **Provide utility functions for layer rules** — e.g., custom conditions like checking user roles, tenant state, or external service status.
- **Make services accessible in workflow script expressions** — e.g., wrapping an injected service so workflow activities can call it from JavaScript.
- **Add cross-cutting scripting capabilities** — e.g., logging, data protection, or feature-flag checks usable from any scripting context.

### When NOT to Use IGlobalMethodProvider

- **For C#-only logic** — if the function is only called from C# code, use a normal service interface and DI instead.
- **For Liquid filters** — Liquid has its own filter registration mechanism; `IGlobalMethodProvider` is for the JavaScript scripting engine.
- **For one-off recipe steps** — if the logic is a discrete action (like importing data), implement a custom recipe step handler instead.

### Key Interfaces and Classes

```
IGlobalMethodProvider          — Interface with a single method: GetMethods()
GlobalMethod                   — Data class with Name (string) and Method (Func<IServiceProvider, Delegate>)
IScriptingManager              — Aggregates all registered IGlobalMethodProvider instances
```

### The GlobalMethod.Method Delegate Pattern

The `Method` property on `GlobalMethod` has the type:

```csharp
Func<IServiceProvider, Delegate>
```

This means it is a **factory function** that:

1. Receives an `IServiceProvider` (the request-scoped service provider).
2. Returns a **strongly-typed delegate** that the scripting engine invokes.

**Critical rules:**

- **Always cast to a concrete delegate type** — `Func<string, string>`, `Func<string, string, string>`, `Action<string>`, etc. Never use `Delegate` or an untyped lambda.
- **Resolve scoped services inside the Method delegate** — the `IServiceProvider` is the request-scoped container. Resolve services like `ISession`, `IContentManager`, or `IDataProtectionProvider` here.
- **Use constructor injection only for singletons** — services like `ILogger<T>` that are registered as singletons can be injected via the constructor since `IGlobalMethodProvider` is itself a singleton.

### Creating a Custom Global Method Provider

There are three patterns used in Orchard Core for implementing `IGlobalMethodProvider`, depending on how services are obtained.

#### Pattern 1: Static Methods (No Service Dependencies)

Use this pattern when the function has no service dependencies and only performs pure computation. Define the `GlobalMethod` as a `static readonly` field.

**Reference:** `CommonGeneratorMethods` in `OrchardCore.Infrastructure` — provides `base64()`, `html()`, and `gzip()`.

```csharp
using System.Net;
using OrchardCore.Scripting;

namespace MyModule;

public sealed class MyStaticMethodProvider : IGlobalMethodProvider
{
    private static readonly GlobalMethod _htmlEncode = new()
    {
        Name = "htmlEncode",
        Method = serviceProvider => (Func<string, string>)(value =>
        {
            return WebUtility.HtmlEncode(value);
        }),
    };

    public IEnumerable<GlobalMethod> GetMethods()
    {
        return [_htmlEncode];
    }
}
```

#### Pattern 2: Resolving Services from IServiceProvider

Use this pattern when the function depends on **scoped or transient services** that must be resolved per-request. Resolve from the `serviceProvider` parameter passed to the `Method` delegate.

**Reference:** `IdGeneratorMethod` in `OrchardCore.Infrastructure` — resolves `IIdGenerator` to generate UUIDs. `ProtectDataProvider` in `OrchardCore.Scripting` — resolves `IDataProtectionProvider`.

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
            var dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
            var protector = dataProtectionProvider.CreateProtector(purpose);

            return protector.Protect(value);
        }),
    };

    public IEnumerable<GlobalMethod> GetMethods()
    {
        return [_protect];
    }
}
```

#### Pattern 3: Constructor Injection for Singleton Services

Use this pattern when the function depends on **singleton services** (e.g., `ILogger<T>`). Since `IGlobalMethodProvider` is registered as a singleton, only singleton services can be safely injected via the constructor.

**Reference:** `LogProvider` in `OrchardCore.Scripting` — injects `ILogger<LogProvider>`.

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.Scripting;

namespace MyModule;

public sealed class LogProvider : IGlobalMethodProvider
{
    private readonly GlobalMethod _log;

    public LogProvider(ILogger<LogProvider> logger)
    {
        _log = new GlobalMethod
        {
            Name = "log",
            Method = serviceProvider => (Action<string, string, object>)((level, text, param) =>
            {
                if (!Enum.TryParse<LogLevel>(level, true, out var logLevel))
                {
                    logLevel = LogLevel.Information;
                }

                if (param == null)
                {
#pragma warning disable CA2254 // Template should be a static expression
                    logger.Log(logLevel, text);
#pragma warning restore CA2254 // Template should be a static expression
                }
                else
                {
                    object[] args = param is not Array ? [param] : (object[])param;

#pragma warning disable CA2254 // Template should be a static expression
                    logger.Log(logLevel, text, args);
#pragma warning restore CA2254 // Template should be a static expression
                }
            }),
        };
    }

    public IEnumerable<GlobalMethod> GetMethods()
    {
        return [_log];
    }
}
```

### Returning Multiple Methods from One Provider

A single `IGlobalMethodProvider` can expose multiple functions. Use a collection expression to return them:

```csharp
using System.Net;
using OrchardCore.Scripting;

namespace MyModule;

public sealed class EncodingMethodProvider : IGlobalMethodProvider
{
    private static readonly GlobalMethod _urlEncode = new()
    {
        Name = "urlEncode",
        Method = serviceProvider => (Func<string, string>)(value =>
        {
            return WebUtility.UrlEncode(value);
        }),
    };

    private static readonly GlobalMethod _urlDecode = new()
    {
        Name = "urlDecode",
        Method = serviceProvider => (Func<string, string>)(value =>
        {
            return WebUtility.UrlDecode(value);
        }),
    };

    public IEnumerable<GlobalMethod> GetMethods()
    {
        return [_urlEncode, _urlDecode];
    }
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
    }
}
```

### Common Delegate Signatures

| Signature | Use Case |
|---|---|
| `Func<string>` | No parameters, returns a string (e.g., `uuid()`) |
| `Func<string, string>` | One string parameter, returns a string (e.g., `base64(encoded)`) |
| `Func<string, string, string>` | Two string parameters, returns a string (e.g., `protect(purpose, value)`) |
| `Action<string>` | One string parameter, no return value |
| `Action<string, string, object>` | Multiple parameters, no return value (e.g., `log(level, text, param)`) |
| `Func<string, bool>` | One string parameter, returns a boolean (e.g., `url(pattern)`) |

### Anti-Patterns to Avoid

**Do NOT use untyped delegates or `args[]` arrays:**

```csharp
// ❌ WRONG — untyped delegate, will fail at runtime
Method = serviceProvider => (args) =>
{
    var name = args[0]?.ToString();
    return $"Hello, {name}!";
},

// ✅ CORRECT — strongly-typed Func delegate
Method = serviceProvider => (Func<string, string>)(name =>
{
    return $"Hello, {name}!";
}),
```

**Do NOT inject scoped services via constructor:**

```csharp
// ❌ WRONG — ISession is scoped, but IGlobalMethodProvider is a singleton
public sealed class BadProvider : IGlobalMethodProvider
{
    private readonly ISession _session; // Captive dependency!

    public BadProvider(ISession session)
    {
        _session = session;
    }
}

// ✅ CORRECT — resolve scoped services inside the Method delegate
private static readonly GlobalMethod _query = new()
{
    Name = "queryContent",
    Method = serviceProvider => (Func<string, object>)(contentType =>
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        // Use session here...
        return null;
    }),
};
```

### Layer Rules with Scripting

Layer rules use JavaScript expressions to determine visibility:

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

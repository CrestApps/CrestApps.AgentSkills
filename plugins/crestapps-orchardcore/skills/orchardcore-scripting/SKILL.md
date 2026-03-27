---
name: orchardcore-scripting
description: Skill for using the Orchard Core scripting engine. Covers JavaScript evaluation, built-in global methods for content management, HTTP requests, workflow signals, recipe execution, environment variables, layer rules, and custom scripting providers. Use this skill when requests mention Orchard Core Scripting, Use the Orchard Core Scripting Engine, Enabling Scripting Features, Evaluating JavaScript from Code, Built-In Global Methods, File Scripting Engine, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Scripting, OrchardCore.Scripting.JavaScript, OrchardCore.Contents, OrchardCore.Layers, OrchardCore.Queries, OrchardCore.Workflows.Http, OrchardCore.Recipes, OrchardCore.Workflows, IScriptingManager. It also helps with Built-In Global Methods, File Scripting Engine, Using Scripts in Recipes, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Scripting - Prompt Templates

## Use the Orchard Core Scripting Engine

You are an Orchard Core expert. Generate code and configuration for scripting, JavaScript evaluation, global methods, custom scripting providers, and recipe scripting expressions.

### Guidelines

- The scripting module provides `IScriptingManager` for evaluating scripts in different languages.
- The default JavaScript engine uses the `js:` prefix and is powered by Jint (Esprima.NET).
- Scripts are prefixed with the engine identifier (e.g., `js:`, `file:`).
- Global methods are available across all scripting engines unless engine-specific.
- Use `IGlobalMethodProvider` to register custom global methods.
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

### Creating a Custom Global Method Provider

Register custom methods available to all scripting engines:

```csharp
using OrchardCore.Scripting;

public sealed class MyGlobalMethodProvider : IGlobalMethodProvider
{
    private readonly GlobalMethod _greetMethod;

    public MyGlobalMethodProvider()
    {
        _greetMethod = new GlobalMethod
        {
            Name = "greet",
            Method = serviceProvider => (args) =>
            {
                var name = args[0]?.ToString() ?? "World";
                return $"Hello, {name}!";
            },
        };
    }

    public IEnumerable<GlobalMethod> GetMethods()
    {
        yield return _greetMethod;
    }
}
```

Register the provider in your module's `Startup`:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGlobalMethodProvider, MyGlobalMethodProvider>();
    }
}
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

---
name: crestapps-core-custom-tools
description: Skill for code-defined AI tools in CrestApps.Core 2.0. Covers implementing AIFunction tools with a JSON schema, registering them with AddCoreAITool and the AIToolBuilder (WithTitle, WithDescription, WithCategory, WithPurpose, WithDependency, Selectable, Hidden), system versus selectable versus hidden tools, AIToolPurposes values, tool dependencies, returning errors instead of throwing, rich MCP results with IAIToolContentProvider, custom IToolRegistryProvider sources and ToolRegistryEntry, tool authorization where the AI Profile is the boundary for sessions and IAIToolAccessEvaluator re-checks Chat Interaction tools, IUserAccessor for the caller, and how tools relate to tool instances, agents and MCP server exposure. Use it when giving the model a new callable function, controlling which tools a profile or user can reach, or troubleshooting tools that never reach the model.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Custom Tools

A code-defined tool is a Microsoft.Extensions.AI `AIFunction` registered under a name. The orchestrator adds it to requests through the tool registry; the model supplies all its arguments. Use tool instances (`crestapps-core-tool-instances`) when end users configure the tool, and agents (`crestapps-core-ai-agents`) when the callable thing is a whole profile.

## Guidelines

- Inherit from `AIFunction` and override `Name`, `Description`, `JsonSchema` and `InvokeCoreAsync`.
- Register with `AddCoreAITool<TTool>(name)`. Tools are singletons, also registered as keyed `AITool` services under the name.
- A tool is a **system** tool by default (hidden from pickers, added by the orchestrator by purpose or context). Call `.Selectable()` for tools users attach to profiles, and `.Hidden()` for helper tools only explicitly configured profiles or agents may use; hidden tools stay out of picker and shared MCP listings.
- Catch failures and return a descriptive result the model can act on. Never return `null`.
- Purpose values are snake case strings (`content_generation`, not `ContentGeneration`); always use the `AIToolPurposes` constants.
- Resolve scoped services from `AIFunctionArguments.Services`, not from the constructor (the tool is a singleton).
- The deployment must declare `toolCalling`, or capability enforcement removes every tool from the request.

## Registration

```csharp
builder.Services
    .AddCoreAITool<WeatherTool>(WeatherTool.TheName)
        .WithTitle("Get Weather")
        .WithDescription("Returns current weather for a location.")
        .WithCategory("Utilities")
        .Selectable();
```

| Builder method | Use |
|---|---|
| `.WithTitle(...)` | UI title (defaults to the name) |
| `.WithDescription(...)` | Description (defaults to the name) |
| `.WithCategory(...)` | UI grouping |
| `.WithPurpose(...)` | Purpose tag: `AIToolPurposes.DocumentProcessing` (`document_processing`), `ContentGeneration` (`content_generation`), `DataSourceSearch` (`data_source_search`), `Memory` (`memory`) |
| `.WithDependency(...)` / `.WithDependencies(...)` | Tools added automatically when this one is selected |
| `.WithoutDependency(...)` / `.WithoutDependencies(...)` | Remove registered dependencies |
| `.Selectable()` | User-assignable |
| `.Hidden()` | Only for profiles or agents that name it |

`AddCoreAIToolServices<TTool>(name)` registers the singleton and keyed service without a definition, for tools resolved only in code.

## Tool Example

```csharp
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

public sealed class WeatherTool : AIFunction
{
    public const string TheName = "get-weather";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>("""
    {
      "type": "object",
      "required": ["location"],
      "properties": {
        "location": { "type": "string", "description": "City name." },
        "units": { "type": "string", "enum": ["celsius", "fahrenheit"], "description": "Temperature units." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "Returns current weather for a location.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("location", out var raw) || raw?.ToString() is not { Length: > 0 } location)
        {
            return """{"error":"A location is required."}""";
        }

        var weatherClient = arguments.Services.GetRequiredService<IWeatherClient>();

        try
        {
            var forecast = await weatherClient.GetCurrentAsync(location, cancellationToken);

            return JsonSerializer.Serialize(new { forecast.Temperature, forecast.Condition, Location = location });
        }
        catch (HttpRequestException)
        {
            return """{"error":"The weather service is unavailable. Try again later."}""";
        }
    }
}
```

`IWeatherClient` stands for the application's own service. Arguments arrive as `JsonElement` values from most providers, so read them with `ToString()` or deserialize rather than casting to `string`.

## Dependencies

```csharp
builder.Services
    .AddCoreAITool<CreateOrderTool>("create_order")
        .WithDescription("Creates an order from validated input.")
        .WithDependency("get_order_schema")
        .Selectable();

builder.Services
    .AddCoreAITool<GetOrderSchemaTool>("get_order_schema")
        .WithDescription("Returns the fields an order needs.")
        .Hidden();
```

Profiles store only the top-level name. Missing dependencies are ignored, and shared or circular graphs are deduplicated.

## Rich Results for MCP Clients

A result that is more than text can implement `IAIToolContentProvider` (`IReadOnlyList<AIContent> ToContents()`); the MCP server maps it to content blocks (for example text plus resource links) instead of one text block. A plain string is still one text block.

## Custom Registry Provider

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

public sealed class FeatureFlagToolRegistryProvider : IToolRegistryProvider
{
    private readonly IFeatureFlags _featureFlags;

    public FeatureFlagToolRegistryProvider(IFeatureFlags featureFlags)
    {
        _featureFlags = featureFlags;
    }

    public async Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context is null || context.DisableTools || !await _featureFlags.IsEnabledAsync("beta-tools", cancellationToken))
        {
            return [];
        }

        return
        [
            new ToolRegistryEntry
            {
                Id = "beta:get-weather",
                Name = WeatherTool.TheName,
                Description = "Returns current weather for a location.",
                Source = ToolRegistryEntrySource.Local,
                CreateAsync = services => ValueTask.FromResult<AITool>(services.GetRequiredService<WeatherTool>()),
            },
        ];
    }
}
```

```csharp
builder.Services.AddScoped<IToolRegistryProvider, FeatureFlagToolRegistryProvider>();
```

`IFeatureFlags` stands for the host's own service. `IToolRegistry.GetAllAsync(context)` concatenates every provider; the built-ins are `SystemToolRegistryProvider`, `ProfileToolRegistryProvider`, `AgentToolRegistryProvider` and, with tool instances, `ToolInstanceRegistryProvider`. `ToolRegistryEntrySource` is `Local`, `McpServer`, `System` or `Agent`.

## Tool Authorization

- **AI sessions**: the AI Profile is the authorization boundary. Profile-selected tools, attached MCP connections and context-driven system tools run for any caller allowed to use the profile, including anonymous sessions. No per-user check runs at completion time; restrict who can edit profiles instead.
- **Chat Interactions**: the saved tool names are caller-controllable, so each send re-checks every **selectable** tool with `IAIToolAccessEvaluator.IsAuthorizedAsync(ClaimsPrincipal user, string toolName)` and drops the ones the caller may not use (one warning). System, hidden and dependency tools are not checked, and a `null` user (server-side work) skips the check. The default evaluator allows everything.

```csharp
using System.Security.Claims;
using CrestApps.Core.AI.Tooling;
using Microsoft.AspNetCore.Authorization;

public sealed class PermissionToolAccessEvaluator : IAIToolAccessEvaluator
{
    private readonly IAuthorizationService _authorizationService;

    public PermissionToolAccessEvaluator(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, string toolName)
    {
        var result = await _authorizationService.AuthorizeAsync(user, toolName, "UseAITool");

        return result.Succeeded;
    }
}
```

```csharp
builder.Services.AddScoped<IAIToolAccessEvaluator, PermissionToolAccessEvaluator>();
```

The caller comes from `IUserAccessor.User` (namespace `CrestApps.Core.Security`), which prefers a principal assigned on the current async flow and falls back to `HttpContext.User`. The built-in hubs assign `Context.User` at the start of each invocation; custom hosts that run completions outside a request or hub should assign it too.

## MCP Server Exposure

An MCP server built on CrestApps.Core publishes registered tools through its own allow-list (`ExposeAllTools` or a list of tool names) and agents through a separate `Agents`/`ExposeAllAgents` switch. Hidden tools are not listed. Each MCP tool call runs inside an `AIInvocationScope`. See `crestapps-core-mcp`.

## Upgrading from 1.x

- `FunctionInvocationAICompletionServiceHandler` no longer filters AI session tools through `IAIToolAccessEvaluator`. Keep a custom evaluator; it now governs Chat Interaction sends. It is not obsolete.
- `IToolRegistryProvider.GetToolsAsync` returns `Task<IReadOnlyList<ToolRegistryEntry>>`. Some published samples still show `ValueTask<IEnumerable<AIToolMetadataEntry>>`; follow the interface.
- Declare `toolCalling` on deployments; a 1.x `Chat` purpose projects to `textGeneration` only, and tools are then stripped.

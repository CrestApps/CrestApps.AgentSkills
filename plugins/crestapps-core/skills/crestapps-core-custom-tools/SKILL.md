---
name: crestapps-core-custom-tools
description: Skill for registering custom AI-callable tools, tool metadata, and access control in CrestApps.Core.
---

# CrestApps.Core Custom Tools - Prompt Templates

## Create Custom AI Tools

You are a CrestApps.Core expert. Generate tool classes and registration code for CrestApps.Core custom AI tools.

### Guidelines

- Inherit from `AIFunction` (from `Microsoft.Extensions.AI`) and override `Name`, `Description`, `JsonSchema`, and `InvokeCoreAsync`.
- Register tools with `AddCoreAITool<TTool>(name)` and chain the metadata builder.
- Use `.Selectable()` only for tools that should appear in UI assignment surfaces; use `.Hidden()` to keep a tool out of the shared MCP/agent listings while still available to explicitly configured profiles.
- Prefer clear titles, descriptions, categories, and purpose tags.
- Catch failures inside the tool and return descriptive results instead of throwing.
- These code-registered tools are distinct from parameterized tool instances (`AIToolInstance` / `IAIToolInstanceSource`, prefixed `tool_instance_`); use tool instances when end users configure the tool, and `AddCoreAITool<T>` when the tool is defined in code.

### Registration

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOrchestration()
    .AddCoreAITool<WeatherTool>("get-weather")
        .WithTitle("Get Weather")
        .WithDescription("Returns current weather for a location.")
        .WithCategory("Utilities")
        .Selectable();
```

### Tool Example

```csharp
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

    protected override ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("location", out var raw) || raw is not string location || string.IsNullOrWhiteSpace(location))
        {
            return ValueTask.FromResult<object>("""{"error":"A location is required."}""");
        }

        return ValueTask.FromResult<object>(
            JsonSerializer.Serialize(new { Temperature = 22, Condition = "Sunny", Location = location }));
    }
}
```

### Tool Metadata Guidance

| Builder method | Use |
|---|---|
| `.WithTitle(...)` | Friendly UI title |
| `.WithDescription(...)` | Description shown to the model |
| `.WithCategory(...)` | UI grouping |
| `.WithPurpose(...)` | Semantic auto-inclusion hints |
| `.Selectable()` | User-assignable tool |
| `.Hidden()` | Keep the tool out of shared MCP/agent listings (still usable by explicitly configured profiles) |

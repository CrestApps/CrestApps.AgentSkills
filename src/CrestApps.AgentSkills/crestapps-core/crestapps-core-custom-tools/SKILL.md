---
name: crestapps-core-custom-tools
description: Skill for registering custom AI-callable tools, tool metadata, and access control in CrestApps.Core.
---

# CrestApps.Core Custom Tools - Prompt Templates

## Create Custom AI Tools

You are a CrestApps.Core expert. Generate tool classes and registration code for CrestApps.Core custom AI tools.

### Guidelines

- Inherit from `AITool`.
- Register tools with `AddCoreAITool<TTool>(name)`.
- Use `.Selectable()` only for tools that should appear in UI assignment surfaces.
- Prefer clear titles, descriptions, categories, and purpose tags.
- Catch failures inside the tool and return descriptive results instead of throwing.

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
public sealed class WeatherTool : AITool
{
    private sealed record WeatherInput(string Location, string Units = "celsius");

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var input = arguments.Deserialize<WeatherInput>();

        if (string.IsNullOrWhiteSpace(input?.Location))
        {
            return "A location is required.";
        }

        return new { Temperature = 22, Condition = "Sunny", Location = input.Location };
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

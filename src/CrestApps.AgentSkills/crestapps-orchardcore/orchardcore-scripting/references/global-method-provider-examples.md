# `IGlobalMethodProvider` examples

## Singleton-safe provider with scoped service resolution

```csharp
using System.Text.Json;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Scripting;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AITemplateMethodProvider : IGlobalMethodProvider
{
    private readonly ILogger<AITemplateMethodProvider> _logger;
    private readonly GlobalMethod _globalMethod;

    public AITemplateMethodProvider(ILogger<AITemplateMethodProvider> logger)
    {
        _logger = logger;

        _globalMethod = new GlobalMethod
        {
            Name = "renderAITemplate",
            Method = serviceProvider => (Func<string, object, object>)((templateId, arguments) =>
                ResolveTemplateContentAsync(serviceProvider, templateId, arguments).GetAwaiter().GetResult()),
            AsyncMethod = serviceProvider => (templateId, arguments) =>
                ResolveTemplateContentAsync(serviceProvider, templateId, arguments),
        };
    }

    public IEnumerable<GlobalMethod> GetMethods()
    {
        yield return _globalMethod;
    }

    private async Task<object> ResolveTemplateContentAsync(
        IServiceProvider serviceProvider,
        string templateId,
        object arguments)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return string.Empty;
        }

        var templateService = serviceProvider.GetRequiredService<ITemplateService>();
        var normalizedTemplateId = templateId.Trim();
        var rendered = await templateService.RenderAsync(normalizedTemplateId, NormalizeArguments(arguments));

        if (string.IsNullOrWhiteSpace(rendered))
        {
            _logger.LogWarning("AI template '{TemplateId}' rendered empty content.", normalizedTemplateId);
            return string.Empty;
        }

        return rendered;
    }

    private static Dictionary<string, object> NormalizeArguments(object arguments)
    {
        return arguments is string json && !string.IsNullOrWhiteSpace(json)
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(json)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
}
```

## Registration

```csharp
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGlobalMethodProvider, AITemplateMethodProvider>();
    }
}
```

## Why this pattern works

- `IScriptingManager` is singleton, so the provider must be singleton-safe.
- `ITemplateService` is scoped, so it is resolved per invocation from the delegate `serviceProvider`.
- Both sync and async callers share the same underlying async implementation.

## What to avoid

```csharp
internal sealed class BrokenMethodProvider : IGlobalMethodProvider
{
    private readonly ITemplateService _templateService;

    public BrokenMethodProvider(ITemplateService templateService)
    {
        _templateService = templateService;
    }
}
```

That pattern can fail with:

`Cannot consume scoped service 'CrestApps.Core.Templates.Services.ITemplateService' from singleton 'OrchardCore.Scripting.IScriptingManager'.`

---
name: crestapps-core-templates
description: Skill for registering and rendering general-purpose Liquid templates with CrestApps.Core.Templates.
---

# CrestApps.Core Templates - Prompt Templates

## Build General-Purpose Templates

You are a CrestApps.Core expert. Generate code and guidance for `CrestApps.Core.Templates` template discovery, Liquid rendering, and composition.

### Guidelines

- Reference the standalone `CrestApps.Core.Templates` package and register it with `AddTemplating(...)`.
- `ITemplateService` discovers templates from registered `ITemplateProvider` implementations and caches the resolved list.
- Use `ITemplateService.RenderAsync(id, arguments)` for registered templates and `ITemplateEngine.RenderAsync(template, arguments)` for an in-memory Liquid string.
- Register templates in code, scan `Templates/` and `Templates/Prompts/` with `TemplateOptions.AddDiscoveryPath(...)`, or scan embedded `Templates/*.md` resources with `AddTemplatesFromAssembly(...)`.
- The built-in `FluidTemplateEngine` uses deny-by-default member access. Register any POCO type that Liquid must inspect through `Fluid.TemplateOptions`.
- This is the general template engine. `crestapps-core-ai-templates` adds AI template sources, AI-profile template services, and built-in AI prompt definitions through `AddCoreAITemplating()`.

### Registration

```csharp
using CrestApps.Core.Templates.Extensions;

builder.Services
    .AddTemplating(options => options
        .AddDiscoveryPath(builder.Environment.ContentRootPath)
        .AddTemplate(
            "welcome",
            "Hello {{ customer }}!",
            metadata => metadata.Title = "Welcome"))
    .AddTemplatesFromAssembly(typeof(Program).Assembly, source: "MyApp");
```

`AddTemplating(...)` registers `ITemplateEngine` as `FluidTemplateEngine`, `ITemplateService` as `DefaultTemplateService`, and the built-in code, file-system, and prompt-file-system providers.

### Render a Registered Template

```csharp
using CrestApps.Core.Templates.Services;

namespace MyApp;

public sealed class WelcomeMessageService
{
    private readonly ITemplateService _templates;

    public WelcomeMessageService(ITemplateService templates)
    {
        _templates = templates;
    }

    public Task<string> RenderAsync(
        string customer,
        CancellationToken cancellationToken = default)
    {
        return _templates.RenderAsync(
            "welcome",
            new Dictionary<string, object>
            {
                ["customer"] = customer,
            },
            cancellationToken);
    }
}
```

### Compose Template Fragments

```csharp
var message = await new TemplateBuilder()
    .Append("Follow these instructions.")
    .AppendTemplate("welcome", new Dictionary<string, object>
    {
        ["customer"] = customer,
    })
    .BuildAsync(templateService, cancellationToken);
```

### Template Locations

- `Templates/*.md` — generic file-system templates.
- `Templates/Prompts/*.md` — system-prompt templates, including feature subdirectories.
- Embedded `Templates/*.md` resources — templates loaded with `AddTemplatesFromAssembly(...)`.

Markdown templates can include front matter for `Title`, `Description`, `Category`, and `IsListable`; their bodies support Liquid values such as `{{ customer }}`.

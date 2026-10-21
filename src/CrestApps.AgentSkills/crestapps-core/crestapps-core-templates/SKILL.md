---
name: crestapps-core-templates
description: Skill for the general-purpose Liquid template engine in CrestApps.Core.Templates. Covers AddTemplating and TemplateOptions (code templates, discovery paths), AddTemplatesFromAssembly for embedded templates, the Templates and Templates/Prompts folder conventions and the Kind front matter, Markdown front matter fields including Parameters, ITemplateService (ListAsync, GetAsync with aliases, RenderAsync, MergeAsync, GetByKindAsync), ITemplateEngine and FluidTemplateEngine behavior (deny-by-default member access, whitespace normalization, TryValidate), the include_prompt filter and render_ai_template tag, TemplateBuilder composition, and custom ITemplateProvider and IDynamicTemplateProvider sources for templates stored in a database. Use it when registering, rendering, composing, or storing Liquid templates; use crestapps-core-ai-templates for AI prompt and profile templates.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Templates

You are a CrestApps.Core expert. Generate code for `CrestApps.Core.Templates` discovery, Liquid rendering and composition.

## Guidelines

- `CrestApps.Core.Templates` is standalone. `services.AddTemplating(...)` registers `ITemplateEngine` (`FluidTemplateEngine`, singleton), `ITemplateService` (`DefaultTemplateService`, scoped), the Markdown `ITemplateParser`, and the code, file-system and prompt file-system providers. It also adds the `include_prompt` Liquid filter.
- `CrestApps.Core.AI` already calls `AddTemplating()` through `AddCoreAITemplating()`, which adds AI template sources and the system-prompt profile template provider. Use `crestapps-core-ai-templates` for that layer.
- `ITemplateService.RenderAsync(id, arguments)` renders a registered template; `ITemplateEngine.RenderAsync(text, arguments)` renders an in-memory Liquid string.
- `FluidTemplateEngine` uses Fluid's deny-by-default member access. Register every POCO a template reads: `services.Configure<Fluid.TemplateOptions>(o => o.MemberAccessStrategy.Register<Customer>())`. Dictionaries and primitives need no registration.
- Rendering trims each line and collapses runs of blank lines. A template that fails to parse is logged as a warning and returned unrendered; check templates with `TryValidate` before saving them.
- Template identifiers are case-insensitive. When two providers return the same `Id`, the first one read wins.
- `ITemplateService`, `ITemplateEngine` and `ITemplateProvider` members take an optional `CancellationToken`; pass it through custom providers.

## Registration

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

| Source | Template id | `Kind` |
|---|---|---|
| `AddTemplate(id, content, ...)` | the given id | set `Template.Kind` yourself |
| `{discovery path}/Templates/*.md` (not recursive) | file name without extension | from `Kind` front matter |
| `{discovery path}/Templates/Prompts/*.md` and one level of subfolders | file name; a subfolder name becomes `FeatureId` | `SystemPrompt` unless front matter sets it |
| Embedded resources under `Templates/` via `AddTemplatesFromAssembly(...)` | path after `Templates.`; a `Prompts.` prefix is dropped | `SystemPrompt` for `Prompts/`, otherwise front matter |

Mark the Markdown files as `EmbeddedResource` in the `.csproj` for `AddTemplatesFromAssembly(...)`.

## Template File Format

```markdown
---
Title: Order Follow-up
Description: Asks a customer to confirm a delivery window.
Category: Support
IsListable: true
Kind: SystemPrompt
Parameters:
  - customer_name: The customer's display name
  - order_number: The order being discussed
---
You are writing to {{ customer_name | default: "the customer" }} about order {{ order_number }}.
{% if delivery_window %}
Offer the delivery window {{ delivery_window }}.
{% endif %}
```

`Title`, `Description`, `Category`, `IsListable` (default `true`) and `Parameters` map to `TemplateMetadata`; `Kind` maps to `Template.Kind`; any other key lands in `TemplateMetadata.AdditionalProperties`. A missing `Title` is derived from the id.

## Render and Compose

```csharp
using CrestApps.Core.Templates;
using CrestApps.Core.Templates.Services;

namespace MyApp.Messaging;

public sealed class FollowUpMessageService
{
    private readonly ITemplateService _templates;

    public FollowUpMessageService(ITemplateService templates)
    {
        _templates = templates;
    }

    public Task<string> RenderAsync(string customer, string order, CancellationToken cancellationToken = default)
    {
        return new TemplateBuilder()
            .WithSeparator("\n\n")
            .Append("Keep the reply under 120 words.")
            .AppendTemplate("order-follow-up", new Dictionary<string, object>
            {
                ["customer_name"] = customer,
                ["order_number"] = order,
            })
            .BuildAsync(_templates, cancellationToken);
    }
}
```

- `MergeAsync(ids, arguments, separator)` renders several templates with the same arguments and skips any that render to `null`.
- `GetByKindAsync(kind)` (in `TemplateServiceExtensions`) lists templates of one kind, for example `SystemPrompt`.
- `RenderAsync` throws `KeyNotFoundException` for an unknown id.
- Inside a template, `{{ "safety-rules" | include_prompt }}` renders another template without arguments, and `{% render_ai_template "safety-rules" %}` renders it in a child scope that sees the caller's variables. Both guard against recursion: `include_prompt` returns nothing for a template that is already rendering, and `render_ai_template` stops at a depth of ten.

## Store Templates in a Database

A provider whose templates change at runtime implements `IDynamicTemplateProvider` (a marker over `ITemplateProvider`). `DefaultTemplateService` reads static providers first, so a stored template never replaces a shipped template with the same id. Use `Aliases` to keep old identifiers resolvable; `GetAsync` matches an id first and an alias second.

```csharp
using CrestApps.Core.Services;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Parsing;
using CrestApps.Core.Templates.Providers;

namespace MyApp.Templates;

public sealed class StoredTemplateProvider : IDynamicTemplateProvider
{
    private readonly ICatalog<StoredTemplate> _catalog;
    private readonly ITemplateParser _parser;

    public StoredTemplateProvider(ICatalog<StoredTemplate> catalog, IEnumerable<ITemplateParser> parsers)
    {
        _catalog = catalog;
        _parser = parsers.First(parser => parser.SupportedExtensions.Contains(".md"));
    }

    public async Task<IReadOnlyList<Template>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var records = await _catalog.GetAllAsync(cancellationToken);

        return records
            .Select(record =>
            {
                var parsed = _parser.Parse(record.Markdown);

                return new Template
                {
                    Id = record.Name,
                    Aliases = [$"stored:{record.ItemId}"],
                    Metadata = parsed.Metadata,
                    Content = parsed.Body,
                    Source = "Database",
                };
            })
            .ToArray();
    }
}
```

```csharp
builder.Services.AddScoped<ITemplateProvider, StoredTemplateProvider>();
```

`StoredTemplate` is any `CatalogItem` with `Name` and `Markdown` properties registered through a store package. Register dynamic providers as scoped, since `ITemplateService` is scoped and reads them in every scope. A custom template service that caches for longer than a scope should override `GetStaticTemplatesAsync` and cache only what it returns.

## Upgrading from 1.x

- New: `IDynamicTemplateProvider`, `Template.Aliases`, and `DefaultTemplateService.GetStaticTemplatesAsync`. Implement `IDynamicTemplateProvider` on providers backed by editable storage so edits apply on the next request.
- The system-prompt AI profile template provider moved from the sample hosts into `CrestApps.Core.AI` and is registered by `AddCoreAITemplating()`; template ids are now the template name, with the old `ai-profile:` ids kept as aliases.

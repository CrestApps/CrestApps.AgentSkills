---
name: crestapps-core-ai-templates
description: Skill for Liquid prompt templates in CrestApps.Core 2.0. Covers AddCoreAITemplating and AddTemplating registration, Markdown templates with front matter under Templates and Templates Prompts, AddTemplatesFromAssembly, code and discovery-path templates through TemplateOptions, ITemplateService ListAsync, GetAsync, RenderAsync and MergeAsync, ITemplateEngine validation, custom ITemplateProvider and IDynamicTemplateProvider sources, template Aliases, the AIProfileSystemPromptTemplateProvider that exposes SystemPrompt AI profile templates by name, selecting templates on a profile with PromptTemplateMetadata, seeding editable defaults embedded under SystemPromptDefaults with IAISystemPromptTemplateSeeder and AddCoreAISystemPromptTemplateDefaults, listing a template category with AITemplateCategoryExtensions, and how templates compose with personas. Use it when authoring reusable system prompts, loading templates from a database, or fixing a selected template that does not render.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Templates

Prompts are Markdown files with YAML front matter, rendered with Liquid (Fluid) and discovered from embedded resources, the file system, code, or custom providers.

## Guidelines

- `AddCoreAIServices()` already chains `AddCoreAITemplating()`; call it directly only when using templating on its own.
- Keep each template to one concern (tone, safety, RAG rules) and compose at runtime with `MergeAsync` or profile selections.
- Put system prompts under `Templates/Prompts/` (their `Kind` defaults to `SystemPrompt`). Files directly under `Templates/` must declare `Kind`. Generic `Templates/` discovery is not recursive.
- Use a persona (`crestapps-core-ai-personas`) for identity and voice instead of copying the same persona prompt into many templates.
- All `ITemplateService` and `ITemplateProvider` methods take an optional `CancellationToken`; pass it through.

## Registration

```csharp
using CrestApps.Core.Templates.Extensions;

builder.Services.AddCoreAITemplating(options =>
{
    options.AddDiscoveryPath("/app/templates");
});

// Embedded .md resources under Templates/ in an assembly.
builder.Services.AddTemplatesFromAssembly(typeof(Program).Assembly, source: "MyApp");
```

`AddCoreAITemplating()` builds on `AddTemplating()` and adds the `Profile` and `SystemPrompt` template sources, the AI profile template manager, and `AIProfileSystemPromptTemplateProvider`.

| Service | Purpose |
|---|---|
| `ITemplateService` (`DefaultTemplateService`) | Discover, render and merge |
| `ITemplateEngine` (`FluidTemplateEngine`) | Render and validate Liquid |
| `ITemplateParser` | Parse Markdown front matter |
| `OptionsTemplateProvider`, `FileSystemTemplateProvider`, `PromptsFileSystemTemplateProvider` | Code, `Templates/`, `Templates/Prompts/` |
| `AIProfileSystemPromptTemplateProvider` | `SystemPrompt` AI profile templates from the profile template store |

## Template Example

```markdown
---
Title: Customer Support Assistant
Description: Prompt for a customer support chatbot
Category: Support
IsListable: true
---
You are a customer support assistant for {{ company_name | default: "our company" }}.

{% if support_hours %}
Support hours are {{ support_hours }}.
{% endif %}
```

Front matter fields are `Title`, `Description`, `Category`, `IsListable` (default `true`) and `Kind`. The file name without extension is the template id. `ITemplateParser.Parse(content)` returns a `TemplateParseResult` with `Metadata` and `Body`.

## Using Templates

```csharp
using CrestApps.Core.Templates.Services;

var templates = await templateService.ListAsync(cancellationToken);

var prompt = await templateService.RenderAsync(
    "customer-support",
    new Dictionary<string, object> { ["company_name"] = "Contoso" },
    cancellationToken);

var systemPrompt = await templateService.MergeAsync(
    ["base-personality", "safety-rules", "rag-instructions"],
    new Dictionary<string, object> { ["user_name"] = userName },
    separator: "\n\n---\n\n",
    cancellationToken);
```

Validate user-authored Liquid before saving:

```csharp
using CrestApps.Core.Templates.Rendering;

if (!templateEngine.TryValidate(content, out var errors))
{
    // errors is IList<string>
}
```

## Selecting Templates on a Profile

```csharp
profile.Put(new PromptTemplateMetadata
{
    Templates =
    [
        new PromptTemplateSelectionEntry
        {
            TemplateId = "customer-support",
            Parameters = new Dictionary<string, object> { ["company_name"] = "Contoso" },
        },
    ],
});
```

The profile context handler renders the selections into the system message. A linked persona's block is placed before them, and the profile's own system message after them.

## Seeded System Prompt Defaults

Speaking styles and guardrail topics are System Prompt templates in their own category (`Speaking style`, `Guardrail topic`). Their defaults are not served read-only: they ship as Markdown embedded under `SystemPromptDefaults` and are copied once into the saved AI profile templates, where users can edit or delete them. `AddSpeakingStyles()` and `AddGuardrails()` register the framework's defaults.

```csharp
using CrestApps.Core.AI.Services;

// Once, from a migration or a setup step, with the AI profile template store registered.
var seeder = serviceProvider.GetRequiredService<IAISystemPromptTemplateSeeder>();
IReadOnlyList<string> created = await seeder.SeedAsync(); // or SeedAsync(AISpeakingStyles.Category)
```

- A saved template with the same name is never overwritten; one deleted after seeding comes back if the seeder runs again. `GetDefaultsAsync(category)` reads the defaults without saving.
- Ship your own defaults: embed Markdown under `SystemPromptDefaults` (for example `<EmbeddedResource Include="SystemPromptDefaults\**\*" />`) and call `services.AddCoreAISystemPromptTemplateDefaults(typeof(Startup).Assembly)`, optionally with a category to read only that category. Front matter sets `Title`, `Description`, `Category` and `IsListable`; the body is the system message.

```markdown
---
Title: Echo Verification
Description: Reads back what the customer gave before acting on it
IsListable: false
Category: Speaking style
---

## Read back what you are given

When the customer gives you something you will act on or record, repeat it back once and ask them to confirm.
```

Fill a picker from one category, file-based and saved templates together, deduplicated by id and ordered by title:

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Models;

IReadOnlyList<AITemplateOption> styles = await templateService.ListOptionsByCategoryAsync(AISpeakingStyles.Category, cancellationToken);
IReadOnlyList<Template> topics = await templateService.ListByCategoryAsync(AIGuardrailTopics.Category, cancellationToken);
```

`AITemplateOption` is `(Id, Title, Description)`; `AITemplateCategoryExtensions.GetTitle(template)` falls back to the id.

## Custom Providers

```csharp
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Parsing;
using CrestApps.Core.Templates.Providers;

public sealed class DatabaseTemplateProvider : IDynamicTemplateProvider
{
    private readonly IPromptRecordStore _store;
    private readonly ITemplateParser _parser;

    public DatabaseTemplateProvider(IPromptRecordStore store, ITemplateParser parser)
    {
        _store = store;
        _parser = parser;
    }

    public async Task<IReadOnlyList<Template>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = new List<Template>();

        foreach (var record in await _store.ListAsync(cancellationToken))
        {
            var parsed = _parser.Parse(record.Content);

            templates.Add(new Template
            {
                Id = record.Name,
                Aliases = [$"legacy:{record.Id}"],
                Metadata = parsed.Metadata,
                Content = parsed.Body,
                Kind = "SystemPrompt",
                Source = "Database",
            });
        }

        return templates;
    }
}
```

```csharp
builder.Services.AddScoped<ITemplateProvider, DatabaseTemplateProvider>();
```

`IPromptRecordStore` stands for the application's own storage. Use plain `ITemplateProvider` for templates that never change while the app runs.

- `IDynamicTemplateProvider` adds no members. `DefaultTemplateService` reads every static provider first, so a dynamic template cannot replace a shipped template with the same id, and dynamic providers are read again each scope so edits apply on the next request.
- A service that caches templates longer than a scope overrides the protected `GetStaticTemplatesAsync` and caches only those.
- `GetAsync` and `RenderAsync` match `Id` first and `Aliases` second. Among static providers, the first template with an id wins.

## Troubleshooting

- **`KeyNotFoundException` rendering a selected system prompt template**: hosts on 1.x had no `AIProfileSystemPromptTemplateProvider`. In 2.0 it ships with `AddCoreAITemplating()`; without a profile template store it returns nothing.
- **A template under `Templates/` is ignored**: it lacks `Kind`, or it sits in a subfolder.

## Upgrading from 1.x

- `AIProfileSystemPromptTemplateProvider` moved from the sample hosts into `CrestApps.Core.AI` and is registered by `AddCoreAITemplating()`. Remove any copied provider from your host.
- A `SystemPrompt` profile template's identifier is now its name. Earlier `ai-profile:` identifiers are kept as `Template.Aliases`, so stored selections still render.
- Implement `IDynamicTemplateProvider` for user-editable templates so they cannot shadow shipped ones and are re-read every scope.

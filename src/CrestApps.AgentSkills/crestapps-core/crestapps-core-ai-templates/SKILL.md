---
name: crestapps-core-ai-templates
description: Skill for Liquid-based prompt templates, template discovery, and prompt composition in CrestApps.Core.
---

# CrestApps.Core AI Templates - Prompt Templates

## Build Prompt Templates

You are a CrestApps.Core expert. Generate template files and registration code for CrestApps.Core prompt templates.

### Guidelines

- `AddCoreAIServices()` already chains `AddCoreAITemplating()`.
- Use markdown files with YAML front matter for reusable prompts.
- Use Liquid syntax for dynamic values.
- Store prompt templates under `Templates/Prompts/` when they are system prompts.
- Use generic `Templates/` plus `Kind` when the template is not prompt-only.
- Merge templates when multiple prompt fragments need to become one final system message.

### Direct Registration

```csharp
builder.Services.AddCoreAITemplating();
```

### Embedded Template Registration

```csharp
builder.Services.AddTemplatesFromAssembly(typeof(MyClass).Assembly, source: "MyApp");
```

### Template Example

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

### Key Template Service Methods

| Method | Use |
|---|---|
| `ListAsync()` | Discover templates |
| `GetAsync(id)` | Load one template |
| `RenderAsync(id, args)` | Render one template |
| `MergeAsync(ids, args)` | Combine multiple templates |

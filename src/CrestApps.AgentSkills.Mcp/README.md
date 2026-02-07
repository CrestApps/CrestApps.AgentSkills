# CrestApps.AgentSkills.Mcp

A generic, reusable **MCP (Model Context Protocol)** engine that discovers and exposes **Agent Skills** as MCP Prompts and Resources. Built on the [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

This package is **framework-agnostic** and independent of Orchard Core. Any application can use it to expose its own skills via MCP.

## Role in CrestApps.AgentSkills

`CrestApps.AgentSkills.Mcp` is the generic MCP engine in the CrestApps.AgentSkills repository. It solves the problem of repeatedly implementing skill file parsing and MCP provider wiring by offering a reusable, framework-agnostic library.

## Who Is This For?

- **Any .NET application** that wants to expose Agent Skills via MCP
- Developers building custom AI skill libraries
- Teams that want a reusable MCP skill engine without Orchard Core dependencies

For Orchard Core–specific skills, see the companion package [`CrestApps.OrchardCore.AgentSkills.Mcp`](../CrestApps.OrchardCore.AgentSkills.Mcp/README.md), which builds on top of this project and bundles Orchard Core skills.

## Install

```bash
dotnet add package CrestApps.AgentSkills.Mcp
```

## Usage

### Register Skills with an MCP Server

```csharp
builder.Services.AddMcpServer(mcp =>
{
    mcp.AddAgentSkills();
});
```

### Register Services Only (No Eager Loading)

To register the skill services in DI without eagerly loading them:

```csharp
builder.Services.AddAgentSkillServices();
```

The consumer is then responsible for resolving `IPromptProvider` and `IResourceProvider` and attaching them to the MCP server.

### With Custom Path

```csharp
builder.Services.AddMcpServer(mcp =>
{
    mcp.AddAgentSkills(options =>
    {
        options.Path = "./my-skills";
    });
});
```

### Resolving Providers from DI

The file store and providers are registered as singletons and can be injected:

```csharp
public sealed class MyService
{
    private readonly IPromptProvider _promptProvider;
    private readonly IResourceProvider _resourceProvider;

    public MyService(
        IPromptProvider promptProvider,
        IResourceProvider resourceProvider)
    {
        _promptProvider = promptProvider;
        _resourceProvider = resourceProvider;
    }

    public async Task ListPromptsAsync()
    {
        var prompts = await _promptProvider.GetPromptsAsync();
        // ...
    }
}
```

## Supported Skill Formats

Skills are discovered from subdirectories under the configured skills path. Each skill directory must contain one of the following skill definition files:

### Markdown (`.md`)

A `SKILL.md` file with YAML front-matter:

```markdown
---
name: my-skill
description: A description of my skill.
---
# Prompt content

This is the body that becomes the MCP prompt.
```

### YAML (`.yaml` / `.yml`)

A `SKILL.yaml` or `SKILL.yml` file:

```yaml
name: my-skill
description: A description of my skill.
body: |
  # Prompt content
  This is the body that becomes the MCP prompt.
```

### Required Fields

Both formats require:

| Field | Description |
|---|---|
| `name` | Unique skill identifier |
| `description` | Human-readable description |

The `body` field (or Markdown body after front-matter) provides the prompt content.

### Reference Files

Each skill directory may include a `references/` subdirectory containing additional `.md` files. These are exposed as MCP Resources:

```
.agents/skills/
  my-skill/
    SKILL.md          (or SKILL.yaml / SKILL.yml)
    references/
      example1.md
      example2.md
```

### Links

- [Agent Skills Specification](https://agentskills.io/specification)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)

## What Gets Exposed

| MCP Type | Source | Description |
|---|---|---|
| **Prompts** | Skill file body content | Prompt templates from `SKILL.md` body or `SKILL.yaml` `body` field |
| **Resources** | Skill files | Full skill file content (metadata + body) |
| **Resources** | `references/*.md` files | Additional reference documents for each skill |

## Architecture

### Services

| Service | Lifetime | Purpose |
|---|---|---|
| `ISkillFileStore` | Singleton | Abstraction for skill file access |
| `PhysicalSkillFileStore` | Singleton | Concrete implementation using `System.IO` |
| `IPromptProvider` | Singleton | Reads skill files → cached `McpServerPrompt` instances |
| `IResourceProvider` | Singleton | Reads skill + reference files → cached `McpServerResource` instances |

### Parsers

| Parser | Formats | Purpose |
|---|---|---|
| `SkillFrontMatterParser` | `.md` | Parses YAML front-matter from Markdown files |
| `SkillYamlParser` | `.yaml`, `.yml` | Parses YAML skill definitions |
| `SkillFileParser` | All | Unified parser that detects format and delegates |

## How It Works

1. Place skill directories under `.agents/skills/` (or a custom path).
2. Each skill directory contains a `SKILL.md`, `SKILL.yaml`, or `SKILL.yml` file.
3. `AddAgentSkills()` registers `ISkillFileStore`, `IPromptProvider`, and `IResourceProvider` as singletons.
4. At runtime, providers discover skill files, parse metadata, and create MCP prompts/resources.
5. Results are cached after the first call for optimal performance.
6. MCP clients can discover and use these prompts and resources via the MCP protocol.

## Development

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Validate Skills Locally

Place your skill files in a directory and use the test infrastructure:

```csharp
var fileStore = new PhysicalSkillFileStore("/path/to/skills");
IPromptProvider provider = new SkillPromptProvider(fileStore, NullLogger<SkillPromptProvider>.Instance);
var prompts = await provider.GetPromptsAsync();
```

## Relationship to Other Packages

```
CrestApps.AgentSkills.Mcp              ← Generic, reusable MCP engine (this package)
        ↑
CrestApps.OrchardCore.AgentSkills.Mcp  ← Orchard Core distribution with bundled skills
```

| Package | Purpose |
|---|---|
| `CrestApps.AgentSkills.Mcp` | Generic MCP engine — any application can use this |
| `CrestApps.OrchardCore.AgentSkills.Mcp` | Orchard Core–specific — bundles Orchard Core skills |
| `CrestApps.OrchardCore.AgentSkills` | Dev/design-time — copies skills to solution root |

## Requirements

- .NET 10.0+
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol` NuGet package)
- An MCP server host (e.g., ASP.NET Core with `ModelContextProtocol.AspNetCore`)

## License

This project is licensed under the [MIT License](../../LICENSE).

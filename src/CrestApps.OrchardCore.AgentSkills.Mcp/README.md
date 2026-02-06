# CrestApps.OrchardCore.AgentSkills.Mcp

A runtime NuGet package that exposes Orchard Core agent skills to **MCP (Model Context Protocol) servers** using the [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

This package loads skill files at runtime from the package output directory and registers them as MCP prompts and resources via dedicated provider classes. It does **not** copy files to the solution — use the companion `CrestApps.OrchardCore.AgentSkills` package for local AI authoring.

## Install

```bash
dotnet add package CrestApps.OrchardCore.AgentSkills.Mcp
```

## Usage

```csharp
builder.Services.AddMcpServer(mcp =>
{
    mcp.AddOrchardCoreSkills();
});
```

### With Custom Path

```csharp
builder.Services.AddMcpServer(mcp =>
{
    mcp.AddOrchardCoreSkills(options =>
    {
        options.Path = "./custom-skills";
    });
});
```

## Requirements

- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol` NuGet package)
- An MCP server host (e.g., ASP.NET Core with `ModelContextProtocol.AspNetCore`)

## Architecture

### Providers

The package uses two provider classes for clean separation of concerns:

| Provider | Purpose |
|---|---|
| `FileSystemSkillPromptProvider` | Reads `prompts.md` files from skill directories and produces `McpServerPrompt` instances |
| `FileSystemSkillResourceProvider` | Reads `skill.yaml` and `examples/*.md` files and produces `McpServerResource` instances |

### What Gets Exposed

| MCP Type | Source | Description |
|---|---|---|
| **Prompts** | `prompts.md` files | Orchard Core prompt templates for content types, modules, recipes |
| **Resources** | `skill.yaml` files | Skill metadata definitions with inputs/outputs |
| **Resources** | `examples/*.md` files | Code examples for each skill category |

### Skills Included

| Skill | Description |
|---|---|
| `orchardcore.content-types` | Content type creation, parts, fields, and stereotypes |
| `orchardcore.modules` | Module scaffolding, features, manifests, and startup |
| `orchardcore.recipes` | Recipe structure, steps, and content definitions |
| `orchardcore.deployments` | Deployment plans and import/export |
| `orchardcore.ai` | AI service integration and MCP enablement |

## How It Works

1. Skill files are packed into the NuGet package under `contentFiles/any/any/.agents/skills/`.
2. NuGet copies these files into the project output directory on restore.
3. At runtime, `FileSystemSkillPromptProvider` reads `prompts.md` files and `FileSystemSkillResourceProvider` reads `skill.yaml` and example files.
4. The `AddOrchardCoreSkills()` extension registers the resulting MCP prompts and resources.
5. MCP clients can then discover and use these prompts and resources.

## Companion Packages

| Package | Purpose |
|---|---|
| `CrestApps.OrchardCore.AgentSkills` | Dev/design-time — copies skills to solution root for local AI authoring |
| `CrestApps.OrchardCore.AgentSkills.Mcp` | Runtime — exposes skills via MCP server |

Install both for the full experience.

## License

This project is licensed under the [MIT License](../../LICENSE).

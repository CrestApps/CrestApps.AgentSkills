# CrestApps.OrchardCore.AgentSkills.Mcp

A runtime NuGet package that exposes Orchard Core agent skills to **MCP (Model Context Protocol) servers** using the [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

This package loads skill files at runtime via OrchardCore's `FileSystemStore` and registers them as MCP prompts and resources through dedicated provider classes. It does **not** copy files to the solution — use the companion `CrestApps.OrchardCore.AgentSkills` package for local AI authoring.

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

### Register Services Only (No Eager Loading)

To register the skill services in DI without eagerly loading them:

```csharp
builder.Services.AddOrchardCoreAgentSkillServices();
```

The consumer is then responsible for resolving `FileSystemSkillPromptProvider` and `FileSystemSkillResourceProvider` and attaching them to the MCP server.

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

### Resolving Providers from DI

The file store and providers are registered as singletons and can be injected:

```csharp
public sealed class MyService
{
    private readonly FileSystemSkillPromptProvider _promptProvider;
    private readonly FileSystemSkillResourceProvider _resourceProvider;

    public MyService(
        FileSystemSkillPromptProvider promptProvider,
        FileSystemSkillResourceProvider resourceProvider)
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

## Requirements

- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol` NuGet package)
- [OrchardCore.FileStorage.FileSystem](https://www.nuget.org/packages/OrchardCore.FileStorage.FileSystem) for file access
- An MCP server host (e.g., ASP.NET Core with `ModelContextProtocol.AspNetCore`)

## Architecture

### Services

| Service | Lifetime | Purpose |
|---|---|---|
| `IMcpResourceFileStore` | Singleton | Marker interface wrapping OrchardCore `FileSystemStore` for skill file access |
| `McpSkillFileStore` | Singleton | Concrete implementation rooted at the skills directory |
| `FileSystemSkillPromptProvider` | Singleton | Reads `prompts.md` → cached `McpServerPrompt` instances |
| `FileSystemSkillResourceProvider` | Singleton | Reads `skill.yaml` + `examples/*.md` → cached `McpServerResource` instances |

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
3. `AddOrchardCoreSkills()` registers `IMcpResourceFileStore`, `FileSystemSkillPromptProvider`, and `FileSystemSkillResourceProvider` as singletons.
4. At runtime, providers use OrchardCore `FileSystemStore` to read files — results are cached after the first call.
5. MCP clients can discover and use these prompts and resources via the MCP protocol.

## Companion Packages

| Package | Purpose |
|---|---|
| `CrestApps.OrchardCore.AgentSkills` | Dev/design-time — copies skills to solution root for local AI authoring |
| `CrestApps.OrchardCore.AgentSkills.Mcp` | Runtime — exposes skills via MCP server |

Install both for the full experience.

## License

This project is licensed under the [MIT License](../../LICENSE).

# CrestApps.AgentSkills.Mcp.OrchardCore

A runtime NuGet package that exposes Orchard Core agent skills to **MCP (Model Context Protocol) servers** using the [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

This package loads skill files at runtime via OrchardCore's `FileSystemStore` and registers them as MCP prompts and resources through dedicated provider classes. It does **not** copy files to the solution — use the companion `CrestApps.AgentSkills.OrchardCore` package for local AI authoring.

## Role in CrestApps.AgentSkills

`CrestApps.AgentSkills.Mcp.OrchardCore` is the Orchard Core runtime project in the CrestApps.AgentSkills repository. It solves the problem of exposing Orchard Core skills via MCP by loading the packaged skills and wiring them into an MCP server at runtime.

## Install

```bash
dotnet add package CrestApps.AgentSkills.Mcp.OrchardCore
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

The consumer is then responsible for resolving `IMcpPromptProvider` and `IMcpResourceProvider` and attaching them to the MCP server.

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
    private readonly IMcpPromptProvider _promptProvider;
    private readonly IMcpResourceProvider _resourceProvider;

    public MyService(
        IMcpPromptProvider promptProvider,
        IMcpResourceProvider resourceProvider)
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
| `IMcpPromptProvider` | Singleton | Reads `SKILL.md` files → cached `McpServerPrompt` instances |
| `IMcpResourceProvider` | Singleton | Reads `SKILL.md` + `references/*.md` files → cached `McpServerResource` instances |

### What Gets Exposed

| MCP Type | Source | Description |
|---|---|---|
| **Prompts** | `SKILL.md` files | Skill body content from Markdown files with front-matter |
| **Resources** | `SKILL.md` files | Full skill file content (front-matter + body) |
| **Resources** | `references/*.md` files | Additional reference documents for each skill |

### Skills Included

The package includes all Orchard Core skills organized under the `orchardcore/` directory:

| Skill | Description |
|---|---|
| `orchardcore.ai` | AI service integration and MCP enablement |
| `orchardcore.background-tasks` | Background task implementation and scheduling |
| `orchardcore.content-fields` | Content field types and configurations |
| `orchardcore.content-parts` | Content parts with settings and migrations |
| `orchardcore.content-queries` | Content querying with YesSql indexes |
| `orchardcore.content-types` | Content type creation, parts, fields, and stereotypes |
| `orchardcore.data-migrations` | Database migrations and content type updates |
| `orchardcore.deployments` | Deployment plans and import/export |
| `orchardcore.display-management` | Display drivers and view models |
| `orchardcore.graphql` | GraphQL API queries and custom types |
| `orchardcore.liquid` | Liquid template tags and filters |
| `orchardcore.localization` | Multi-language support and PO files |
| `orchardcore.media` | Media library and storage configuration |
| `orchardcore.modules` | Module scaffolding, features, and manifests |
| `orchardcore.navigation` | Admin menus and navigation providers |
| `orchardcore.placement` | Shape placement and alternates |
| `orchardcore.recipes` | Recipe structure, steps, and content definitions |
| `orchardcore.search-indexing` | Lucene/Elasticsearch search configuration |
| `orchardcore.security` | Authentication, authorization, and security headers |
| `orchardcore.setup` | Project creation and configuration |
| `orchardcore.shapes` | Shape rendering and templates |
| `orchardcore.taxonomies` | Hierarchical taxonomy management |
| `orchardcore.tenants` | Multi-tenancy configuration |
| `orchardcore.theming` | Theme scaffolding and layouts |
| `orchardcore.users-roles` | User management and permissions |
| `orchardcore.widgets` | Widget layers and placement |
| `orchardcore.workflows` | Workflow types and custom activities |

## How It Works

1. Skill files are packed into the NuGet package under `contentFiles/any/any/.agents/skills/orchardcore/`.
2. NuGet copies these files into the project output directory on restore (typically `bin/<config>/.agents/skills/orchardcore/`).
3. `AddOrchardCoreSkills()` registers `IMcpResourceFileStore`, `IMcpPromptProvider`, and `IMcpResourceProvider` as singletons.
4. At runtime, providers use OrchardCore `FileSystemStore` to read files from the output directory — results are cached after the first call.
5. MCP clients can discover and use these prompts and resources via the MCP protocol.

## Companion Packages

| Package | Purpose |
|---|---|
| `CrestApps.AgentSkills.OrchardCore` | Dev/design-time — copies skills to solution root for local AI authoring |
| `CrestApps.AgentSkills.Mcp.OrchardCore` | Runtime — exposes skills via MCP server |

Install both for the full experience.

## License

This project is licensed under the [MIT License](../../LICENSE).

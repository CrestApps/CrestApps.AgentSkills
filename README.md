# CrestApps.OrchardCore.AgentSkills

Distributes shared **AI agent instruction and guardrail files** for Orchard Core development, available as two complementary NuGet packages:

| Package | Purpose |
|---|---|
| [`CrestApps.OrchardCore.AgentSkills`](src/CrestApps.OrchardCore.AgentSkills/) | **Dev / design-time** — copies skills into solution-root `.agents/skills` for local AI authoring tools (GitHub Copilot, Cursor, Cline) |
| [`CrestApps.OrchardCore.AgentSkills.Mcp`](src/CrestApps.OrchardCore.AgentSkills.Mcp/) | **Runtime / MCP server** — loads skills at runtime via OrchardCore `FileSystemStore` and exposes them as MCP prompts and resources |

## Quick Start

### Local AI Authoring

```bash
dotnet add package CrestApps.OrchardCore.AgentSkills
dotnet build
```

After the first **build** after install, the solution root will contain:

```
.agents/
  skills/
    orchardcore.content-types/
      SKILL.md
      references/
    orchardcore.modules/
      SKILL.md
      references/
    orchardcore.recipes/
      SKILL.md
    ...
```

- Files are copied on the **first build** after install/update, before compilation starts (`BeforeTargets="PrepareForBuild;CompileDesignTime"`).
- In **Visual Studio**, a design-time build fires automatically after package install, so the folder appears immediately.
- `dotnet restore` alone does **not** trigger the copy — this is a fundamental NuGet/MSBuild limitation.
- **No runtime dependency** — purely for development and AI tooling guidance.
- Files are refreshed when the package is updated.

### MCP Server Hosting

```bash
dotnet add package CrestApps.OrchardCore.AgentSkills.Mcp
```

```csharp
builder.Services.AddMcpServer(mcp =>
{
    mcp.AddOrchardCoreSkills();
});
```

- Loads skills at runtime via OrchardCore `FileSystemStore`.
- `IMcpResourceFileStore`, `FileSystemSkillPromptProvider`, and `FileSystemSkillResourceProvider` registered as **singletons** — no repeated file reads.
- No file copying to solution.

### Full Experience

Install both packages to get local AI authoring **and** MCP server support.

## Skill Categories

| Skill | Description |
|---|---|
| `orchardcore.ai` | AI service integration (CrestApps.OrchardCore.AI), provider configuration, MCP server setup |
| `orchardcore.background-tasks` | `IBackgroundTask` implementation, cron scheduling, service resolution patterns |
| `orchardcore.content-fields` | Every built-in content field type with all settings, editors, and display modes |
| `orchardcore.content-parts` | Every built-in content part with all settings and migration patterns |
| `orchardcore.content-queries` | Querying content with `ContentItemIndex`, custom YesSql indexes, `ISession` and `IContentManager` |
| `orchardcore.content-types` | Creating and managing content types, parts, and fields |
| `orchardcore.data-migrations` | Content type/part/field migrations, YesSql index tables, `UpdateFrom` pattern |
| `orchardcore.deployments` | Deployment plans, remote deployment, custom `IDeploymentSource` |
| `orchardcore.display-management` | Display drivers (`ContentPartDisplayDriver<T>`), view models, shapes |
| `orchardcore.graphql` | GraphQL API queries, filtering, pagination, custom `ObjectGraphType` |
| `orchardcore.liquid` | OC-specific Liquid tags/filters, global objects, custom `ILiquidFilter` |
| `orchardcore.localization` | PO files, `IStringLocalizer`, culture picker, content localization |
| `orchardcore.media` | Media library configuration, Azure Blob Storage, media profiles |
| `orchardcore.modules` | Module scaffolding, features, manifests, startup configuration |
| `orchardcore.navigation` | Admin menus via `INavigationProvider`, menu recipes, breadcrumbs |
| `orchardcore.placement` | `placement.json` configuration, zones, alternates, wrappers |
| `orchardcore.recipes` | Recipe structure, steps, content definitions, and content items |
| `orchardcore.search-indexing` | Lucene/Elasticsearch indexes, search queries, custom index handlers |
| `orchardcore.security` | CORS, OpenID Connect, security headers, content authorization |
| `orchardcore.setup` | Project creation, `Program.cs` configuration, Blog recipe testing |
| `orchardcore.shapes` | Shape rendering pipeline, templates, alternates, wrappers, `IShapeFactory` |
| `orchardcore.taxonomies` | Hierarchical/flat taxonomies, TaxonomyField, term content types |
| `orchardcore.tenants` | Multi-tenancy setup, SaaS recipe, tenant creation, feature profiles |
| `orchardcore.theming` | Theme scaffolding, layouts, zones, shape templates, resource manifests |
| `orchardcore.users-roles` | Permission providers, role recipes, custom user settings, external auth |
| `orchardcore.widgets` | Widget stereotype, layers/layer rules, zone placement, FlowPart |
| `orchardcore.workflows` | Workflow types, custom activities/events, timer expressions |

## Skill Format (agentskills.io specification)

Each skill is defined in a single **`SKILL.md`** file inside a skill directory under `src/skills/.agents/skills/`. The file must contain YAML front-matter with at least `name` and `description` fields:

```md
---
name: orchardcore.example
description: A description of what this skill does and when to use it.
---

# Skill Title

Skill content goes here (guidelines, code templates, examples, etc.)
```

### Requirements

- The `SKILL.md` file **must** start with `---` and contain a closing `---` delimiter
- The `name` field **must** match the directory name exactly
- The `description` field is required and should clearly explain the skill's purpose
- Additional reference material can be placed in a `references/` subdirectory as `.md` files

## Repository Structure

```
src/
├─ skills/.agents/skills/                        ← Central skill content (single source of truth)
│  ├─ orchardcore.content-types/
│  │  ├─ SKILL.md                               ← Skill definition (front-matter + body)
│  │  └─ references/                            ← Optional reference/example files
│  ├─ orchardcore.modules/
│  ├─ orchardcore.recipes/
│  └─ ...
│
├─ CrestApps.OrchardCore.AgentSkills/            ← Dev package
│  ├─ buildTransitive/                           ← MSBuild .targets for solution-root copy
│  ├─ README.md
│  └─ CrestApps.OrchardCore.AgentSkills.csproj
│
└─ CrestApps.OrchardCore.AgentSkills.Mcp/        ← MCP runtime package
   ├─ Extensions/                                ← MCP extension methods
   ├─ Providers/                                 ← Prompt & resource providers
   ├─ Services/                                  ← IMcpResourceFileStore, McpSkillFileStore, SkillFrontMatterParser
   ├─ README.md
   └─ CrestApps.OrchardCore.AgentSkills.Mcp.csproj
```

Both NuGet packages pack skill files from the central `src/skills/` directory — the dev package packs them under `skills/` (MSBuild copy only), while the MCP package packs them under `contentFiles/any/any/.agents/skills/`.

## Build & Test

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

### Build

```bash
dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false
```

### Run Tests

```bash
dotnet test -c Release --verbosity normal
```

### Validate Skills Locally

You can verify all skill files are valid before submitting a PR:

```bash
for dir in src/skills/.agents/skills/*/; do
  name=$(basename "$dir")
  if [ ! -f "$dir/SKILL.md" ]; then echo "FAIL: $name missing SKILL.md"; continue; fi
  if ! head -1 "$dir/SKILL.md" | grep -q "^---$"; then echo "FAIL: $name bad front-matter"; continue; fi
  echo "OK: $name"
done
```

> **Warning:**
> This package will **always overwrite** files in the `.agents/` folder in your solution root.
> Any changes you make to files inside `.agents/` that were generated by this package **will be lost** when you build after installing or updating this NuGet package.
> Do **not** modify files added by this package inside `.agents/`. Treat them as read-only.

## License

This project is licensed under the [MIT License](LICENSE).

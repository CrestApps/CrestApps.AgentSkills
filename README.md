# CrestApps.AgentSkills

Shared **AI agent skills** and MCP tooling for .NET applications and Orchard Core based projects. The repository contains three projects that solve distinct problems:

| Project | README | Problem it solves |
|---|---|---|
| `CrestApps.AgentSkills.Mcp` | [`src/CrestApps.AgentSkills.Mcp/README.md`](src/CrestApps.AgentSkills.Mcp/README.md) | Provides a reusable MCP skill engine for any .NET app without building custom parsers/providers. |
| `CrestApps.AgentSkills.OrchardCore` | [`src/CrestApps.AgentSkills.OrchardCore/README.md`](src/CrestApps.AgentSkills.OrchardCore/README.md) | Used for Orchard Core local development by copying skills to `.agents/skills`. |
| `CrestApps.AgentSkills.Mcp.OrchardCore` | [`src/CrestApps.AgentSkills.Mcp.OrchardCore/README.md`](src/CrestApps.AgentSkills.Mcp.OrchardCore/README.md) | Exposes Orchard Core skills as MCP prompts and MCP resources at runtime. |

## Quick Start

### Generic MCP Skill Engine

```bash
dotnet add package CrestApps.AgentSkills.Mcp
```

```csharp
builder.Services.AddMcpServer(mcp =>
{
    mcp.AddAgentSkills();
});
```

- Works with any `.agents/skills` directory (or a custom path).
- Use this when you need a framework-agnostic MCP skill engine.

### Orchard Core Local AI Authoring

```bash
dotnet add package CrestApps.AgentSkills.OrchardCore
dotnet build
```

After the first **build** after install, the solution root will contain:

```
.agents/
  skills/
    orchardcore/
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

### Orchard Core MCP Server Hosting

```bash
dotnet add package CrestApps.AgentSkills.Mcp.OrchardCore
```

```csharp
builder.Services.AddMcpServer(mcp =>
{
    mcp.AddOrchardCoreSkills();
});
```

- Loads skills at runtime via OrchardCore `FileSystemStore`.
- `IMcpResourceFileStore`, `IMcpPromptProvider`, and `IMcpResourceProvider` registered as **singletons** — no repeated file reads.
- No file copying to solution.

### Full Orchard Core Experience

Install both Orchard Core packages to get local AI authoring **and** MCP server support.

## Skill Format (agentskills.io specification)

Each skill is defined in a single **`SKILL.md`** file inside a skill directory under `src/CrestApps.AgentSkills/orchardcore/`. The file must contain YAML front-matter with at least `name` and `description` fields:

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
├─ CrestApps.AgentSkills/                 ← Central skill content (single source of truth)
│  └─ orchardcore/                               ← Orchard Core specific skills
│     ├─ orchardcore.content-types/
│     │  ├─ SKILL.md                             ← Skill definition (front-matter + body)
│     │  └─ references/                          ← Optional reference/example files
│     ├─ orchardcore.modules/
│     ├─ orchardcore.recipes/
│     └─ ...
│
├─ CrestApps.AgentSkills.Mcp/                    ← Generic MCP engine
│  ├─ Extensions/                                ← MCP extension methods
│  ├─ Providers/                                 ← Prompt & resource providers
│  ├─ Services/                                  ← Skill file store + parsing
│  ├─ README.md
│  └─ CrestApps.AgentSkills.Mcp.csproj
│
├─ CrestApps.AgentSkills.OrchardCore/            ← Orchard Core dev package
│  ├─ buildTransitive/                           ← MSBuild .targets for solution-root copy
│  ├─ README.md
│  └─ CrestApps.AgentSkills.OrchardCore.csproj
│
└─ CrestApps.AgentSkills.Mcp.OrchardCore/        ← Orchard Core MCP runtime package
   ├─ Extensions/                                ← MCP extension methods
   ├─ Providers/                                 ← Prompt & resource providers
   ├─ Services/                                  ← IMcpResourceFileStore, McpSkillFileStore, SkillFrontMatterParser
   ├─ README.md
   └─ CrestApps.AgentSkills.Mcp.OrchardCore.csproj
```

The Orchard Core packages pack skill files from the central `src/CrestApps.AgentSkills/orchardcore/` directory — the dev package packs them under `skills/orchardcore/` (MSBuild copy only), while the MCP package packs them under `contentFiles/any/any/.agents/skills/orchardcore/`. The generic `CrestApps.AgentSkills.Mcp` package is skill-source agnostic and expects skills to be provided by your application.

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

**Bash (Linux/macOS):**
```bash
for dir in src/CrestApps.AgentSkills/orchardcore/*/; do
  name=$(basename "$dir")
  if [ ! -f "$dir/SKILL.md" ]; then echo "FAIL: $name missing SKILL.md"; continue; fi
  if ! head -1 "$dir/SKILL.md" | grep -q "^---$"; then echo "FAIL: $name bad front-matter"; continue; fi
  echo "OK: $name"
done
```

**PowerShell (Windows):**
```powershell
Get-ChildItem -Path "src\CrestApps.AgentSkills\orchardcore" -Directory | ForEach-Object {
    $name = $_.Name
    $skillFile = Join-Path $_.FullName "SKILL.md"
    if (-not (Test-Path $skillFile)) {
        Write-Host "FAIL: $name missing SKILL.md" -ForegroundColor Red
    } else {
        $firstLine = Get-Content $skillFile -First 1
        if ($firstLine -ne "---") {
            Write-Host "FAIL: $name bad front-matter" -ForegroundColor Red
        } else {
            Write-Host "OK: $name" -ForegroundColor Green
        }
    }
}
```

## Contributing

Contributions are welcome! Please review [CONTRIBUTING.md](.github/CONTRIBUTING.md) for setup details and coding standards.

### Submitting a New Skill PR

1. Open a **New Skill Request** issue (or confirm an existing one) to align on scope: <https://github.com/CrestApps/CrestApps.AgentSkills/issues/new?template=skill_request.md>.
2. Add a new directory under `src/CrestApps.AgentSkills/orchardcore/<skill-name>/` with a `SKILL.md` that matches the [agentskills.io specification](https://agentskills.io/specification).
3. Run the build and tests listed above, plus the local skill validation script.
4. Open a PR that links the issue (e.g., `Fix #123`), summarizes the skill, and includes any references or screenshots if applicable.

> **Warning:**
> This package will **always overwrite** files in the `.agents/` folder in your solution root.
> Any changes you make to files inside `.agents/` that were generated by this package **will be lost** when you build after installing or updating this NuGet package.
> Do **not** modify files added by this package inside `.agents/`. Treat them as read-only.

## License

This project is licensed under the [MIT License](LICENSE).

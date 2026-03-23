# CrestApps.AgentSkills

Shared **AI agent skills** and MCP tooling for .NET applications and Orchard Core based projects. The repository contains three projects plus a GitHub Copilot CLI plugin marketplace:

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

### Orchard Core Copilot CLI Plugin

If you want Orchard Core skills in GitHub Copilot CLI **without** copying files into your repository, install the `crestapps-orchardcore` plugin from this repository's marketplace:

```bash
copilot plugin marketplace add CrestApps/CrestApps.AgentSkills
copilot plugin install crestapps-orchardcore@crestapps-agentskills
```

- Uses the Orchard Core skills from this repository as a Copilot CLI plugin
- Avoids managing `.agents/skills` inside your solution
- Best option when you want plugin-based installation instead of NuGet-managed or manually copied skill files

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
.github/
├─ plugin/
│  └─ marketplace.json                         ← Copilot CLI marketplace manifest
│
plugins/
└─ crestapps-orchardcore/
   ├─ plugin.json                              ← Copilot CLI plugin manifest
   └─ README.md

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

## Copilot CLI Plugin Marketplace

This repository now includes GitHub Copilot CLI marketplace manifests at `.github/plugin/marketplace.json` **and** `.claude-plugin/marketplace.json`, plus a plugin at `plugins/crestapps-orchardcore`.

### Install the Orchard Core plugin from the marketplace

```bash
copilot plugin marketplace add CrestApps/CrestApps.AgentSkills
copilot plugin install crestapps-orchardcore@crestapps-agentskills
```

You can browse the marketplace entries after adding it:

```bash
copilot plugin marketplace browse crestapps-agentskills
```

You can confirm it loaded successfully with:

```bash
copilot plugin list
```

In an interactive Copilot CLI session you can also run:

```text
/skills list
```

The plugin loads Orchard Core skills from `src/CrestApps.AgentSkills/orchardcore`, so users do **not** need to clone this repository into their own project or copy files into `.agents/skills` just to use the skills.

### How Copilot CLI discovers this plugin

Copilot CLI does **not** automatically scan GitHub for plugins in arbitrary repositories. Users must tell Copilot CLI where the plugin or marketplace lives.

There are two supported ways:

1. Add the repository as a marketplace:

```bash
copilot plugin marketplace add CrestApps/CrestApps.AgentSkills
copilot plugin install crestapps-orchardcore@crestapps-agentskills
```

When users run `copilot plugin marketplace add CrestApps/CrestApps.AgentSkills`, Copilot CLI looks for a marketplace manifest in `.github/plugin/marketplace.json` or `.claude-plugin/marketplace.json`. That is why both files are now included in this repository.

2. Install the plugin directly from the repository subdirectory:

```bash
copilot plugin install CrestApps/CrestApps.AgentSkills:plugins/crestapps-orchardcore
```

That direct install works because the plugin itself has its own `plugin.json` in `plugins/crestapps-orchardcore`.

### Publish the marketplace so others can use it

There is no separate approval-based central marketplace submission process described in the current Copilot CLI docs. A public repository becomes a marketplace when it contains `.github/plugin/marketplace.json` or `.claude-plugin/marketplace.json` and the referenced plugin content.

To publish this plugin and marketplace:

1. Commit and push the plugin files, plugin README, and both marketplace manifests to the default branch of the public `CrestApps/CrestApps.AgentSkills` repository.
2. Verify the marketplace manifests point to `plugins/crestapps-orchardcore` and the plugin entry points its `skills` path at `src/CrestApps.AgentSkills/orchardcore`.
3. From a clean machine or user profile, run `copilot plugin marketplace add CrestApps/CrestApps.AgentSkills`.
4. Optionally confirm discovery with `copilot plugin marketplace browse crestapps-agentskills`.
5. Install the plugin with `copilot plugin install crestapps-orchardcore@crestapps-agentskills`.
6. Verify installation with `copilot plugin list` and confirm the skills appear with `/skills list` in a new Copilot CLI session.
7. Optionally verify the direct install path also works with `copilot plugin install CrestApps/CrestApps.AgentSkills:plugins/crestapps-orchardcore`.
8. Optionally tag a release in GitHub so users have a clear published milestone to reference.
9. Share the marketplace add/install commands and the direct install command in release notes, documentation, and examples so users can discover it easily.

### Manual folder download fallback

If you do not want to use the Copilot CLI plugin system, you can still download only the Orchard Core skill files under `src/CrestApps.AgentSkills/orchardcore` into `.agents/skills/orchardcore` with a small PowerShell script:

```powershell
$owner = "CrestApps"
$repo = "CrestApps.AgentSkills"
$branch = "main"
$sourceRoot = "src/CrestApps.AgentSkills/orchardcore/"
$targetRoot = ".agents\skills\orchardcore"

$tree = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repo/git/trees/$branch?recursive=1"
$files = $tree.tree | Where-Object { $_.type -eq "blob" -and $_.path.StartsWith($sourceRoot) }

foreach ($file in $files) {
    $relativePath = $file.path.Substring($sourceRoot.Length)
    $destinationPath = Join-Path $targetRoot $relativePath
    $destinationDirectory = Split-Path $destinationPath -Parent

    New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/$owner/$repo/$branch/$($file.path)" -OutFile $destinationPath
}
```

That script downloads only the files inside `src/CrestApps.AgentSkills/orchardcore` and recreates the same directory structure under `.agents/skills/orchardcore`.

## License

This project is licensed under the [MIT License](LICENSE).

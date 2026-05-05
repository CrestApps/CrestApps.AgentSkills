# CrestApps.AgentSkills

Shared **AI agent skills** and MCP tooling for .NET applications, Orchard Core solutions, CrestApps OrchardCore modules, and CrestApps.Core libraries.

## Projects

| Project | README | Purpose |
|---|---|---|
| `CrestApps.AgentSkills` | [`src/CrestApps.AgentSkills/README.md`](src/CrestApps.AgentSkills/README.md) | Canonical skill source roots grouped by owning project. |
| `CrestApps.AgentSkills.Mcp` | [`src/CrestApps.AgentSkills.Mcp/README.md`](src/CrestApps.AgentSkills.Mcp/README.md) | Generic MCP skill engine for any .NET app. |
| `CrestApps.AgentSkills.OrchardCore` | [`src/CrestApps.AgentSkills.OrchardCore/README.md`](src/CrestApps.AgentSkills.OrchardCore/README.md) | Dev-time package that copies Orchard Core and CrestApps OrchardCore skills into `.agents/skills`. |
| `CrestApps.AgentSkills.Mcp.OrchardCore` | [`src/CrestApps.AgentSkills.Mcp.OrchardCore/README.md`](src/CrestApps.AgentSkills.Mcp.OrchardCore/README.md) | Runtime MCP package that bundles Orchard Core and CrestApps OrchardCore skills. |

## Skill source roots

| Source root | Contents | Used by |
|---|---|---|
| `src/CrestApps.AgentSkills/orchardcore/` | Framework-only Orchard Core skills that work out of the box without CrestApps modules | Orchard Core packages, `orchardcore` plugin |
| `src/CrestApps.AgentSkills/crestapps-orchardcore/` | Skills for `CrestApps.OrchardCore` modules such as AI, MCP, A2A, and CrestApps docs | Orchard Core packages, `crestapps-orchardcore` plugin |
| `src/CrestApps.AgentSkills/crestapps-core/` | Skills dedicated to `CrestApps.Core` | `crestapps-core` plugin |

`CrestApps.AgentSkills.OrchardCore` and `CrestApps.AgentSkills.Mcp.OrchardCore` intentionally bundle **both** `orchardcore` and `crestapps-orchardcore` so Orchard Core consumers get the full framework + CrestApps module experience from one package.

## Quick start

### Generic MCP skill engine

```bash
dotnet add package CrestApps.AgentSkills.Mcp
```

```csharp
builder.Services.AddMcpServer(mcp =>
{
    mcp.AddAgentSkills();
});
```

### Orchard Core local AI authoring

```bash
dotnet add package CrestApps.AgentSkills.OrchardCore
dotnet build
```

After the first build, the solution root contains:

```text
.agents/
  skills/
    orchardcore/
      orchardcore-content-types/
      orchardcore-recipes/
      ...
    crestapps-orchardcore/
      orchardcore-ai/
      orchardcore-ai-chat/
      orchardcore-ai-mcp/
      ...
```

### Orchard Core MCP server hosting

```bash
dotnet add package CrestApps.AgentSkills.Mcp.OrchardCore
```

```csharp
builder.Services.AddMcpServer(mcp =>
{
    mcp.AddOrchardCoreSkills();
});
```

## Copilot CLI plugins

Add this repository as a marketplace:

```bash
copilot plugin marketplace add CrestApps/CrestApps.AgentSkills
```

Then install the plugin you want:

| Plugin | Installs |
|---|---|
| `orchardcore` | Framework-only Orchard Core skills |
| `crestapps-orchardcore` | CrestApps OrchardCore module skills |
| `crestapps-core` | CrestApps.Core skills |

```bash
copilot plugin install orchardcore@crestapps-agentskills
copilot plugin install crestapps-orchardcore@crestapps-agentskills
copilot plugin install crestapps-core@crestapps-agentskills
```

If you want the full Orchard Core plugin experience without copying files into your repository, install **both** `orchardcore` and `crestapps-orchardcore`.

The `crestapps-orchardcore` plugin is versioned from **2.0.0** onward because it now contains only CrestApps OrchardCore module skills instead of the previous mixed bundle.

## Plugin publishing

Marketplace manifests live at:

- `.github/plugin/marketplace.json`
- `.claude-plugin/marketplace.json`

Generated plugin bundles live at:

- `plugins/orchardcore`
- `plugins/crestapps-orchardcore`
- `plugins/crestapps-core`

The `Publish plugin bundles` workflow recreates each `plugins/*/skills` directory from its matching source root and increments the version of each changed plugin in the marketplace manifests. Do not edit generated plugin bundles manually in normal pull requests, and do not copy skill contents into `plugins/*/skills` as part of source changes. Those generated files should arrive only through the workflow's follow-up PR.

## Repository structure

```text
.github/
├─ plugin/
│  └─ marketplace.json
├─ workflows/
│  ├─ publish-plugin.yml
│  └─ validate-plugin-bundle.yml
│
plugins/
├─ orchardcore/
│  ├─ README.md
│  └─ skills/
├─ crestapps-orchardcore/
│  ├─ README.md
│  └─ skills/
└─ crestapps-core/
   ├─ README.md
   └─ skills/

src/
├─ CrestApps.AgentSkills/
│  ├─ orchardcore/
│  ├─ crestapps-orchardcore/
│  └─ crestapps-core/
├─ CrestApps.AgentSkills.Mcp/
├─ CrestApps.AgentSkills.OrchardCore/
└─ CrestApps.AgentSkills.Mcp.OrchardCore/
```

## Skill format

Each skill lives in its own directory and must contain `SKILL.md` with YAML front-matter:

```md
---
name: orchardcore-content-types
description: Clear description of what this skill does and when to use it.
---

# Skill Title

Guidelines, code templates, and examples go here.
```

### Requirements

- `name` must match the directory name exactly
- `description` is required
- Optional references go under `references/`
- Orchard Core framework skills belong in `src/CrestApps.AgentSkills/orchardcore/`
- CrestApps OrchardCore module skills belong in `src/CrestApps.AgentSkills/crestapps-orchardcore/`
- Direct CrestApps.Core skills belong in `src/CrestApps.AgentSkills/crestapps-core/`

## Build and test

```bash
dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false
dotnet test -c Release --verbosity normal
```

### Validate skill front-matter locally

**PowerShell**

```powershell
Get-ChildItem -Path "src\CrestApps.AgentSkills" -Directory | ForEach-Object {
    Get-ChildItem -Path $_.FullName -Directory | ForEach-Object {
        $skillFile = Join-Path $_.FullName "SKILL.md"
        if (-not (Test-Path $skillFile)) { Write-Host "FAIL: $($_.FullName) missing SKILL.md" -ForegroundColor Red }
        elseif ((Get-Content $skillFile -First 1) -ne "---") { Write-Host "FAIL: $($_.FullName) bad front-matter" -ForegroundColor Red }
        else { Write-Host "OK: $($_.FullName)" -ForegroundColor Green }
    }
}
```

**Bash**

```bash
for root in src/CrestApps.AgentSkills/orchardcore src/CrestApps.AgentSkills/crestapps-orchardcore src/CrestApps.AgentSkills/crestapps-core; do
  [ -d "$root" ] || continue
  for dir in "$root"/*/; do
    [ -d "$dir" ] || continue
    if [ ! -f "$dir/SKILL.md" ]; then echo "FAIL: $dir missing SKILL.md"; continue; fi
    if ! head -1 "$dir/SKILL.md" | grep -q "^---$"; then echo "FAIL: $dir bad front-matter"; continue; fi
    echo "OK: $dir"
  done
done
```

## Contributing

See [`.github/CONTRIBUTING.md`](.github/CONTRIBUTING.md) for contribution guidelines and the expected source-root placement for new skills.

## License

This project is licensed under the [MIT License](LICENSE).

# CrestApps.AgentSkills.Skills

This project is a **non-packagable** skill repository that serves as the single source of truth for all agent skills in this solution.

## Purpose

This project exists solely to **host and organize shared skills** by framework. It is not published as a NuGet package. Instead, other projects in this solution reference these skills and include them in their respective NuGet packages during the packaging process.

## Structure

Skills are organized into framework-specific subdirectories:

```
CrestApps.AgentSkills.Skills/
├─ orchardcore/              ← Orchard Core framework skills
│  ├─ orchardcore.content-types/
│  │  ├─ SKILL.md
│  │  └─ references/
│  ├─ orchardcore.modules/
│  ├─ orchardcore.recipes/
│  └─ ...
│
└─ [future-framework]/       ← Additional frameworks can be added here
   └─ ...
```

### Current Frameworks

- **`orchardcore/`** - Contains all Orchard Core-specific skills (content types, modules, recipes, etc.)

Future frameworks can be added as new subdirectories alongside `orchardcore/`.

## How Skills Are Packaged

Skills from this project are included in NuGet packages by referencing them in the corresponding project's `.csproj` file:

### Example: Orchard Core Skills

The `CrestApps.OrchardCore.AgentSkills.csproj` includes:

```xml
<ItemGroup>
  <None Include="..\CrestApps.AgentSkills.Skills\orchardcore\**\*"
        Pack="true"
        PackagePath="skills"
        Visible="false" />
</ItemGroup>
```

Similarly, `CrestApps.OrchardCore.AgentSkills.Mcp.csproj` includes:

```xml
<ItemGroup>
  <Content Include="..\CrestApps.AgentSkills.Skills\orchardcore\**\*"
           Pack="true"
           PackagePath="contentFiles\any\any\.agents\skills"
           BuildAction="Content"
           CopyToOutputDirectory="PreserveNewest"
           Visible="false">
    <PackageCopyToOutput>true</PackageCopyToOutput>
  </Content>
</ItemGroup>
```

This approach ensures:
- ✅ Single source of truth for all skills
- ✅ Skills can be shared across multiple packages
- ✅ Framework-specific skills are cleanly separated
- ✅ Easy to add skills for new frameworks in the future

## Adding a New Framework

To add skills for a new framework (e.g., `aspnetcore`):

1. Create a new directory: `CrestApps.AgentSkills.Skills/aspnetcore/`
2. Add skill directories under it (e.g., `aspnetcore/aspnetcore.minimal-apis/`)
3. Create corresponding NuGet package projects that reference these skills
4. Update package `.csproj` files to include the new framework skills

## Skill Format

Each skill must follow the [agentskills.io specification](https://agentskills.io/specification). Every skill directory must contain a `SKILL.md` file with YAML front-matter:

```md
---
name: framework.skill-name
description: Clear description of what this skill does and when to use it.
---

# Skill Title

Guidelines, code templates, and examples go here.
```

## Validation

Validate all skills before committing:

**PowerShell (Windows):**
```powershell
Get-ChildItem -Path "src\CrestApps.AgentSkills.Skills\orchardcore" -Directory | ForEach-Object {
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

**Bash (Linux/macOS):**
```bash
for dir in src/CrestApps.AgentSkills.Skills/orchardcore/*/; do
  name=$(basename "$dir")
  if [ ! -f "$dir/SKILL.md" ]; then echo "FAIL: $name missing SKILL.md"; continue; fi
  if ! head -1 "$dir/SKILL.md" | grep -q "^---$"; then echo "FAIL: $name bad front-matter"; continue; fi
  echo "OK: $name"
done
```

## See Also

- [Main README](../../README.md) - Project overview and quick start
- [Contributing Guide](../../.github/CONTRIBUTING.md) - How to contribute new skills

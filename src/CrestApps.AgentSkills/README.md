# CrestApps.AgentSkills

This project is the **skill content container** for the repository. It is not published directly; other projects and plugin bundles consume the source roots defined here.

## Source roots

| Root | Purpose |
|---|---|
| `orchardcore/` | Framework-only Orchard Core skills |
| `crestapps-orchardcore/` | Skills for `CrestApps.OrchardCore` modules |
| `crestapps-core/` | Skills for direct `CrestApps.Core` scenarios |

## Packaging behavior

- `CrestApps.AgentSkills.OrchardCore` packs `orchardcore/` and `crestapps-orchardcore/` under `skills/`
- `CrestApps.AgentSkills.Mcp.OrchardCore` packs `orchardcore/` and `crestapps-orchardcore/` under `contentFiles/any/any/.agents/skills/`
- The Copilot CLI plugins are generated from matching roots:
  - `plugins/orchardcore` ← `src/CrestApps.AgentSkills/orchardcore`
  - `plugins/crestapps-orchardcore` ← `src/CrestApps.AgentSkills/crestapps-orchardcore`
  - `plugins/crestapps-core` ← `src/CrestApps.AgentSkills/crestapps-core`

## Adding a new skill

1. Choose the owning source root based on the project the skill targets.
2. Create a directory under that root, for example `src/CrestApps.AgentSkills/orchardcore/orchardcore-content-types/`.
3. Add `SKILL.md` with front-matter whose `name` matches the directory name exactly.
4. Add optional supporting files under `references/`.
5. Run the repository build and tests.

## Validation

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

## See also

- [Repository README](../../README.md)
- [Contributing guide](../../.github/CONTRIBUTING.md)

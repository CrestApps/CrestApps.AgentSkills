# Contributing to CrestApps.AgentSkills

Thanks for contributing.

## Skill placement

Choose the source root that matches the project the skill belongs to:

- `src/CrestApps.AgentSkills/orchardcore/` for framework-only Orchard Core skills
- `src/CrestApps.AgentSkills/crestapps-orchardcore/` for `CrestApps.OrchardCore` module skills
- `src/CrestApps.AgentSkills/crestapps-core/` for direct `CrestApps.Core` skills

Each skill must live in its own directory and include `SKILL.md` with front-matter whose `name` matches the directory name exactly.

## Skill format

```md
---
name: orchardcore-content-types
description: A clear description of what this skill does and when to use it.
---

# Skill Title

Guidelines, code templates, and examples go here.
```

Use `references/` for extra examples. Keep directory names lowercase and hyphenated.

## Validation

```bash
dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false
dotnet test -c Release --verbosity normal
```

## Plugin bundles

Do **not** manually edit generated files under:

- `plugins/orchardcore/skills`
- `plugins/crestapps-orchardcore/skills`
- `plugins/crestapps-core/skills`

Update the matching source root under `src/CrestApps.AgentSkills/` instead. The `Publish plugin bundles` workflow refreshes generated plugin bundles separately.

## Pull requests

1. Open or confirm the related issue first for new skills or substantial changes.
2. Add the skill under the correct source root.
3. Run the build and tests.
4. Submit a PR summarizing the change and linking the issue.

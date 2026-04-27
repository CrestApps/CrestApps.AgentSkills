# Agent Instructions

This repository contains agent skills grouped by owning project under `src/CrestApps.AgentSkills/`.

## Skill roots

- `src/CrestApps.AgentSkills/orchardcore/` - framework-only Orchard Core skills
- `src/CrestApps.AgentSkills/crestapps-orchardcore/` - `CrestApps.OrchardCore` module skills
- `src/CrestApps.AgentSkills/crestapps-core/` - direct `CrestApps.Core` skills

## Skill structure

- Each skill lives in its own directory under the correct source root.
- Directory names use lowercase, hyphenated format and the `name` front-matter value must match the directory name exactly.
- Every skill directory must contain a `SKILL.md` file with YAML front-matter including `name` and `description`.
- Use a `references/` subdirectory for supporting documentation and examples.

## Content conventions

- Front-matter must start and end with `---`.
- All recipe step JSON blocks must be wrapped in the root recipe format: `{ "steps": [...] }`.
- All C# classes in code samples must use the `sealed` modifier, except View Models.
- Third-party module packages must be installed in the web/startup project.
- Keep guidance concise, example-driven, and actionable.

## Adding a new skill

1. Choose the correct source root for the project the skill targets.
2. Create a directory under that root, for example `src/CrestApps.AgentSkills/orchardcore/orchardcore-content-types/`.
3. Add `SKILL.md` with matching `name` and `description`.
4. Add optional `references/<topic>-examples.md`.
5. Run the repository build and tests.

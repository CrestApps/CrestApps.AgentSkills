# Agent Instructions

This repository contains Orchard Core agent skills under `src/CrestApps.AgentSkills.Skills/orchardcore/`.

## Skill Structure

- Each skill lives in its own directory under `src/CrestApps.AgentSkills.Skills/orchardcore/<skill-name>/`.
- Directory names use lowercase, hyphenated format, prefixed with `orchardcore.` (e.g., `orchardcore.content-types`).
- Every skill directory must contain a `SKILL.md` file with YAML front-matter including `name` and `description`.
- The `name` field in front-matter must exactly match the directory name.
- Use a `references/` subdirectory for supporting documentation and examples.

## Content Conventions

- Front-matter must start and end with `---`.
- All recipe step JSON blocks must be wrapped in root recipe format: `{ "steps": [...] }`.
- All C# classes in code samples must use the `sealed` modifier, **except for View Models** which must not be `sealed` because they are used for model binding.
- Third-party module packages (non `OrchardCore.*`) must be installed in the web/startup project.
- Keep guidance concise, example-driven, and actionable.
- Prefer ready-to-use patterns over abstract descriptions.

## Code Style

- Use file-scoped namespaces in C# examples.
- Enable nullable reference types.
- Target `net10.0` framework.
- Follow `.editorconfig` formatting rules.

## Skill Categories

Skills are organized by Orchard Core functional area:
- **Content Model**: content-types, content-parts, content-fields, content-items, content-queries, taxonomies
- **Templating**: theming, razor, liquid, shapes, placement, display-management
- **Infrastructure**: modules, features, setup, tenants, data-migrations, background-tasks, caching
- **Recipes & Deployment**: recipes, deployments, autoroute, site-settings
- **Security**: security, users-roles, openid
- **UI & Navigation**: navigation, menus, widgets, forms, admin
- **Search & Media**: search-indexing, media, graphql
- **Communication**: email, notifications, workflows
- **Other**: ai, localization, seo, audit-trail

## Adding a New Skill

1. Create a directory under `src/CrestApps.AgentSkills.Skills/orchardcore/orchardcore.<skill-name>/`.
2. Add `SKILL.md` with front-matter (`name`, `description`) and prompt templates with guidelines and examples.
3. Add `references/<topic>-examples.md` with practical code examples.
4. Run validation: ensure `SKILL.md` starts with `---` and `name` matches directory name.
5. Build and test: `dotnet build -c Release -warnaserror` and `dotnet test -c Release`.

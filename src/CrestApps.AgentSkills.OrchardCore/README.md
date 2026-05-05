# CrestApps.AgentSkills.OrchardCore

Development-only NuGet package that copies Orchard Core skill files into the **solution-root** `.agents/skills` folder.

## What it includes

This package bundles both source roots needed for Orchard Core development:

- `orchardcore/` for framework-only Orchard Core skills
- `crestapps-orchardcore/` for CrestApps OrchardCore module skills

It does **not** bundle `crestapps-core/`.

## Install

```bash
dotnet add package CrestApps.AgentSkills.OrchardCore
dotnet build
```

## Result

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

## How it works

1. Skills are packed into the NuGet package under `skills/`.
2. A `buildTransitive/` target runs before build and design-time compilation.
3. The target copies the packaged skill files into `.agents/skills/` at the solution root.
4. Files are always overwritten so generated skill copies stay aligned with the package version.

## Important

- Run `dotnet build` after install or update; restore alone cannot trigger the copy.
- In Visual Studio, a design-time build usually causes the files to appear immediately after package install.
- Do not edit generated `.agents/skills/*` files manually; they will be overwritten on the next build.

## License

This project is licensed under the [MIT License](../../LICENSE).

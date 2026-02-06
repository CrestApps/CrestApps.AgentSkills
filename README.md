# CrestApps.OrchardCore.AgentSkills

A NuGet package that distributes shared **AI agent instruction and guardrail files** for Orchard Core development.

When installed, agent files are automatically copied into the consuming project's `.agents/` folder on package install, update, and restore. There is **no runtime dependency** — this package is used purely for development and AI tooling guidance.

## Overview

- **Shared AI agent guardrails**: Provides standardized agent instruction files that guide AI code-generation tools when working with Orchard Core projects.
- **Standardizes agent behavior**: Every project that installs this package receives the same set of agent skills, ensuring consistent AI-assisted development across teams and repositories.
- **Installs files into `.agents/`**: On package install or update, skill files are copied into the project's `.agents/` directory where AI tools can discover them.
- **No runtime footprint**: This package contains no compiled assemblies and adds nothing to your application's deployment. It is a development-only dependency.

## Installation

```bash
dotnet add package CrestApps.OrchardCore.AgentSkills
```

## How It Works

1. The NuGet package includes agent skill files under `contentFiles/any/any/.agents/skills/`.
2. An embedded MSBuild `.targets` file runs automatically on **restore, install, and update** — not just at build time.
3. The `.targets` file copies all agent files from the NuGet package cache into the consuming project's `.agents/` folder.
4. AI development tools (e.g., GitHub Copilot, Cursor, Cline) read the guardrail files from `.agents/` and use them to generate higher-quality Orchard Core code.

**Key points:**

- Files are copied locally on install and update — **no manual copying required**.
- There is **no runtime dependency** and **no deployment impact**.
- The package is used purely for **development and AI tooling guidance**.
- Copying happens via MSBuild targets, independent of compilation.

## How to Consume

1. Install the NuGet package:
   ```bash
   dotnet add package CrestApps.OrchardCore.AgentSkills
   ```
2. Restore packages:
   ```bash
   dotnet restore
   ```
3. The `.agents/` folder appears in your project directory:
   ```
   .agents/
     skills/
       orchardcore.content-types/
       orchardcore.modules/
       orchardcore.recipes/
       orchardcore.deployments/
       orchardcore.ai/
   ```
4. AI tools automatically read guardrails from these files.

## File Update Behavior

- When the NuGet package is **updated**, all agent files are **refreshed automatically** on the next restore or build.
- The latest agent standards and guardrails are always applied without manual intervention.
- Files originating from this package are **overwritten** to ensure they stay in sync with the package version.

> **Note:**
> This project installs shared agent files into your local `.agents/` folder.
> If needed, it will replace common agent files (such as `Agents.md`) that already exist in your project.
> Do **not** modify files added by this package inside `.agents/`, as your changes will be lost after a NuGet package update.

## Keeping Projects Up To Date

### Floating Version

Always get the latest agent files on restore:

```xml
<PackageReference Include="CrestApps.OrchardCore.AgentSkills" Version="1.*" />
```

### Locked Version

Pin to a specific version for full control:

```xml
<PackageReference Include="CrestApps.OrchardCore.AgentSkills" Version="1.0.0" />
```

Update manually:

```bash
dotnet add package CrestApps.OrchardCore.AgentSkills --version 1.1.0
```

## Skill Categories

| Skill | Description |
|---|---|
| `orchardcore.content-types` | Creating and managing Orchard Core content types, parts, and fields |
| `orchardcore.modules` | Scaffolding modules, features, manifests, and startup configuration |
| `orchardcore.recipes` | Recipe structure, steps, content definitions, and content items |
| `orchardcore.deployments` | Deployment plans and import/export configuration |
| `orchardcore.ai` | AI service integration, MCP enablement, and agent framework setup |

## Repository Structure

```
src/
└─ Skills/
   ├─ .agents/skills/       ← agent guardrail content (yaml, md, examples)
   ├─ build/                ← MSBuild .targets for auto-copy on restore
   └─ CrestApps.OrchardCore.AgentSkills.csproj
```

## License

This project is licensed under the [MIT License](LICENSE).

# CrestApps.OrchardCore.AgentSkills

Reusable **Agent Skills for Orchard Core development**, distributed via NuGet. These skills follow [agentskills.io](https://agentskills.io/) standards, encapsulate Orchard Core best practices, and improve AI code generation quality.

Once installed, skills **automatically mount** into the consumer project's `.agents/skills` folder so AI agents discover and use them immediately—no manual copying required.

> **Targets .NET 10** (`net10.0`).

## Overview

- **Purpose**: Provide a centralized, versioned set of Orchard Core skills that AI agents can consume.
- **Orchard Core Specialization**: Covers content types, modules, recipes, deployments, and AI integrations.
- **agentskills.io Compliance**: Every skill includes `skill.yaml`, input/output schemas, prompt templates, examples, and follows naming and validation standards.
- **AI Quality Improvements**: Agents generate higher-quality Orchard Core code by leveraging standardized prompts and examples.
- **Automatic Mounting**: Skills are copied to `.agents/skills` at the solution root during build via an included MSBuild `.targets` file, and can also be mounted at runtime.

## Installation

```bash
dotnet add package CrestApps.OrchardCore.AgentSkills
```

## Repository Structure

```
src/
└─ Skills/
   ├─ .agents/skills/           ← skill content (yaml, md, examples)
   ├─ Extensions/               ← C# extension methods
   ├─ build/                    ← MSBuild .targets for auto-mounting
   ├─ IAgentBuilder.cs
   ├─ skills.manifest.json
   └─ CrestApps.OrchardCore.AgentSkills.csproj
```

Skill content lives directly under `src/Skills/.agents/skills/` and is packed into `contentFiles/any/any/.agents/skills/` in the NuGet package, making it available for all target frameworks and languages.

## How Mounting Works

The package provides **two** complementary mounting strategies so skills are always available:

### 1. Build-Time Mounting (MSBuild `.targets`)

An embedded MSBuild `.targets` file runs automatically before each build. It:

1. Locates the skills bundled inside the NuGet package (`contentFiles/any/any/.agents/skills/`).
2. Determines the solution root (via `$(SolutionDir)`, falling back to the project directory).
3. Creates `.agents/skills` at the solution root if it doesn't already exist.
4. Copies all skill files into that folder, skipping unchanged files.

No configuration is needed — it happens on every build.

### 2. Runtime Mounting (`MountOrchardCoreSkills`)

For scenarios where build-time mounting isn't sufficient (e.g., containerized deployments, dynamic skill loading), call the runtime mounting extension:

```csharp
builder.Services.AddAgents(agent =>
{
    agent.MountOrchardCoreSkills();
});
```

This method:

- Discovers skills from the NuGet package's output directory.
- Walks up the directory tree to find the solution root (looks for `.sln` files).
- Creates `.agents/skills` if it doesn't exist.
- Copies all skills, overwriting existing files to keep the package as the source of truth.
- Is **idempotent** — safe to call multiple times.
- Throws a clear `InvalidOperationException` if the embedded skills source directory is missing.
- Handles filesystem exceptions gracefully for I/O, permission, and security errors.

### Result

After installation and build, your solution root will contain:

```
.agents/
  skills/
    orchardcore.content-types/
    orchardcore.modules/
    orchardcore.recipes/
    orchardcore.deployments/
    orchardcore.ai/
```

## Registering Skills

### Mount and register (recommended)

Automatically copies skills to the solution root **and** registers them:

```csharp
agentBuilder.MountOrchardCoreSkills();
```

### Register only

If skills are already present (e.g., from the build-time `.targets`), you can register them from the output directory:

```csharp
agentBuilder.AddOrchardCoreSkills();
```

### Full usage example

```csharp
builder.Services.AddAgents(agent =>
{
    agent.MountOrchardCoreSkills();
});
```

## Keeping Projects Up To Date

### Floating Version

Always get the latest skills on restore/build with no manual updates:

```xml
<PackageReference
  Include="CrestApps.OrchardCore.AgentSkills"
  Version="*" />
```

or pin to a major version:

```xml
<PackageReference
  Include="CrestApps.OrchardCore.AgentSkills"
  Version="1.*" />
```

### Locked Version

Pin to a specific version for full control:

```xml
<PackageReference
  Include="CrestApps.OrchardCore.AgentSkills"
  Version="1.0.0" />
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

## agentskills.io Compliance

All skills in this package comply with [agentskills.io](https://agentskills.io/) standards:

- Each skill includes a `skill.yaml` with metadata, inputs, and outputs.
- Prompt templates are provided in `prompts.md`.
- Examples are included where applicable.
- Naming follows the `orchardcore.<category>` convention.
- Input/output schemas are fully defined.

## License

This project is licensed under the [MIT License](LICENSE).

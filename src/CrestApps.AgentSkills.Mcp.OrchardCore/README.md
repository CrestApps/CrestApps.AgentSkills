# CrestApps.AgentSkills.Mcp.OrchardCore

Runtime NuGet package that exposes Orchard Core skill files as **MCP prompts** and **MCP resources**.

## What it includes

This package bundles both Orchard Core skill roots:

- `orchardcore/` for framework-only Orchard Core skills
- `crestapps-orchardcore/` for CrestApps OrchardCore module skills

It does **not** bundle `crestapps-core/`.

## Install

```bash
dotnet add package CrestApps.AgentSkills.Mcp.OrchardCore
```

## Usage

```csharp
builder.Services.AddMcpServer(mcp =>
{
    mcp.AddOrchardCoreSkills();
});
```

## How it works

1. The package packs skills under `contentFiles/any/any/.agents/skills/`.
2. NuGet copies those files into the consuming project's output.
3. `AddOrchardCoreSkills()` registers the Orchard Core file store plus cached prompt and resource providers.
4. At runtime, MCP clients can discover prompts/resources from both `orchardcore/` and `crestapps-orchardcore/`.

## Companion package

Use [`CrestApps.AgentSkills.OrchardCore`](../CrestApps.AgentSkills.OrchardCore/README.md) when you also want design-time copying into `.agents/skills/`.

## License

This project is licensed under the [MIT License](../../LICENSE).

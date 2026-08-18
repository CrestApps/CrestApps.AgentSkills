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

1. The package packs skills under `skills/` in the `.nupkg`.
2. A `buildTransitive` MSBuild targets file copies those files into the consuming application's build output and publish output at `.agents/skills/`.
3. `AddOrchardCoreSkills()` registers the Orchard Core file store plus cached prompt and resource providers, reading from `<AppContext.BaseDirectory>/.agents/skills` by default.
4. At runtime, MCP clients can discover prompts/resources from both `orchardcore/` and `crestapps-orchardcore/`.

Skills are delivered with `buildTransitive` rather than NuGet `contentFiles` on purpose. NuGet only applies `contentFiles` to direct package references, so any module that wraps this package would ship without skills. `buildTransitive` assets flow through transitive package references, so applications get the skills no matter how deep this package sits in the dependency graph.

To supply your own skills directory instead of the packaged one, set the `IncludeCrestAppsAgentSkills` MSBuild property to `false`:

```xml
<PropertyGroup>
  <IncludeCrestAppsAgentSkills>false</IncludeCrestAppsAgentSkills>
</PropertyGroup>
```

## Companion package

Use [`CrestApps.AgentSkills.OrchardCore`](../CrestApps.AgentSkills.OrchardCore/README.md) when you also want design-time copying into `.agents/skills/`.

## License

This project is licensed under the [MIT License](../../LICENSE).

# crestapps-orchardcore

GitHub Copilot CLI plugin that bundles **CrestApps OrchardCore module skills** from `src/CrestApps.AgentSkills/crestapps-orchardcore`.

## Install from the marketplace

```bash
copilot plugin marketplace add CrestApps/CrestApps.AgentSkills
copilot plugin install crestapps-orchardcore@crestapps-agentskills
```

## Local install

```bash
copilot plugin install ./plugins/crestapps-orchardcore
```

## What it provides

- CrestApps OrchardCore module skills such as AI, MCP, A2A, and CrestApps documentation lookup
- A generated bundle under `plugins/crestapps-orchardcore/skills`
- A project-aligned plugin separate from the framework-only `orchardcore` plugin

## Versioning

This plugin starts at **2.0.0** in the split model because the bundled skills changed from the previous mixed Orchard Core bundle.

## Maintenance

Do not edit `plugins/crestapps-orchardcore/skills` manually. The source of truth is `src/CrestApps.AgentSkills/crestapps-orchardcore`, and the `Publish plugin bundles` workflow refreshes this generated bundle in a separate follow-up PR.

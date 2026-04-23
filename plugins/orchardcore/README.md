# orchardcore

GitHub Copilot CLI plugin that bundles **framework-only Orchard Core skills** from `src/CrestApps.AgentSkills/orchardcore`.

## Install from the marketplace

```bash
copilot plugin marketplace add CrestApps/CrestApps.AgentSkills
copilot plugin install orchardcore@crestapps-agentskills
```

## Local install

```bash
copilot plugin install ./plugins/orchardcore
```

## What it provides

- Orchard Core framework skills that work without CrestApps modules
- A generated bundle under `plugins/orchardcore/skills`
- The framework half of the split Orchard Core plugin story

## Maintenance

Do not edit `plugins/orchardcore/skills` manually. The source of truth is `src/CrestApps.AgentSkills/orchardcore`, and the `Publish plugin bundles` workflow refreshes this generated bundle in a separate follow-up PR.

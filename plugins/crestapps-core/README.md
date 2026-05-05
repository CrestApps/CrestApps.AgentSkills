# crestapps-core

GitHub Copilot CLI plugin reserved for **CrestApps.Core** skills from `src/CrestApps.AgentSkills/crestapps-core`.

## Install from the marketplace

```bash
copilot plugin marketplace add CrestApps/CrestApps.AgentSkills
copilot plugin install crestapps-core@crestapps-agentskills
```

## Local install

```bash
copilot plugin install ./plugins/crestapps-core
```

## What it provides

- A dedicated plugin channel for direct `CrestApps.Core` skills
- A generated bundle under `plugins/crestapps-core/skills`
- Room to publish `CrestApps.Core` skills independently from Orchard Core skills

At the moment this plugin is scaffolded for the new split structure; add direct `CrestApps.Core` skills under `src/CrestApps.AgentSkills/crestapps-core` to populate it.

## Maintenance

Do not edit `plugins/crestapps-core/skills` manually. The source of truth is `src/CrestApps.AgentSkills/crestapps-core`, and the `Publish plugin bundles` workflow refreshes this generated bundle in a separate follow-up PR.

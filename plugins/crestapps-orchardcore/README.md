# crestapps-orchardcore

`crestapps-orchardcore` is a GitHub Copilot CLI plugin that exposes the Orchard Core skills from `src/CrestApps.AgentSkills/orchardcore` without copying them into your project.

## Install from the CrestApps marketplace

```bash
copilot plugin marketplace add CrestApps/CrestApps.AgentSkills
copilot plugin install crestapps-orchardcore@crestapps-agentskills
```

Then verify the plugin is installed:

```bash
copilot plugin list
```

In a new Copilot CLI session you can also verify the skills are loaded:

```text
/skills list
```

## Local development install

From the repository root:

```bash
copilot plugin install ./plugins/crestapps-orchardcore
```

Reinstall the plugin after changing the manifest or the shared Orchard Core skill files so Copilot CLI refreshes its plugin cache.

## What it provides

- Reuses the existing Orchard Core skill source of truth in `src/CrestApps.AgentSkills/orchardcore`
- Avoids copying files into your repository's `.agents/skills` folder
- Works well for users who want shared skills managed through Copilot CLI instead of solution files

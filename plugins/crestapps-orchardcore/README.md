# crestapps-orchardcore

`crestapps-orchardcore` is a GitHub Copilot CLI plugin that bundles Orchard Core skills for GitHub Copilot CLI without copying them into your project.

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
.\plugins\crestapps-orchardcore\sync-skills.ps1
copilot plugin install ./plugins/crestapps-orchardcore
```

Run `.\plugins\crestapps-orchardcore\sync-skills.ps1` after changing `src/CrestApps.AgentSkills/orchardcore`, then reinstall the plugin so Copilot CLI refreshes its plugin cache.

## What it provides

- Bundles Orchard Core skills directly under `plugins/crestapps-orchardcore/skills`
- Mirrors the Orchard Core skill source of truth from `src/CrestApps.AgentSkills/orchardcore`
- Avoids copying files into your repository's `.agents/skills` folder
- Works well for users who want shared skills managed through Copilot CLI instead of solution files

## Repository maintenance

The plugin must keep its bundled `skills/` directory aligned with `src/CrestApps.AgentSkills/orchardcore`. Copilot CLI rejects plugin skill paths that escape the plugin directory, so the plugin manifest intentionally points at the local `skills` folder. Use `.\plugins\crestapps-orchardcore\sync-skills.ps1` when the source skills change.

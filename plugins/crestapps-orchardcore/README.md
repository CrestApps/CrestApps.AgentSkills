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
copilot plugin install ./plugins/crestapps-orchardcore
```

Reinstall the plugin after the plugin bundle is refreshed so Copilot CLI picks up the latest skills.

## What it provides

- Bundles Orchard Core skills directly under `plugins/crestapps-orchardcore/skills`
- Publishes a generated bundle from the canonical source at `src/CrestApps.AgentSkills/orchardcore`
- Avoids copying files into your repository's `.agents/skills` folder
- Works well for users who want shared skills managed through Copilot CLI instead of solution files

## awesome-copilot listing metadata

If you want to propose this plugin for inclusion in `github/awesome-copilot`, their `Adding External Plugins` instructions expect an entry in `plugins/external.json` that points back to this repository and plugin path:

```json
{
  "name": "crestapps-orchardcore",
  "description": "Orchard Core skills for GitHub Copilot CLI backed by CrestApps.AgentSkills.",
  "version": "1.0.0",
  "author": {
    "name": "CrestApps",
    "url": "https://github.com/CrestApps"
  },
  "homepage": "https://github.com/CrestApps/CrestApps.AgentSkills/tree/main/plugins/crestapps-orchardcore",
  "keywords": ["orchard-core", "copilot-cli", "skills", "dotnet"],
  "license": "MIT",
  "repository": "https://github.com/CrestApps/CrestApps.AgentSkills",
  "source": {
    "source": "github",
    "repo": "CrestApps/CrestApps.AgentSkills",
    "path": "plugins/crestapps-orchardcore"
  }
}
```

Acceptance there is still controlled by the `awesome-copilot` maintainers and their repository policies. Regardless of that review outcome, users can already install this plugin directly from this repository or by adding the CrestApps marketplace shown above.

## Repository maintenance

Copilot CLI rejects plugin skill paths that escape the plugin directory, so the plugin manifest intentionally points at the local `skills` folder.

Do not edit files in `plugins/crestapps-orchardcore/skills` manually. The source of truth is `src/CrestApps.AgentSkills/orchardcore`, and the `Publish plugin bundle` GitHub Actions workflow refreshes the plugin bundle from that source directory.

Pull requests also run the `Validate plugin bundle` workflow, which fails if a PR modifies `plugins/crestapps-orchardcore/skills`. Contributors should update only `src/CrestApps.AgentSkills/orchardcore`; the plugin bundle is refreshed separately during publishing.

`Publish plugin bundle` can still be run manually, and it also runs automatically after `Release - CI` completes successfully. When changes are needed, it creates or updates an automation pull request for the generated plugin bundle instead of pushing directly to `main`.

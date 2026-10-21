# Building the CrestApps.Core Front-End Assets

The chat UI sources live in `src/Resources/CrestApps.AI.Resources/Assets/`. Vite builds them into the committed `wwwroot/` folder, which the sample hosts serve as `/_content/CrestApps.AI.Resources/...` and which the `@crestapps/ai-chat-ui` npm package publishes. Hosts that consume the packages never run this build.

## Requirements

- Node.js 24, pinned in `.node-version` at the repository root. Another major version warns locally and fails in CI.
- npm 11. The lockfile is npm's; do not use another package manager.

## Commands

Run `npm install` once at the repository root (an npm workspace that includes `src/Resources/*`), and again when a `package.json` changes.

| Command | Effect |
|---|---|
| `npm run build` | Builds groups whose definition, sources or outputs changed (content hash, not timestamps) |
| `npm run rebuild` | Builds every group |
| `npm run watch` | Builds, then rebuilds a group when its sources change (copies are not watched) |
| `npm run dry-run` | Lists the selected groups and their inputs and outputs |
| `npm run clean` | Deletes the selected outputs and the build cache |
| `npm run lint` | ESLint over every `Assets/` source and the build scripts |
| `npm run check` | TypeScript check of sources whose first line is `// @ts-check` |
| `npm run test:unit` | Client-side unit tests for the shared marker and citation rules |

Filter any build command after `--`:

```bash
npm run build -- -n ai-chat.js,ai-chat.css
npm run build -- -t AI.Resources
```

## Assets.json

Each project declares its groups in an `Assets.json` next to its `.csproj`. The build discovers every `Assets.json` under `src/Primitives` and `src/Resources`.

```json
[
  {
    "action": "vite",
    "name": "my-widget.js",
    "source": "Assets/js/my-widget.js",
    "dest": "wwwroot/scripts/my-widget.js",
    "sourceMap": true
  },
  {
    "action": "vite",
    "name": "my-widget.css",
    "source": "Assets/css/my-widget.css",
    "dest": "wwwroot/styles/my-widget.css"
  }
]
```

| Key | Meaning |
|---|---|
| `action` | `vite` builds a script or stylesheet; `copy` copies files unchanged |
| `name` | Unique group name across the repository, used by `-n` |
| `source` | One path or an ordered list, relative to the project folder |
| `dest` | The readable output for `vite`; a folder ending in `/` or a file path for `copy` |
| `tags` | Optional extra tags for `-t` (every group is also tagged with its project name without `CrestApps.`) |
| `global` | Optional, for a `vite` script built from one package; exposes its exports as `window[global]` |
| `sourceMap` | Optional, scripts only; writes `.map` files beside the readable and minified outputs |

A `vite` group writes a readable file and a `.min` sibling. Scripts are classic scripts, not ES modules, so publish an API by assigning it to `window` explicitly; a top-level `var` stays inside the bundle. Several sources run in the listed order, so list helpers first. Stylesheets compile `.scss` and `.css` with Dart Sass, minify with Lightning CSS, and have no source maps.

## Rules

- Commit the rebuilt `wwwroot` files with the sources that changed them. The pull-request workflow runs lint, check and `npm run rebuild` and fails when `wwwroot` differs.
- Add a third-party library as a dev dependency of the project that ships it, for example `npm install -D some-library@1.2.3 -w @crestapps/ai-chat-ui`, then copy its built files with a `copy` group or bundle it with a `vite` group that sets `global`.
- List page globals a script reads (such as `marked` or `DOMPurify`) in `pageGlobals` in `eslint.config.mjs`.
- Write intentionally ignored failures as `catch (error) { /* best effort */ }`; empty blocks fail lint.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `'node_modules/...' was not found in any node_modules folder` | Run `npm install` at the repository root |
| A global is missing on the page | Assign the API to `window` instead of a top-level `var` or function |
| The pull request fails the asset build check | Run `npm run rebuild` and commit the `wwwroot` changes |
| A group you expected is skipped | `npm run rebuild -- -n <group>` or `npm run clean` |

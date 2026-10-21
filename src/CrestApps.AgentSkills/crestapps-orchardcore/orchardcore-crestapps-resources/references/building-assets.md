# Building CrestApps Front-End Assets

Each module writes its scripts and stylesheets under `Assets/` and builds them into `wwwroot/`, which Orchard Core serves. The built files are committed, so you only build when you change a module's front end or add a library. NuGet package consumers never build.

## Requirements

- Node.js 24 (pinned in `.node-version` at the repository root). The build warns on another major version and fails in CI.
- npm 11. Use npm only; the lockfile is npm's.

## Install

```bash
npm install
```

Run it once after cloning and again whenever a `package.json` changes. The repository is a single npm workspace (`src/Modules/*`, `src/Startup/*`) with one root `package-lock.json`. Each module that ships a third-party library declares it in its own `package.json`.

## Commands

| Command | What it does |
|---|---|
| `npm run build` | Builds groups whose definition, sources or outputs changed (content based, not timestamps) |
| `npm run rebuild` | Builds every group |
| `npm run watch` | Builds, then rebuilds a group when its sources change (copies are not watched) |
| `npm run dry-run` | Lists selected groups and the files each reads and writes |
| `npm run clean` | Deletes selected groups' outputs and the build cache |
| `npm run lint` | ESLint over every module's `Assets` scripts |
| `npm run check` | TypeScript type-check of scripts starting with `// @ts-check` |

Filters go after `--`. A group is selected when it matches any filter:

```bash
npm run build -- -n soft-phone.js
npm run build -- -n soft-phone.js,telephony-client.js
npm run build -- -t Telephony
npm run watch -- -t ContactCenter
npm run dry-run -- -t vendor
```

Every group is tagged with its module's short name (the module name without `CrestApps.OrchardCore.`), and vendored libraries carry the `vendor` tag. Commit the changed `wwwroot` files with their sources; a pull request runs `npm run rebuild` and fails when committed outputs differ.

## Assets.json format

`Assets.json` sits next to the module's `.csproj` and holds an array of groups. The build discovers every `Assets.json` under `src/Modules`, `src/Startup` and `src/Themes`.

```json
[
  {
    "action": "vite",
    "name": "telephony-client.js",
    "source": [
      "Assets/js/shared/call-timer.js",
      "Assets/js/telephony-client.js"
    ],
    "dest": "wwwroot/scripts/telephony-client.js"
  },
  {
    "action": "vite",
    "name": "soft-phone.css",
    "source": "Assets/scss/soft-phone.scss",
    "dest": "wwwroot/styles/soft-phone.css"
  }
]
```

| Key | Meaning |
|---|---|
| `action` | `vite` builds a script or stylesheet; `copy` copies files unchanged |
| `name` | Unique across the repository; selected by `-n` |
| `source` | A path or an ordered list of paths relative to the module folder |
| `dest` | For `vite`, the readable output file. For `copy`, a folder ending in `/`, or a file path to copy one source under another name |
| `tags` | Optional extra tags for `-t` |
| `global` | Optional, for a `vite` script built from one package; exposes its exports as `window[global]` |

The old `inputs`, `output` and `copy` keys are no longer read.

A `vite` group writes the readable `dest` and a minified sibling (`.min.js` or `.min.css`). Register both; Orchard Core serves the minified file in production:

```csharp
_manifest
    .DefineScript("telephony-client")
    .SetUrl(
        "~/CrestApps.OrchardCore.Telephony/scripts/telephony-client.min.js",
        "~/CrestApps.OrchardCore.Telephony/scripts/telephony-client.js")
    .SetVersion("1.0.0");
```

## Scripts

Scripts are bundled by Vite into a classic script (not an ES module) loaded with a plain `<script>` tag. Write helpers as ES modules and point `source` at the main script; the build follows imports.

```js
// Assets/js/soft-phone/format.js
export function formatPhoneNumber(number) {
    // ...
}
```

```js
// Assets/js/soft-phone.js
import { formatPhoneNumber } from './soft-phone/format.js';

(function () {
    'use strict';
    // ...
}());
```

Each source is its own strict-mode module:

- A top-level `var` stays inside the bundle. Publish a page API through `window`, for example `window.checkoutPayment = (function () { ... })();`.
- Assigning an undeclared name throws a `ReferenceError` instead of creating a global.

When a script reads a global defined by another script (such as `signalR` or `Stripe`), add it to the page globals in `eslint.config.mjs`.

## Stylesheets

Groups whose `dest` ends in `.css` compile `.scss` and `.css` with Dart Sass and minify the `.min.css` with Lightning CSS.

## Third-party libraries

```bash
npm install some-library@1.2.3 -w crestapps.orchardcore.resources
```

Copy browser-ready files (a `node_modules/` path resolves in the module or the repository root):

```json
[
  {
    "action": "copy",
    "name": "flatpickr-js",
    "source": [
      "node_modules/flatpickr/dist/flatpickr.js",
      "node_modules/flatpickr/dist/flatpickr.min.js"
    ],
    "dest": "wwwroot/vendors/flatpickr/js/",
    "tags": ["vendor"]
  }
]
```

Bundle a package that ships only ES modules or CommonJS into one global:

```json
[
  {
    "action": "vite",
    "name": "sip.js",
    "source": "node_modules/sip.js/lib/index.js",
    "dest": "wwwroot/vendors/sip.js/sip.js",
    "global": "SIP",
    "tags": ["vendor"]
  }
]
```

Pin exact versions for libraries whose behavior you tested, such as telephony SDKs.

## Add an asset to a module

1. Write the source under `Assets/`, for example `Assets/js/my-widget.js`.
2. Add a group to the module's `Assets.json` (create it next to the `.csproj` if missing).
3. Run `npm run build -- -n my-widget.js`.
4. Register the `.min.js` and `.js` pair in the module's resource manifest and require it from a view or driver.
5. Commit the source, the `Assets.json` change and the built `wwwroot` files.

## Troubleshooting

| Problem | Fix |
|---|---|
| `'node_modules/...' was not found in any node_modules folder` | Run `npm install` at the repository root |
| Node.js version warning | Switch to Node.js 24 |
| Global missing on the page | Assign the API to `window` instead of a top-level `var` |
| Pull request fails "Test building assets" | Run `npm run lint`, `npm run check`, `npm run rebuild`, then commit the `wwwroot` changes |
| `npm run build` skips a group | Run `npm run rebuild -- -n <group name>` or `npm run clean` |

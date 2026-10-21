---
name: orchardcore-asset-manager
description: Skill for building, watching, hosting, and copying frontend assets of Orchard Core modules and themes with the Orchard Core Assets Manager. Covers which parts of the asset pipeline work outside the Orchard Core repository, vendoring the assets-manager tool into your own solution, the three-tier package structure, Assets.json actions (vite, sass with RTL output, min, copy, concat, parcel, webpack, run), the orchard-minify output convention, build.config.mjs and assetsLookupGlob, Yarn workspaces and NPM aliasing, Vue 3 admin apps built as ES modules, the Bloom library, migration from Gulp, yarn build, watch, host, dry-run and clean commands, and troubleshooting. Use this skill when modifying SCSS, JS, TS, or Vue files of a module or theme, adding an Assets.json entry, or fixing asset build failures.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Asset Manager

The Orchard Core Assets Manager compiles the frontend sources of modules and themes into their `wwwroot` folders. It is a Node.js CLI based on [Concurrently](https://github.com/open-cli-tools/concurrently) that reads `Assets.json` files and runs Parcel, Vite, Webpack, Sass, minification, concatenation, copy, or arbitrary shell actions. Its own code is ES modules (`.mjs`).

The Asset Manager only **produces files**. Orchard Core serves and declares them through Resource Management (resource manifests and the `<script>`/`<style>` tag helpers), which works the same whatever tool built the files; see `orchardcore-resources`.

## What works from your own solution

Orchard Core packages already contain their compiled `wwwroot` files, so you never build Orchard Core's own assets. For your modules and themes:

| Piece | Outside the Orchard Core repository |
| --- | --- |
| Resource manifests, `asp-src`/`debug-src`, `.min.*` convention | Always works; independent of how the files are built. |
| `@orchardcore/assets-manager` CLI (`yarn build`, `watch`, `host`, `dry-run`, `clean`) | **Not published to npm.** It is a Yarn workspace inside the Orchard Core repository. Use it by copying `.scripts/assets-manager` into your repository (see below); every action then works. |
| `Assets.json` actions and the `orchard-minify` Vite plugin | Part of the CLI, so available once the tool is vendored. |
| Bloom (`@bloom` alias, `.scripts/bloom`) | **Repository-only** workspace, not published. Do not import it from an external module; use your own packages or the registered resources (`vuejs`, `signalr`, ...). |
| Parcel `bundleEntrypoint` | Works, but `parcelBundleOutput` must point to a folder of **your** solution. |
| Node version prompt (fnm/Volta) | Only when a `.node-version` file exists two levels above the tool (the repository root). |

When you do not want to vendor the tool, run any bundler directly (for example `vite build`, `sass`, or npm scripts) and write its output to the module's `wwwroot`. The rest of this skill applies to the vendored tool, and to the Orchard Core repository itself.

## Set up the tool in your solution

1. Copy `.scripts/assets-manager/` from the Orchard Core repository (same Orchard Core version as your packages) to `.scripts/assets-manager/` at your repository root. Orchard Core is BSD-3-Clause licensed; keep its license notice.
2. Add a root `package.json` that makes the tool and your `Assets` folders Yarn workspaces:

   ```json
   {
     "name": "@contoso/root",
     "private": true,
     "packageManager": "yarn@4.17.0",
     "scripts": {
       "build": "assets-manager build",
       "watch": "assets-manager watch",
       "host": "assets-manager host",
       "copy": "assets-manager copy",
       "dry-run": "assets-manager dry-run",
       "clean": "assets-manager clean"
     },
     "workspaces": [
       ".scripts/assets-manager",
       "src/Modules/*/Assets/**",
       "src/Themes/*/Assets/**"
     ],
     "devDependencies": {
       "@orchardcore/assets-manager": "workspace:^"
     }
   }
   ```

3. Add `.yarnrc.yml` with `nodeLinker: node-modules`. The tool resolves `node_modules/...` paths from the workspace root, so Plug'n'Play does not work.
4. Add `build.config.mjs` next to the root `package.json`. Without `assetsLookupGlob` the tool searches `src/{Modules,Themes}/*/Assets.json`:

   ```js
   export const assetsLookupGlob = "src/{Modules,Themes}/**/Assets.json";
   ```

5. Optionally pin Node.js with a root `.node-version` file (Orchard Core uses Node.js 24.x LTS).
6. Run `corepack enable`, then `yarn` from the repository root. If Corepack is missing (some Node.js distributions), run `npm install -g corepack` first.
7. Commit the generated `wwwroot` files with their sources, or run `yarn build` in CI before `dotnet publish`; Orchard Core serves only what is in `wwwroot`.

## Package structure (three tiers)

| Location | Responsibility |
| --- | --- |
| Root `package.json` | Yarn workspaces, top-level scripts, shared dev dependencies, optional `resolutions` |
| `.scripts/assets-manager/package.json` | The build toolchain (Parcel, Vite, Webpack, Sass, PostCSS, esbuild, Lightning CSS) |
| Module or theme `Assets/package.json` | Runtime libraries bundled into that module or theme |

Add a runtime dependency from the module's `Assets` folder, then reinstall from the root:

```bash
cd src/Modules/Contoso.Rating/Assets
yarn add some-library@1.2.3
cd ../../../..
yarn
yarn build -n rating
```

Change build tools only in `.scripts/assets-manager/package.json` and rerun `yarn`. Do not add a separate workspace `package.json` inside a Vite app folder; use the module's `Assets/package.json`.

## Assets.json

`Assets.json` sits at the module or theme root and is a JSON array of actions (JSON5, so comments are allowed). Paths are relative to that project, except `node_modules/...` sources, which resolve from the workspace root. `name` is used by `-n` and `tags` (string or array) by `-t`. `dest` is always a folder; files are never renamed.

### Parcel

The simplest bundler, with no configuration:

```json
[
  {
    "action": "parcel",
    "name": "rating",
    "source": "Assets/Scripts/rating.js",
    "dest": "wwwroot/Scripts/rating",
    "tags": ["js", "admin"]
  }
]
```

- `source` is the entry point and `dest` is required. Give every Parcel action its own `dest`, because the folder is cleaned before build and watch.
- Parcel writes a `.js`, a `.min.js` without a source-map reference, and a `.map`. An `options` object overrides Parcel options.
- `bundleEntrypoint` combines several entries into one bundle written to `parcelBundleOutput` from `build.config.mjs`; omit `dest` then, and build bundles only with `yarn build -b`.

### Vite

Use Vite for Vue and TypeScript apps. `source` is the folder that contains `vite.config.ts` or `vite.config.js`:

```json
[
  {
    "action": "vite",
    "name": "rating-app",
    "source": "Assets/rating-app/",
    "tags": ["admin", "js"]
  }
]
```

Set an absolute `build.outDir`:

```ts
import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import path from "node:path";
import { fileURLToPath } from "node:url";

const directory = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  plugins: [vue()],
  build: {
    outDir: path.resolve(directory, "../../wwwroot/Scripts/rating-app"),
  },
});
```

During `build` and `watch` the tool injects its `orchard-minify` plugin, which minifies JavaScript with esbuild and CSS with Lightning CSS after Vite writes the bundle:

| Output | Content |
| --- | --- |
| `file.js` / `file.css` | Minified, with a `sourceMappingURL` reference (debug) |
| `file.min.js` / `file.min.css` | Minified, without a source-map reference (production) |
| `file.map` / `file.css.map` | Source map |

Do not set `build.minify` in `vite.config.ts`; the plugin owns minification. Register both files: `SetUrl("~/Contoso.Rating/Scripts/rating-app/main.min.js", "~/Contoso.Rating/Scripts/rating-app/main.js")`.

### Vue 3 admin apps and ES modules

Orchard Core 4.0 admin Vue apps use Vue 3, admin scripts no longer use jQuery or jQuery UI, and AJAX-injected part and field editors are ES modules. Build admin apps as ES module libraries:

```ts
import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import path from "node:path";
import { fileURLToPath } from "node:url";

const directory = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  resolve: {
    alias: { vue: "vue/dist/vue.esm-bundler.js" },
  },
  plugins: [vue()],
  build: {
    outDir: path.resolve(directory, "../../wwwroot"),
    emptyOutDir: false,
    lib: {
      entry: path.resolve(directory, "src/main.ts"),
      formats: ["es"],
      fileName: () => "Scripts/my-app.js",
    },
  },
});
```

Declare the output as a module script and require it by name:

```csharp
manifest
    .DefineScript("my-app")
    .SetUrl("~/Contoso.Rating/Scripts/my-app.min.js", "~/Contoso.Rating/Scripts/my-app.js")
    .SetAttribute("type", "module");
```

```html
<script type="module" asp-name="my-app" at="Foot"></script>
```

Inside the Orchard Core repository, apps also import the shared **Bloom** library (SignalR client, event bus, notifications, media API client, OIDC helpers) through an `@bloom` alias to `.scripts/bloom`, and get `@microsoft/signalr` from it. External modules cannot rely on Bloom (see the table above).

### Sass

```json
[
  {
    "action": "sass",
    "name": "rating-styles",
    "source": "Assets/scss/rating.scss",
    "dest": "wwwroot/Styles/",
    "generateRTL": true,
    "tags": ["admin", "css"]
  }
]
```

`source` is a file or glob; `dest` defaults to `wwwroot/Styles/`. The action writes `.css`, `.min.css`, and a map; `generateRTL` also produces RTL output with `postcss-rtlcss`.

### Min

```json
[
  {
    "action": "min",
    "name": "rating-field",
    "source": "Assets/js/rating-field.js",
    "dest": "wwwroot/Scripts/",
    "tags": ["admin", "js"]
  }
]
```

Minifies a file or glob into `.min.js` / `.min.css` plus a map. Use it for plain scripts that need no bundling.

### Copy

```json
[
  {
    "action": "copy",
    "name": "vendor-bootstrap",
    "source": [
      "node_modules/bootstrap/dist/css/bootstrap.css",
      "node_modules/bootstrap/dist/js/bootstrap.bundle.js"
    ],
    "dest": "wwwroot/Vendor/bootstrap/",
    "tags": ["resources"]
  }
]
```

- `source` is a path, a glob, or an array mixing both. With `**` the base folder is detected and the relative structure is kept below `dest`.
- When `dest` is omitted it is inferred from `tags` and the extension of the first source (for example `wwwroot/Scripts/` for `js`).
- `copy` runs on `build` and `copy`, never on `watch`. Add `"dryRun": true` to a copy or min action, or run `yarn dry-run`, to log what would be written.

### Concat

```json
[
  {
    "action": "concat",
    "name": "rating-bundle",
    "source": [
      "node_modules/some-library/dist/some-library.js",
      "Assets/js/rating.js"
    ],
    "dest": "wwwroot/Scripts"
  }
]
```

`concat` physically joins an array of files (no globs) in order; it is not a bundler. Because `node_modules/...` always resolves from the root, a different version installed locally in one workspace is ignored. Keep versions equal, force them with root `resolutions`, use an NPM alias for a genuinely different version, or switch to a bundler:

```json
{
  "dependencies": {
    "bootstrap": "5.3.8",
    "bootstrap-4.6.1": "npm:bootstrap@4.6.1"
  }
}
```

Then reference `node_modules/bootstrap-4.6.1/dist/js/bootstrap.js`.

### Webpack

```json
[
  {
    "action": "webpack",
    "name": "rating-webpack",
    "config": "Assets/webpack.config.js",
    "tags": ["js"]
  }
]
```

`config` is the `webpack.config.js` path relative to the module root.

### Run

Run any command through Concurrently (for a project with its own build tool):

```json
[
  {
    "action": "run",
    "name": "rating-viewer",
    "source": "Assets/rating-viewer",
    "scripts": {
      "build": "yarn build",
      "watch": "yarn start"
    }
  }
]
```

`source` is the working directory. The `scripts` keys match the pipeline command, so `yarn build` runs every `build` script. Concurrently retries builds up to three times.

## Commands

Run from the repository root:

| Command | Use |
| --- | --- |
| `yarn build` | Build all discovered assets |
| `yarn build -n rating` | Build one named action (`-n`, `--name`, `--names`) |
| `yarn build -n rating,rating-app` | Build several named actions |
| `yarn build -t admin` | Build actions with a tag (`-t`, `--tag`, `--tags`) |
| `yarn build -b` | Build only Parcel `bundleEntrypoint` bundles |
| `yarn watch -n rating` | Rebuild on save (does not copy) |
| `yarn host -n rating-app` | Start the bundler dev server with HMR |
| `yarn copy -n vendor-bootstrap` | Run copy actions |
| `yarn dry-run -n rating` | Log what copy, min, and concat would write |
| `yarn clean` | Delete generated output and `.parcel-cache` |

`-n` matches the `name` in `Assets.json`, not the `package.json` name; a wrong value silently matches nothing or, without `-n`, builds everything. The tasks also run from Visual Studio's Task Runner Explorer.

## build.config.mjs

| Export | Purpose |
| --- | --- |
| `assetsLookupGlob` | Glob for `Assets.json` files (default `src/{Modules,Themes}/*/Assets.json`) |
| `parcelBundleOutput` | Output folder for `bundleEntrypoint` bundles (required when any action uses it) |
| `parcel(type, group)` | Returns Parcel option overrides |
| `vite(type, group)` | Returns shared Vite configuration |

```js
export const assetsLookupGlob = "src/{Modules,Themes}/**/Assets.json";
export const parcelBundleOutput = "src/Modules/Contoso.Core/wwwroot/Scripts/bundle";

export function parcel() {
  return {
    targets: {
      default: {
        engines: { browsers: "> 1%, last 4 versions, not dead" },
      },
    },
  };
}
```

Keep JavaScript modules consistent: set `"type": "module"` in a package that must run as ESM, alias Vue to its ESM bundler build when needed, and load Vite output with `type="module"`.

## Moving from Gulp

1. Record each Gulp task's inputs, output folder, minification, Sass processing, concatenation order, and watch behavior.
2. Map simple tasks to `copy`, `min`, `sass`, or `concat`; use Parcel, Vite, or Webpack for module bundling; use `run` for a project with its own tool.
3. Put runtime libraries in the module `Assets/package.json` and build tools in `.scripts/assets-manager/package.json`.
4. Compare the generated files, source maps, and resource manifest URLs with the Gulp output before deleting the Gulp task.
5. Switch development and CI to `yarn build`, `yarn watch`, or `yarn host`.

Orchard Core no longer ships a `gulpfile.js`; the Gulp guide is historical.

## Troubleshooting

| Problem | Fix |
| --- | --- |
| Node version warning | Use the version in `.node-version`; the tool offers to install it with fnm or Volta. On Windows restart the terminal after a first install so the shims are on `PATH`. |
| `Cannot find package ...` (for example `@tailwindcss/vite`) | Run `yarn` from the root after any `package.json` change. |
| No action found | Run from the root and check that `assetsLookupGlob` matches the `Assets.json` location. |
| Parcel does not rebuild after deleting output | `yarn clean`, then build again. |
| Copy output missing during watch | Expected; run `yarn build`. |
| `concat` picks the wrong package version | Align versions, add `resolutions`, use an NPM alias, or use a bundler. |
| Vite output in the wrong folder | Set `build.outDir` with `path.resolve()`; `source` must contain the Vite config. |
| Vite files unexpectedly minified or without maps | Remove `build.minify`; `orchard-minify` handles it. |
| A full build ran by accident | `git status --short` and revert changed `wwwroot` files outside your target. |
| Source edited but the running site is unchanged | The bundle is older than the source; rebuild with `yarn build -n <name>` and hard-refresh. |
| `@bloom/...` imports fail | Only available inside the Orchard Core repository with the `@bloom` alias and the `.scripts/bloom` workspace installed. |
| A library fails to transpile with Parcel | Check its module format and `type`; use Vite or Webpack with explicit configuration. |

## Sources

- [Orchard Core Assets Manager guide](https://docs.orchardcore.net/en/latest/guides/assets-manager/)
- [Orchard Core 3.0.0 release notes](https://docs.orchardcore.net/en/latest/releases/3.0.0/#asset-manager)
- [Orchard Core 4.0.0 release notes](https://docs.orchardcore.net/en/latest/releases/4.0.0/)

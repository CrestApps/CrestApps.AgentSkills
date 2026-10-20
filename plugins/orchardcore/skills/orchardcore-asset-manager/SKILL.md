---
name: orchardcore-asset-manager
description: Skill for building, watching, hosting, and copying frontend assets in Orchard Core 3.0 and 4.0 with the Assets Manager. Covers Assets.json actions, Concurrently, Parcel, Vite, Webpack, Sass, min, concat and copy actions, Yarn workspace setup, Vue 3 admin apps built as ES modules, the shared Bloom library and the bloom alias, build.config.mjs, migration from Gulp, common commands, package dependencies, and troubleshooting. Use this skill for framework-only Orchard Core modules and themes that manage SCSS, JavaScript, TypeScript, Vue, or other frontend assets.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Asset Manager

Use the Orchard Core Asset Manager for frontend assets in Orchard Core 3.0 and later modules and themes. It is based on [Concurrently](https://github.com/open-cli-tools/concurrently), so it can run shell commands as well as Parcel, Vite, Webpack, Sass, minification, concatenation, and copy actions. Definitions live in `Assets.json` files and the tool uses ES modules (`.mjs`) for its own configuration.

## Scope and transition from Gulp

- This skill covers the framework's development asset pipeline for Orchard Core modules and themes. It does not cover the Media module's runtime media storage or image processing.
- The Asset Manager replaced the Gulp pipeline. The Orchard Core repository no longer ships a `gulpfile.js`, and the Gulp pipeline guide is kept for historical reference only. Use the Asset Manager for all new work and migrate custom Gulp pipelines once their output and resource references are verified.
- The Asset Manager builds files into `wwwroot`; it does not register those files with Orchard Core's Resource Management system. Keep the existing resource manifest or tag helper registration and point it to the generated files.

## Prerequisites and project setup

1. Install the Node.js LTS version required by the repository, pinned in the root `.node-version` file (Node.js 24.x LTS for Orchard Core 3.0 and 4.0).
2. From the repository root, enable Corepack and install workspace dependencies:

   ```bash
   corepack enable
   yarn
   ```

   Verify that the Yarn version matches the `packageManager` value in the root `package.json`. If Node.js came from a distributor without Corepack, install Corepack first with `npm install -g corepack`.
3. Keep the three package responsibilities separate:

   | Location | Responsibility |
   | --- | --- |
   | Root `package.json` | Yarn workspaces, top-level scripts, general development dependencies, and optional `resolutions` |
   | `.scripts/assets-manager/package.json` | Asset Manager CLI and build tools such as Parcel, Vite, Webpack, Sass, and PostCSS |
   | Module or theme `Assets/package.json` | Runtime frontend dependencies shipped by that module or theme |

   Add a module or theme dependency from its `Assets` directory, then run `yarn` from the repository root:

   ```bash
   cd src/OrchardCore.Modules/YourModule/Assets
   yarn add your-library
   cd ../../../../
   yarn
   ```

   Update the build toolchain only in `.scripts/assets-manager/package.json`. Use root `resolutions` when the workspace must force one version.

4. Add `Assets.json` at the module or theme root. Its paths are relative to that project. The `source` entry points to an input file or folder, and `dest` is a folder when the action writes files.

## Assets.json

`Assets.json` is a JSON array of named actions. Names are used by `-n` filters and tags are used by `-t` filters.

### Parcel

Parcel is the recommended simple starting point because it needs little configuration:

```json
[
  {
    "action": "parcel",
    "name": "your-module",
    "source": "Assets/Scripts/app.js",
    "dest": "wwwroot/Scripts/your-module",
    "tags": ["js", "admin"]
  }
]
```

The `source` is the Parcel entry point. Set a different `dest` folder for each Parcel action because the folder is cleaned before a build or watch operation. Parcel can also use `bundleEntrypoint` to place several applications in the shared output configured by `build.config.mjs`; when it is used, omit `dest`.

Parcel creates JavaScript source-map output. Register the minified and non-minified files in the resource manifest, for example `SetUrl("~/YourModule/Scripts/app.min.js", "~/YourModule/Scripts/app.js")`.

### Vite

Use Vite when the application needs a Vite configuration, such as a Vue app:

```json
[
  {
    "action": "vite",
    "name": "your-vue-app",
    "source": "Assets/vite-project",
    "tags": ["admin", "dashboard", "js"]
  }
]
```

`source` must be the folder containing `vite.config.ts` or `vite.config.js`. Configure an absolute output directory in Vite:

```ts
import { defineConfig } from "vite";
import path from "node:path";
import { fileURLToPath } from "node:url";

const directory = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  build: {
    outDir: path.resolve(directory, "../../wwwroot/Scripts/your-app"),
  },
});
```

Keep Vite's own minification off (`build.minify: false`) for Asset Manager Vite builds. The Asset Manager injects its `orchard-minify` plugin for `build` and `watch`. It produces a source-map-aware file, a `.min.js` or `.min.css` file without a source-map reference, and a map file.

### Vue 3 admin apps and Bloom

In Orchard Core 4.0 the admin Vue apps (for example the Media Gallery) use Vue 3,
admin scripts no longer depend on jQuery or jQuery UI, and AJAX-injected field
and part editors are ES modules. Shared components and services (SignalR
client, event bus, notifications, media API client, OIDC helpers) live in the
**Bloom** workspace package under `.scripts/bloom` and are imported through a
Vite alias:

```ts
import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import path from "path";

export default defineConfig({
  resolve: {
    alias: {
      vue: "vue/dist/vue.esm-bundler.js",
      "@bloom": path.resolve(__dirname, "../../../../../.scripts/bloom"),
    },
  },
  plugins: [vue()],
  build: {
    outDir: path.resolve(__dirname, "../../wwwroot"),
    emptyOutDir: false,
    minify: false,
    lib: {
      entry: path.resolve(__dirname, "src/main.ts"),
      formats: ["es"],
      fileName: () => "Scripts/my-app.js",
    },
  },
});
```

Register ES module output as a module script in the resource manifest and
require it by name:

```csharp
manifest
    .DefineScript("my-app")
    .SetUrl("~/MyModule/Scripts/my-app.min.js", "~/MyModule/Scripts/my-app.js")
    .SetAttribute("type", "module");
```

```html
<script type="module" asp-name="my-app" at="Foot"></script>
```

Bundled apps get `@microsoft/signalr` through Bloom instead of the `signalr`
resource.

### Webpack

Use the Webpack action when the project already has a Webpack configuration:

```json
[
  {
    "action": "webpack",
    "name": "your-webpack-app",
    "config": "Assets/webpack.config.js",
    "tags": ["js"]
  }
]
```

`config` points to the `webpack.config.js` file.

### Concurrently run actions

Use `run` to execute any project command through Concurrently:

```json
[
  {
    "action": "run",
    "name": "your-app",
    "source": "Assets/your-app",
    "scripts": {
      "build": "yarn build",
      "watch": "yarn start"
    }
  }
]
```

`source` is the command working directory. The `scripts` keys must match the pipeline command. For example, `yarn build` runs each `build` script. Concurrently retries builds up to three times, which helps reduce transient CI failures.

### Copy, min, Sass, and concat

Use `copy` for files that do not need bundling:

```json
[
  {
    "action": "copy",
    "name": "vendor-bootstrap",
    "source": [
      "node_modules/bootstrap/dist/css/bootstrap.css",
      "node_modules/bootstrap/dist/js/bootstrap.js"
    ],
    "dest": "wwwroot/Vendor/bootstrap",
    "tags": ["resources"]
  }
]
```

- `source` can be one path, a glob, or an array of paths and globs. `dest` is always a folder and files are not renamed.
- `copy` does not watch. A `build` copies files; `watch` does not.
- `min` minifies a file or glob into a destination folder.
- `sass` transpiles SCSS into a destination folder.
- `concat` takes an array of files and physically joins them in the listed order. It is not a module resolver or bundler.

When `copy` uses `**`, the base folder is detected and preserved below `dest`. Use `dryRun` on copy or min actions to inspect matched files and destinations before writing output.

For `concat` sources beginning with `node_modules/`, resolution uses the workspace root `node_modules` directory. Keep shared package versions equal, enforce them with root `resolutions`, use an NPM alias for genuinely different versions, or use a bundler instead:

```json
{
  "dependencies": {
    "bootstrap": "5.3.8",
    "bootstrap-4.6.1": "npm:bootstrap@4.6.1"
  }
}
```

## Commands

Run commands from the repository root:

| Command | Use |
| --- | --- |
| `yarn build` | Build all discovered assets |
| `yarn build -n your-name` | Build one named action |
| `yarn build -n first,second` | Build multiple named actions |
| `yarn build -t admin` | Build actions with a tag |
| `yarn watch -n your-name` | Rebuild a named action when source files change |
| `yarn host -n your-name` | Start a bundler development server |
| `yarn copy -n your-name` | Run copy actions |
| `yarn dry-run -n your-name` | Preview copy, min, and concat actions without writing files |
| `yarn clean` | Clean generated folders and the Parcel cache |

Use `-n`, `--name`, or `--names` for names and `-t`, `--tag`, or `--tags` for tags. `watch` does not copy files, so run `yarn build` after changing a copy action. The Asset Manager can also run from Visual Studio Task Runner Explorer, and the `Asset Bundler Tool Debug` VS Code launcher is available in the Orchard Core repository.

## Configuration

Create `build.config.mjs` next to the root `package.json` to customize tool options:

```js
export const assetsLookupGlob = "src/{OrchardCore.Themes,OrchardCore.Modules}/**/Assets.json";
export const parcelBundleOutput = "src/OrchardCore.Modules/OrchardCore.Resources/wwwroot/Scripts/bundle";

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

`parcelBundleOutput` is the shared folder used by Parcel actions that set `bundleEntrypoint`.

Use `viteConfig` for shared Vite configuration. Keep JavaScript modules consistent: add `"type": "module"` to a package when it must run as ESM, use the ESM Vue alias when required, and emit Vite scripts with `type="module"` in HTML.

## Moving from Gulp

1. Record each Gulp task's input files, output folder, minification, Sass processing, concatenation order, and watch behavior.
2. Create a named `Assets.json` action. Map simple tasks to `copy`, `min`, `sass`, or `concat`; use Parcel, Vite, or Webpack for module bundling; use `run` for a command that has its own build tool.
3. Put runtime libraries in the module or theme `Assets/package.json`, and put shared build tools in `.scripts/assets-manager/package.json`.
4. Compare the generated files, source maps, and resource manifest URLs with the old Gulp output before removing the Gulp task.
5. Switch development and CI commands to `yarn build`, `yarn watch`, or `yarn host`.

Do not treat Asset Manager migration as a replacement for Resource Management. The migration changes how files are generated; it does not change how Orchard Core serves or declares them.

## Troubleshooting

- **Node version warning:** Use the version in `.node-version`. The tool can offer to install it through `fnm` or Volta. On Windows, restart the terminal after installing a version manager so its shims are on `PATH`.
- **Yarn or Corepack is missing:** Run `npm install -g corepack`, then `corepack enable` and `yarn` from the repository root. Confirm the Yarn version matches `package.json`.
- **No action is found:** Run from the repository root, confirm `Assets.json` is in a module or theme folder matched by `assetsLookupGlob` in `build.config.mjs`.
- **`@bloom/...` imports fail:** Add the `@bloom` alias to the Vite config and run `yarn` at the root so the `.scripts/bloom` workspace is installed.
- **Copy output is missing during watch:** This is expected. `watch` does not run copy actions; use `yarn build`.
- **Parcel does not rebuild after deleting output:** Run `yarn clean` to remove `.parcel-cache`, then build again. Give each Parcel action its own `dest`.
- **Concat uses the wrong package version:** Align workspace versions, add a root `resolutions` entry, use an NPM alias, or replace concat with a bundler. Concat reads the hoisted root package.
- **Vite output is in the wrong folder:** Set `build.outDir` with `path.resolve()` and ensure the `Assets.json` `source` folder contains the Vite config.
- **Vite files are unexpectedly minified or lack maps:** Keep `build.minify: false`; the Asset Manager's minification plugin owns this step. Use the non-min file for debugging and `.min.*` for production.
- **A library fails to transpile with Parcel:** Check the package's module format and its `package.json` `type` value. Use Vite or Webpack when the library needs explicit bundler configuration.

## Sources

- [Orchard Core Assets Manager guide](https://docs.orchardcore.net/en/latest/guides/assets-manager/) — prerequisites, `Assets.json`, Concurrently, supported actions, commands, package layout, bundlers, and troubleshooting notes.
- [Orchard Core 3.0.0 release notes](https://docs.orchardcore.net/en/latest/releases/3.0.0/#asset-manager) — Asset Manager introduction and the reason for replacing Gulp.
- [Orchard Core 4.0.0 release notes](https://docs.orchardcore.net/en/latest/releases/4.0.0/) — Vue 3 admin apps, removal of jQuery from admin scripts, and ES module editors.

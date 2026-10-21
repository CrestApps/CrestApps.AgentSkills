---
name: orchardcore-tester
description: Tests a site built on Orchard Core through browser automation with playwright-cli. Use when the user needs to build, run, set up, or test an Orchard Core web project and its modules and themes. Covers running the site on a random port in the background, unattended provisioning with Auto Setup environment variables, the interactive setup wizard, the password field workaround for setup and login forms, playwright-cli browser installation and eval rules, log files, and the Orchard Core 4.0 admin UI including the redesigned admin menu, breadcrumb page titles, the Ctrl+K quick navigation palette, List and Grid admin list layouts, page size selection, and admin pages without jQuery. Use this skill when requests mention Orchard Core feature testing, running the site in the background, Auto Setup for tests, the setup wizard, or related testing or troubleshooting work. Strong matches include OrchardCore.Cms.Web, OrchardCore.AutoSetup, AddSetupFeatures, playwright-cli.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Feature Testing

This skill guides you through testing a site built on Orchard Core (its web project plus your modules and themes) using browser automation with `playwright-cli`.

## Prerequisites

- An Orchard Core web (startup) project, for example one created with `dotnet new occms`. Below it is `$webProject` (PowerShell) or `$WEB_PROJECT` (bash), such as `src/MySite.Web`. Inside the Orchard Core repository itself it is `src/OrchardCore.Cms.Web`.
- .NET SDK matching the project's target framework. Add `-f <tfm>` to `dotnet build`/`dotnet run` only when the project targets several frameworks.
- The `playwright-cli` skill. If the CLI is missing, install it with `npm install -g @playwright/cli@latest` (this installs the latest global version; the commands below remain valid).
- A browser engine for `playwright-cli`. Without Chrome (for example on macOS) install webkit and pass `--browser webkit` on the first `open` of a session; see `references/playwright-cli.md`.

## Core Workflow

1. **Build** the web project
2. **Run** it in the background on a session port
3. **Set up** a test site (Auto Setup is the recommended unattended path)
4. **Test** the feature in the browser
5. **Verify** results and clean up

## Fastest Reliable Path (bash)

```bash
WEB_PROJECT=src/MySite.Web

# 1. Build.
dotnet build "$WEB_PROJECT" -c Debug

# 2. Fresh state and a session port.
rm -rf "$WEB_PROJECT/App_Data"
PORT=$(( (RANDOM % 1000) + 5000 )); echo -n $PORT > .orchardcore-port

# 3. Run with Auto Setup (provisions the Default tenant on the first request).
#    Values are bare: quotes would become part of the value.
OrchardCore__AutoSetup__AutoSetupPath= \
OrchardCore__AutoSetup__Tenants__0__ShellName=Default \
OrchardCore__AutoSetup__Tenants__0__SiteName=TestSite \
OrchardCore__AutoSetup__Tenants__0__SiteTimeZone=America/Los_Angeles \
OrchardCore__AutoSetup__Tenants__0__AdminUsername=admin \
OrchardCore__AutoSetup__Tenants__0__AdminEmail=admin@test.com \
OrchardCore__AutoSetup__Tenants__0__AdminPassword=Password1! \
OrchardCore__AutoSetup__Tenants__0__DatabaseProvider=Sqlite \
OrchardCore__AutoSetup__Tenants__0__RecipeName=Blog \
dotnet run --project "$WEB_PROJECT" --no-build --urls "http://localhost:$PORT" > autosetup-console.log 2>&1 &

# 4. Trigger and confirm.
curl -s -o /dev/null "http://localhost:$PORT/"
grep -m1 "successfully provisioned" autosetup-console.log
```

This requires Auto Setup to be registered in the web project (see Step 3). Then log in as described in Step 4; the login password field needs the native-setter workaround.

## Step 1: Build

```powershell
$webProject = "src/MySite.Web"
dotnet build $webProject -c Debug
```

## Step 2: Run Application (Background)

Several agents may run the site from different worktrees at the same time, so use a random port and run in the background.

### Get or Create Session Port

```powershell
# Check for existing port file, or generate random port (5000-5999)
$portFile = ".orchardcore-port"
if (Test-Path $portFile) {
    $port = Get-Content $portFile
} else {
    $port = Get-Random -Minimum 5000 -Maximum 6000
    $port | Out-File $portFile -NoNewline
}
Write-Host "Using port: $port"
```

### Start Application in Background

```powershell
# Start the site in a background process
$proc = Start-Process dotnet `
    -ArgumentList "run","--no-build","--urls","http://localhost:$port" `
    -WorkingDirectory $webProject `
    -PassThru -NoNewWindow

# Save PID for later cleanup
$proc.Id | Out-File ".orchardcore-pid" -NoNewline
Write-Host "Started Orchard Core (PID: $($proc.Id)) on http://localhost:$port"
```

### Wait for Application Ready

```powershell
# Poll until app responds (max 60 seconds)
$port = Get-Content ".orchardcore-port"
$timeout = 60; $elapsed = 0
while ($elapsed -lt $timeout) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:$port" -UseBasicParsing -TimeoutSec 2
        Write-Host "Application ready at http://localhost:$port"
        break
    } catch {
        Start-Sleep -Seconds 2
        $elapsed += 2
    }
}
```

### Stop Application

```powershell
# Stop the background process
if (Test-Path ".orchardcore-pid") {
    $pid = Get-Content ".orchardcore-pid"
    Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
    Remove-Item ".orchardcore-pid" -Force
    Write-Host "Stopped Orchard Core (PID: $pid)"
}
```

```bash
# bash: backgrounding dotnet makes $! unreliable, so resolve the process by port.
PORT=$(cat .orchardcore-port)
PID=$(lsof -ti tcp:$PORT -sTCP:LISTEN | head -1)
[ -n "$PID" ] && kill "$PID" && echo "Stopped Orchard Core (PID: $PID)"
rm -f .orchardcore-pid
```

## Step 3: Setup Test Site

A fresh `App_Data` is uninitialized, so the site shows the setup wizard. Reset it to start over:

```powershell
Remove-Item -Recurse -Force "$webProject/App_Data"
```

### Option A: Auto Setup (recommended, unattended)

Auto Setup provisions tenants from the `OrchardCore:AutoSetup` configuration section on their first request, with no browser and no wizard. The module ships in `OrchardCore.Application.Cms.Targets`, but project templates do not register it; add it to the web project's `Program.cs` (the Orchard Core repository's `OrchardCore.Cms.Web` already does):

```csharp
builder.Services
    .AddOrchardCms()
    .AddSetupFeatures("OrchardCore.AutoSetup");
```

Then pass the settings as environment variables (see the bash path above), user secrets, or a launch profile. Key rules, detailed in `references/autosetup.md`:

- The prefix is `OrchardCore__AutoSetup__Tenants__0__<Option>`; `__` separates configuration levels. The deprecated `OrchardCore__OrchardCore_AutoSetup__` prefix still works.
- Do not quote values in the bash inline form; the quotes become part of the password or email.
- `SiteTimeZone` is required, and the first tenant must be `Default`.

Success is logged as `The AutoSetup successfully provisioned the site 'TestSite'.` Keep this configuration out of production; see `orchardcore-auto-setup` for all options.

### Option B: Interactive setup wizard

Run the site without Auto Setup settings and drive the wizard. Site Name, User Name and Email fill normally, the recipe is a dropdown button backed by a hidden `RecipeName` input, and the password fields need the native-setter workaround. Full steps: `references/setup-wizard.md`.

```bash
PORT=$(cat .orchardcore-port)
playwright-cli --browser webkit open "http://localhost:$PORT/"
playwright-cli snapshot
# Fill Site Name, User Name, Email; choose Blog in the recipe dropdown;
# set Password and PasswordConfirmation with the native setter; click Finish Setup.
```

## Step 4: Test Features

**Important**: Replace `5000` with the actual port from the `.orchardcore-port` file.

### Login to Admin

The username fills normally, but `fill`/`type` leave the login password (`input[name="LoginForm.Password"]`) empty with `playwright-cli`, so set it through the native value setter.

```bash
PORT=$(cat .orchardcore-port)
playwright-cli open "http://localhost:$PORT/Login"
playwright-cli snapshot
playwright-cli fill <username-ref> "admin"

playwright-cli eval '((p)=>{var s=Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype,"value").set; s.call(p,"Password1!"); p.dispatchEvent(new Event("input",{bubbles:true})); p.dispatchEvent(new Event("change",{bubbles:true})); return p.value.length})(document.querySelector("input[name=\"LoginForm.Password\"]"))'

playwright-cli click <login-button-ref>
playwright-cli open "http://localhost:$PORT/Admin"
playwright-cli eval 'window.location.pathname'   # "/Admin" when signed in
```

The `eval` may print a benign `result is not a function` message because the expression returns a number; the value is still set.

### Common Test Scenarios

**Test Media Library** (Vue 3 Media Gallery backed by the `api/media` endpoints):
```bash
playwright-cli open http://localhost:$port/Admin/Media
playwright-cli snapshot
```

**Test Content Creation**:
```bash
playwright-cli open http://localhost:$port/Admin/Contents/ContentItems
playwright-cli snapshot
# Click New, select content type, fill fields, publish
```

**Enable a Feature** (for example the module under test):
```bash
playwright-cli open http://localhost:$port/Admin/Features
playwright-cli snapshot
# Search for feature, click Enable
```

See `references/common-features.md` for detailed workflows.

## Step 5: Verify Results

After each action:
```bash
# Check page state
playwright-cli snapshot

# Check for JavaScript errors
playwright-cli console error

# Verify page title (the admin heading is rendered by the breadcrumb)
playwright-cli eval "document.title"
playwright-cli eval "document.querySelector('h1.oc-breadcrumb-title')?.textContent.trim()"
```

### Orchard Core 4.0 Admin UI Notes

- **Page title**: the admin `<h1>` is `h1.oc-breadcrumb-title`, under a `nav[aria-label="Breadcrumb"]` trail. Its text is also part of `document.title`.
- **Admin menu**: `ul#adminMenu`; the current item is `li.current`; items with children expand through `button.nav-toggle`. Below the `md` breakpoint the menu is an off-canvas drawer opened by `button.ta-sidebar-toggler`, so use a desktop-sized viewport or open the drawer first.
- **Quick navigation**: Ctrl+K (or `#adminQuickNavigationToggle`) opens a palette; type in `#adminQuickNavigationInput`, press Enter to go. Handy to reach pages without walking the menu.
- **Admin lists**: List or Grid layout (`?layout=Grid` when users may choose), row actions as buttons or an ellipsis menu, bulk **Actions** after selecting rows, optional **Items per page** selector (`?pageSize=`).
- **No jQuery in the admin**: use DOM APIs in `playwright-cli eval`; `window.jQuery` is undefined on admin pages.
- **Media Gallery**: a Vue 3 app; uploads go through `api/media` (and TUS when `OrchardCore.Media.Tus` is enabled). Wait for the file list to render before asserting.

See `references/admin-navigation.md` for selectors.

### Debugging with Log Files

Console output is not visible when running in the background. Sites using NLog (the `dotnet new occms` default) write `App_Data/logs/orchard-log-{date}.log`:

```powershell
# View last 50 lines of today's log
Get-Content "$webProject/App_Data/logs/orchard-log-$(Get-Date -Format 'yyyy-MM-dd').log" -Tail 50

# Search for errors
Select-String -Path "$webProject/App_Data/logs/orchard-log-$(Get-Date -Format 'yyyy-MM-dd').log" -Pattern "ERROR|Exception" -Context 2,5
```

See `references/debugging.md` for more debugging techniques.

## Quick Reference

| Task | URL Path |
|------|----------|
| Admin Dashboard | `/Admin` |
| Features | `/Admin/Features` |
| Content Items | `/Admin/Contents/ContentItems` |
| Content Types | `/Admin/ContentTypes/List` |
| Media Library | `/Admin/Media` |
| General Settings | `/Admin/Settings/general` |
| Admin Settings | `/Admin/Settings/admin` |
| Users | `/Admin/Users/Index` |
| Themes | `/Admin/Themes` |

## Session Files

| File | Purpose |
|------|---------|
| `.orchardcore-port` | Persisted port number for session |
| `.orchardcore-pid` | Process ID for cleanup |

Close browser sessions with `playwright-cli session-stop-all` when done.

## Default Credentials

- **Username**: admin
- **Email**: admin@test.com
- **Password**: Password1!

## References

- `references/autosetup.md` - Unattended Auto Setup for tests (environment variables, options, troubleshooting)
- `references/playwright-cli.md` - Browser install, `open` and `snapshot`, single-expression `eval`, the password field workaround
- `references/setup-wizard.md` - Interactive setup wizard
- `references/admin-navigation.md` - Admin URLs and UI patterns
- `references/common-features.md` - Feature-specific testing guides
- `references/debugging.md` - Log files and troubleshooting

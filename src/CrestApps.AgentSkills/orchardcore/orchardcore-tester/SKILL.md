---
name: orchardcore-tester
description: Tests Orchard Core CMS features through browser automation with playwright-cli. Use when the user needs to build, run, set up, or test Orchard Core functionality including admin features, content management, the Vue 3 Media Gallery, and module testing. Covers the Orchard Core 4.0 admin UI including the redesigned admin menu and off-canvas sidebar, breadcrumb page titles, the Ctrl+K quick navigation palette, List and Grid admin list layouts, page size selection, and admin pages without jQuery. Use this skill when requests mention Orchard Core Feature Testing, Core Workflow, Build, Run Application in the background, Session Port, setup wizard, or closely related Orchard Core testing or troubleshooting work. Strong matches include OrchardCore.Cms.Web, OrchardCore.Cms.Web.csproj, AGENTS.md, playwright-cli.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Feature Testing

This skill guides you through testing Orchard Core CMS features using browser automation with `playwright-cli`.

## Prerequisites

- OrchardCore repository (working directory)
- .NET SDK 10.0+ installed
- `playwright-cli` skill available
- If `playwright-cli` is unavailable, install it globally with `npm install -g @playwright/cli@latest`.

> **Warning:** This installs the latest global CLI version. The existing `playwright-cli` commands in this skill remain valid.

## Core Workflow

Testing an Orchard Core feature follows these steps:

1. **Build** the application
2. **Run** the application server (background)
3. **Setup** a test site (if needed)
4. **Test** the feature via browser
5. **Verify** results and clean up

## Step 1: Build

```powershell
dotnet build src/OrchardCore.Cms.Web/OrchardCore.Cms.Web.csproj -c Debug -f net10.0
```

## Step 2: Run Application (Background)

Since multiple agents may run Orchard Core from different worktrees simultaneously, use a random port and run in background.

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
# Start Orchard Core in background process
$proc = Start-Process dotnet `
    -ArgumentList "run","-f","net10.0","--no-build","--urls","http://localhost:$port" `
    -WorkingDirectory "src/OrchardCore.Cms.Web" `
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

## Step 3: Setup Test Site

**Check if setup is needed**: Navigate to the app URL - if you see a setup wizard, the site needs setup.

**Reset for fresh setup** (optional):
```powershell
Remove-Item -Recurse -Force src/OrchardCore.Cms.Web/App_Data
```

**Setup workflow**:
```bash
# Get the port
$port = Get-Content ".orchardcore-port"

playwright-cli open http://localhost:$port
playwright-cli snapshot
# Fill: Site Name = "Test Site", Recipe = "Blog", Username = "admin", 
# Email = "admin@test.com", Password = "Password1!"
# Click "Finish Setup"
```

See `references/setup-wizard.md` for detailed field mapping.

## Step 4: Test Features

**Important**: Replace `5000` with the actual port from `.orchardcore-port` file.

### Login to Admin

```bash
playwright-cli open http://localhost:$port/Login
playwright-cli snapshot
# Fill username: admin, password: Password1!
# Click login button
playwright-cli open http://localhost:$port/Admin
playwright-cli snapshot
```

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

**Enable a Feature**:
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

Console output is not visible when running in background. Use log files instead:

```powershell
# View last 50 lines of today's log
Get-Content "src/OrchardCore.Cms.Web/App_Data/logs/orchard-log-$(Get-Date -Format 'yyyy-MM-dd').log" -Tail 50

# Search for errors
Select-String -Path "src/OrchardCore.Cms.Web/App_Data/logs/orchard-log-$(Get-Date -Format 'yyyy-MM-dd').log" -Pattern "ERROR|Exception" -Context 2,5
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

## Default Credentials

- **Username**: admin
- **Email**: admin@test.com
- **Password**: Password1!

## References

- `references/admin-navigation.md` - Admin URLs and UI patterns
- `references/setup-wizard.md` - Site setup details
- `references/common-features.md` - Feature-specific testing guides
- `references/debugging.md` - Log files and troubleshooting
- `AGENTS.md` (repo root) - Build and development instructions

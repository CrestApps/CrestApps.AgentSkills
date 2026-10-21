# Setup Wizard Reference

## Overview

When the site runs without an initialized tenant (no `App_Data`), it presents a setup wizard at the root URL. Prefer Auto Setup (`autosetup.md`) for unattended provisioning; drive the wizard only when you need to exercise it.

## Prerequisites

1. The web project is built: `dotnet build $webProject`
2. No existing data: delete `$webProject/App_Data` if present
3. The site is running without Auto Setup settings (see SKILL.md Step 2)
4. A browser engine is installed for `playwright-cli` (see `playwright-cli.md`)

## Setup Wizard Form Fields

| Field | Description | Recommended Value |
|-------|-------------|-------------------|
| Site Name | Display name for the site | "Test Site" |
| Recipe | Dropdown button backed by a hidden `RecipeName` input | **"Blog"**, or your own setup recipe |
| Default Time Zone | Pre-filled `<select>` | Leave the default |
| Database | Database provider | "Sqlite" (default, easiest) |
| Table Prefix | Database table prefix | Leave empty |
| Connection String | DB connection (if not Sqlite) | N/A for Sqlite |
| User Name | Admin username | "admin" |
| Email | Admin email | "admin@test.com" |
| Password | Admin password | "Password1!" |
| Password Confirmation | Confirm password | "Password1!" |

When `OrchardCore:DatabaseProvider` (and optionally `ConnectionString`) is configured, those fields are fixed and not shown.

## Recipe Options

| Recipe | Description | Use Case |
|--------|-------------|----------|
| **Blog** | Blog with common features enabled | **Recommended for testing** |
| Headless | API-only, no frontend | API testing |
| Agency | Agency website template | Theme testing |
| Coming Soon | Simple landing page | Minimal testing |
| SaaS | Multi-tenant SaaS setup | Tenant testing |
| Blank | No features enabled | Custom setup |

**Why "Blog"?** It pre-enables Media, content management, the admin UI, Markdown, Autoroute and other commonly tested features. Recipes from your own modules and themes with `"issetuprecipe": true` are listed too.

## Setup Workflow with playwright-cli

```bash
PORT=$(cat .orchardcore-port)

# 1. Open the wizard and capture refs.
playwright-cli --browser webkit open "http://localhost:$PORT/"
playwright-cli snapshot

# 2. Plain inputs fill normally.
playwright-cli fill <sitename-ref> "Test Site"
playwright-cli fill <username-ref> "admin"
playwright-cli fill <email-ref> "admin@test.com"

# 3. Recipe: open the dropdown, then click the item.
playwright-cli click <recipe-button-ref>
playwright-cli snapshot
playwright-cli click <blog-item-ref>
playwright-cli eval 'document.getElementById("RecipeName").value'   # "Blog"

# 4. Database (Sqlite) and time zone defaults are valid; leave them.

# 5. Passwords: fill does not work here; use the native setter for both.
playwright-cli eval '((p)=>{var s=Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype,"value").set; s.call(p,"Password1!"); p.dispatchEvent(new Event("input",{bubbles:true})); p.dispatchEvent(new Event("change",{bubbles:true})); return p.value.length})(document.getElementById("Password"))'
playwright-cli eval '((p)=>{var s=Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype,"value").set; s.call(p,"Password1!"); p.dispatchEvent(new Event("input",{bubbles:true})); p.dispatchEvent(new Event("change",{bubbles:true})); return p.value.length})(document.getElementById("PasswordConfirmation"))'
playwright-cli eval 'document.getElementById("Password").value+"|"+document.getElementById("PasswordConfirmation").value'

# 6. Submit and verify.
playwright-cli click <finish-setup-ref>
playwright-cli eval 'window.location.pathname+" | "+document.title'
```

The password `eval` calls may print a benign `result is not a function` message; the follow-up read confirms the values.

## Post-Setup Verification

1. You are redirected to the home page; the title reflects the recipe and site name.
2. `$webProject/App_Data/Sites/Default/` exists.
3. Log in at `/Login` (same password workaround) and confirm `/Admin` loads without redirecting to `/Login`.
4. Check `/Admin/Features` to see enabled features.

## Resetting for Fresh Setup

```powershell
# Stop the running application
if (Test-Path ".orchardcore-pid") {
    Stop-Process -Id (Get-Content ".orchardcore-pid") -Force -ErrorAction SilentlyContinue
    Remove-Item ".orchardcore-pid" -Force
}

# Delete App_Data
Remove-Item -Recurse -Force "$webProject/App_Data"

# Restart application (see SKILL.md Step 2)
```

## Common Setup Issues

| Issue | Solution |
|-------|----------|
| Setup page doesn't appear | `App_Data` already holds an initialized tenant; delete it |
| Wizard appears but you wanted unattended | Auto Setup is not registered or not configured; see `autosetup.md` |
| Password field stays empty | Use the native-setter `eval`, not `fill`/`type` |
| Recipe dropdown does nothing | Click the dropdown button first, then the item; check the hidden `RecipeName` |
| Submit appears to do nothing | Both password fields must be set and meet the policy; check `playwright-cli console error` |
| "Port already in use" | Stop other instances or use a different port |

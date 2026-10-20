# Admin Navigation Reference

## Common Admin URLs

| Path | Description |
|------|-------------|
| `/Admin` | Admin dashboard |
| `/Admin/Features` | Enable/disable features (bulk **Enable**/**Disable**; **Toggle** was removed in 4.0) |
| `/Admin/Contents/ContentItems` | List all content items |
| `/Admin/Contents/ContentItems?contentTypeId=Article,BlogPost` | List several content types at once (4.0) |
| `/Admin/ContentTypes/List` | Manage content types |
| `/Admin/ContentTypes/ListParts` | Manage content parts |
| `/Admin/Media` | Media Library (Vue 3 Media Gallery) |
| `/Admin/Settings/general` | General site settings (any group: `/Admin/Settings/{groupId}`) |
| `/Admin/Settings/admin` | Admin settings (menu behavior, breadcrumbs, quick navigation, list layout) |
| `/Admin/Users/Index` | User management |
| `/Admin/Roles/Index` | Role management |
| `/Admin/Tenants` | Multi-tenant management |
| `/Admin/Recipes` | Run recipes |
| `/Admin/DeploymentPlan/Index` | Deployment plans |
| `/Admin/Themes` | Themes |
| `/Admin/BackgroundTasks` | Background tasks (bulk Enable/Disable in 4.0) |

Prefer navigating by URL; menu positions and grouping change between versions.

## Admin Menu Structure (Orchard Core 4.0)

First-level items that hold children render as **groups** (a small caption above their items):

```
Dashboard
Content
├── Content Items
Media
├── Library
Design
├── Themes
├── Content Definition (Content Types, Content Parts)
├── Templates / Workflows (when enabled)
Search
Access Control
├── Users
├── Roles
Multi-Tenancy
├── Tenants
Tools
├── Features
├── Recipes
├── Deployments
Settings
├── General
├── Admin
├── Security, User Login, Media, ...
```

### Menu Markup and Behavior

- The menu is `nav#left-nav > ul#adminMenu`. The selected item `li` has the `current` class; `active` marks it and all ancestors.
- Group `ul`s carry `nav-group-items` and an `id` of `m{hash}`; first-level `li`s carry `nav-group`. Do not rely on the `show` class: the menu renders collapsed and the script expands it, so toggles start with `aria-expanded="false"`.
- An item that both links somewhere and has children renders a label link plus a separate `button.nav-toggle`; click the button to expand, the label to navigate.
- **Navigation menu behavior** setting: `Focused` (default; opening a section closes its siblings) or `Persistent` (expanded sections are remembered per user).
- Below the `md` breakpoint the menu is an off-canvas drawer: open it with `button.ta-sidebar-toggler`, close it with `div.ta-sidebar-backdrop`, Esc, or by opening an item. Resize the browser to desktop width when a test expects the sidebar to be visible.
- The menu filter input is inside `div.nav-filter` (`input#filter`, shortcut Ctrl+Shift+F) when enabled.

## Page Title and Breadcrumbs

Admin screens render a breadcrumb trail and the page heading from the `<breadcrumb>` tag helper:

- Trail: `nav[aria-label="Breadcrumb"] ol.breadcrumb` (above the title).
- Heading: `h1.oc-breadcrumb-title` holds the page title (e.g. "Edit Blog Post"). Assert on this `h1`, not on a hard-coded `zone` markup.
- The title is also a segment of `document.title`.
- **Show breadcrumb** can be disabled in Admin settings; the `h1` title is still rendered.
- The Dashboard ancestor is added automatically when the Admin Dashboard feature is enabled.

```bash
playwright-cli eval "document.querySelector('h1.oc-breadcrumb-title')?.textContent.trim()"
playwright-cli eval "[...document.querySelectorAll('ol.breadcrumb li')].map(li => li.textContent.trim())"
```

## Quick Navigation Palette (Ctrl+K)

- Open with Ctrl+K (Cmd+K on macOS) or the magnifier button `#adminQuickNavigationToggle` in the navbar.
- Modal `#adminQuickNavigationModal`; filter input `#adminQuickNavigationInput`; results `#adminQuickNavigationResults` (`role="listbox"`); status `#adminQuickNavigationStatus`.
- Type to filter (case- and accent-insensitive, matches parent names), Arrow keys + Enter to navigate, Esc to close.
- Only admin menu items you are allowed to see are listed, plus recent content items when **Content Quick Navigation** (`OrchardCore.Contents.QuickNavigation`) is enabled.
- Can be disabled in Admin settings (**Enable quick navigation**); then the button is not rendered.

```bash
playwright-cli press "Control+k"
playwright-cli fill [quick-nav-input-ref] "Features"
playwright-cli press "Enter"
playwright-cli snapshot
```

## Admin Lists

Lists (content items, features, users, tenants, queries, workflows, ...) render through the `AdminList` shape:

- Layouts: `List` (rows) or `Grid` (CSS grid with one column per field; stacks on small screens). Force a layout with `?layout=Grid` or `?layout=List` when **Let users choose the layout of a list** is enabled; the choice is kept in a cookie per list.
- Row actions are `Buttons` or a `Menu` (an ellipsis dropdown) depending on **List actions layout**; open the dropdown before clicking Edit/Delete in `Menu` mode.
- Selecting rows shows a toolbar with item count, select-all and an **Actions** bulk menu.
- With **Page size selection** enabled (General settings), an **Items per page** selector appears next to the pager and adds `?pageSize=` to the URL.

## Common UI Element Patterns

### Login Form
- Username field: `input[name="UserName"]` or `#UserName`
- Password field: `input[name="Password"]` or `#Password`
- Login button: `button[type="submit"]`

### Feature Toggle (Features Page)
- Enable/Disable buttons next to each feature, or row checkboxes plus the **Actions** bulk menu
- Search box: `input[placeholder*="Search"]`

### Content Items List
- New button: Button containing "New"
- Edit links: Links within the content item rows (or inside the row actions menu)
- Pagination: Pager at the bottom of the list

### Modal Dialogs
- Confirm buttons typically have class `btn-primary` or text "OK"/"Yes"
- Cancel buttons typically have class `btn-secondary` or text "Cancel"/"No"

### Scripts in the Admin
- The admin no longer loads jQuery or jQuery UI. In `playwright-cli eval`, use DOM APIs (`document.querySelector`), not `$()`/`jQuery()`.
- Vue apps (Media Gallery, pickers) use Vue 3; wait for them to render before taking a snapshot.

## Admin Authentication

Default test credentials (from setup):
- **Username**: `admin`
- **Email**: `admin@test.com`
- **Password**: `Password1!`

Login URL: `/Login` or click "Log In" from any page

## Workflow: Login to Admin

First, get the port:
```powershell
$port = Get-Content ".orchardcore-port"
```

Then login:
```bash
# Navigate to login
playwright-cli open http://localhost:$port/Login
playwright-cli snapshot

# Fill credentials
playwright-cli fill [username-ref] "admin"
playwright-cli fill [password-ref] "Password1!"
playwright-cli click [submit-ref]
playwright-cli snapshot

# Navigate to admin
playwright-cli open http://localhost:$port/Admin
playwright-cli snapshot
```

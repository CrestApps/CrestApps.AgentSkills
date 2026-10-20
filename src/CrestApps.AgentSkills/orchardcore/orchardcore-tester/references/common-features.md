# Common Features Testing Reference

## Media Library

**URL**: `/Admin/Media`

### Testing Media Upload

```bash
# Navigate to Media
playwright-cli open http://localhost:5000/Admin/Media
playwright-cli snapshot

# Upload a file (find upload button/area)
playwright-cli upload ./test-image.jpg

# Verify upload appears in library
playwright-cli snapshot
```

### Media Library UI Elements (Vue 3 Media Gallery)
- Upload button or drag-and-drop onto the file list
- Folder tree: folders can be created, renamed, deleted, and files dragged onto them
- File list with list/thumbnail display modes, sorting, pagination, and a storage-information panel
- Search: input field for filtering files in the current folder
- Per-file context menu: Rename, Move, Copy, Delete (with confirmation), Download
- Uploads call the `api/media` endpoints (cookie + antiforgery by default); check `playwright-cli console error` and the application log when an upload fails. File size and extension limits are validated before upload.

---

## Content Items

**URL**: `/Admin/Contents/ContentItems`

### Creating Content

```bash
# Navigate to content items
playwright-cli open http://localhost:5000/Admin/Contents/ContentItems
playwright-cli snapshot

# Click "New" and select content type
playwright-cli click [new-button-ref]
playwright-cli snapshot

# Select content type (e.g., "Blog Post")
playwright-cli click [blogpost-ref]
playwright-cli snapshot

# Fill title and content
playwright-cli fill [title-ref] "Test Blog Post"
playwright-cli fill [markdown-ref] "This is test content."

# Publish
playwright-cli click [publish-ref]
playwright-cli snapshot
```

### Content Item UI Elements
- List layout: List or Grid (`?layout=Grid` when users may choose); in Grid each field is a column
- Row actions: buttons, or an ellipsis dropdown when **List actions layout** is `Menu`
- New button: Creates new content
- Content type selector: Modal/dropdown after clicking New
- Title field: Usually first prominent text input
- Editor: Markdown editor, HTML editor, or WYSIWYG
- Save Draft / Publish buttons: Action buttons at bottom or top

---

## Features Management

**URL**: `/Admin/Features`

### Enabling a Feature

```bash
# Navigate to Features
playwright-cli open http://localhost:5000/Admin/Features
playwright-cli snapshot

# Search for feature
playwright-cli fill [search-ref] "Media"
playwright-cli snapshot

# Click Enable button next to feature
playwright-cli click [enable-button-ref]
playwright-cli snapshot

# Verify feature is now enabled (button changes to Disable)
```

### Features UI Elements
- Search box: Filter features by name
- Features are grouped by category, one admin list per category
- Enable/Disable buttons per feature
- Row checkboxes plus the **Actions** bulk menu (**Enable**, **Disable**; there is no **Toggle** action in 4.0)

---

## User Management

**URL**: `/Admin/Users/Index`

### Creating a User

```bash
# Navigate to Users
playwright-cli open http://localhost:5000/Admin/Users/Index
playwright-cli snapshot

# Click Add User
playwright-cli click [add-user-ref]
playwright-cli snapshot

# Fill user details
playwright-cli fill [username-ref] "testuser"
playwright-cli fill [email-ref] "test@test.com"
playwright-cli fill [password-ref] "Password1!"
playwright-cli fill [confirm-password-ref] "Password1!"

# Save
playwright-cli click [save-ref]
playwright-cli snapshot
```

---

## Content Types

**URL**: `/Admin/ContentTypes/List`

### Creating a Content Type

```bash
# Navigate to Content Types
playwright-cli open http://localhost:5000/Admin/ContentTypes/List
playwright-cli snapshot

# Click Create new type
playwright-cli click [create-type-ref]
playwright-cli snapshot

# Fill type details
playwright-cli fill [displayname-ref] "Product"
playwright-cli fill [technicalname-ref] "Product"

# Add parts (e.g., TitlePart)
playwright-cli click [add-part-ref]
playwright-cli snapshot
```

---

## Workflows (if enabled)

**URL**: `/Admin/Workflows`

### Workflow UI Elements
- Workflow list: Shows available workflows
- Create button: New workflow
- Workflow designer: Visual editor for workflow steps
- Activity palette: Drag-and-drop activities

---

## Themes

**URL**: `/Admin/Themes`

### Switching Themes

```bash
# Navigate to Themes
playwright-cli open http://localhost:5000/Admin/Themes
playwright-cli snapshot

# Find theme and click "Make Current"
playwright-cli click [make-current-ref]
playwright-cli snapshot
```

---

## Verification Patterns

### Check for Success Messages
After actions, look for (the page heading is `h1.oc-breadcrumb-title`):
- Toast notifications (usually top-right)
- Alert boxes with success styling (green)
- Page redirect to expected location

### Check for Errors
Look for:
- Red/danger styled alerts
- Validation messages near form fields
- Console errors: `playwright-cli console error`

### Verify Page State
```bash
# Take snapshot to see current state
playwright-cli snapshot

# Check console for JavaScript errors
playwright-cli console error

# Evaluate page title
playwright-cli eval "document.title"
```

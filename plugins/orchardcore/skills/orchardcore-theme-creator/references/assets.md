# Theme Assets Reference

## Asset Pipeline

Orchard Core's Asset Manager builds the entries of each project's `Assets.json` with `yarn build`. A typical theme compiles SCSS with `sass` and bundles scripts with `parcel` (or `min` for plain files); `vite` is used for Vue and TypeScript apps.

## Assets.json Configuration

```json
[
  {
    "action": "sass",
    "name": "theme-yourtheme",
    "source": "Assets/scss/site.scss",
    "dest": "wwwroot/css/",
    "tags": ["theme", "css"]
  },
  {
    "action": "min",
    "name": "theme-yourtheme",
    "source": "Assets/js/site.js",
    "dest": "wwwroot/js/",
    "tags": ["theme", "js"]
  }
]
```

### Configuration Options

| Property | Description |
|----------|-------------|
| `action` | Build action (`sass`, `min`, `copy`, `concat`, `parcel`, `vite`, `webpack`, `run`) |
| `name` | Name used to build or watch selectively (`-n`) |
| `source` | Source file, glob, or folder |
| `dest` | Output folder (produces `.css`/`.min.css` or `.js`/`.min.js`) |
| `tags` | Tags used to filter builds (`-t`) |

See `orchardcore-asset-manager` for every action.

## Package.json

```json
{
  "name": "@orchardcore/your-theme",
  "version": "1.0.0",
  "private": true,
  "dependencies": {
    "bootstrap": "5.3.8"
  },
  "devDependencies": {}
}
```

## SCSS Structure

### Assets/scss/site.scss

```scss
// Import Bootstrap (optional)
@import "bootstrap/scss/bootstrap";

// Custom variables
$primary-color: #007bff;
$font-family-base: 'Segoe UI', sans-serif;

// Base styles
body {
    font-family: $font-family-base;
    line-height: 1.6;
}

// Layout
.container {
    max-width: 1200px;
    margin: 0 auto;
    padding: 0 1rem;
}

// Header
.site-header {
    background: $primary-color;
    color: white;
    padding: 1rem 0;
}

// Navigation
.main-nav {
    ul {
        list-style: none;
        padding: 0;
        margin: 0;
        display: flex;
        gap: 1rem;
    }
    
    a {
        color: inherit;
        text-decoration: none;
        
        &:hover {
            text-decoration: underline;
        }
    }
}

// Content
.content-area {
    padding: 2rem 0;
}

// Footer
.site-footer {
    background: #333;
    color: #fff;
    padding: 2rem 0;
    margin-top: 2rem;
}
```

## JavaScript

### Assets/js/site.js

```javascript
// Wait for DOM ready
document.addEventListener('DOMContentLoaded', function() {
    initializeTheme();
});

function initializeTheme() {
    // Mobile menu toggle
    const menuToggle = document.querySelector('.menu-toggle');
    const mainNav = document.querySelector('.main-nav');
    
    if (menuToggle && mainNav) {
        menuToggle.addEventListener('click', function() {
            mainNav.classList.toggle('is-open');
        });
    }
    
    // Smooth scroll for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                target.scrollIntoView({ behavior: 'smooth' });
            }
        });
    });
}
```

## Building Assets

```bash
# From repository root
cd /path/to/orchardcore

# Install dependencies (first time)
corepack enable
yarn

# Build all assets
yarn build

# Build only this theme's assets
yarn build -n theme-yourtheme

# Rebuild on save while developing
yarn watch -n theme-yourtheme
```

## Output Structure

After building, assets are output to `wwwroot/`:

```
wwwroot/
├── css/
│   └── site.css          # Compiled CSS
├── js/
│   └── site.js           # Compiled JavaScript
└── images/               # Copied images
```

## Using Resources in Views

### Link CSS

```html
<style asp-name="YourTheme" asp-src="~/YourTheme/css/site.min.css" debug-src="~/YourTheme/css/site.css" at="Head"></style>
```

### Include JavaScript

```html
<script asp-name="YourTheme" asp-src="~/YourTheme/js/site.min.js" debug-src="~/YourTheme/js/site.js" at="Foot"></script>
```

### Using a Resource Manifest

Define named resources once in an `IConfigureOptions<ResourceManagementOptions>` class registered with `services.AddResourceConfiguration<T>()`, then require them by name (`<style asp-name="YourTheme">`). From C# code, inject `IResourceManager` and call `RegisterResource("stylesheet", "YourTheme")` or `RegisterResource("script", "YourTheme").AtFoot()`. See `orchardcore-resources`.

## Third-Party Libraries

### Bootstrap Integration

```scss
// In site.scss
// Customize Bootstrap variables before import
$primary: #0d6efd;
$secondary: #6c757d;
$body-bg: #fff;
$body-color: #212529;

// Import all of Bootstrap
@import "bootstrap/scss/bootstrap";

// Or import specific modules
@import "bootstrap/scss/functions";
@import "bootstrap/scss/variables";
@import "bootstrap/scss/mixins";
@import "bootstrap/scss/grid";
@import "bootstrap/scss/utilities";
```

### Font Awesome

Orchard Core already registers `font-awesome` (7.3.1 by default), so `<style asp-name="font-awesome" version="7" at="Head"></style>` is usually enough. Bundle it only when you need a custom subset:

```json
// In package.json
{
  "dependencies": {
    "@fortawesome/fontawesome-free": "7.3.1"
  }
}
```

```scss
// In site.scss
@import "@fortawesome/fontawesome-free/scss/fontawesome";
@import "@fortawesome/fontawesome-free/scss/solid";
@import "@fortawesome/fontawesome-free/scss/regular";
```

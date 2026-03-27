---
name: orchardcore-blazor
description: Skill for building Blazor CMS applications with Orchard Core. Covers Blazor SSR and Server Interactive rendering, Razor class library structure, Blazor component architecture (App, Layout, NavMenu, Router), content integration with IContentHandleManager, dependency injection in Blazor pages, loading CMS content in Blazor components, SEO and alias handling, and multi-tenant support. Use this skill when requests mention Orchard Core Blazor CMS, Building Blazor Applications with Orchard Core, Solution Structure, Create the Solution from CLI, Razor Class Library Project File, Program.cs — Orchard Core with Blazor Pipeline, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. It also helps with Razor Class Library Project File, Program.cs — Orchard Core with Blazor Pipeline, App.razor — Root Component, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Blazor CMS - Prompt Templates

## Building Blazor Applications with Orchard Core

You are an Orchard Core expert. Generate code and configuration for building Blazor CMS applications powered by Orchard Core, including SSR, Server Interactive rendering, content integration, and multi-tenant support.

### Guidelines

- Orchard Core supports Blazor SSR (Static Server Rendering) and Server Interactive rendering modes.
- Blazor `.razor` files **must** live in a separate Razor class library project, not directly in the Orchard Core web project. Adding `.razor` files to Orchard Core modules is not supported.
- The Razor class library must reference `Microsoft.AspNetCore.App` as a `FrameworkReference` to access ASP.NET Core–specific APIs.
- Use the **Headless site** recipe when setting up an Orchard Core instance for Blazor front-end consumption.
- Register Blazor services and map Razor components inside `AddOrchardCms()` using `.ConfigureServices()` and `.Configure()` — do not register them outside of the Orchard Core pipeline.
- Use `IContentHandleManager` to resolve a content item ID from an alias handle, then use `IContentManager` to load the content item.
- Use `IContentManager.PopulateAspectAsync<BodyAspect>(contentItem)` to extract rendered body HTML from content items.
- Use `ISiteService.GetSiteSettingsAsync()` to access site-level settings like `SiteName`.
- Add `@attribute [StreamRendering]` to Blazor pages to enable streaming rendering for async content loading.
- For multi-tenant support, dynamically set the `<base href>` tag using `NavigationManager.BaseUri` to reflect tenant-specific URL prefixes.
- Each tenant has its own URL prefix (e.g., `/tenant01/`) and its own content, content types, and settings.
- Use `@rendermode="RenderMode.InteractiveServer"` on individual components to enable interactivity via SignalR within an otherwise SSR page.
- Do **not** call `builder.Services.AddRazorPages()` separately — `AddOrchardCms()` handles this internally.

### Solution Structure

A typical Blazor + Orchard Core solution consists of two projects:

```
MySolution/
├── MySolution.sln
├── BlazorCms/                    # Orchard Core web project
│   ├── BlazorCms.csproj
│   └── Program.cs
└── BlazorLib/                    # Razor class library (Blazor components)
    ├── BlazorLib.csproj
    ├── App.razor
    ├── App.razor.css
    ├── Routes.razor
    ├── _Imports.razor
    ├── Layout/
    │   ├── MainLayout.razor
    │   ├── MainLayout.razor.css
    │   ├── NavMenu.razor
    │   └── NavMenu.razor.css
    ├── Pages/
    │   ├── Home.razor
    │   └── Content.razor
    └── Components/
        └── InteractiveButton.razor
```

### Create the Solution from CLI

```shell
mkdir BlazorSolution
cd BlazorSolution
dotnet new sln
dotnet new occms -o BlazorCms
dotnet sln add ./BlazorCms
dotnet new razorclasslib -f net10.0 -o BlazorLib
dotnet sln add ./BlazorLib
dotnet add ./BlazorCms/BlazorCms.csproj reference ./BlazorLib/BlazorLib.csproj
```

### Razor Class Library Project File

The Razor class library must include a `FrameworkReference` to `Microsoft.AspNetCore.App` and reference the `OrchardCore.ContentManagement` package for content APIs:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="OrchardCore.ContentManagement" Version="2.2.1" />
  </ItemGroup>

</Project>
```

### Program.cs — Orchard Core with Blazor Pipeline

Register Blazor services and map components within the Orchard Core pipeline using `ConfigureServices` and `Configure`:

```csharp
using BlazorLib;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services
            .AddOrchardCms()
            .ConfigureServices(services =>
            {
                services.AddRazorComponents()
                    .AddInteractiveServerComponents();
            })
            .Configure((app, routes, services) =>
            {
                app.UseStaticFiles();
                app.UseAntiforgery();
                routes.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode();
            });

        var app = builder.Build();

        app.UseOrchardCore();

        app.Run();
    }
}
```

### App.razor — Root Component

The root Blazor component that renders the HTML document. For multi-tenant support, dynamically set `<base href>` using `NavigationManager`:

```razor
@inject NavigationManager NavManager

<!DOCTYPE html>
<html lang="en">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href=@_baseUrl />
    <link rel="stylesheet" href="OrchardCore.Resources/Styles/bootstrap.min.css" />
    <link rel="stylesheet" href="_content/BlazorLib/BlazorLib.bundle.scp.css" />
    <HeadOutlet />
</head>

<body>
    <Routes />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>

@code
{
    protected string _baseUrl = "/";

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        _baseUrl = NavManager.BaseUri;
    }
}
```

### Routes.razor — Router Component

```razor
<Router AppAssembly="typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

### _Imports.razor

```csharp
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.JSInterop
@using BlazorLib
@using OrchardCore
@using OrchardCore.ContentManagement
@using OrchardCore.Settings
```

### MainLayout.razor

```razor
@inherits LayoutComponentBase

<div class="page">
    <div class="sidebar">
        <NavMenu />
    </div>

    <main>
        <div class="top-row px-4">
            <a href="https://docs.orchardcore.net/" target="_blank">Learn Orchard</a>
        </div>

        <article class="content px-4">
            @Body
        </article>
    </main>
</div>

<div id="blazor-error-ui">
    An unhandled error has occurred.
    <a href="" class="reload">Reload</a>
    <a class="dismiss">🗙</a>
</div>
```

### NavMenu.razor

```razor
<div class="top-row ps-3 navbar navbar-dark">
    <div class="container-fluid">
        <a class="navbar-brand" href="">BlazorCMS</a>
    </div>
</div>

<input type="checkbox" title="Navigation menu" class="navbar-toggler" />

<div class="nav-scrollable" onclick="document.querySelector('.navbar-toggler').click()">
    <nav class="flex-column">
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="home" Match="NavLinkMatch.All">
                <span class="bi bi-house-door-fill-nav-menu" aria-hidden="true"></span> Home
            </NavLink>
        </div>
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="content/my-alias">
                <span class="bi bi-list-nested-nav-menu" aria-hidden="true"></span> My Content
            </NavLink>
        </div>
    </nav>
</div>
```

### Home.razor — Home Page

For multi-tenant compatibility, use `/home` instead of `/` to avoid conflicting with Orchard Core's root route (used for setup and tenant routing):

```razor
@page "/home"

<PageTitle>Home</PageTitle>

<h1>Hello, Orchard!</h1>

Welcome to your new Blazor CMS app.
```

### Content.razor — Loading CMS Content

Use `IContentHandleManager` to resolve the content item ID from an alias, then `IContentManager` to load the content item:

```razor
@page "/content/{alias}"
@attribute [StreamRendering]
@using OrchardCore.ContentManagement.Models
@inject IContentHandleManager HandleManager
@inject IContentManager ContentManager
@inject ISiteService SiteService

@if (ContentItem == null)
{
    <p><em>Loading...</em></p>
}
else
{
    <PageTitle>@ContentItem.DisplayText - @Site?.SiteName</PageTitle>

    <h1>@ContentItem.DisplayText</h1>
    @((MarkupString)Markup)
}

@code
{
    [Parameter]
    public string Alias { get; set; }

    protected ContentItem? ContentItem { get; set; }
    protected string Markup { get; set; } = string.Empty;
    protected ISite? Site { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Site = await SiteService.GetSiteSettingsAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        var id = await HandleManager.GetContentItemIdAsync($"alias:{Alias}");

        if (id != null)
        {
            ContentItem = await ContentManager.GetAsync(id, VersionOptions.Published);

            if (ContentItem != null)
            {
                var bodyAspect = await ContentManager.PopulateAspectAsync<BodyAspect>(ContentItem);
                Markup = bodyAspect.Body?.ToString() ?? string.Empty;
            }
        }
    }
}
```

### Key Blazor + Orchard Core Services

| Service | Description |
|---|---|
| `IContentHandleManager` | Resolves a content item ID from a handle string (e.g., `alias:my-slug`). |
| `IContentManager` | Full content item CRUD API. Use `GetAsync(id, VersionOptions)` to load content. |
| `ISiteService` | Access site-level settings such as `SiteName`, `BaseUrl`, and `TimeZoneId`. |
| `BodyAspect` | Aspect that provides the rendered body HTML of a content item. Populated via `PopulateAspectAsync`. |
| `NavigationManager` | Blazor's built-in service for navigation. Use `BaseUri` to get the tenant-specific base URL. |

### Content Loading Patterns

| Pattern | Code |
|---|---|
| Load by alias | `var id = await HandleManager.GetContentItemIdAsync($"alias:{alias}");` |
| Load by content item ID | `var item = await ContentManager.GetAsync(contentItemId, VersionOptions.Published);` |
| Load latest draft | `var item = await ContentManager.GetAsync(contentItemId, VersionOptions.Latest);` |
| Get rendered body HTML | `var body = await ContentManager.PopulateAspectAsync<BodyAspect>(item);` |
| Get site settings | `var site = await SiteService.GetSiteSettingsAsync();` |

### Interactive Server Components

To add interactivity to specific components within an SSR page, use `@rendermode="RenderMode.InteractiveServer"`. This establishes a SignalR connection for that component.

```razor
<!-- InteractiveButton.razor in Components/ folder -->
<h3>Interactivity Test</h3>

<button class="btn-primary" @onclick="Clicked">Click Me</button>
<hr />
@_feedback

@code {
    protected string? _feedback;

    protected void Clicked()
    {
        _feedback = $"Clicked at {DateTime.Now.ToLocalTime()}";
    }
}
```

Use the component in a page with the interactive render mode:

```razor
@page "/demo"

<h1>Interactive Demo</h1>

<BlazorLib.Components.InteractiveButton @rendermode="RenderMode.InteractiveServer" />
```

### Multi-Tenant Configuration

Orchard Core multi-tenancy assigns each tenant a unique URL prefix. The Blazor app must adapt its `<base href>` accordingly.

#### Enable Multi-Tenancy

1. In the admin UI, go to `Tools > Features` and enable the **Tenants** feature.
2. Navigate to `Multi-Tenancy > Tenants` and add a new tenant:
   - **Tenant name**: `FirstTenant`
   - **URL Prefix**: `tenant01`
   - **Recipe**: Headless site
3. Click **Setup** and complete the tenant setup.

#### Base URL Strategies for Tenants

| Method | Code |
|---|---|
| `NavigationManager` | `NavManager.BaseUri` — simplest approach, returns the full base URI including tenant prefix. |
| `HttpContext` | `HttpContext.Request.PathBase` — available from server-side code. |
| `ShellScope` | `ShellScope.Context.Settings.RequestUrlPrefix` — Orchard Core shell-level setting. |
| `ISiteService` | `(await SiteService.GetSiteSettingsAsync()).BaseUrl` — configurable in the admin UI. |

#### App.razor with Dynamic Base URL

```razor
@inject NavigationManager NavManager

<!DOCTYPE html>
<html lang="en">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href=@_baseUrl />
    <link rel="stylesheet" href="OrchardCore.Resources/Styles/bootstrap.min.css" />
    <link rel="stylesheet" href="_content/BlazorLib/BlazorLib.bundle.scp.css" />
    <HeadOutlet />
</head>

<body>
    <Routes />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>

@code
{
    protected string _baseUrl = "/";

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        _baseUrl = NavManager.BaseUri;
    }
}
```

### Importing Content Type and Data via Recipe

Import content definitions and content items for your Blazor app through the admin UI at `Tools > Deployments > JSON Import`:

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "MarkdownPage",
          "DisplayName": "Markdown Page",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true,
              "Versionable": true,
              "Securable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            },
            {
              "PartName": "AliasPart",
              "Name": "AliasPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "1"
                }
              }
            },
            {
              "PartName": "MarkdownBodyPart",
              "Name": "MarkdownBodyPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "2"
                }
              }
            }
          ]
        }
      ],
      "ContentParts": []
    },
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "MarkdownPage",
          "DisplayText": "Welcome to Blazor CMS",
          "Latest": true,
          "Published": true,
          "AliasPart": {
            "Alias": "welcome"
          },
          "MarkdownBodyPart": {
            "Markdown": "## Welcome\nThis content is managed by Orchard Core and rendered by Blazor."
          },
          "TitlePart": {
            "Title": "Welcome to Blazor CMS"
          }
        }
      ]
    }
  ]
}
```

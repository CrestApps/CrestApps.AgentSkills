---
name: orchardcore-decoupled-cms
description: Skill for building decoupled CMS applications with Orchard Core. Covers headless CMS architecture, content API consumption, custom Razor Pages for content rendering, loading content by ID, alias, and handle, auto-generating aliases with Liquid patterns, preview feature integration, and content property access patterns. Use this skill when requests mention Orchard Core Decoupled CMS, Building a Decoupled CMS with Orchard Core, Project Setup, Content Item Properties, Loading Content by ID, Loading Content by Alias (SEO-Friendly Slug), or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Application.Cms.Core.Targets, OrchardCore.IOrchardHelper, IOrchardHelper, MarkdownBodyPart. It also helps with Loading Content by ID, Loading Content by Alias (SEO-Friendly Slug), Accessing Dynamic Content Properties, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Decoupled CMS - Prompt Templates

## Building a Decoupled CMS with Orchard Core

You are an Orchard Core expert. Generate code and configuration for building decoupled CMS applications where the front-end is driven by custom Razor Pages or Controllers while the back-end content management is handled by Orchard Core.

### Guidelines

- A **decoupled** CMS hosts the front-end and back-end in the same web application, but only the back-end is driven by the CMS. Developers write custom Razor Pages or Controllers for the front-end.
- Use the `OrchardCore.Application.Cms.Core.Targets` NuGet package to add Orchard Core CMS capabilities to any ASP.NET Core app.
- Call `builder.Services.AddOrchardCms()` in `Program.cs` to register all Orchard Core services. Do **not** call `builder.Services.AddRazorPages()` separately — `AddOrchardCms()` invokes it internally.
- Call `app.UseOrchardCore()` to add the Orchard Core middleware pipeline. Remove default ASP.NET middleware like `UseRouting()`, `UseAuthorization()`, and `MapRazorPages()` — Orchard Core handles these internally.
- Use the **Blank site** or **Headless site** recipe during setup for decoupled/headless scenarios.
- Inject `OrchardCore.IOrchardHelper` (aliased as `Orchard`) in Razor Pages to load and render content items.
- Load content items by their immutable `ContentItemId` using `Orchard.GetContentItemByIdAsync(id)`.
- Load content items by alias using `Orchard.GetContentItemByHandleAsync($"alias:{slug}")`.
- Access standard properties like `DisplayText`, `ContentItemId`, `Author`, and `ContentType` directly on the content item object.
- Access dynamic part data through the `Content` property (a JSON document), e.g., `blogPost.Content.MarkdownBodyPart.Markdown`.
- Use `Orchard.MarkdownToHtmlAsync((string) contentItem.Content.MarkdownBodyPart.Markdown)` to convert Markdown content to HTML.
- Use `Orchard.ConsoleLog(contentItem)` during development to inspect a content item's full JSON structure in the browser console.
- Attach **AliasPart** to content types that need SEO-friendly URL slugs.
- Configure AliasPart with a Liquid pattern like `{{ ContentItem | display_text | slugify }}` to auto-generate aliases from titles.
- Attach **PreviewPart** to content types to enable live preview during editing, using a pattern like `/blogpost/{{ ContentItem.Content.AliasPart.Alias }}` to route previews to custom Razor Pages.

### Project Setup

#### Minimum `Program.cs`

```csharp
public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOrchardCms();

        var app = builder.Build();

        app.UseStaticFiles();
        app.UseOrchardCore();

        app.Run();
    }
}
```

#### Project File Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="OrchardCore.Application.Cms.Core.Targets" Version="2.2.1" />
  </ItemGroup>

</Project>
```

### Content Item Properties

| Property | Type | Description |
|---|---|---|
| `ContentItemId` | `string` | Unique, immutable identifier for the content item. |
| `ContentItemVersionId` | `string` | Unique identifier for this specific version. |
| `ContentType` | `string` | The technical name of the content type (e.g., `BlogPost`). |
| `DisplayText` | `string` | The display title set by TitlePart. |
| `Author` | `string` | The username of the content item's author. |
| `Owner` | `string` | The user ID of the content item's owner. |
| `Published` | `bool` | Whether this version is the published version. |
| `Latest` | `bool` | Whether this version is the latest version. |
| `CreatedUtc` | `DateTime` | UTC timestamp when the content item was created. |
| `ModifiedUtc` | `DateTime` | UTC timestamp when the content item was last modified. |
| `PublishedUtc` | `DateTime` | UTC timestamp when the content item was published. |
| `Content` | `dynamic` | JSON document containing all dynamic part and field data. |

### Loading Content by ID

Use the immutable `ContentItemId` to load a specific content item. The `ContentItemId` is visible in the admin URL when editing a content item (the segment after `/ContentItems/`).

```html
@page "/blogpost/{id}"
@inject OrchardCore.IOrchardHelper Orchard

@{
    var blogPost = await Orchard.GetContentItemByIdAsync(Id);
}

<h1>@blogPost.DisplayText</h1>

<p>@await Orchard.MarkdownToHtmlAsync((string) blogPost.Content.MarkdownBodyPart.Markdown)</p>

@functions
{
    [FromRoute]
    public string Id { get; set; }
}
```

### Loading Content by Alias (SEO-Friendly Slug)

Attach **AliasPart** to the content type, then use `GetContentItemByHandleAsync` with the `alias:` prefix to load content by its slug.

```html
@page "/blogpost/{slug}"
@inject OrchardCore.IOrchardHelper Orchard

@{
    var blogPost = await Orchard.GetContentItemByHandleAsync($"alias:{Slug}");
}

<h1>@blogPost.DisplayText</h1>

<p>@await Orchard.MarkdownToHtmlAsync((string) blogPost.Content.MarkdownBodyPart.Markdown)</p>

@functions
{
    [FromRoute]
    public string Slug { get; set; }
}
```

### Accessing Dynamic Content Properties

All part and field data is stored in the `Content` property as a JSON document. Use `Orchard.ConsoleLog()` during development to inspect the structure.

```html
@page "/blogpost/{slug}"
@inject OrchardCore.IOrchardHelper Orchard

@{
    var blogPost = await Orchard.GetContentItemByHandleAsync($"alias:{Slug}");
}

<h1>@blogPost.DisplayText</h1>
<p>Author: @blogPost.Author</p>
<p>Published: @blogPost.PublishedUtc</p>

<!-- Access MarkdownBodyPart content -->
<div>@await Orchard.MarkdownToHtmlAsync((string) blogPost.Content.MarkdownBodyPart.Markdown)</div>

<!-- Access HtmlBodyPart content (if attached) -->
@if (blogPost.Content.HtmlBodyPart != null)
{
    <div>@Html.Raw((string) blogPost.Content.HtmlBodyPart.Html)</div>
}

<!-- Debug: inspect full content item JSON in browser console -->
@Orchard.ConsoleLog(blogPost)

@functions
{
    [FromRoute]
    public string Slug { get; set; }
}
```

### Configuring AliasPart for Auto-Generated Slugs

Add AliasPart to a content type via a data migration. The Liquid pattern `{{ ContentItem | display_text | slugify }}` generates slugs from the title automatically (e.g., "This is a New Day" becomes `this-is-a-new-day`).

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("BlogPost", type => type
            .DisplayedAs("Blog Post")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
            )
            .WithPart("AliasPart", part => part
                .WithPosition("1")
                .WithSettings(new AliasPartSettings
                {
                    Pattern = "{{ ContentItem | display_text | slugify }}",
                })
            )
            .WithPart("MarkdownBodyPart", part => part
                .WithPosition("2")
                .WithSettings(new MarkdownBodyPartSettings
                {
                    Editor = MarkdownBodyPartEditor.Wysiwyg,
                })
            )
        );

        return 1;
    }
}
```

### Configuring Content Type with AliasPart via Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "BlogPost",
          "DisplayName": "Blog Post",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true,
              "Versionable": true
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
                },
                "AliasPartSettings": {
                  "Pattern": "{{ ContentItem | display_text | slugify }}"
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
    }
  ]
}
```

### Configuring Preview for Decoupled Pages

Attach **PreviewPart** to the content type and configure its pattern to point to your custom Razor Page route. The pattern uses Liquid syntax to generate the preview URL.

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "BlogPost",
          "DisplayName": "Blog Post",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true,
              "Versionable": true
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
                },
                "AliasPartSettings": {
                  "Pattern": "{{ ContentItem | display_text | slugify }}"
                }
              }
            },
            {
              "PartName": "PreviewPart",
              "Name": "PreviewPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "2"
                },
                "PreviewPartSettings": {
                  "Pattern": "/blogpost/{{ ContentItem.Content.AliasPart.Alias }}"
                }
              }
            },
            {
              "PartName": "MarkdownBodyPart",
              "Name": "MarkdownBodyPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "3"
                }
              }
            }
          ]
        }
      ],
      "ContentParts": []
    }
  ]
}
```

### Complete Decoupled Page with Error Handling

A production-ready Razor Page that loads content by alias with proper null checking:

```html
@page "/article/{slug}"
@inject OrchardCore.IOrchardHelper Orchard

@{
    var article = await Orchard.GetContentItemByHandleAsync($"alias:{Slug}");

    if (article == null)
    {
        <h1>Article Not Found</h1>
        <p>The article you requested could not be found.</p>
        return;
    }
}

<article>
    <h1>@article.DisplayText</h1>
    <p><small>By @article.Author | Published @article.PublishedUtc?.ToString("MMMM dd, yyyy")</small></p>

    @if (article.Content.MarkdownBodyPart?.Markdown != null)
    {
        @await Orchard.MarkdownToHtmlAsync((string) article.Content.MarkdownBodyPart.Markdown)
    }
    else if (article.Content.HtmlBodyPart?.Html != null)
    {
        @Html.Raw((string) article.Content.HtmlBodyPart.Html)
    }
</article>

@functions
{
    [FromRoute]
    public string Slug { get; set; }
}
```

### Loading Content from a Controller

For MVC-style decoupled applications, inject `IOrchardHelper` into a controller:

```csharp
using Microsoft.AspNetCore.Mvc;
using OrchardCore;

public sealed class BlogController : Controller
{
    private readonly IOrchardHelper _orchardHelper;

    public BlogController(IOrchardHelper orchardHelper)
    {
        _orchardHelper = orchardHelper;
    }

    [Route("/blog/{slug}")]
    public async Task<IActionResult> Post(string slug)
    {
        var contentItem = await _orchardHelper.GetContentItemByHandleAsync($"alias:{slug}");

        if (contentItem == null)
        {
            return NotFound();
        }

        return View(contentItem);
    }
}
```

### IOrchardHelper API Reference

| Method | Description |
|---|---|
| `GetContentItemByIdAsync(string id)` | Loads a content item by its immutable `ContentItemId`. |
| `GetContentItemByHandleAsync(string handle)` | Loads a content item by handle. Use `alias:{slug}` for alias lookups or `slug:{path}` for autoroute lookups. |
| `GetContentItemByVersionIdAsync(string versionId)` | Loads a specific version of a content item. |
| `QueryContentItemsAsync(Func<IQuery, IQuery<ContentItem>> queryBuilder)` | Queries content items using YesSql. |
| `MarkdownToHtmlAsync(string markdown)` | Converts Markdown text to sanitized HTML. |
| `ConsoleLog(object value)` | Outputs a content item's JSON structure to the browser's developer console for debugging. |

### Importing Content via Recipe

Create content items by importing a recipe through the admin UI at `Tools > Deployments > JSON Import`:

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "BlogPost",
          "DisplayText": "Getting Started with Decoupled Orchard Core",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Getting Started with Decoupled Orchard Core"
          },
          "AliasPart": {
            "Alias": "getting-started-decoupled"
          },
          "MarkdownBodyPart": {
            "Markdown": "## Introduction\nOrchard Core can be used as a decoupled CMS..."
          }
        }
      ]
    }
  ]
}
```

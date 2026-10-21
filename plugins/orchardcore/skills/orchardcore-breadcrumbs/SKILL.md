---
name: orchardcore-breadcrumbs
description: Skill for Orchard Core 4.0 breadcrumbs on admin screens and front-end views. Covers the breadcrumb and breadcrumb-item tag helpers, the required IHtmlContent title and safe HtmlContentString wrapping, ancestor links with routes, URLs, permissions and link-enabled, IBreadcrumbProvider and BreadcrumbContext postprocessing registered with AddBreadcrumbProvider, the DashboardBreadcrumbProvider ancestor, AdminSettings.ShowBreadcrumb, Breadcrumb and BreadcrumbItem shape alternates, Breadcrumb.cshtml overrides, and IShapeFactory.BreadcrumbAsync. Use this skill when requests mention Orchard Core breadcrumbs, replacing an admin page title h1, breadcrumb trails, Show breadcrumb, BreadcrumbItem-Title alternates, or closely related implementation, theming, or troubleshooting work. Strong matches include OrchardCore.Navigation, OrchardCore.Navigation.Core, OrchardCore.AdminDashboard, BreadcrumbTagHelper, BreadcrumbItem, IBreadcrumbProvider, BreadcrumbContext, HtmlContentString.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Breadcrumbs

Orchard Core 4.0 renders admin page titles as a **named trail with an explicit current-page title**, e.g. _Dashboard › Manage Content › Edit Article_. The `Breadcrumb` and `BreadcrumbItem` shapes live in the always-enabled `OrchardCore.Navigation` module; the tag helpers live in `OrchardCore.Navigation.Core`. No feature needs to be enabled and inline authoring needs no provider registration.

## Mental Model

- The parent `<breadcrumb>` declares the trail `name` and the current page `title`. Both are required.
- Every `<breadcrumb-item>` child is an **ancestor**, never the current page.
- For a visible trail the helper collects children in declaration order, runs every `IBreadcrumbProvider` in DI registration order on the same ancestor list, keeps the resulting order (no sorting), then **always appends the title as the final, unlinked current node** with the fixed `Id = "Title"`.
- On admin requests the helper also renders the title as an `<h1>` below the trail and registers it as a `<title>` segment, so the view must not render its own heading.
- Visibility is decided first. A hidden admin trail skips all child and provider work and uses only the explicit title (see "Hiding Admin Breadcrumbs").
- Keep built-in-style views inline: the title and screen-specific ancestors in the owning view, a lightweight provider only for shared or cross-module ancestor changes, and shape alternates for presentation. No breadcrumb manager, builder, or named-provider base class exists or is needed.
- `INavigationProvider` is unrelated: it builds menus, not breadcrumb trails.

## Adding a Breadcrumb to a View

Import the tag helpers once in the module's `Views/_ViewImports.cshtml` (reference the `OrchardCore.Navigation.Core` package or project):

```razor
@addTagHelper *, OrchardCore.Navigation.Core
```

Put the helper in the `Title` zone, replacing the old `<h1>` and any `@RenderTitleSegments(...)` call for the same title:

```razor
<zone Name="Title">
    <breadcrumb name="RecordsEdit" title="@T["Edit {0}", Model.Record.Name]">
        <breadcrumb-item id="Records" action="Index" controller="Record" area="My.Module"
                         permission="@MyPermissions.ManageRecords.Name">@T["Records"]</breadcrumb-item>
    </breadcrumb>
</zone>
```

A page with no ancestors (or whose ancestors come only from providers) still declares both attributes:

```razor
<zone Name="Title">
    <breadcrumb name="Records" title="@T["Records"]" />
</zone>
```

Rendering it in the `Title` zone keeps the **Display titles in the top bar** admin setting working; `TheAdmin`'s own `Breadcrumb.cshtml` moves the trail into the `Breadcrumbs` zone above the title. A trail that should not carry the page title goes in the `Breadcrumbs` zone with `heading=""` (and `page-title="false"` when the page registers its browser title itself).

### Naming

- Use stable literal strings for `name` and ancestor `id`, in PascalCase without separators (`ContentsEdit`, `Records`), so alternate file names stay readable.
- Reuse the screen's admin list name when it has one; the content items list uses `Contents` for both. Content screens use `Contents`, `ContentsCreate`, and `ContentsEdit`.
- The name drives provider targeting, the `breadcrumb-{name}` CSS class, shape alternates, and Dashboard deduplication. No constants class, `data` attribute, or breadcrumb manager is needed.

## `<breadcrumb>` Attributes

| Attribute | Type | Behavior |
|---|---|---|
| `name` | `string` | Required stable trail name. |
| `title` | `Microsoft.AspNetCore.Html.IHtmlContent` | Required and non-null, including `LocalizedHtmlString`. `HtmlString.Empty` suppresses heading and browser-title text but still leaves the current node in a visible trail. |
| `heading` | `string` | Heading tag rendered below the trail. Defaults to `h1` on admin requests and to none on the front end. `heading=""` renders none. |
| `page-title` | `bool` | Registers the title as a `<title>` segment. Defaults to `true` everywhere. |
| `display-type` | `string` | Defaults to `DetailAdmin` on admin requests and `Detail` elsewhere; becomes an alternate of every shape the trail renders. |

### Title Values

Pass the Razor localizer result directly. Wrap anything that is not already `IHtmlContent` with `OrchardCore.DisplayManagement.Html.HtmlContentString`, which encodes untrusted strings when written:

| Value in the view | Title expression |
|---|---|
| HTML-localized label | `title="@T["Edit {0}", record.Name]"` |
| Plain model string | `title="@(new HtmlContentString(Model.DisplayText ?? string.Empty))"` |
| `LocalizedString` (`IStringLocalizer`) | `title="@(new HtmlContentString(localizedTitle.Value ?? string.Empty))"` |
| `DataLocalizedString` (`IDataLocalizer`) | `title="@(new HtmlContentString(dataTitle.Value ?? string.Empty))"` |
| Intentionally empty | `title="@Microsoft.AspNetCore.Html.HtmlString.Empty"` |

Add `@using OrchardCore.DisplayManagement.Html` for the short form. **Never** extract `T[...].Value` or use `Html.Raw` as a conversion.

The helper renders the title once with `WriteTo(writer, HtmlEncoder.Default)`, HTML-decodes it once, then safely encodes that plain text for the heading, the current node, and the browser title. Output is always text-only: `IHtmlContent` does not allow markup in titles, and a literal `&amp;` wrapped with `HtmlContentString` stays literal.

## `<breadcrumb-item>` Attributes

The child content is the ancestor text, e.g. `@T["Records"]` (HTML-decoded after Razor evaluates it, then re-encoded on output).

| Attribute | Type | Behavior |
|---|---|---|
| `id` | `string` | Stable node ID for shape alternates (not an HTML `id`). |
| `action`, `controller`, `area` | `string` | MVC route target; prefer these over hand-built URLs. |
| `route-*` | `string` | Extra route values, e.g. `route-id="@record.Id"`. |
| `route-values` | `IDictionary<string, string>` | Extra route values as a dictionary (not an anonymous object or `RouteValueDictionary`). |
| `url` | `string` | Direct link; ignored when `action` is set. |
| `permission` | `string` | Permission name stored as `BreadcrumbItem.PermissionName`; unknown names are ignored. |
| `resource` | `object` | Resource the permission is evaluated against. |
| `link-enabled` | `bool` | Defaults to `true`; `false` renders the ancestor as plain text. |

There is no `position` attribute: `BreadcrumbItem` has no `Position` and does not implement `IPositioned`. Unauthorized ancestors, ancestors with `link-enabled="false"`, and ancestors without a target stay visible as plain text; permissions control links, not node visibility. Named permissions are resolved only when finalizing visible ancestor links after providers run. Link checks are not endpoint security: keep authorization on the target action.

### Nested Editors

Link back through the real parent entity, read from the view model or loaded with an existing service injected into Razor (not a breadcrumb-specific service):

```razor
@inject OrchardCore.ContentManagement.IContentManager ContentManager
@{
    var parent = await ContentManager.GetAsync(Model.ParentContentItemId);
}

<zone Name="Title">
    <breadcrumb name="RecordsEdit" title="@T["Edit {0}", Model.Record.Name]">
        <breadcrumb-item id="Records" action="Index" controller="Record" area="My.Module">@T["Records"]</breadcrumb-item>
        <breadcrumb-item id="Parent" action="Edit" controller="Record" area="My.Module"
                         route-id="@parent.ContentItemId"
                         permission="@MyPermissions.ManageRecords.Name"
                         resource="@parent">@T["Edit {0}", parent.DisplayText]</breadcrumb-item>
    </breadcrumb>
</zone>
```

## Postprocessing with `IBreadcrumbProvider`

Use a provider for shared or cross-module ancestor changes; keep screen-specific nodes in the owning view.

```csharp
public interface IBreadcrumbProvider
{
    ValueTask BuildBreadcrumbAsync(BreadcrumbContext context);
}
```

`BreadcrumbContext` (namespace `OrchardCore.Navigation`) exposes read-only properties; its constructor is `BreadcrumbContext(string name, string title, List<BreadcrumbItem> items, ViewContext viewContext, bool showTrail)`.

| Property | Type | Use |
|---|---|---|
| `Name` | `string` | Literal trail name; filter on it explicitly. |
| `Title` | `string` | Normalized plain-text title. Providers cannot change the current node. |
| `Items` | `List<BreadcrumbItem>` | Shared mutable **ancestor** list: add, insert, remove, reorder, or edit. The current node is not in it. |
| `ViewContext` | `ViewContext` | Request context, e.g. `AdminAttribute.IsApplied(context.ViewContext.HttpContext)`. |
| `ShowTrail` | `bool` | Always `true` in helper-created contexts; hidden trails never invoke providers. |

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.Navigation;

public sealed class RecordsBreadcrumbProvider : IBreadcrumbProvider
{
    private readonly IStringLocalizer S;

    public RecordsBreadcrumbProvider(IStringLocalizer<RecordsBreadcrumbProvider> localizer)
    {
        S = localizer;
    }

    public ValueTask BuildBreadcrumbAsync(BreadcrumbContext context)
    {
        if (!AdminAttribute.IsApplied(context.ViewContext.HttpContext) ||
            !context.Name.StartsWith("Records", StringComparison.Ordinal) ||
            context.Name == "Records" ||
            context.Items.Any(item => item.Id == "Records"))
        {
            return ValueTask.CompletedTask;
        }

        context.Items.Insert(0, new BreadcrumbItem
        {
            Id = "Records",
            Text = S["Records"].Value,
            RouteValues = new() { ["area"] = "My.Module", ["controller"] = "Record", ["action"] = "Index" },
            PermissionName = "ManageRecords",
        });

        return ValueTask.CompletedTask;
    }
}
```

```csharp
services.AddBreadcrumbProvider<RecordsBreadcrumbProvider>();
```

- `AddBreadcrumbProvider<T>()` registers a scoped service with `TryAddEnumerable`; providers run sequentially in registration order and see earlier changes.
- Set `Text` to a plain string (not encoded HTML). Describe links with `Url` or `RouteValues`, `PermissionName` or `Permissions` (accepts `Permission` objects), `Resource`, and `LinkEnabled`. Do not set or rely on `Href` or `IsCurrent`; the helper finalizes them after providers.
- Filter by `Name` or request, never by comparing localized title text.

## Dashboard Ancestor

Enabling `OrchardCore.AdminDashboard` registers `OrchardCore.AdminDashboard.Services.DashboardBreadcrumbProvider`. On admin requests it inserts a localized `Dashboard` ancestor (ID `Dashboard`) at index `0`, unless `context.Name == "Dashboard"` or an ancestor already has that ID. Its `Url` is `"~/" + AdminOptions.AdminUrlPrefix` and its `Permissions` list holds the static `OrchardCore.AdminDashboard.Permissions.AccessAdminDashboard` permission, so a denied user sees it as plain text. It never changes front-end trails or the title, and later providers can move it. The dashboard page itself uses `<breadcrumb name="Dashboard" title="@T["Dashboard"]" />`.

## Hiding Admin Breadcrumbs

**Show breadcrumb** under **Configuration → Settings → Admin** (`AdminSettings.ShowBreadcrumb`, default `true`) hides admin trails only:

```json
{
  "steps": [
    {
      "name": "settings",
      "AdminSettings": {
        "ShowBreadcrumb": false
      }
    }
  ]
}
```

When hidden, the helper never evaluates its child content, never resolves or runs providers (Dashboard included), creates no `BreadcrumbContext` or items, generates no URLs, checks no permissions, and renders no shapes. It renders only the title, directly in a heading with the `oc-breadcrumb-title` class, and registers the browser title according to `heading` and `page-title`; with `heading=""` and `page-title="false"` it still requires a non-null title but does not even render it. Breadcrumb template overrides therefore do not apply to the hidden state. Attribute expressions (`T[...]`, `HtmlContentString` wrappers) and Razor code or model lookups outside the child content still run.

**All visibility decisions belong to the tag helper.** Views must not read `AdminSettings.ShowBreadcrumb` or guard lookups, ancestor declarations, or the helper on it. Front-end trails ignore the setting.

## Front-End Use

The same helpers work in a front-end theme with display type `Detail` and no heading by default:

```razor
@using OrchardCore.DisplayManagement.Html

<breadcrumb name="ProductDetails" title="@(new HtmlContentString(Model.Product.Name ?? string.Empty))" page-title="false">
    <breadcrumb-item id="Catalog" action="Index" controller="Catalog" area="My.Module">@T["Catalog"]</breadcrumb-item>
</breadcrumb>
```

A visible front-end trail always evaluates its children and providers, even with no heading and `page-title="false"`. Providers see front-end trails too unless they filter on the request, and the Dashboard provider leaves them unchanged.

## Theming

| Shape | Alternates (least to most specific) |
|---|---|
| `Breadcrumb` | `Breadcrumb__[Name]`, `Breadcrumb_[DisplayType]`, `Breadcrumb_[DisplayType]__[Name]`, e.g. `Breadcrumb-ContentsEdit.cshtml`, `Breadcrumb.DetailAdmin.cshtml`, `Breadcrumb-ContentsEdit.DetailAdmin.cshtml` |
| `BreadcrumbItem` | `BreadcrumbItem__[Name]`, `BreadcrumbItem__[Id]`, `BreadcrumbItem__[Name]__[Id]`, then the same three with the display type, e.g. `BreadcrumbItem-ContentsEdit-Title.DetailAdmin.cshtml` |

- Current-node hooks: `BreadcrumbItem-Title.cshtml`, `BreadcrumbItem-[Name]-Title.cshtml`, `BreadcrumbItem-Title.[DisplayType].cshtml`, `BreadcrumbItem-[Name]-Title.[DisplayType].cshtml`.
- In names and IDs, `.` encodes to `_` and `-` to `__`.
- `Breadcrumb.cshtml` renders the `<ol class="breadcrumb">` trail and the heading together, so it is the one template to override to rearrange them. Template `Breadcrumb.DetailAdmin.cshtml` to restyle only the admin, or `Breadcrumb.Detail.cshtml` for the front end.
- The trail carries `oc-breadcrumb` and `breadcrumb-{name}` classes and is plain Bootstrap markup, so `--bs-breadcrumb-*` custom properties restyle it without a template.
- `Breadcrumb` exposes `Name`, `Heading`, `Title`, and `ShowTrail`; `BreadcrumbItem` (`BreadcrumbItemViewModel`) exposes `Name`, `Item` (the `BreadcrumbItem`), `Breadcrumb` (the parent shape), `Text`, `Href`, `IsCurrent`, and `Level`. Render text with `@Model.Text` or `TagBuilder.InnerHtml.Append(...)`, never `Html.Raw` or a second decode, and link only when `Href` is set rather than rebuilding links from route values.
- The default item renders `<li class="breadcrumb-item">`, adding `active` and `aria-current="page"` to the current node. Classes a provider adds to `BreadcrumbItem.Classes` are rendered on that `<li>`.
- `[Id]` alternates exist only for nodes with an ID. On the Dashboard page, `BreadcrumbItem-Dashboard-Title.cshtml` restyles its current node; `BreadcrumbItem-Dashboard.cshtml` matches both the Dashboard ancestor (by ID) and every node of the `Dashboard` trail (by name).
- Ship a `.DetailAdmin` or `.Detail` template only when the admin and the front end really differ.

See `references/breadcrumbs-examples.md` for the full alternate tables, a title-above-trail override, and the low-level shape API.

## Gotchas

- **A missing title is invalid**, even on a self-closing helper and on the hidden path. `HtmlString.Empty` is valid and still leaves a current node in a visible trail.
- **Children are ancestors only.** Never add a child for the current page; the helper appends it with ID `Title`, and providers can neither remove nor replace it.
- **Second heading.** Remove the view's old `<h1>` and `RenderTitleSegments` call when the helper owns the title.
- **No visibility guards in views.** Never read `AdminSettings.ShowBreadcrumb` or wrap lookups or markup in setting-dependent branches.
- **`route-values` type.** It takes `IDictionary<string, string>`, not an anonymous object or `RouteValueDictionary`.
- **Link checks are not endpoint security.** `permission` and `link-enabled` only control whether an ancestor is rendered as a link.
- **Renaming breaks customizations.** Trail names and ancestor IDs are the contract for providers and alternates; keep them stable.
- **Hidden trails bypass templates.** A `Breadcrumb` override cannot change the directly rendered heading of a hidden admin trail.

## Upgrading from 3.x

- Before: `<zone Name="Title"><h1>@RenderTitleSegments(T["Edit Record"])</h1></zone>`.
- After: `<zone Name="Title"><breadcrumb name="RecordsEdit" title="@T["Edit Record"]">...</breadcrumb></zone>`, with `@addTagHelper *, OrchardCore.Navigation.Core` in `_ViewImports.cshtml`. Remove the old heading and title segment call to avoid duplicates.
- Theme overrides of admin page titles should now target `Breadcrumb.cshtml` or the `BreadcrumbItem-Title` alternates.

## Related Skills

- `orchardcore-admin` for admin controllers, views, and settings.
- `orchardcore-admin-lists` for list pages whose trail name matches the list name.
- `orchardcore-admin-edit-views` for the edit pages and forms below the title.
- `orchardcore-navigation` for `INavigationProvider` menus and pager shapes.

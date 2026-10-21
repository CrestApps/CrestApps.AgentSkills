# Breadcrumb Examples

## Admin Edit View with Two Ancestors

`Views/Record/Edit.cshtml`:

```razor
@model RecordEditViewModel

<zone Name="Title">
    <breadcrumb name="RecordsEdit" title="@T["Edit {0}", Model.Name]">
        <breadcrumb-item id="Records" action="Index" controller="Record" area="My.Module"
                         permission="@MyPermissions.ManageRecords.Name">@T["Records"]</breadcrumb-item>
        <breadcrumb-item id="Category" action="Edit" controller="Category" area="My.Module"
                         route-id="@Model.CategoryId"
                         link-enabled="@Model.CanEditCategory">@T["Category {0}", Model.CategoryName]</breadcrumb-item>
    </breadcrumb>
</zone>

<form asp-action="Edit" method="post">
    @* editor fields *@
</form>
```

With `OrchardCore.AdminDashboard` enabled, the trail reads _Dashboard › Records › Category Books › Edit My Record_, followed by an `<h1>` holding _Edit My Record_.

## Route Values as a Dictionary

```razor
@{
    var routeValues = new Dictionary<string, string>
    {
        ["contentTypeId"] = Model.ContentType,
        ["stereotype"] = "Widget",
    };
}

<breadcrumb name="WidgetsEdit" title="@T["Edit Widget"]">
    <breadcrumb-item id="Contents" action="List" controller="Admin" area="OrchardCore.Contents"
                     route-values="@routeValues">@T["Widgets"]</breadcrumb-item>
</breadcrumb>
```

## Trail Without the Page Title

Place a trail above a page that keeps its own heading:

```razor
<zone Name="Breadcrumbs">
    <breadcrumb name="ReportsDetail" title="@T["Monthly Report"]" heading="" page-title="false">
        <breadcrumb-item id="Reports" action="Index" controller="Reports" area="My.Module">@T["Reports"]</breadcrumb-item>
    </breadcrumb>
</zone>
```

## Title Above the Trail (Theme Override)

`Views/Breadcrumb.DetailAdmin.cshtml` in a custom admin theme:

```razor
@{
    var list = new TagBuilder("ol");
    list.AddCssClass("breadcrumb");

    string heading = Model.Heading;
    string title = Model.Title;

    foreach (var item in Model)
    {
        list.InnerHtml.AppendHtml(await DisplayAsync(item));
    }

    TagBuilder nav = Tag(Model, "nav");
    nav.Attributes["aria-label"] = T["Breadcrumb"].Value;
    nav.InnerHtml.AppendHtml(list);
}

@if (!string.IsNullOrEmpty(heading) && !string.IsNullOrEmpty(title))
{
    TagBuilder titleTag = new(heading);
    titleTag.AddCssClass("oc-breadcrumb-title");
    titleTag.InnerHtml.Append(title);

    @titleTag
}
@if (Model.ShowTrail)
{
    @nav
}
```

Drop the `oc-breadcrumb-title` block to let the trail stand in for the title, or skip items whose `IsCurrent` is `true` so the current page is not repeated.

## Current Node of One Trail

`Views/BreadcrumbItem-ContentsEdit-Title.DetailAdmin.cshtml`:

```razor
<li class="breadcrumb-item active" aria-current="page">
    <i class="fa-solid fa-pen-to-square me-1" aria-hidden="true"></i>@Model.Text
</li>
```

## Alternate Tables

The display engine tries the most specific alternate first (the last row of each table). For a trail named `ContentsEdit` on an admin request (`DetailAdmin`):

| `Breadcrumb` shape alternate | File | Matches |
|---|---|---|
| `Breadcrumb` | `Breadcrumb.cshtml` | Every trail |
| `Breadcrumb__ContentsEdit` | `Breadcrumb-ContentsEdit.cshtml` | This trail, admin and front end |
| `Breadcrumb_DetailAdmin` | `Breadcrumb.DetailAdmin.cshtml` | Every admin trail |
| `Breadcrumb_DetailAdmin__ContentsEdit` | `Breadcrumb-ContentsEdit.DetailAdmin.cshtml` | This trail on the admin |

For its current node (`Id = "Title"`):

| `BreadcrumbItem` shape alternate | File |
|---|---|
| `BreadcrumbItem` | `BreadcrumbItem.cshtml` |
| `BreadcrumbItem__ContentsEdit` | `BreadcrumbItem-ContentsEdit.cshtml` |
| `BreadcrumbItem__Title` | `BreadcrumbItem-Title.cshtml` |
| `BreadcrumbItem__ContentsEdit__Title` | `BreadcrumbItem-ContentsEdit-Title.cshtml` |
| `BreadcrumbItem_DetailAdmin` | `BreadcrumbItem.DetailAdmin.cshtml` |
| `BreadcrumbItem_DetailAdmin__ContentsEdit` | `BreadcrumbItem-ContentsEdit.DetailAdmin.cshtml` |
| `BreadcrumbItem_DetailAdmin__Title` | `BreadcrumbItem-Title.DetailAdmin.cshtml` |
| `BreadcrumbItem_DetailAdmin__ContentsEdit__Title` | `BreadcrumbItem-ContentsEdit-Title.DetailAdmin.cshtml` |

Ancestors get the same set with their own ID instead of `Title`; nodes without an ID only get the name and display type alternates. A trail named `My.Trail` contributes `Breadcrumb__My_Trail` (`.` becomes `_`, `-` becomes `__`), so keep names and IDs to letters and digits.

Restyle without templates through the `oc-breadcrumb` and `breadcrumb-{name}` classes (e.g. `breadcrumb-contents-edit`) and the Bootstrap `--bs-breadcrumb-*` custom properties.

## Low-Level Shape Creation

`IShapeFactory.BreadcrumbAsync` (namespace `OrchardCore.DisplayManagement`) renders a prebuilt sequence. The caller supplies every node, including the final current node, with `Href` and `IsCurrent` already resolved. It does not run providers, authorize links, add Dashboard, or register the browser title, so prefer the tag helper in views.

```csharp
var items = new[]
{
    new BreadcrumbItem { Id = "Records", Text = S["Records"].Value, Href = recordsUrl },
    new BreadcrumbItem { Id = "Title", Text = S["Import"].Value, IsCurrent = true },
};

var shape = await _shapeFactory.BreadcrumbAsync("RecordsImport", items, heading: "h1", displayType: "DetailAdmin");
```

Pass `showTrail: false` to create a shape with `ShowTrail` false and the last item's text as `Title`, which the default template renders as the heading only. Custom `Breadcrumb` templates must honor `ShowTrail` and use `Title` in that case instead of relying on the child item shapes.

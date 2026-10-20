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

Pass `showTrail: false` to render only the heading through the template.

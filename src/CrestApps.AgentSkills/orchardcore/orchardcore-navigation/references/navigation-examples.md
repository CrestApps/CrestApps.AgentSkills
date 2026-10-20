# Navigation Examples

## Example 1: Admin Menu for a Custom Module

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

public sealed class AdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> localizer)
    {
        S = localizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Content"], NavigationConstants.AdminMenuContentPosition, content => content
                .Add(S["Testimonials"], S["Testimonials"].PrefixPosition(), testimonials => testimonials
                    .Action("Index", "Admin", "CrestApps.Testimonials")
                    .Permission(TestimonialPermissions.ManageTestimonials)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
```

Register it with `services.AddNavigationProvider<AdminMenu>();`.

## Example 2: Main Menu via Recipe

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "main-menu-001",
          "ContentType": "Menu",
          "DisplayText": "Main Menu",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Main Menu"
          },
          "AliasPart": {
            "Alias": "main-menu"
          },
          "MenuPart": {},
          "MenuItemsListPart": {
            "MenuItems": [
              {
                "ContentType": "LinkMenuItem",
                "ContentItemId": "menu-item-home",
                "DisplayText": "Home",
                "LinkMenuItemPart": {
                  "Url": "~/"
                }
              },
              {
                "ContentType": "LinkMenuItem",
                "ContentItemId": "menu-item-blog",
                "LinkMenuItemPart": {
                  "Name": "Blog",
                  "Url": "~/blog"
                }
              },
              {
                "ContentType": "LinkMenuItem",
                "ContentItemId": "menu-item-about",
                "LinkMenuItemPart": {
                  "Name": "About Us",
                  "Url": "~/about"
                }
              },
              {
                "ContentType": "LinkMenuItem",
                "ContentItemId": "menu-item-contact",
                "DisplayText": "Contact",
                "LinkMenuItemPart": {
                  "Url": "~/contact"
                }
              }
            ]
          }
        }
      ]
    }
  ]
}
```

## Example 3: Front-End List with a Page Size Selector

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;
using OrchardCore.Navigation;

public sealed class TestimonialsController : Controller
{
    private readonly ITestimonialStore _store;
    private readonly IShapeFactory _shapeFactory;
    private readonly PagerOptions _pagerOptions;

    public TestimonialsController(ITestimonialStore store, IShapeFactory shapeFactory, IOptions<PagerOptions> pagerOptions)
    {
        _store = store;
        _shapeFactory = shapeFactory;
        _pagerOptions = pagerOptions.Value;
    }

    public async Task<IActionResult> Index(PagerParameters pagerParameters)
    {
        // ?page=2&pageSize=25 is honored only when 25 is an allowed page size.
        var pager = new Pager(pagerParameters, _pagerOptions);
        var (items, total) = await _store.ListAsync(pager.GetStartIndex(), pager.PageSize);

        var pagerShape = await _shapeFactory.PagerAsync(pager, total, new RouteData());

        return View(new TestimonialsViewModel { Items = items, Pager = pagerShape });
    }
}
```

```cshtml
@foreach (var item in Model.Items)
{
    <blockquote>@item.Quote</blockquote>
}

@await DisplayAsync(Model.Pager)
```

The `Pager` shape renders the **Items per page** selector automatically when page size selection is enabled in the General settings.

## Example 4: Styled Page Size Selector

`Views/Pager_PageSizeSelector.cshtml` in a front-end theme (TheTheme ships a similar template):

```cshtml
@using Microsoft.AspNetCore.Mvc.Rendering
@{
    var items = Model.PageSizes as List<SelectListItem>;
}
@if (items is { Count: > 0 })
{
    <div class="pager-page-size d-flex align-items-center ms-auto">
        <label class="col-form-label text-nowrap me-2">@T["Items per page"]</label>
        <select class="form-select form-select-sm w-auto" aria-label="@T["Items per page"]" onchange="if (this.value) { window.location.href = this.value; }">
            @foreach (var item in items)
            {
                <option value="@item.Value" selected="@item.Selected">@item.Text</option>
            }
        </select>
    </div>
}
```

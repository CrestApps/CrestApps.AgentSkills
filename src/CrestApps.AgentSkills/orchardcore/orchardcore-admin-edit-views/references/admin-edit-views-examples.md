# Admin Edit View Examples

A `My.Catalog` module with a `ProductPart` (part editor plus per-type settings), a site settings group, and a workflow activity. Every editor renders only `ocat-*` rows; the hosting pages (content item editor, content type editor, settings page, workflow activity page) own the form and the Save button.

## Content Part Editor

```csharp
using OrchardCore.ContentManagement;

public sealed class ProductPart : ContentPart
{
    public string Sku { get; set; }

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public bool IsFeatured { get; set; }
}

public sealed class ProductPartSettings
{
    public bool RequireSku { get; set; }

    public string SkuPattern { get; set; }
}

public class ProductPartViewModel
{
    public string Sku { get; set; }

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public bool IsFeatured { get; set; }

    public ProductPartSettings Settings { get; set; }
}
```

```csharp
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

public sealed class ProductPartDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    internal readonly IStringLocalizer S;

    public ProductPartDisplayDriver(IStringLocalizer<ProductPartDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Edit(ProductPart part, BuildPartEditorContext context)
    {
        return Initialize<ProductPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Sku = part.Sku;
            model.Price = part.Price;
            model.Stock = part.Stock;
            model.IsFeatured = part.IsFeatured;
            model.Settings = context.TypePartDefinition.GetSettings<ProductPartSettings>();
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ProductPart part, UpdatePartEditorContext context)
    {
        var model = new ProductPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = context.TypePartDefinition.GetSettings<ProductPartSettings>();

        if (settings.RequireSku && string.IsNullOrWhiteSpace(model.Sku))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Sku), S["A SKU is required."]);
        }
        else if (!string.IsNullOrEmpty(settings.SkuPattern) && !string.IsNullOrEmpty(model.Sku) && !Regex.IsMatch(model.Sku, settings.SkuPattern))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Sku), S["The SKU does not match the expected format."]);
        }

        if (model.Price < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Price), S["The price can't be negative."]);
        }

        part.Sku = model.Sku;
        part.Price = model.Price;
        part.Stock = model.Stock;
        part.IsFeatured = model.IsFeatured;

        return Edit(part, context);
    }
}
```

`Views/ProductPart.Edit.cshtml` (the content item editor already wraps it in `ocat-wrapper content-part-wrapper content-part-wrapper-product-part`):

```cshtml
@model ProductPartViewModel

<div class="ocat-wrapper" asp-validation-class-for="Sku">
    <label asp-for="Sku" class="ocat-label @(Model.Settings.RequireSku ? "ocat-label-required" : "")">@T["SKU"]</label>
    <div class="ocat-end">
        <input asp-for="Sku" class="form-control code content-preview-text" />
        <span asp-validation-for="Sku"></span>
        <span class="hint">@T["The stock keeping unit."]</span>
    </div>
</div>

<div class="ocat-limited-wrapper" asp-validation-class-for="Price">
    <label asp-for="Price" class="ocat-label">@T["Price"]</label>
    <div class="ocat-limited">
        <div class="input-group">
            <span class="input-group-text">$</span>
            <input asp-for="Price" type="number" step="0.01" min="0" class="form-control" />
        </div>
        <span asp-validation-for="Price"></span>
    </div>
</div>

<div class="ocat-limited-wrapper" asp-validation-class-for="Stock">
    <label asp-for="Stock" class="ocat-label">@T["Stock"]</label>
    <div class="ocat-limited">
        <input asp-for="Stock" type="number" min="0" class="form-control" />
        <span asp-validation-for="Stock"></span>
    </div>
</div>

<div class="ocat-wrapper">
    <div class="ocat-end-offset">
        <div class="form-check form-switch">
            <input asp-for="IsFeatured" type="checkbox" class="form-check-input" />
            <label asp-for="IsFeatured" class="form-check-label">@T["Featured"]</label>
            <span class="hint dashed">@T["Featured products are shown first."]</span>
        </div>
    </div>
</div>
```

## Part Settings Editor

Rendered on the content type editor when the part is attached to a type.

```csharp
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class ProductPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<ProductPart>
{
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<ProductPartSettings>("ProductPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<ProductPartSettings>();

            model.RequireSku = settings.RequireSku;
            model.SkuPattern = settings.SkuPattern;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new ProductPartSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(model);

        return Edit(contentTypePartDefinition, context);
    }
}
```

`Views/ProductPartSettings.Edit.cshtml`:

```cshtml
@model ProductPartSettings

<div class="ocat-wrapper">
    <div class="ocat-end-offset">
        <div class="form-check">
            <input asp-for="RequireSku" type="checkbox" class="form-check-input" />
            <label asp-for="RequireSku" class="form-check-label">@T["Require a SKU"]</label>
            <span class="hint dashed">@T["Whether editors must enter a SKU."]</span>
        </div>
    </div>
</div>

<div class="ocat-limited-wrapper" asp-validation-class-for="SkuPattern">
    <label asp-for="SkuPattern" class="ocat-label">@T["SKU pattern"]</label>
    <div class="ocat-limited">
        <input asp-for="SkuPattern" class="form-control code" />
        <span asp-validation-for="SkuPattern"></span>
        <span class="hint">@T["Optional regular expression the SKU must match."]</span>
    </div>
</div>
```

## Site Settings Editor

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

public sealed class CatalogSettings
{
    public int PageSize { get; set; } = 20;

    public string Currency { get; set; } = "USD";

    public bool ShowOutOfStock { get; set; }
}

public sealed class CatalogSettingsDisplayDriver : SiteDisplayDriver<CatalogSettings>
{
    public const string GroupId = "catalog";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public CatalogSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId => GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, CatalogSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CatalogPermissions.ManageCatalogSettings))
        {
            return null;
        }

        return Initialize<CatalogSettings>("CatalogSettings_Edit", model =>
        {
            model.PageSize = settings.PageSize;
            model.Currency = settings.Currency;
            model.ShowOutOfStock = settings.ShowOutOfStock;
        }).Location("Content:5#General")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, CatalogSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CatalogPermissions.ManageCatalogSettings))
        {
            return null;
        }

        await context.Updater.TryUpdateModelAsync(settings, Prefix);

        return await EditAsync(site, settings, context);
    }
}
```

`Views/CatalogSettings.Edit.cshtml` (no form, no Save button):

```cshtml
@model CatalogSettings

<div class="ocat-limited-wrapper" asp-validation-class-for="PageSize">
    <label asp-for="PageSize" class="ocat-label ocat-label-required">@T["Page size"]</label>
    <div class="ocat-limited">
        <input asp-for="PageSize" type="number" min="1" max="200" class="form-control" />
        <span asp-validation-for="PageSize"></span>
    </div>
</div>

<div class="ocat-limited-wrapper" asp-validation-class-for="Currency">
    <label asp-for="Currency" class="ocat-label">@T["Currency"]</label>
    <div class="ocat-limited">
        <select asp-for="Currency" class="form-select">
            <option value="USD">USD</option>
            <option value="EUR">EUR</option>
        </select>
    </div>
</div>

<div class="ocat-wrapper">
    <div class="ocat-end-offset">
        <div class="form-check">
            <input asp-for="ShowOutOfStock" type="checkbox" class="form-check-input" />
            <label asp-for="ShowOutOfStock" class="form-check-label">@T["Show out-of-stock products"]</label>
        </div>
    </div>
</div>
```

## Workflow Activity Editor

`RestockTask` is a `TaskActivity<RestockTask>` with `Sku` and `Quantity` (`WorkflowExpression<int>`) properties (see `orchardcore-workflows`).

```csharp
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

public class RestockTaskViewModel
{
    public string Sku { get; set; }

    public string QuantityExpression { get; set; }
}

public sealed class RestockTaskDisplayDriver : ActivityDisplayDriver<RestockTask, RestockTaskViewModel>
{
    protected override void EditActivity(RestockTask activity, RestockTaskViewModel model)
    {
        model.Sku = activity.Sku;
        model.QuantityExpression = activity.Quantity.Expression;
    }

    protected override void UpdateActivity(RestockTaskViewModel model, RestockTask activity)
    {
        activity.Sku = model.Sku;
        activity.Quantity = new WorkflowExpression<int>(model.QuantityExpression);
    }
}
```

`Views/Items/RestockTask.Fields.Edit.cshtml`:

```cshtml
@model RestockTaskViewModel

<div class="ocat-wrapper" asp-validation-class-for="Sku">
    <label asp-for="Sku" class="ocat-label ocat-label-required">@T["SKU"]</label>
    <div class="ocat-end">
        <input asp-for="Sku" class="form-control" />
        <span asp-validation-for="Sku"></span>
    </div>
</div>

<div class="ocat-wrapper" asp-validation-class-for="QuantityExpression">
    <label asp-for="QuantityExpression" class="ocat-label">@T["Quantity"]</label>
    <div class="ocat-end">
        <input asp-for="QuantityExpression" class="form-control code" />
        <span asp-validation-for="QuantityExpression"></span>
        <span class="hint">@T["A JavaScript expression returning the quantity to add. Example {0}", "input('Quantity')"]</span>
    </div>
</div>
```

## Registration

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductPartDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ProductPartSettingsDisplayDriver>();
        services.AddSiteDisplayDriver<CatalogSettingsDisplayDriver>();
    }
}

[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<RestockTask, RestockTaskDisplayDriver>();
    }
}
```

## Common Mistakes

| Wrong | Right |
|---|---|
| `<div class="mb-3">` row, `<label class="form-label">` | `ocat-wrapper` row, `ocat-label` |
| Input or hint directly inside `ocat-wrapper` | Inside `ocat-end` |
| Checkbox in `ocat-end` with an empty left label | Checkbox in `ocat-end-offset` |
| A row with both `ocat-end` and `ocat-end-offset` | Exactly one of them |
| `<form>` or Save button inside a part, field, or settings editor | Only rows; the host page owns the form |
| `d-none` toggled on the inner `ocat-end` | `d-none` on the `ocat-wrapper` |
| Replacing a `field-wrapper-*` row with `ocat-limited-wrapper` | Nest `ocat-limited-wrapper` inside `ocat-end` |
| Rendering a field's configured hint as `<span class="hint">` | `@await DisplayAsync(await New.Hint_Admin(Hint: settings.Hint))` |
| `ocat-*` classes in a front-end theme view | Front-end themes use their own markup |

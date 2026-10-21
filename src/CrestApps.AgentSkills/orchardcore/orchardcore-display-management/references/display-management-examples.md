# Orchard Core Display Management Examples

## Example 1: Blog Post Subtitle Part Display Driver

A complete display driver for a SubtitlePart:

```csharp
using OrchardCore.ContentManagement;

public sealed class SubtitlePart : ContentPart
{
    public string Subtitle { get; set; }
}
```

```csharp
public class SubtitlePartViewModel
{
    public string Subtitle { get; set; }
    public ContentItem ContentItem { get; set; }
}
```

```csharp
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

public sealed class SubtitlePartDisplayDriver : ContentPartDisplayDriver<SubtitlePart>
{
    public override IDisplayResult Display(SubtitlePart part, BuildPartDisplayContext context)
    {
        return Initialize<SubtitlePartViewModel>("SubtitlePart", model =>
        {
            model.Subtitle = part.Subtitle;
            model.ContentItem = part.ContentItem;
        })
        .Location("Detail", "Content:3")
        .Location("Summary", "Content:3");
    }

    public override IDisplayResult Edit(SubtitlePart part, BuildPartEditorContext context)
    {
        return Initialize<SubtitlePartViewModel>("SubtitlePart_Edit", model =>
        {
            model.Subtitle = part.Subtitle;
            model.ContentItem = part.ContentItem;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(SubtitlePart part, UpdatePartEditorContext context)
    {
        var model = new SubtitlePartViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        part.Subtitle = model.Subtitle;
        return Edit(part, context);
    }
}
```

### Display Template (Views/SubtitlePart.cshtml)

```cshtml
@model SubtitlePartViewModel

<h2 class="subtitle">@Model.Subtitle</h2>
```

### Editor Template (Views/SubtitlePart_Edit.cshtml)

```cshtml
@model SubtitlePartViewModel

<div class="ocat-wrapper" asp-validation-class-for="Subtitle">
    <label asp-for="Subtitle" class="ocat-label">Subtitle</label>
    <div class="ocat-end">
        <input asp-for="Subtitle" class="form-control" />
        <span asp-validation-for="Subtitle" class="text-danger"></span>
    </div>
</div>
```

### Registration in Startup.cs

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<SubtitlePart>()
            .UseDisplayDriver<SubtitlePartDisplayDriver>();
    }
}
```

## Example 2: Content Part with Multiple Display Types

```csharp
public sealed class ProductInfoPartDisplayDriver : ContentPartDisplayDriver<ProductInfoPart>
{
    public override IDisplayResult Display(ProductInfoPart part, BuildPartDisplayContext context)
    {
        return Combine(
            Initialize<ProductInfoPartViewModel>("ProductInfoPart", model =>
            {
                model.Price = part.Price;
                model.Sku = part.Sku;
            })
            .Location("Detail", "Content:5"),

            Initialize<ProductInfoPartViewModel>("ProductInfoPart_Summary", model =>
            {
                model.Price = part.Price;
            })
            .Location("Summary", "Meta:5"),

            Initialize<ProductInfoPartViewModel>("ProductInfoPart_SummaryAdmin", model =>
            {
                model.Price = part.Price;
                model.Sku = part.Sku;
            })
            .Location("SummaryAdmin", "Meta:5")
        );
    }
}
```

## Example 3: Migrating an Obsolete `Shape()` Call (4.0)

Before (3.x), the shape is allocated even when placement hides it:

```csharp
public override async Task<IDisplayResult> DisplayAsync(Banner model, BuildDisplayContext context)
{
    var shape = await context.ShapeFactory.CreateAsync("Banner", Arguments.From(new { model.Text }));

    return Shape("Banner", shape).Location("Content:1");
}
```

After (4.0), the shape is created lazily, only when it is displayed:

```csharp
public sealed class BannerDisplayDriver : DisplayDriver<Banner>
{
    public override IDisplayResult Display(Banner model, BuildDisplayContext context)
    {
        return Factory("Banner", static (buildContext, model) =>
            buildContext.ShapeFactory.CreateAsync("Banner", Arguments.From(new { model.Text })), model)
            .Location("Content:1");
    }
}
```

When the shape only needs a view model, prefer `Initialize<TModel>()`:

```csharp
return Initialize<BannerViewModel, Banner>("Banner", static (viewModel, model) =>
{
    viewModel.Text = model.Text;
}, model)
.Location("Content:1");
```

## Example 4: Custom Editor for an Existing Field

A new editor choice for `TextField` needs only two templates in your module or theme, because `GetEditorShapeType(context)` turns the selected editor into the shape type `TextField_Edit__PostalCode`.

`Views/TextField-PostalCode.Option.cshtml` adds the choice to the field's **Editor** setting:

```cshtml
@{
    string currentEditor = Model.Editor;
}
<option value="PostalCode" selected="@(currentEditor == "PostalCode")">@T["Postal code"]</option>
```

`Views/TextField-PostalCode.Edit.cshtml` renders the editor:

```cshtml
@model OrchardCore.ContentFields.ViewModels.EditTextFieldViewModel

<div class="ocat-wrapper">
    <label asp-for="Text" class="ocat-label">@Model.PartFieldDefinition.DisplayName()</label>
    <div class="ocat-end">
        <input asp-for="Text" class="form-control" pattern="[0-9]{5}" inputmode="numeric" />
    </div>
</div>
```

When the editor needs a different view model or validation, add a driver for that editor only and narrow the default one. Depend on `OrchardCore.ContentFields` in the manifest so this `Startup` runs after the original registration:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentField<TextField>()
            .ForEditor<TextFieldDisplayDriver>(editor => editor != "PostalCode")
            .ForEditor<PostalCodeTextFieldDisplayDriver>(editor => editor == "PostalCode");
    }
}
```

Display modes work the same way: `{FieldType}-{Mode}.DisplayOption.cshtml` and `{FieldType}-{Mode}.Display.cshtml` templates, with `ForDisplayMode<TDriver>(mode => ...)` for a dedicated driver.

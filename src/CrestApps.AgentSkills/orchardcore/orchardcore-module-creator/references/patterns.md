# OrchardCore Patterns Reference

## Content Part

A content part is a reusable piece of content that can be attached to content types.

```csharp
using OrchardCore.ContentManagement;

namespace OrchardCore.YourModule.Models;

public sealed class YourPart : ContentPart
{
    public string Title { get; set; }
    public string Description { get; set; }
    public bool IsEnabled { get; set; }
}
```

## Content Part Driver

Drivers handle display and editing of content parts.

```csharp
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.YourModule.Models;
using OrchardCore.YourModule.ViewModels;

namespace OrchardCore.YourModule.Drivers;

public sealed class YourPartDisplayDriver : ContentPartDisplayDriver<YourPart>
{
    public override IDisplayResult Display(YourPart part, BuildPartDisplayContext context)
    {
        return Initialize<YourPartViewModel>("YourPart", model =>
        {
            model.Title = part.Title;
            model.Description = part.Description;
        }).Location("Detail", "Content:5");
    }

    public override IDisplayResult Edit(YourPart part, BuildPartEditorContext context)
    {
        return Initialize<YourPartViewModel>("YourPart_Edit", model =>
        {
            model.Title = part.Title;
            model.Description = part.Description;
            model.IsEnabled = part.IsEnabled;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(
        YourPart part, 
        UpdatePartEditorContext context)
    {
        var viewModel = new YourPartViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);
        
        part.Title = viewModel.Title;
        part.Description = viewModel.Description;
        part.IsEnabled = viewModel.IsEnabled;
        
        return Edit(part, context);
    }
}
```

## Content Field

Custom fields for content types.

```csharp
using OrchardCore.ContentManagement;

namespace OrchardCore.YourModule.Fields;

public sealed class YourField : ContentField
{
    public string Value { get; set; }
    public string[] Tags { get; set; }
}
```

## Migrations (YesSql)

Database migrations for creating indexes and tables.

```csharp
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace OrchardCore.YourModule;

public sealed class Migrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<YourIndex>(table => table
            .Column<string>("DocumentId", col => col.WithLength(26))
            .Column<string>("Name", col => col.WithLength(255))
            .Column<bool>("IsEnabled")
        );

        await SchemaBuilder.AlterIndexTableAsync<YourIndex>(table => table
            .CreateIndex("IDX_YourIndex_DocumentId", "DocumentId")
        );

        return 1;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<YourIndex>(table => table
            .AddColumn<DateTime>("CreatedUtc")
        );

        return 2;
    }
}
```

## Index Definition

```csharp
using YesSql.Indexes;

namespace OrchardCore.YourModule.Indexes;

public sealed class YourIndex : MapIndex
{
    public string DocumentId { get; set; }
    public string Name { get; set; }
    public bool IsEnabled { get; set; }
}

public sealed class YourIndexProvider : IndexProvider<YourDocument>
{
    public override void Describe(DescribeContext<YourDocument> context)
    {
        context.For<YourIndex>()
            .Map(doc => new YourIndex
            {
                DocumentId = doc.Id,
                Name = doc.Name,
                IsEnabled = doc.IsEnabled,
            });
    }
}
```

## Permission Provider

```csharp
using OrchardCore.Security.Permissions;

namespace OrchardCore.YourModule;

public sealed class PermissionProvider : IPermissionProvider
{
    public static readonly Permission ManageYourFeature = 
        new("ManageYourFeature", "Manage your feature");
    
    public static readonly Permission ViewYourFeature = 
        new("ViewYourFeature", "View your feature", [ManageYourFeature]);

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult<IEnumerable<Permission>>([ManageYourFeature, ViewYourFeature]);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = [ManageYourFeature],
        },
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Editor,
            Permissions = [ViewYourFeature],
        },
    ];
}
```

## Admin Menu

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace OrchardCore.YourModule;

public sealed class AdminMenu : NamedNavigationProvider
{
    private readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> localizer)
        : base(NavigationConstants.AdminId)
    {
        S = localizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Your Menu"], NavigationConstants.AdminMenuYourModulePriority, menu => menu
                .Id("yourmodule")
                .Add(S["Your Item"], S["Your Item"].PrefixPosition(), item => item
                    .Action("Index", "Admin", "OrchardCore.YourModule")
                    .Permission(PermissionProvider.ManageYourFeature)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
```

Prefer `NamedNavigationProvider` for admin menu providers. Fall back to implementing `INavigationProvider` only when one provider must intentionally handle more than one named menu.

## Content Handler

```csharp
using OrchardCore.ContentManagement.Handlers;

namespace OrchardCore.YourModule.Handlers;

public sealed class YourContentHandler : ContentHandlerBase
{
    public override Task PublishedAsync(PublishContentContext context)
    {
        // Handle content published event
        return Task.CompletedTask;
    }

    public override Task CreatedAsync(CreateContentContext context)
    {
        // Handle content created event
        return Task.CompletedTask;
    }

    public override Task RemovedAsync(RemoveContentContext context)
    {
        // Handle content removed event
        return Task.CompletedTask;
    }
}
```

## Background Task

```csharp
using OrchardCore.BackgroundTasks;

namespace OrchardCore.YourModule;

[BackgroundTask(Schedule = "*/15 * * * *", Description = "Runs every 15 minutes")]
public sealed class YourBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // Background work implementation
        return Task.CompletedTask;
    }
}
```

## Service Registration in Startup.cs

```csharp
public override void ConfigureServices(IServiceCollection services)
{
    // Content part
    services.AddContentPart<YourPart>()
        .UseDisplayDriver<YourPartDisplayDriver>();
    
    // Content field
    services.AddContentField<YourField>()
        .UseDisplayDriver<YourFieldDisplayDriver>();
    
    // Services
    services.AddScoped<IYourService, YourService>();
    
    // Migrations
    services.AddDataMigration<Migrations>();
    
    // Index provider
    services.AddIndexProvider<YourIndexProvider>();
    
    // Permissions
    services.AddPermissionProvider<PermissionProvider>();
    
    // Navigation
    services.AddNavigationProvider<AdminMenu>();
    
    // Handlers
    services.AddContentHandler<YourContentHandler>();
}
```

## DisplayDriver for Non-Content Models (Admin CRUD)

When building admin CRUD for custom models (not content items), use `DisplayDriver<TModel>`. This pattern requires **wrapper templates** for each display type used.

### Custom Model Display Driver

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace YourModule.Drivers;

internal sealed class YourModelDisplayDriver : DisplayDriver<YourModel>
{
    public override Task<IDisplayResult> DisplayAsync(YourModel model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("YourModel_Fields_SummaryAdmin", model).Location("Content:1"),
            View("YourModel_Buttons_SummaryAdmin", model).Location("Actions:5"),
            View("YourModel_DefaultMeta_SummaryAdmin", model).Location("Meta:5"),
            View("YourModel_Description_SummaryAdmin", model).Location("Description:1")
        );
    }

    public override IDisplayResult Edit(YourModel model, BuildEditorContext context)
    {
        return Initialize<YourModelViewModel>("YourModelFields_Edit", m =>
        {
            m.DisplayText = model.DisplayText;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(YourModel model, UpdateEditorContext context)
    {
        var viewModel = new YourModelViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);
        model.DisplayText = viewModel.DisplayText?.Trim();
        return Edit(model, context);
    }
}
```

### CRITICAL: Required Wrapper Templates

For `DisplayDriver<TModel>`, Orchard Core resolves a **root shape** per display type. If the wrapper template is missing, you get:

```
InvalidOperationException: The shape type 'YourModel_Edit' is not found
```

**You MUST create these wrapper template files:**

1. **`Views/YourModel.Edit.cshtml`** — root wrapper for the editor:

```cshtml
@await DisplayAsync(Model.Content)
```

2. **`Views/YourModel.SummaryAdmin.cshtml`** — root wrapper for admin list items:

```cshtml
<div class="row g-0">
    <div class="col-lg col-12 title d-flex align-items-center">
        <div class="summary">
            <div class="d-flex flex-column flex-md-row">
                <div class="me-2">
                    @if (Model.Content != null)
                    {
                        @await DisplayAsync(Model.Content)
                    }
                </div>
                @if (Model.Tags != null)
                {
                    <div class="tags me-1">@await DisplayAsync(Model.Tags)</div>
                }
                @if (Model.Meta != null)
                {
                    <div class="metadata me-1">@await DisplayAsync(Model.Meta)</div>
                }
            </div>
            @if (Model.Description != null)
            {
                <div>@await DisplayAsync(Model.Description)</div>
            }
        </div>
    </div>
    <div class="col-lg-auto col-12 d-flex justify-content-end align-items-center">
        <div class="actions">
            @if (Model.Actions != null)
            {
                @await DisplayAsync(Model.Actions)
            }
        </div>
    </div>
</div>
```

### Child Shape Templates (in Views/Items/ or Views/)

| Shape name in driver | Template file |
|-----|------|
| `YourModelFields_Edit` | `Views/YourModelFields.Edit.cshtml` |
| `YourModel_Fields_SummaryAdmin` | `Views/Items/YourModel.Fields.SummaryAdmin.cshtml` |
| `YourModel_Buttons_SummaryAdmin` | `Views/Items/YourModel.Buttons.SummaryAdmin.cshtml` |
| `YourModel_DefaultMeta_SummaryAdmin` | `Views/Items/YourModel.DefaultMeta.SummaryAdmin.cshtml` |
| `YourModel_Description_SummaryAdmin` | `Views/Items/YourModel.Description.SummaryAdmin.cshtml` |

### Shape Name to File Name Mapping Rules

- Underscores (`_`) in shape names map to dots (`.`) in file names: `YourModel_Edit` → `YourModel.Edit.cshtml`
- Shapes referenced with `View("ShapeName", model)` use the above dot mapping.
- Shapes referenced with `Initialize<TViewModel>("ShapeName", ...)` use the dot mapping: `YourModelFields_Edit` → `YourModelFields.Edit.cshtml`
- Child shapes placed in the `Items/` subfolder when they represent parts of a summary display: `Items/YourModel.Fields.SummaryAdmin.cshtml`

### Startup Registration for DisplayDriver<TModel>

```csharp
services.AddDisplayDriver<YourModel, YourModelDisplayDriver>();
services.AddScoped<ICatalogEntryHandler<YourModel>, YourModelHandler>();
```

# OrchardCore Patterns Reference

## Content Part

A content part is a reusable piece of content that can be attached to content types.

```csharp
using OrchardCore.ContentManagement;

namespace Contoso.YourModule.Models;

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
using Contoso.YourModule.Models;
using Contoso.YourModule.ViewModels;

namespace Contoso.YourModule.Drivers;

public sealed class YourPartDisplayDriver : ContentPartDisplayDriver<YourPart>
{
    public override IDisplayResult Display(YourPart part, BuildPartDisplayContext context)
    {
        // Typed-state overload with a static lambda: no closure allocation, and the shape
        // is only created when it is actually displayed.
        return Initialize<YourPartViewModel, YourPart>("YourPart", static (model, part) =>
        {
            model.Title = part.Title;
            model.Description = part.Description;
        }, part).Location("Detail", "Content:5");
    }

    public override IDisplayResult Edit(YourPart part, BuildPartEditorContext context)
    {
        return Initialize<YourPartViewModel, YourPart>("YourPart_Edit", static (model, part) =>
        {
            model.Title = part.Title;
            model.Description = part.Description;
            model.IsEnabled = part.IsEnabled;
        }, part);
    }

    public override async Task<IDisplayResult> UpdateAsync(YourPart part, UpdatePartEditorContext context)
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

`YourPartViewModel` must not be `sealed` (analyzer `OCSG001`). Async initializers return `ValueTask`:

```csharp
return Initialize<YourPartViewModel>("YourPart", async model =>
{
    model.Related = await _yourService.GetRelatedAsync(part.ContentItem.ContentItemId);
});
```

`Shape()` is obsolete because it allocates the shape eagerly; use `Factory()` when you build an `IShape` yourself:

```csharp
return Factory("YourPart_Summary", static (ctx, part) => ctx.ShapeFactory.CreateAsync("YourPart_Summary", Arguments.From(new { part.Title })), part);
```

## Content Field

Custom fields for content types.

```csharp
using OrchardCore.ContentManagement;

namespace Contoso.YourModule.Fields;

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

namespace Contoso.YourModule;

public sealed class Migrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<YourIndex>(table => table
            .Column<YourDocumentStatusEnum>("Status")
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

namespace Contoso.YourModule.Indexes;

public sealed class YourIndex : MapIndex
{
    public long DocumentId { get; set; }
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

## Permissions

Declare permissions in a static class (put it in a `Core`/`Abstractions` project when other modules must reference it) and expose them with an `IPermissionProvider`.

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace Contoso.YourModule;

public static class YourModulePermissions
{
    public static readonly Permission ManageYourFeature = new(
        "ManageYourFeature",
        LocalizationSource.Create("Manage your feature", typeof(YourModulePermissions)));

    public static readonly Permission ViewYourFeature = new(
        "ViewYourFeature",
        LocalizationSource.Create("View your feature", typeof(YourModulePermissions)),
        [ManageYourFeature]);
}

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        YourModulePermissions.ManageYourFeature,
        YourModulePermissions.ViewYourFeature,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Editor,
            Permissions = [YourModulePermissions.ViewYourFeature],
        },
    ];
}
```

Register with `services.AddPermissionProvider<Permissions>()`. `Permission.Description` is a `LocalizationSource`: translate it at display time with `IStringLocalizerFactory.Localize(permission.Description)`, read the raw text with `Description.Value`. Dynamic (templated) permissions keep using string descriptions.

## Admin Menu

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace Contoso.YourModule;

public sealed class AdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Tools"], tools => tools
                .Add(S["Your Item"], S["Your Item"].PrefixPosition(), item => item
                    .Id("yourModule")
                    .Action("Index", "Admin", "Contoso.YourModule")
                    .Permission(YourModulePermissions.ManageYourFeature)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
```

`AdminNavigationProvider` is a `NamedNavigationProvider` bound to the admin menu. Top-level positions available in `NavigationConstants` are `AdminMenuContentPosition`, `AdminMenuDesignPosition`, `AdminMenuSearchPosition` and `AdminMenuAccessControlPosition`; `Tools` and `Settings` are positioned by the Settings module. The legacy admin menu layout and its position constants (`AdminMenuConfigurationPosition`, `AdminMenuSecurityPosition`, ...) were removed in 4.0.

## Content Handler

```csharp
using OrchardCore.ContentManagement.Handlers;

namespace Contoso.YourModule.Handlers;

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

namespace Contoso.YourModule;

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
    services.AddPermissionProvider<Permissions>();
    
    // Navigation
    services.AddNavigationProvider<AdminMenu>();
    
    // Handlers
    services.AddScoped<IContentHandler, YourContentHandler>();
}
```

Part- and field-specific handlers (`ContentPartHandler<TPart>`, `ContentFieldHandler<TField>`) are chained on the builder: `services.AddContentPart<YourPart>().UseDisplayDriver<YourPartDisplayDriver>().AddHandler<YourPartHandler>()`. There is no `AddContentHandler` extension.

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
        return Initialize<YourModelViewModel, YourModel>("YourModelFields_Edit", static (m, model) =>
        {
            m.DisplayText = model.DisplayText;
        }, model).Location("Content:1");
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
```

### Admin List Layouts (4.0)

Admin lists now render through the `AdminList` shape with `List` and `Grid` layouts. `SummaryAdmin` rows and templates keep working in the `List` layout; the `Grid` layout renders `SummaryAdminGrid` shapes, one cell per column, so give row shapes a location for both display types (e.g. `Content:10` and `Title:10`). In the list view, pass a controller-built `AdminListContext<YourModel>` and declare the columns:

```cshtml
@addTagHelper *, OrchardCore.Admin.Abstractions

<admin-list name="YourModels" context="Model.List">
    <admin-list-list item-css-class="list-group-item" />
    <admin-list-grid>
        <admin-list-column type="Select" />
        <admin-list-column name="Title" position="20" title="@T["Name"]" />
        <admin-list-column type="Actions" title="@T["Actions"]" />
    </admin-list-grid>
</admin-list>
```

See `orchardcore-admin-lists` for `AdminListContext`, column providers and row actions.

## Temporary Files

```csharp
using OrchardCore.FileStorage;

public sealed class YourImportService
{
    private readonly ITempDirectoryProvider _tempDirectoryProvider;

    public YourImportService(ITempDirectoryProvider tempDirectoryProvider)
    {
        _tempDirectoryProvider = tempDirectoryProvider;
    }

    public async Task ImportAsync(Stream package, CancellationToken cancellationToken)
    {
        // Tenant-scoped, under OrchardCore:TempDirectory:Path. Never use Path.GetTempPath().
        var archivePath = _tempDirectoryProvider.GetTempFileName(".zip");
        var extractPath = _tempDirectoryProvider.CreateTempSubdirectory();

        try
        {
            await using (var file = File.Create(archivePath))
            {
                await package.CopyToAsync(file, cancellationToken);
            }

            ZipFile.ExtractToDirectory(archivePath, extractPath);
            // ... process the files
        }
        finally
        {
            File.Delete(archivePath);
            Directory.Delete(extractPath, recursive: true);
        }
    }
}
```

The provider only hands out paths; delete what you create.

## User Uploads

Run every user-uploaded file through `FileCreationService` (antivirus and other `IFileEventHandler`s) before writing it permanently. Writing to a file store does **not** do this for you: `IMediaFileStore.CreateFileFromStreamAsync(path, stream)` only runs the `IMediaCreatingEventHandler`s. For the media library, call the extension overload that runs the pipeline:

```csharp
// MediaFileStoreExtensions (OrchardCore.Media.Abstractions); throws FileStoreException when a handler rejects the file.
var path = await _mediaFileStore.CreateFileFromStreamAsync(
    _fileCreationService, mediaPath, stream, overwrite: false, file.Length, file.ContentType, cancellationToken);
```

For any other `IFileStore`, run the pipeline yourself:

```csharp
using OrchardCore.FileStorage;

public sealed class YourUploadService
{
    private readonly FileCreationService _fileCreationService;
    private readonly IFileStore _fileStore;

    public YourUploadService(FileCreationService fileCreationService, IFileStore fileStore)
    {
        _fileCreationService = fileCreationService;
        _fileStore = fileStore;
    }

    public async Task<string> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var uploadedStream = file.OpenReadStream();
        await using var result = await _fileCreationService.CreateAsync(
            new FileCreatingContext(file.FileName, file.Length, file.ContentType),
            uploadedStream,
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new FileStoreException(result.ErrorMessage ?? $"The file '{file.FileName}' was rejected.");
        }

        var path = await _fileStore.CreateFileFromStreamAsync(file.FileName, result.Stream);
        await _fileCreationService.CreatedAsync(await _fileStore.GetFileInfoAsync(path), cancellationToken);

        return path;
    }
}
```

See `orchardcore-file-upload-security` for the file event pipeline.

## Editor Scripts (No jQuery)

Admin pages no longer include jQuery or jQuery UI. Write part and field editor scripts as ES modules so they also run when the editor is injected by a Flow, Bag, or Widgets "Add" action:

```cshtml
<script at="Foot" type="module">
    const input = document.getElementById('@Html.IdFor(m => m.Title)');
    input?.addEventListener('input', () => {
        input.classList.toggle('is-invalid', input.value.trim().length === 0);
    });
</script>
```

Do not wrap the code in `DOMContentLoaded` (it has already fired for injected editors). If a front-end theme really needs jQuery, require it explicitly with `<script asp-name="jQuery" at="Foot"></script>`.

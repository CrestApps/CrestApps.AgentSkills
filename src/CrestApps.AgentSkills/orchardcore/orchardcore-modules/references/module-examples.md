# Module Examples

## Example 1: Basic Module with Content Part

### Manifest.cs

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "CrestApps.Testimonials",
    Author = "CrestApps",
    Website = "https://crestapps.com",
    Version = "1.0.0",
    Description = "Provides a testimonial content type and part.",
    Category = "Content"
)]

[assembly: Feature(
    Id = "CrestApps.Testimonials",
    Name = "Testimonials",
    Description = "Adds testimonial support with a dedicated content part.",
    Dependencies =
    [
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
    ],
    Category = "Content"
)]
```

### Startup.cs

```csharp
using CrestApps.Testimonials.Drivers;
using CrestApps.Testimonials.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.Testimonials;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<TestimonialPart>()
            .UseDisplayDriver<TestimonialPartDisplayDriver>();

        services.AddDataMigration<Migrations>();
    }
}
```

### Models/TestimonialPart.cs

```csharp
using OrchardCore.ContentManagement;

namespace CrestApps.Testimonials.Models;

public sealed class TestimonialPart : ContentPart
{
    public string AuthorName { get; set; }

    public string Quote { get; set; }

    public int Rating { get; set; }
}
```

### Drivers/TestimonialPartDisplayDriver.cs

```csharp
using CrestApps.Testimonials.Models;
using CrestApps.Testimonials.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.Testimonials.Drivers;

public sealed class TestimonialPartDisplayDriver : ContentPartDisplayDriver<TestimonialPart>
{
    public override IDisplayResult Display(TestimonialPart part, BuildPartDisplayContext context)
        => Initialize<TestimonialPartViewModel, TestimonialPart>(GetDisplayShapeType(context), static (model, part) =>
        {
            model.AuthorName = part.AuthorName;
            model.Quote = part.Quote;
            model.Rating = part.Rating;
        }, part).Location("Detail", "Content:5")
        .Location("Summary", "Content:5");

    public override IDisplayResult Edit(TestimonialPart part, BuildPartEditorContext context)
        => Initialize<TestimonialPartViewModel, TestimonialPart>(GetEditorShapeType(context), static (model, part) =>
        {
            model.AuthorName = part.AuthorName;
            model.Quote = part.Quote;
            model.Rating = part.Rating;
        }, part);

    public override async Task<IDisplayResult> UpdateAsync(TestimonialPart part, UpdatePartEditorContext context)
    {
        var model = new TestimonialPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.AuthorName = model.AuthorName?.Trim();
        part.Quote = model.Quote;
        part.Rating = Math.Clamp(model.Rating, 1, 5);

        return Edit(part, context);
    }
}
```

### ViewModels/TestimonialPartViewModel.cs

```csharp
namespace CrestApps.Testimonials.ViewModels;

// View models are not sealed: Orchard Core creates proxy-based shapes from them.
public class TestimonialPartViewModel
{
    public string AuthorName { get; set; }

    public string Quote { get; set; }

    public int Rating { get; set; }
}
```

## Example 2: Optional Integration Without a New Feature

Add a workflow activity only when Workflows is enabled, without asking users to enable another feature:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.Testimonials;

[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<TestimonialSubmittedEvent, TestimonialSubmittedEventDisplayDriver>();
    }
}
```

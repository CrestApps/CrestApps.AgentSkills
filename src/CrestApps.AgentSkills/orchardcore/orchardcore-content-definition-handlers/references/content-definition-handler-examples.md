# Content Definition Handler Examples

## Example 1: Mark an existing field on a part system-defined

Prevent users from removing a specific field (`InternalId`) from a specific part (`ProductPart`) by flagging the field during `ContentPartFieldBuilding`.

```csharp
using System.Text.Json.Nodes;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes.Events;

namespace MyModule;

public sealed class ProductFieldContentDefinitionHandler : IContentDefinitionHandler
{
    public void ContentTypeBuilding(ContentTypeBuildingContext context)
    {
    }

    public void ContentPartBuilding(ContentPartBuildingContext context)
    {
    }

    public void ContentTypePartBuilding(ContentTypePartBuildingContext context)
    {
    }

    public void ContentPartFieldBuilding(ContentPartFieldBuildingContext context)
    {
        if (context?.Record?.Settings is null ||
            !context.Record.Name.EqualsOrdinalIgnoreCase("InternalId"))
        {
            return;
        }

        var settings = context.Record.Settings[nameof(ContentSettings)]?.ToObject<ContentSettings>()
            ?? new ContentSettings();

        settings.IsSystemDefined = true;

        context.Record.Settings[nameof(ContentSettings)] = JObject.FromObject(settings);
    }
}
```

`context.Record.FieldName` is the field type (for example `TextField`); `context.Record.Name` is the field's name on the part. Guard on `Name` so only the intended field is protected.

Register it in `Startup`:

```csharp
services.AddScoped<IContentDefinitionHandler, ProductFieldContentDefinitionHandler>();
```

## Example 2: Mark a whole content type system-defined

Flag a content type so it cannot be deleted. Guard on the type name.

```csharp
using System.Text.Json.Nodes;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes.Events;

namespace MyModule;

public sealed class SettingsTypeContentDefinitionHandler : IContentDefinitionHandler
{
    public void ContentTypeBuilding(ContentTypeBuildingContext context)
    {
        if (context?.Record is null ||
            !context.Record.Name.EqualsOrdinalIgnoreCase("ApplicationSettings"))
        {
            return;
        }

        context.Record.Settings ??= new JsonObject();

        var settings = context.Record.Settings[nameof(ContentSettings)]?.ToObject<ContentSettings>()
            ?? new ContentSettings();

        settings.IsSystemDefined = true;

        context.Record.Settings[nameof(ContentSettings)] = JObject.FromObject(settings);
    }

    public void ContentPartBuilding(ContentPartBuildingContext context)
    {
    }

    public void ContentTypePartBuilding(ContentTypePartBuildingContext context)
    {
    }

    public void ContentPartFieldBuilding(ContentPartFieldBuildingContext context)
    {
    }
}
```

## Verifying the flag

Check whether any definition is system-defined:

```csharp
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;

// typeDefinition, partDefinition, typePartDefinition, or fieldDefinition
var isSystem = typeDefinition.GetSettings<ContentSettings>().IsSystemDefined;
```

Attempting to remove a system-defined definition through `IContentDefinitionService` throws:

```
System.InvalidOperationException: Unable to remove system-defined type.
```

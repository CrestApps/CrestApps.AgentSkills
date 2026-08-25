---
name: orchardcore-content-definition-handlers
description: Skill for intercepting Orchard Core content definitions as they are built with IContentDefinitionHandler, and for creating system-defined content types, parts, and fields that users cannot remove or modify through the admin UI or recipes. Covers the ContentTypeBuilding, ContentPartBuilding, ContentTypePartBuilding, and ContentPartFieldBuilding events, injecting a part into a type programmatically, and marking a definition system-defined via ContentSettings.IsSystemDefined. Use this skill when requests mention IContentDefinitionHandler, system-defined or system-type content types, parts, or fields, undeletable or non-removable parts, injecting a content part into every content type, ContentSettings.IsSystemDefined, ContentTypeBuildingContext, or the DashboardPartContentTypeDefinitionHandler pattern. Strong matches include OrchardCore.ContentTypes.Events.IContentDefinitionHandler, ContentSettings, IsSystemDefined, and ContentTypeDefinitionRecord.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Definition Handlers & System-Defined Definitions

## Intercept content definitions as they are built

You are an Orchard Core expert. `IContentDefinitionHandler` lets a module intercept and modify content type, part, and field definitions **while they are being built, before they are cached**. The primary use is injecting a part into a content type programmatically and marking types, parts, or fields as *system-defined* so users cannot remove or modify them through the admin UI or recipes. This complements — it does not replace — the `IContentDefinitionManager` migration APIs (see the `orchardcore-content-types` skill) used for ordinary, user-manageable definitions.

### Guidelines

- Implement `OrchardCore.ContentTypes.Events.IContentDefinitionHandler` and register it with `services.AddScoped<IContentDefinitionHandler, T>()` in the module's `Startup`.
- The interface has four events; each receives a `*BuildingContext` exposing the definition **record** being built and its name:
  - `ContentTypeBuilding(ContentTypeBuildingContext context)` — a content type is being built.
  - `ContentPartBuilding(ContentPartBuildingContext context)` — a content part definition is being built.
  - `ContentTypePartBuilding(ContentTypePartBuildingContext context)` — a part is being attached to a type.
  - `ContentPartFieldBuilding(ContentPartFieldBuildingContext context)` — a field is being built on a part.
- Handlers run on every build, so guard with the stereotype, type name, or part name before mutating. Leave unrelated definitions untouched, and implement the events you do not need as empty methods.
- Mark a type, part, type-part, or field system-defined by writing a `ContentSettings` with `IsSystemDefined = true` into the record's `Settings` under the `nameof(ContentSettings)` key.
- Because the handler sets this at build time, it is **not persisted** to the stored definition; the capability is re-applied every time the definition is built, so it cannot be lost or edited away.
- A system-defined definition throws `InvalidOperationException` if code attempts to remove it through `IContentDefinitionService`, and the admin type/part editors render the **Delete**/**Remove** buttons as disabled.
- Read the flag on any definition with `definition.GetSettings<ContentSettings>().IsSystemDefined`.
- Always seal handler classes.

## Inject a system-defined part into a content type

This handler attaches an `SeoPart` to every content type whose stereotype is `Page`, and marks the part system-defined so users cannot detach it. It mirrors the built-in `DashboardPartContentTypeDefinitionHandler`.

```csharp
using System.Text.Json.Nodes;
using OrchardCore.ContentManagement.Metadata.Records;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes.Events;

namespace MyModule;

public sealed class SeoPartContentTypeDefinitionHandler : IContentDefinitionHandler
{
    // Attach SeoPart to any type with the `Page` stereotype.
    public void ContentTypeBuilding(ContentTypeBuildingContext context)
    {
        if (context?.Record?.Settings is null ||
            !context.Record.Settings.TryGetPropertyValue(nameof(ContentTypeSettings), out var node))
        {
            return;
        }

        var settings = node.ToObject<ContentTypeSettings>();

        if (!string.Equals(settings.Stereotype, "Page", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Do not add it twice.
        if (context.Record.ContentTypePartDefinitionRecords
            .Any(x => x.Name.EqualsOrdinalIgnoreCase(nameof(SeoPart))))
        {
            return;
        }

        context.Record.ContentTypePartDefinitionRecords.Add(new ContentTypePartDefinitionRecord
        {
            Name = nameof(SeoPart),
            PartName = nameof(SeoPart),
            Settings = new JsonObject
            {
                [nameof(ContentSettings)] = JObject.FromObject(new ContentSettings
                {
                    IsSystemDefined = true,
                }),
            },
        });
    }

    // Mark the type-part system-defined so it cannot be removed from the type.
    public void ContentTypePartBuilding(ContentTypePartBuildingContext context)
    {
        if (context?.Record?.Settings is null ||
            !context.Record.PartName.EqualsOrdinalIgnoreCase(nameof(SeoPart)))
        {
            return;
        }

        var settings = context.Record.Settings[nameof(ContentSettings)]?.ToObject<ContentSettings>()
            ?? new ContentSettings();

        settings.IsSystemDefined = true;

        context.Record.Settings[nameof(ContentSettings)] = JObject.FromObject(settings);
    }

    // Create the part definition on the fly if the module does not define it via migration.
    public void ContentPartBuilding(ContentPartBuildingContext context)
    {
        if (context.Record is not null || context.PartName != nameof(SeoPart))
        {
            return;
        }

        context.Record = new ContentPartDefinitionRecord
        {
            Name = context.PartName,
            Settings = new JsonObject
            {
                [nameof(ContentPartSettings)] = JObject.FromObject(new ContentPartSettings
                {
                    Attachable = false,
                    Reusable = false,
                }),
                [nameof(ContentSettings)] = JObject.FromObject(new ContentSettings
                {
                    IsSystemDefined = true,
                }),
            },
        };
    }

    public void ContentPartFieldBuilding(ContentPartFieldBuildingContext context)
    {
    }
}
```

Register the handler in `Startup`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionHandler, SeoPartContentTypeDefinitionHandler>();
    }
}
```

Setting `Attachable = false` on the injected part keeps it out of the "add part" list, so the part exists only where the handler places it.

## Read the system-defined flag

`ContentSettings` lives in `OrchardCore.ContentManagement.Metadata.Settings`; the `GetSettings<T>()` accessor is on the definition models in `OrchardCore.ContentManagement.Metadata.Models`.

```csharp
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;

var isSystem = typeDefinition.GetSettings<ContentSettings>().IsSystemDefined;
```

The same call works on `ContentTypeDefinition`, `ContentPartDefinition`, `ContentTypePartDefinition`, and `ContentPartFieldDefinition`.

## What "system-defined" enforces

- `IContentDefinitionService.RemoveType`, `RemovePart`, `RemovePartFromType`, and `RemoveFieldFromPart` throw `InvalidOperationException` for a system-defined definition — this blocks both admin actions and recipe-driven removals that flow through the service.
- The content type and part editors disable the **Delete**/**Remove** controls and show a tooltip explaining the definition is integral to the system.
- The flag does not make a definition read-only for code; a migration or handler can still alter it. It prevents user-initiated removal.

## Troubleshooting

- If the handler never runs, confirm it is registered with `services.AddScoped<IContentDefinitionHandler, T>()` and that the guarding stereotype/name check actually matches.
- If a user can still delete the part or type, verify the `ContentSettings` block is written under the `nameof(ContentSettings)` key with `IsSystemDefined = true` on the correct record (the type, the type-part, the part, or the field).
- If the injected part appears in the "add part" picker, set `ContentPartSettings.Attachable = false` when building the part definition.
- If `JObject.FromObject` does not resolve, add `using System.Text.Json.Nodes;` — `JObject` is the Orchard Core helper in that namespace, and it returns a `JsonObject`.
- For ordinary, user-editable types and parts, use `IContentDefinitionManager` migrations instead; reach for a handler only when the definition must be injected or protected.

See [references/content-definition-handler-examples.md](references/content-definition-handler-examples.md) for a system-defined field example.

---
name: orchardcore-data-migrations
description: Skill for creating data migrations in Orchard Core. Covers content type migrations, YesSql index table creation, adding SQL indexes to existing tables, schema alterations, column rules and SQLite or MySQL provider quirks, patching existing content item JSON in batches, replacing a part on every content type, recipe migrations with IRecipeMigrator, data seeding, uninstall, AddDataMigration registration, and the migration version chain with skipped versions. Use this skill when requests mention Orchard Core Data Migrations, Migration with Custom Content Part and Fields, Migration with YesSql Index Table, Incremental Migration (UpdateFrom), or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.ContentManagement.Metadata, OrchardCore.Data.Migration, DataMigration, IDataMigration, AddDataMigration, IRecipeMigrator, IContentDefinitionManager, SchemaBuilder, CreateIndex, WithPart.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Data Migrations - Prompt Templates

## Create Data Migrations

You are an Orchard Core expert. Generate data migration code for Orchard Core modules.

### Guidelines

- Data migrations inherit from `DataMigration`.
- Place migrations in a `Migrations` folder in the feature project.
- Name migration classes clearly and keep them `internal sealed`.
- Migrations define content types, parts, fields, and database indexes.
- Use `CreateAsync()` for initial migration, `UpdateFrom1Async()`, `UpdateFrom2Async()` for incremental updates.
- `Create`, `CreateAsync`, `UpdateFromX`, `UpdateFromXAsync`, `Uninstall`, and `UninstallAsync` can be marked `static` when they do not use injected services, inherited instance members, or other instance state.
- Keep migration methods instance methods when they rely on constructor-injected services like `IContentDefinitionManager` or inherited members like `SchemaBuilder`.
- Register migrations in `Startup.cs` using `services.AddDataMigration<Migrations>()` (namespace `OrchardCore.Data.Migration`).
- Migrations run automatically when the tenant starts and strictly follow the version chain: `UpdateFrom4Async()` runs only when the last method returned `4`. Never renumber shipped migrations.
- Use `IRecipeMigrator.ExecuteAsync("file.recipe.json", this)` to run a recipe file from the module's `Migrations` folder.
- `IContentDefinitionManager` is used to define content types and parts.
- `SchemaBuilder` is used to create and alter YesSql index tables.
- Use stable column names in `CreateMapIndexTableAsync()` and `AlterIndexTableAsync()`. A string literal is clearest for a persisted schema; use `nameof(...)` only when the member name is intentionally the persisted column name.
- `MapIndex` tables already get a `DocumentId` column from YesSql. Never declare or alter that column manually in the migration.
- If an index needs access to the YesSql document id, expose `public long DocumentId { get; set; }` on the `MapIndex` type and let YesSql populate it.
- Prefer keeping reusable content-part models in the corresponding `*.Core` project when they are used outside the feature wiring layer.

Use the current async migration and clock APIs. `IDataMigrationManager.UpdateAsync`
accepts the feature names to update, and `ILocalClock.GetLocalNowAsync` replaces the
removed local-clock async member. Do not add `IAsyncOptions` or
`IAsyncConfigureOptions<TOptions>`; configure options with the synchronous options APIs.

### How Migrations Run

`AutomaticDataMigrations` runs `IDataMigrationManager.UpdateAllFeaturesAsync()` while each initialized tenant activates, which also happens after a feature is enabled because that reloads the shell. For each migration class it stores the last returned number in the tenant's `DataMigrationRecord`, keyed by the class full name.

| Rule | Why it matters |
|------|----------------|
| `Create`/`CreateAsync` runs only when no version is stored for the class. | Existing tenants already passed it. |
| After `Create` (or any step) the manager keeps calling `UpdateFrom{returned}` while a matching method exists. | Returning `1` from `CreateAsync` runs every `UpdateFromX` on a fresh install; returning the latest number skips them. |
| `UpdateFromXAsync` runs only when the stored version is `X`. | Sequential upgrade path. |
| Never renumber a shipped method, and never rename or move the namespace of a shipped migration class. | Tenants store the old number under the old class name; a rename re-runs `CreateAsync`. |
| Migrations of the features a feature depends on run first. | You can rely on dependency tables and parts existing. |
| An exception logs an error, cancels the session and keeps the previous version. | The step is retried on the next tenant start; make steps safe to re-run. |
| Supported signatures are `int Create()`, `Task<int> CreateAsync()`, `int UpdateFromX()`, `Task<int> UpdateFromXAsync()`, `void Uninstall()`, `Task UninstallAsync()`. | Static or instance, sync or async all work; prefer async in new code. |

`SchemaBuilder` is assigned by the manager on the `DataMigration` base class before each run. Never inject `ISchemaBuilder` through the constructor.

When a step changes the schema in a release, append a new `UpdateFromXAsync`, never edit a shipped one, and bump the number `CreateAsync` returns so new tenants get the final schema directly. A comment such as `// This code can be removed in a later version.` marks old upgrade steps, but keep them until you drop that upgrade path.

### Basic Migration with Content Type

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
            .DisplayedAs("{{DisplayName}}")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "{{ ContentItem | display_text | slugify }}"
                })
            )
            .WithPart("HtmlBodyPart", part => part
                .WithPosition("2")
                .WithEditor("Wysiwyg")
            )
            .WithPart("{{ContentType}}Part", part => part.WithPosition("3"))
        );

        return 1;
    }
}
```

### Folder and Class Conventions

```text
src/Modules/{{FeatureName}}/
  Migrations/
    {{FeatureName}}Migrations.cs
```

```csharp
using OrchardCore.Data.Migration;

namespace CrestApps.Sports.Teams.Migrations;

internal sealed class TeamMigrations : DataMigration
{
    public static Task<int> CreateAsync()
    {
        // migration steps
        return Task.FromResult(1);
    }
}
```

Use `static` for migration methods only when the body does not touch injected services, `SchemaBuilder`, or any other instance members.

### Migration with Custom Content Part and Fields

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        // Define the custom part with fields
        await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
            .WithField("{{FieldName}}", field => field
                .OfType("TextField")
                .WithDisplayName("{{FieldDisplayName}}")
                .WithPosition("0")
                .WithSettings(new TextFieldSettings
                {
                    Required = true,
                    Hint = "{{FieldHint}}"
                })
            )
            .WithField("Description", field => field
                .OfType("TextField")
                .WithDisplayName("Description")
                .WithPosition("1")
                .WithEditor("TextArea")
            )
            .WithField("Image", field => field
                .OfType("MediaField")
                .WithDisplayName("Image")
                .WithPosition("2")
            )
        );

        // Define the content type with the custom part
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
            .DisplayedAs("{{DisplayName}}")
            .Creatable()
            .Listable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("{{PartName}}", part => part.WithPosition("1"))
        );

        return 1;
    }
}
```

### Migration with YesSql Index Table

```csharp
using OrchardCore.Data.Migration;
using YesSql.Sql;

public sealed class Migrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<{{IndexName}}>(table => table
            .Column<string>("ContentItemId", col => col.WithLength(26))
            .Column<string>("{{PropertyName}}", col => col.WithLength(256))
            .Column<bool>("Published")
            .Column<DateTime>("CreatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<{{IndexName}}>(table => table
            .CreateIndex(
                "IDX_{{IndexName}}_{{PropertyName}}",
                "{{PropertyName}}",
                "Published"
            )
        );

        return 1;
    }
}
```

### Adding a SQL Index to an Existing Table

Create the index in a shared helper, call it from `CreateAsync()` for new tenants and from a new `UpdateFromXAsync()` for existing ones, and bump the returned version in both. Orchard Core 4.0 added `IDX_ContentItemIndex_ContentItemId` to `ContentItemIndex` exactly this way (version 6 to 7). Lead the index with the column you filter on; creating an index on a large table takes time during the upgrade.

```csharp
public sealed class Migrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<{{IndexName}}>(table => table
            .Column<string>("ContentItemId", col => col.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest")
        );

        await CreateContentItemIdIndexAsync();

        // Shortcut: new tenants skip UpdateFrom1Async.
        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await CreateContentItemIdIndexAsync();

        return 2;
    }

    private Task CreateContentItemIdIndexAsync()
        => SchemaBuilder.AlterIndexTableAsync<{{IndexName}}>(table => table
            .CreateIndex("IDX_{{IndexName}}_ContentItemId",
                "ContentItemId",
                "DocumentId",
                "Published",
                "Latest"));
}
```

`DocumentId` can be listed in a `CreateIndex` call even though it is never declared as a column.

### Incremental Migration (UpdateFrom)

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        // Initial migration
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
            .DisplayedAs("{{DisplayName}}")
            .Creatable()
            .Listable()
            .WithPart("TitlePart")
        );

        return 1;
    }

    public async Task<int> UpdateFrom1Async()
    {
        // Add a new field in version 2
        await _contentDefinitionManager.AlterPartDefinitionAsync("{{ContentType}}", part => part
            .WithField("Category", field => field
                .OfType("TextField")
                .WithDisplayName("Category")
                .WithPosition("2")
            )
        );

        return 2;
    }

    public async Task<int> UpdateFrom2Async()
    {
        // Add an index table in version 3
        await SchemaBuilder.CreateMapIndexTableAsync<{{ContentType}}Index>(table => table
            .Column<string>("ContentItemId", col => col.WithLength(26))
            .Column<string>("Category", col => col.WithLength(256))
            .Column<bool>("Published")
        );

        return 3;
    }
}
```

### Registering Migrations and Indexes

```csharp
using OrchardCore.Data.Migration;
using YesSql.Indexes;

public enum {{IndexName}}Status
{
    One,
    Two,
}

public sealed class {{IndexName}} : MapIndex
{
    public long DocumentId { get; set; }

    public string ContentItemId { get; set; }

    public {{IndexName}}Status Status { get; set; }
}

await SchemaBuilder.CreateMapIndexTableAsync<{{IndexName}}>(table => table
    .Column<string>("ContentItemId", col => col.WithLength(26))
    .Column<{{IndexName}}Status>("Status")
);

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();
        services.AddIndexProvider<{{IndexName}}Provider>();
    }
}
```

### Uninstall Migration

`Uninstall`/`UninstallAsync` runs only when code calls `IDataMigrationManager.Uninstall(featureId)`. Disabling a feature does not call it, so never rely on it for cleanup that must happen on disable.

```csharp
public sealed class Migrations : DataMigration
{
    public async Task UninstallAsync()
    {
        await SchemaBuilder.DropMapIndexTableAsync<{{IndexName}}>();
        // Or drop a table that has no index type.
        await SchemaBuilder.DropTableAsync("{{TableName}}");
    }
}
```

### Running a Recipe from a Migration

`IRecipeMigrator.ExecuteAsync(fileName, this)` resolves the file from the `Migrations` folder of the module that declares the migration class, runs it in the current scope, and returns the execution id (or `null` when the file is not found). Use it to seed templates, queries, layers, roles or sample content with ordinary recipe steps (see `orchardcore-recipes`).

```csharp
using OrchardCore.Data.Migration;
using OrchardCore.Recipes.Services;

public sealed class Migrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;

    public Migrations(IRecipeMigrator recipeMigrator)
    {
        _recipeMigrator = recipeMigrator;
    }

    public async Task<int> CreateAsync()
    {
        // Loads {{ModuleName}}/Migrations/init.recipe.json.
        await _recipeMigrator.ExecuteAsync("init.recipe.json", this);

        return 1;
    }
}
```

### Replacing a Part on Every Content Type

Use `LoadTypeDefinitionsAsync()` (a fresh, writable copy) inside migrations, not `ListTypeDefinitionsAsync()` (the cached, read-only copy).

```csharp
public async Task<int> UpdateFrom3Async()
{
    foreach (var contentType in await _contentDefinitionManager.LoadTypeDefinitionsAsync())
    {
        if (contentType.Parts.Any(x => x.PartDefinition.Name == "{{OldPart}}"))
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(contentType.Name, type => type
                .RemovePart("{{OldPart}}")
                .WithPart("{{NewPart}}"));
        }
    }

    await _contentDefinitionManager.DeletePartDefinitionAsync("{{OldPart}}");

    return 4;
}
```

To change part settings on every type, call `.WithPart("{{PartName}}", part => part.MergeSettings<{{PartName}}Settings>(settings => { ... }))` inside the same loop.

### Patching Existing Content Items

Data patches belong in an `UpdateFromXAsync`, never in `CreateAsync` (a new tenant has no items yet). Page through every version by `DocumentId`, mutate the `System.Text.Json` content, save only what changed, and flush each page.

```csharp
using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using YesSql;

public sealed class Migrations : DataMigration
{
    private readonly ISession _session;

    public Migrations(ISession session)
    {
        _session = session;
    }

    public async Task<int> UpdateFrom1Async()
    {
        var lastDocumentId = 0L;

        while (true)
        {
            var versions = await _session
                .Query<ContentItem, ContentItemIndex>(x => x.ContentType == "{{ContentType}}" && x.DocumentId > lastDocumentId)
                .OrderBy(x => x.DocumentId)
                .Take(50)
                .ListAsync();

            if (!versions.Any())
            {
                break;
            }

            foreach (var version in versions)
            {
                var content = (JsonObject)version.Content;

                if (content["{{PartName}}"] is JsonObject part && part["OldName"] is JsonNode value)
                {
                    part.Remove("OldName");
                    part["NewName"] = value;

                    await _session.SaveAsync(version);
                }

                lastDocumentId = version.Id;
            }

            await _session.FlushAsync();
        }

        return 2;
    }
}
```

- Query `ContentItemIndex`, iterate `ContentItem`, and cast `Content` to `JsonObject`; `ContentItem.Id` is the YesSql document id used as the cursor.
- Without a `Latest`/`Published` filter the loop patches drafts and history too, which is usually what you want.
- Content can be nested (Flow, Bag and widget parts), so recurse when the data can live in child items.

### Skipping Versions

Return a number higher than `X + 1` when a later step makes an intermediate one obsolete. The manager then looks for the returned number and never calls the skipped method.

```csharp
public async Task<int> UpdateFrom3Async()
{
    // ...
    return 5; // UpdateFrom4Async is no longer needed.
}
```

### Column Rules and Provider Quirks

| Need | Code |
|------|------|
| Bounded string | `.Column<string>("Name", c => c.WithLength(255))` |
| Unbounded text | `.Column<string>("Body", c => c.Nullable().Unlimited())` |
| Nullable value | `.Column<bool>("Latest", c => c.Nullable())` |
| Other types | `int`, `long`, `decimal`, `DateTime`, `TimeSpan`, `bool`, enums |

- Always give string columns `.WithLength(n)` or `.Unlimited()`; an unlengthed string column behaves differently per provider and can break the migration. Use 26 for `ContentItemId`/`ContentItemVersionId` and the `ContentItemIndex.Max*Size` constants (255) for content type, owner, author and display text.
- Test on every provider you target (SQLite, SQL Server, MySQL, PostgreSQL).
- SQLite cannot drop columns through `SchemaBuilder`. Wrap a drop (and the re-add that follows) in `try`/`catch` and log a warning; do the same when dropping an index that may not exist.
- MySQL caps an index key at 768 characters (3072 bytes). Prefix long string columns in `CreateIndex`, for example `"Text(764)"`, and keep the total under the limit (`DocumentId (2) + Text (764) + Published and Latest (1) = 767`).

```csharp
try
{
    await SchemaBuilder.AlterIndexTableAsync<{{IndexName}}>(table => table
        .DropColumn("Time"));

    await SchemaBuilder.AlterIndexTableAsync<{{IndexName}}>(table => table
        .AddColumn<TimeSpan>("Time", column => column.Nullable()));
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to alter the 'Time' column. This is expected on SQLite.");
}
```

### Testing a Migration

1. Fresh install. Set up a clean tenant; only `CreateAsync` runs and the schema lands at the latest version.
2. Upgrade. Start from a database at the previous version and confirm each `UpdateFromXAsync` runs in order (the log shows `Applying migration for ... from version X`).
3. Re-run. Restart the tenant and confirm nothing runs twice.

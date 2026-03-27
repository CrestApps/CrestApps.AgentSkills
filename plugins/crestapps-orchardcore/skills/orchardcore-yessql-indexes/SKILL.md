---
name: orchardcore-yessql-indexes
description: Skill for creating custom YesSql indexes in Orchard Core. Covers MapIndex models, IndexProvider patterns, multi-row mappings for one-to-many relationships, migration table creation, and querying with ISession.QueryIndex(). Use this skill when requests mention Orchard Core YesSql Indexes, Create a Custom YesSql Index, Recommended Placement, Simple Lookup Index, One-to-Many Index with Multiple Rows Per Content Item, Migration Example, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.ContentManagement, CrestApps.Sports.Core.Indexes, CrestApps.Sports.Calendar.Indexes, OrchardCore.Data.Migration, CrestApps.Sports.Calendar.Migrations, OrchardCore.Modules. It also helps with yessql index examples, One-to-Many Index with Multiple Rows Per Content Item, Migration Example, Startup Registration, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core YesSql Indexes - Prompt Templates

## Create a Custom YesSql Index

You are an Orchard Core expert. Generate code for custom YesSql indexes that follows Orchard Core conventions and CrestApps project structure.

### Guidelines

- Keep index models and index providers public sealed.
- Prefer placing reusable index models and index providers in the corresponding *.Core project when they are consumed by shared services.
- Use MapIndex for per-document projections.
- Use IndexProvider<ContentItem> for Orchard Core content-item indexing.
- Register index providers in Startup.cs with services.AddIndexProvider<TProvider>().
- Create index tables in migrations with SchemaBuilder.CreateMapIndexTableAsync<TIndex>().
- Add explicit SQL indexes for the lookup fields you query most often.
- Orchard Core ids such as `ContentItemId` and `ItemId` are typically 26 characters long; define indexed string id columns with `.WithLength(26)`.
- When new Orchard ids are needed, prefer `IdGenerator.GenerateId()` instead of GUID strings or custom random values.
- Query indexes with ISession.QueryIndex<TIndex>() when you only need projected values, rather than hydrating full ContentItem documents.
- For one-to-many relationships, return multiple rows from .Map(...) by projecting with Select(...).
- Keep migrations in a Migrations folder and keep migration classes internal sealed.
- Keep YesSql query predicates limited to expressions the provider can translate: comparisons, null checks, boolean `&&` / `||`, ordering, paging, and simple projections.
- Do **not** use conditional operators (`?:`), `if`-style branching inside expression lambdas, or other unsupported runtime logic in `ISession.Query(...)` / `QueryIndex(...)` predicates.
- When a query has different branches for null and non-null values, express them as separate supported predicates (or separate queries) and combine the results in memory.

### Recommended Placement

`	ext
src/Core/{{FeatureName}}.Core/
  Indexes/
    {{LookupName}}Index.cs
src/Modules/{{FeatureName}}/
  Migrations/
    {{FeatureName}}Migrations.cs
  Startup.cs
`

### Simple Lookup Index

`csharp
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.Sports.Core.Indexes;

public sealed class ClubTeamsIndex : MapIndex
{
    public string ClubContentItemId { get; set; }

    public string TeamContentItemId { get; set; }

    public bool Published { get; set; }
}

public sealed class ClubTeamsIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<ClubTeamsIndex>()
            .Map(contentItem =>
            {
                if (contentItem.ContentType != ContentTypes.Team)
                {
                    return null;
                }

                var clubContentItemId = contentItem.GetContentPickerValue("TeamPart", "Club");

                if (string.IsNullOrWhiteSpace(clubContentItemId))
                {
                    return null;
                }

                return new ClubTeamsIndex
                {
                    ClubContentItemId = clubContentItemId,
                    TeamContentItemId = contentItem.ContentItemId,
                    Published = contentItem.Published,
                };
            });
    }
}
`

### One-to-Many Index with Multiple Rows Per Content Item

`csharp
using OrchardCore.ContentManagement;
using YesSql.Indexes;

public sealed class DrillsIndex : MapIndex
{
    public string DrillContentItemId { get; set; }

    public string SkillContentItemId { get; set; }

    public bool Published { get; set; }
}

public sealed class DrillsIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<DrillsIndex>()
            .Map(contentItem =>
            {
                if (contentItem.ContentType != ContentTypes.Drill)
                {
                    return null;
                }

                var skillIds = contentItem
                    .GetContentPickerValues("DrillPart", "Skills")
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (skillIds.Length == 0)
                {
                    return null;
                }

                return skillIds.Select(skillId => new DrillsIndex
                {
                    DrillContentItemId = contentItem.ContentItemId,
                    SkillContentItemId = skillId,
                    Published = contentItem.Published,
                });
            });
    }
}
`

### Migration Example

`csharp
using CrestApps.Sports.Calendar.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.Sports.Calendar.Migrations;

internal sealed class SportEventMigrations : DataMigration
{
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SportEventPartIndex>(table => table
            .Column<string>(nameof(SportEventPartIndex.ContentItemId), column => column.WithLength(26))
            .Column<bool>(nameof(SportEventPartIndex.Published))
            .Column<DateTime>(nameof(SportEventPartIndex.EventDateUtc))
            .Column<DateTime?>(nameof(SportEventPartIndex.EndDateUtc))
        );

        await SchemaBuilder.AlterIndexTableAsync<SportEventPartIndex>(table => table
            .CreateIndex(
                "IDX_SportEventPartIndex_EventDateUtc",
                nameof(SportEventPartIndex.Published),
                nameof(SportEventPartIndex.EventDateUtc))
            .CreateIndex(
                "IDX_SportEventPartIndex_EndDateUtc",
                nameof(SportEventPartIndex.Published),
                nameof(SportEventPartIndex.EndDateUtc))
        );

        return 2;
    }
}
`

### Startup Registration

`csharp
using CrestApps.Sports.Core.Indexes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using YesSql.Indexes;

namespace CrestApps.Sports.Drills;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProvider<DrillsIndexProvider>();
    }
}
`

### Querying with QueryIndex

`csharp
using CrestApps.Sports.Core.Indexes;
using YesSql;

public sealed class SportsContentService
{
    private readonly ISession _session;

    public SportsContentService(ISession session)
    {
        _session = session;
    }

    public async Task<IEnumerable<string>> GetDrillIdsBySkillAsync(string skillContentItemId)
    {
        var rows = await _session
            .QueryIndex<DrillsIndex>(x => x.Published && x.SkillContentItemId == skillContentItemId)
            .ListAsync();

        return rows.Select(x => x.DrillContentItemId);
    }
}
`

### Best Practices

- Index only the fields required for frequent lookups.
- Prefer exact-match columns over scanning JSON in services.
- Normalize values in the index when queries should be case-insensitive.
- Return multiple MapIndex rows for picker collections instead of storing delimited strings when you need exact matching.
- Keep lookup logic in shared services small and index-driven.
- If a range query needs different handling for `null` and non-null columns, prefer two YesSql queries with supported predicates over a single predicate that uses a ternary.
- Keep Orchard id columns at length 26 and reuse Orchard-style ids generated by `IdGenerator.GenerateId()` when new identifiers are required.

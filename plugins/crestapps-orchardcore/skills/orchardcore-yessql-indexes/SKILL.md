---
name: orchardcore-yessql-indexes
description: Skill for creating custom YesSql indexes in Orchard Core. Covers MapIndex models, IndexProvider patterns, multi-row mappings for one-to-many relationships, migration table creation, and querying with ISession.QueryIndex().
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
- Query indexes with ISession.QueryIndex<TIndex>() when you only need projected values, rather than hydrating full ContentItem documents.
- For one-to-many relationships, return multiple rows from .Map(...) by projecting with Select(...).
- Keep migrations in a Migrations folder and keep migration classes internal sealed.

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
using CrestApps.Sports.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.Sports.Drills.Migrations;

internal sealed class DrillMigrations : DataMigration
{
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<DrillsIndex>(table => table
            .Column<string>(nameof(DrillsIndex.DrillContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(DrillsIndex.SkillContentItemId), column => column.WithLength(26))
            .Column<bool>(nameof(DrillsIndex.Published))
        );

        await SchemaBuilder.AlterIndexTableAsync<DrillsIndex>(table => table
            .CreateIndex("IDX_DrillsIndex_SkillContentItemId", nameof(DrillsIndex.SkillContentItemId), nameof(DrillsIndex.Published))
        );

        return 4;
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
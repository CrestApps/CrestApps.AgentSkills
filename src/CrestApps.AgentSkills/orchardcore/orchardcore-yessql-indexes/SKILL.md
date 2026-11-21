---
name: orchardcore-yessql-indexes
description: Skill for creating custom YesSql indexes in Orchard Core. Covers MapIndex models, IndexProvider patterns, YesSql 6.3 index declarations (ExclusiveToDocumentType, SingleRecordPerDocument), SkipDeduplication and NoIndexOptimizations queries, multi-row mappings for one-to-many relationships, migration table creation, non-blocking index creation, TextHelper.Truncate for index columns, and querying with ISession.QueryIndex(). Use this skill when requests mention Orchard Core YesSql indexes, creating a custom index, one-to-many index rows, index migrations, index registration, slow or duplicated index queries, or closely related Orchard Core implementation or troubleshooting work. Strong matches include ExclusiveToDocumentType, SingleRecordPerDocument, SkipDeduplication, NoDuplicates, NoIndexOptimizations, MapIndex, IndexProvider, DescribeContext, AddIndexProvider, SchemaBuilder, CreateMapIndexTableAsync, AlterIndexTableAsync, ISession, QueryIndex, and OrchardCore.Data.Migration.
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
- Declare what a map index holds on `context.For<TIndex>()`, and only when it is true: `.ExclusiveToDocumentType()` when only documents of the provider's type (for example `ContentItem`) write records to the index, and `.SingleRecordPerDocument()` when the map returns at most one record per document. A content item index usually is exclusive; it is single-record only when it never returns several rows for one content item version (not when it returns one row per tag, term or picked item). See [Declaring What an Index Holds](#declaring-what-an-index-holds-yessql-63).
- `IQuery<T>.NoDuplicates()` is obsolete in YesSql 6.3; use `SkipDeduplication()`.
- Register index providers in Startup.cs with services.AddIndexProvider<TProvider>().
- Create index tables in migrations with SchemaBuilder.CreateMapIndexTableAsync<TIndex>().
- Use literal column names such as `"ContentItemId"` and `"Published"` when creating or altering YesSql index tables; do not use `nameof(...)` inside `CreateMapIndexTableAsync()` or `AlterIndexTableAsync()`.
- `MapIndex` tables already include a `DocumentId` column automatically. Never add or alter `DocumentId` manually in `CreateMapIndexTableAsync()` or `AlterIndexTableAsync()`.
- If you need to read the YesSql document id from the index, add `public long DocumentId { get; set; }` to the index model and let YesSql populate it automatically.
- Add explicit SQL indexes for the lookup fields you query most often, and lead each SQL index with the column you filter on. Orchard Core 4.0 replaced most `ContentItemIndex` indexes, which led with `DocumentId`, with indexes led by the filtered columns (`ContentItemId`, `ContentType`, `Owner`, `ContentItemVersionId`) followed by `DocumentId`, `Published` and `Latest`: a filter can only seek on an index that starts with its column. Joins on `DocumentId` already seek on the `IDX_FK_*` index YesSql creates.
- Orchard Core ids such as `ContentItemId` and `ItemId` are typically 26 characters long; define indexed string id columns with `.WithLength(26)`.
- When a mapped string must fit a column length, shorten it with `TextHelper.Truncate(value, maxLength)` (namespace `OrchardCore`, package `OrchardCore.Abstractions`), as the built-in field and content indexes do since 4.0: it keeps whole Unicode text elements (emoji, surrogate pairs, combining marks) instead of splitting them like `value[..maxLength]`.
- Enum properties can be stored directly in `MapIndex` tables with `.Column<YourEnum>("Status")`.
- Query indexes with ISession.QueryIndex<TIndex>() when you only need projected values, rather than hydrating full ContentItem documents.
- For one-to-many relationships, return multiple rows from .Map(...) by projecting with Select(...).
- Keep migrations in a Migrations folder and keep migration classes internal sealed.
- Keep YesSql query predicates limited to expressions the provider can translate: comparisons, null checks, boolean `&&` / `||`, ordering, paging, and simple projections.
- Do **not** use conditional operators (`?:`), `if`-style branching inside expression lambdas, or other unsupported runtime logic in `ISession.Query(...)` / `QueryIndex(...)` predicates.
- When a query has different branches for null and non-null values, express them as separate supported predicates (or separate queries) and combine the results in memory.

### Recommended Placement

```text
src/Core/{{FeatureName}}.Core/
  Indexes/
    {{LookupName}}Index.cs
src/Modules/{{FeatureName}}/
  Migrations/
    {{FeatureName}}Migrations.cs
  Startup.cs
```

### Simple Lookup Index

```csharp
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
        // Only content items write records, and a team version maps at most one record.
        context.For<ClubTeamsIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
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
```

### One-to-Many Index with Multiple Rows Per Content Item

```csharp
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
        // Exclusive to content items, but not single-record: one row per selected skill.
        context.For<DrillsIndex>()
            .ExclusiveToDocumentType()
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
```

### Declaring What an Index Holds (YesSql 6.3)

A query joining a map index groups its matches by document (to return each document once) and counts distinct documents, so a page reads every match before it can be sorted. Declarations let YesSql skip that work:

| Declaration | Meaning | Effect |
| --- | --- | --- |
| `.ExclusiveToDocumentType()` | Only documents of the provider's type have records in the index. | A count reads the index tables without joining the `Document` table. |
| `.SingleRecordPerDocument()` | The map returns at most one record per document; saving throws if it returns more. | A query joining only single-record indexes isn't grouped, so a page stops as soon as it is full when a database index serves its order, and its count uses `count(*)`. |

```csharp
context.For<ProductIndex>()
    .ExclusiveToDocumentType()
    .SingleRecordPerDocument()
    .When(contentItem => contentItem.Has<ProductPart>())
    .Map(contentItem => new ProductIndex
    {
        ContentItemId = contentItem.ContentItemId,
        Price = contentItem.As<ProductPart>().Price,
    });
```

- YesSql can't verify the rows already in the table. Rows written before the map became single-record, by a provider that is no longer registered, or left by deleted documents on SQLite (no foreign keys) break the declarations: documents are returned or counted several times. Declare them on a new index, or rebuild the existing rows first.
- A query is still grouped when it joins an index that doesn't declare `SingleRecordPerDocument()`, or when several providers produce the same index for one document type. The `Document` table is still joined when a provider of another document type produces the index, or a filter is raw SQL.
- `query.NoIndexOptimizations()` runs a query as if its indexes declared nothing. Use it when the declarations make a query slower, for example a text search where the database walks the index of the sort order looking for rare matches (the content items admin list search and the content picker searches do this).
- `query.SkipDeduplication()` (replaces the obsolete `NoDuplicates()`) skips the grouping for a query that joins a multi-record index but matches only one of its records per document. Nothing verifies it, and the count then uses `count(*)`, so a document matched several times is counted several times.
- On MySQL, an index that starts with `DocumentId` can make an exclusive count slower: lead SQL indexes with the filtered columns and end them with `DocumentId`.

Built-in indexes declaring both: `ContentItemIndex`, `ContainedPartIndex`, `LocalizedContentItemIndex`, `UserIndex`, `WorkflowIndex`, `DataPipelineRunIndex`, `AuditTrailEventIndex` and `NotificationIndex`. A query that joins only these (for example `ContentItemIndex` alone) is no longer grouped by document; joining any other index, such as `TaxonomyIndex` or your own undeclared index, brings the grouping back.

### Migration Example

```csharp
using CrestApps.Sports.Calendar.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.Sports.Calendar.Migrations;

internal sealed class SportEventMigrations : DataMigration
{
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SportEventPartIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<bool>("Published")
            .Column<DateTime>("EventDateUtc")
            .Column<DateTime?>("EndDateUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<SportEventPartIndex>(table => table
            .CreateIndex(
                "IDX_SportEventPartIndex_EventDateUtc",
                "Published",
                "EventDateUtc")
            .CreateIndex(
                "IDX_SportEventPartIndex_EndDateUtc",
                "Published",
                "EndDateUtc")
        );

        return 2;
    }
}
```

### DocumentId and Enum Pattern

```csharp
public enum SportEventStatus
{
    Draft,
    Published,
}

public sealed class SportEventPartIndex : MapIndex
{
    public long DocumentId { get; set; }

    public string ContentItemId { get; set; }

    public SportEventStatus Status { get; set; }
}

await SchemaBuilder.CreateMapIndexTableAsync<SportEventPartIndex>(table => table
    .Column<string>("ContentItemId", column => column.WithLength(26))
    .Column<SportEventStatus>("Status")
);
```

### Startup Registration

```csharp
using CrestApps.Sports.Core.Indexes;
using CrestApps.Sports.Drills.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql.Indexes;

namespace CrestApps.Sports.Drills;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProvider<DrillsIndexProvider>();
        // DrillsMigrations creates the DrillsIndex table, as in the migration example above.
        services.AddDataMigration<DrillsMigrations>();
    }
}
```

### Querying with QueryIndex

```csharp
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
```

### Exposing an Index to Data Pipelines and Reports

A `MapIndex` makes a good backing store for a custom data source: derive a `RecordDataSet<TIndex>` that pages through `ISession.QueryIndex<TIndex>()` in a stable order (for example by `Id`), and apply `DataConditions.Keys(...)`/`DataConditions.DateRange(...)` to the index query. Implement `IAggregateRecordDataSet` to answer counts and sums with a `GROUP BY` on the index table. See `orchardcore-data-sources`, and `orchardcore-data-formulas` for how pipeline filters arrive as `DataConditions`.

### YesSql Configuration

YesSql options (command page size, query gating, identity column size, ...) are configured in the `OrchardCore:YesSql` section (formerly `OrchardCore_YesSql`, still read but deprecated in 4.0); see `orchardcore-data`.

Orchard Core 4.0 uses YesSql 6.3.0. Index providers, `MapIndex`/`ReduceIndex`, `SchemaBuilder` and `QueryIndex` APIs are unchanged; 6.3 adds the index declarations above, `SkipDeduplication()` and `NoIndexOptimizations()`, and creates the indexes that a data migration adds to an existing table without blocking writes (online on SQL Server editions that support it, `ALGORITHM=INPLACE, LOCK=NONE` on MySQL; PostgreSQL and SQLite still block). Set `OrchardCore:YesSql:ConcurrentIndexCreationEnabled` to `false` to create them the regular way. YesSql also emits telemetry: with `orchardcore-opentelemetry`, add `YesSql` to `Tracing:Sources` and `Metrics:Meters` to see a span per command, `db.client.operation.duration`, and the `yessql.session.commands` histogram that exposes N+1 query patterns. `IncludeQueryTextInTelemetry` (default `false`) adds the SQL text to those spans; raw SQL fragments passed to a query are included verbatim, so leave it off when telemetry leaves your trust boundary.

### Best Practices

- Index only the fields required for frequent lookups.
- Prefer exact-match columns over scanning JSON in services.
- Normalize values in the index when queries should be case-insensitive.
- Return multiple MapIndex rows for picker collections instead of storing delimited strings when you need exact matching.
- Keep lookup logic in shared services small and index-driven.
- If a range query needs different handling for `null` and non-null columns, prefer two YesSql queries with supported predicates over a single predicate that uses a ternary.
- Keep Orchard id columns at length 26.
- Declare `ExclusiveToDocumentType()` and `SingleRecordPerDocument()` on new map indexes when they hold; never declare `SingleRecordPerDocument()` on an index that returns one row per tag, term or picked item.
- Never add `DocumentId` explicitly to a `MapIndex` migration table, because YesSql creates that column automatically.

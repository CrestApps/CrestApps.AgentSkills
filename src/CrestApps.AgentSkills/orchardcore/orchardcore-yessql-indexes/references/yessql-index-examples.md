# Orchard Core YesSql Index Examples

## Club-to-Team Lookup

Use a dedicated index when a service repeatedly resolves all teams for a selected club. Index the selected club id and the team content item id, then query with ISession.QueryIndex<ClubTeamsIndex>().

## Team-to-Player Lookup

Use a dedicated index for player roster lookups when PlayerPart.Team is queried frequently. This avoids loading every Player content item and filtering in memory.

## Skill Category Lookup

If category lookups should be case-insensitive, normalize the category during indexing and normalize incoming query values the same way.

## Drill-to-Skill Lookup

When drills can target many skills, project one DrillsIndex row per selected skill rather than storing a single concatenated value. This keeps the query exact and lets SQL indexes help.

## Migration Checklist

- Add or update the content definition first if the relationship field is new.
- Create the map-index table with correctly sized string columns and use literal column names instead of `nameof(...)`.
- Do not add `DocumentId` manually for `MapIndex` tables, because YesSql creates it automatically.
- Add a SQL index for the main lookup column plus `Published` if you query published items, for example `CreateIndex("IDX_ClubTeamsIndex_ClubContentItemId", "ClubContentItemId", "Published")`.
- If you need the YesSql document id in code, add `public long DocumentId { get; set; }` to the index model and let YesSql populate it.
- Store enum values directly with `.Column<YourEnum>("Status")` when the index needs enum-based filtering.
- Return the next migration version after the index table is created.

## Startup Checklist

- Register the provider with services.AddIndexProvider<TProvider>().
- Keep shared index code in the matching core project if multiple modules or services consume it.
- Make sure the consuming module references that core project.
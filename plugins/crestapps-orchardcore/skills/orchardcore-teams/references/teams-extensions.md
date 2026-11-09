# Teams Extension Examples

Reference `CrestApps.OrchardCore.Teams.Abstractions` for the contracts and `CrestApps.OrchardCore.Teams.Core` for `AddTeamsCore()`, `AddTeamDeletionGuard<T>()` and `TeamStore`.

## Refuse to Delete a Team Still in Use

`ITeamManager.GetDeletionBlockersAsync` asks every guard; the **Delete** row action shows each reason and deletes nothing when there is one.

```csharp
public sealed class RosterTeamDeletionGuard : ITeamDeletionGuard
{
    private readonly RosterStore _rosters;

    public RosterTeamDeletionGuard(RosterStore rosters)
    {
        _rosters = rosters;
    }

    public async Task<string> GetBlockerAsync(Team team, CancellationToken cancellationToken = default)
    {
        var count = await _rosters.CountForTeamAsync(team.ItemId, cancellationToken);

        return count == 0
            ? null
            : $"The team '{team.Name}' still has {count} rosters. Move them to another team first.";
    }
}

// Startup:
// services.AddTeamsCore();
// services.AddTeamDeletionGuard<RosterTeamDeletionGuard>();
```

## Give Teams Their Members

Teams stores no member list. A module that knows who belongs to a team gives them through `ITeamMemberProvider`; everyone reads them through `ITeamMembershipService`, which adds sub-team members and filters by day.

```csharp
public sealed class VolunteerTeamMemberProvider : ITeamMemberProvider
{
    private readonly VolunteerStore _volunteers;

    public VolunteerTeamMemberProvider(VolunteerStore volunteers)
    {
        _volunteers = volunteers;
    }

    public async Task<IReadOnlyList<TeamMember>> GetMembersAsync(IReadOnlyCollection<string> teamIds, CancellationToken cancellationToken = default)
    {
        var volunteers = await _volunteers.ListByTeamsAsync(teamIds, cancellationToken);

        return volunteers
            .Where(volunteer => !string.IsNullOrEmpty(volunteer.UserId))
            .Select(volunteer => new TeamMember
            {
                TeamId = volunteer.TeamId,
                UserId = volunteer.UserId,
                Role = volunteer.IsCoordinator ? TeamMemberRoles.Lead : TeamMemberRoles.Member,
                EffectiveFrom = volunteer.JoinedOn,
                Source = "Volunteers",
            })
            .ToList();
    }

    public async Task<IReadOnlyList<TeamMember>> GetMembershipsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var volunteer = await _volunteers.FindByUserIdAsync(userId, cancellationToken);

        return volunteer is null
            ? []
            : [new TeamMember { TeamId = volunteer.TeamId, UserId = userId, Source = "Volunteers" }];
    }
}

// Startup: services.AddTeamMemberProvider<VolunteerTeamMemberProvider>();
```

Reading members:

```csharp
var today = DateOnly.FromDateTime(DateTime.UtcNow);
var members = await _teamMembershipService.GetMembersAsync(teamId, includeSubTeams: true, on: today);
var isMember = await _teamMembershipService.IsMemberAsync(teamId, userId, today);
```

## Add a Row Action to the Team List

The team list renders each team with `DisplayType` `SummaryAdmin` (list layout) or `SummaryAdminGrid` (grid layout). Built-in shapes: `Team_Fields_SummaryAdmin` (`Content:1` / `Name:1`), `Team_Tags_SummaryAdmin` (`Tags:5` / `Details:5`), `Team_Buttons_SummaryAdmin` (`Actions:5`), `Team_ActionsMenu_SummaryAdmin` (`ActionsMenu:5`) and `Team_Delete_SummaryAdmin` (`ActionsMenu:100`).

```csharp
public sealed class RosterTeamRowDisplayDriver : DisplayDriver<Team>
{
    public override IDisplayResult Display(Team team, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        return Initialize<RosterTeamRowViewModel>("Team_Rosters_SummaryAdmin", model => model.TeamId = team.ItemId)
            .Location("ActionsMenu:20");
    }
}

// Startup: services.AddDisplayDriver<Team, RosterTeamRowDisplayDriver>();
```

`Team_Rosters_SummaryAdmin.cshtml` renders one `<li>` with a `dropdown-item` link, like the built-in menu entries.

## Editor Card View

Editor sections are cards with one field per row, matching the built-in **Team** card:

```cshtml
@model RosterTeamViewModel

<div class="card text-bg-theme mb-3">
    <div class="card-header">@T["Rosters"]</div>
    <div class="card-body">
        <div class="ocat-wrapper">
            <label asp-for="Color" class="ocat-label">@T["Color"]</label>
            <div class="ocat-end">
                <input asp-for="Color" type="color" class="form-control form-control-color" />
                <span asp-validation-for="Color" class="text-danger"></span>
            </div>
        </div>
    </div>
</div>
```

The page's own `action-bar-bottom` holds **Save** and **Cancel**; a section never adds buttons.

## Import Teams From Your Own Recipe Step

`TeamRecipeImport.ImportAsync` gives your step the same matching (id, then name), parent-by-name ordering and validation as the `Teams` step, and a callback to copy your settings onto each team.

```csharp
public sealed class RosterRecipeStep : NamedRecipeStepHandler
{
    private readonly ITeamManager _teamManager;

    public RosterRecipeStep(ITeamManager teamManager)
        : base("Rosters")
    {
        _teamManager = teamManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var nodes = (context.Step["Teams"] as JsonArray ?? []).OfType<JsonObject>();

        await TeamRecipeImport.ImportAsync(_teamManager, nodes, context.Errors, "Rosters", (node, team) =>
        {
            var color = node["color"]?.GetValue<string>();
            team.Alter<RosterTeamSettings>(settings => settings.Color = color);
        });
    }
}
```

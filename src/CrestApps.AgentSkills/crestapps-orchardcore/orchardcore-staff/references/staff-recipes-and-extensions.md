# Staff Extension Examples

Examples for modules that build on `CrestApps.OrchardCore.Staff`. Register everything in a startup marked `[RequireFeatures(StaffConstants.Features.Staff)]`.

## Add a Card to the Staff Editor

The staff editor (and Workforce's worker view of it) is built with `IDisplayManager<StaffMember>`. Keep your data in the staff member's `Properties` with `Put`, `TryGet` or `Alter`. Bind with the driver's `Prefix` and report errors under `$"{Prefix}.Field"`; the staff member's own rules run after every card, and when anything fails nothing is saved.

```csharp
public sealed class StaffUniform
{
    public string Size { get; set; }
}

public class StaffUniformViewModel
{
    public string Size { get; set; }
}

internal sealed class StaffUniformDisplayDriver : DisplayDriver<StaffMember>
{
    internal readonly IStringLocalizer S;

    public StaffUniformDisplayDriver(IStringLocalizer<StaffUniformDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(StaffMember staff, BuildEditorContext context)
    {
        return Initialize<StaffUniformViewModel>("StaffUniform_Edit", model =>
        {
            model.Size = staff.TryGet<StaffUniform>(out var uniform) ? uniform.Size : null;
        }).Location("Content:20");
    }

    public override async Task<IDisplayResult> UpdateAsync(StaffMember staff, UpdateEditorContext context)
    {
        var model = new StaffUniformViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Size?.Length > 10)
        {
            context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.Size)}", S["Use a short size code."]);
        }

        staff.Alter<StaffUniform>(uniform => uniform.Size = model.Size);

        return Edit(staff, context);
    }
}

// Startup: services.AddDisplayDriver<StaffMember, StaffUniformDisplayDriver>();
```

Built-in cards: `StaffMemberFields_Edit` (Staff, `Content:1`), `WorkforceStaffFields_Edit` (Workforce, `Content:5`), `StaffTeams_Edit` (Teams), `StaffMemberCapabilities_Edit`, `StaffMemberSkills_Edit` (Skills), `StaffMemberPay_Edit` (Compensation), `StaffMemberBadge_Edit` (Badges), `PhoneStaff_Edit` (Telephony), `AgentStaff_Edit` (Contact Center).

## Add a Section to the Staff Profile

The profile renders the staff member with the `DetailAdmin` display type. Return a shape only for that display type and place it in a section with `StaffProfileSections.Location`. Register a new section's title, icon and position once.

```csharp
internal sealed class StaffUniformProfileDisplayDriver : DisplayDriver<StaffMember>
{
    public override IDisplayResult Display(StaffMember staff, BuildDisplayContext context)
    {
        if (context.DisplayType != StaffProfileSections.DisplayType)
        {
            return null;
        }

        return View("StaffUniformProfile", staff)
            .Location(StaffProfileSections.DisplayType, StaffProfileSections.Location("Uniform", "1"));
    }
}

[RequireFeatures(StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<StaffMember, StaffUniformProfileDisplayDriver>();
        services.AddPageSection(StaffProfileSections.PageName, "Uniform", LocalizationSource.Create<StaffStartup>("Uniform"), "fa-solid fa-shirt", 50);
    }
}
```

To put a shape in an existing section, use `StaffProfileSections.Overview`, `Teams`, `Skills`, `Work`, `Pay` or `History` and skip `AddPageSection`. Shapes placed without a section land in Overview.

## Make the Person Doing the Work a Staff Member

Modules whose users become staff by doing something (signing in to a queue, opening a register) call `IStaffAccountService.EnsureAsync`. It returns the existing staff member with the capability added, or creates one. `defaults` runs only for a new record.

```csharp
internal sealed class CourierSignInHandler
{
    private readonly IStaffAccountService _staffAccounts;

    public CourierSignInHandler(IStaffAccountService staffAccounts)
    {
        _staffAccounts = staffAccounts;
    }

    public Task<StaffMember> EnsureCourierAsync(string userId, string displayName, CancellationToken cancellationToken)
        => _staffAccounts.EnsureAsync(userId, displayName, ["Driver"], staff => staff.JobTitle = "Courier", cancellationToken);
}
```

## Limit a List to the Staff a Person Manages

```csharp
internal sealed class RouteListService
{
    private readonly IStaffScopeService _scopeService;
    private readonly IStaffMemberManager _staffManager;

    public RouteListService(IStaffScopeService scopeService, IStaffMemberManager staffManager)
    {
        _scopeService = scopeService;
        _staffManager = staffManager;
    }

    public async Task<IReadOnlyList<StaffMember>> ListDriversAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.GetScopeAsync(user, cancellationToken);

        if (scope.IsEmpty)
        {
            return [];
        }

        var query = new StaffQuery
        {
            Capability = "Driver",
            Status = StaffStatus.Active,
            TeamIds = scope.All ? null : scope.TeamIds.ToArray(),
        };

        var (_, items) = await _staffManager.SearchAsync(query, 0, 200, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return items.Where(staff => scope.Covers(staff, today)).ToArray();
    }
}
```

## Index Your Own Data on Staff Members

Staff members live in the `Staff` collection, so your index provider sets `CollectionName` to `StaffConstants.CollectionName`. This index maps only `StaffMember` documents and at most one row per person, so it declares both.

```csharp
public sealed class StaffUniformIndex : MapIndex
{
    public string StaffMemberId { get; set; }

    public string Size { get; set; }
}

public sealed class StaffUniformIndexProvider : IndexProvider<StaffMember>
{
    public StaffUniformIndexProvider()
    {
        CollectionName = StaffConstants.CollectionName;
    }

    public override void Describe(DescribeContext<StaffMember> context)
    {
        context.For<StaffUniformIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .When(staff => staff.TryGet<StaffUniform>(out var uniform) && !string.IsNullOrEmpty(uniform.Size))
            .Map(staff => new StaffUniformIndex
            {
                StaffMemberId = staff.ItemId,
                Size = staff.TryGet<StaffUniform>(out var uniform) ? uniform.Size : null,
            });
    }
}
```

Create the table in a migration with `SchemaBuilder.CreateMapIndexTableAsync<StaffUniformIndex>(table => ..., collection: StaffConstants.CollectionName)`, and register the provider with `services.AddIndexProvider<StaffUniformIndexProvider>()`. An index with one row per capability or team membership (like Staff's own `StaffCapabilityIndex`) is exclusive to the type but not single-record.

## Staff Recipe Fields

| Section | Fields |
|---|---|
| `Capabilities` | `name`, `displayName`, `description`, `position`, `enabled` (custom capabilities, or label and order changes to built-in ones). |
| `Staff` | `itemId`, `userName`, `displayName`, `email`, `phone`, `employeeNumber`, `externalId`, `jobTitle`, `employmentType`, `status`, `hireDate`, `seniorityDate`, `terminationDate`, `timeZoneId`, `workLocation`, `homeBase` (`address`, `latitude`, `longitude`), `contractedHoursPerWeek`, `capabilities`, `teams` (`teamName` or `teamId`, `role`, `effectiveFrom`, `effectiveTo`), `managerId` or `managerEmployeeNumber`, `pay`. |
| `pay` | `payBasis` (`Hourly`, `Salary`, `PerUnit`), `currency`, `rates` (`effectiveFrom`, `hourlyRate`, optional `activityCodeId`), `salaries` (`effectiveFrom`, `annualAmount`), `hourlyCost`, `commissionRate` (0 to 100). Negative amounts are refused. |

- Enums are names; dates are `yyyy-MM-dd`.
- A field left out keeps the stored value. `capabilities` and `teams`, when given, replace the lists; the first team becomes the main team.
- A name for a team that doesn't exist, or a manager that can't be found, is reported and that part skipped.

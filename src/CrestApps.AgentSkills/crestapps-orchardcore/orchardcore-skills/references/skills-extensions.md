# Skills Extension Examples

Reference `CrestApps.OrchardCore.Skills.Abstractions` for the contracts and `CrestApps.OrchardCore.Skills.Core` for `AddSkillsCore()` and `SkillSubjectSkillsDisplayDriverBase<TModel>`.

## Keep a Copy of a Person's Skills in Step

`ISkillAssignmentHandler.AssignmentsChangedAsync` runs inside the request that changed the skills, before the YesSql session commits, and once a day for people whose assignment starts or ends that day (`EffectiveDatesOnly` is `true` then). Keep it cheap and never throw for a remote failure: a failed query in the same session cancels the whole save. Contact Center pushes its agent sync to a deferred task for this reason.

```csharp
public sealed class DirectorySkillsHandler : ISkillAssignmentHandler
{
    private readonly DirectorySyncQueue _queue;

    public DirectorySkillsHandler(DirectorySyncQueue queue)
    {
        _queue = queue;
    }

    public Task AssignmentsChangedAsync(SkillAssignmentsChangedContext context, CancellationToken cancellationToken = default)
    {
        if (context.Subject.Kind != SkillSubjectKind.User)
        {
            return Task.CompletedTask;
        }

        // Assignments is the person's whole set now; Changed and RemovedSkillIds are what this change did.
        _queue.Enqueue(context.Subject.Id, context.Assignments.Select(assignment => (assignment.SkillId, assignment.Proficiency)).ToList());

        return Task.CompletedTask;
    }
}

// Startup: services.AddScoped<ISkillAssignmentHandler, DirectorySkillsHandler>();
```

## Link a New Kind of Subject to a User

When your module holds skills under its own subject kind, tell Skills which user (or worker) it is, so `GetEffectiveAsync` and `HoldsAsync` merge them. When both hold a skill, the higher proficiency counts.

```csharp
public sealed class VolunteerSkillSubjectLinkProvider : ISkillSubjectLinkProvider
{
    private readonly VolunteerStore _volunteers;

    public VolunteerSkillSubjectLinkProvider(VolunteerStore volunteers)
    {
        _volunteers = volunteers;
    }

    public async Task<IReadOnlyList<SkillSubject>> GetLinkedSubjectsAsync(SkillSubject subject, CancellationToken cancellationToken = default)
    {
        if (subject.Kind == "Volunteer")
        {
            var volunteer = await _volunteers.FindByIdAsync(subject.Id, cancellationToken);

            return string.IsNullOrEmpty(volunteer?.UserId) ? [] : [SkillSubject.User(volunteer.UserId)];
        }

        if (subject.Kind == SkillSubjectKind.User)
        {
            var volunteer = await _volunteers.FindByUserIdAsync(subject.Id, cancellationToken);

            return volunteer is null ? [] : [new SkillSubject("Volunteer", volunteer.ItemId)];
        }

        return [];
    }
}

// Startup: services.AddScoped<ISkillSubjectLinkProvider, VolunteerSkillSubjectLinkProvider>();
```

## Record a Certificate in Code

```csharp
var certification = await _certificationManager.FindByNameAsync("Gas safety");

var result = await _recordService.SaveAsync(new CertificationRecord
{
    SubjectKind = SkillSubjectKind.Worker,
    SubjectId = workerId,
    SubjectName = "Ana Lopez",
    CertificationId = certification.ItemId,
    Number = "GS-1042",
    IssuedOn = new DateOnly(2026, 3, 15),
    DocumentPath = "certificates/ana-lopez-gas-safety.pdf",
    Status = CertificationStatus.Valid,
}, actor: "hr-import");

// ExpiresOn is worked out from IssuedOn and ValidityMonths when it is left empty.
```

Checking on a day:

```csharp
var valid = await _recordService.IsValidOnAsync(SkillSubject.Worker(workerId), certification.ItemId, today);
var holds = await _assignmentService.HoldsAsync(SkillSubject.Worker(workerId), boilerSkillId, minimumProficiency: 3, today);
```

## Skills Section View

The section shape takes a `SubjectSkillsViewModel` whose `Skills` rows (`SkillId`, `Name`, `Category`, `Proficiency`, 0 for none) are bound back with the driver's prefix. A proficiency outside 1 to 5 removes the skill.

```cshtml
@model SubjectSkillsViewModel

<div class="card text-bg-theme mb-3">
    <div class="card-header">@T["Skills"]</div>
    <div class="card-body">
        @for (var i = 0; i < Model.Skills.Count; i++)
        {
            <div class="ocat-wrapper">
                <label class="ocat-label" for="@Html.IdFor(m => m.Skills[i].Proficiency)">@Model.Skills[i].Name</label>
                <div class="ocat-end">
                    <input type="hidden" asp-for="Skills[i].SkillId" />
                    <select asp-for="Skills[i].Proficiency" class="form-select" style="max-width: 14rem">
                        <option value="0">@T["Doesn't have it"]</option>
                        <option value="1">@T["1 - Trainee"]</option>
                        <option value="2">@T["2"]</option>
                        <option value="3">@T["3"]</option>
                        <option value="4">@T["4"]</option>
                        <option value="5">@T["5 - Expert"]</option>
                    </select>
                </div>
            </div>
        }
    </div>
</div>
```

## Start a Workflow When Skills Change

With Workflows on, the **Skills Changed** event carries `SubjectKind`, `SubjectId`, `SkillIds`, `ChangedSkillIds`, `RemovedSkillIds`, `EffectiveDatesOnly` and `Actor`. The **Assign Skill** task assigns or removes (proficiency `0`) a skill by name or id, with Liquid expressions such as `{{ Workflow.Input.SubjectId }}`.

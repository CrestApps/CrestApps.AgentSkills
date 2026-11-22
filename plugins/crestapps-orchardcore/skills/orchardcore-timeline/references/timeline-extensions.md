# Timeline Extension Examples

Give a custom record kind, `Inspection`, a timeline. Reference `CrestApps.OrchardCore.Timeline.Abstractions` and make your feature depend on `CrestApps.OrchardCore.Timeline`.

## Access Provider

Without a provider for the kind, every read and write is refused.

```csharp
using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;

public sealed class InspectionTimelineAccess : ITimelineAccessProvider
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IInspectionStore _inspections;

    public InspectionTimelineAccess(IAuthorizationService authorizationService, IInspectionStore inspections)
    {
        _authorizationService = authorizationService;
        _inspections = inspections;
    }

    public string SubjectKind => "Inspection";

    public async Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
    {
        if (await _inspections.FindAsync(subject.Id, cancellationToken) is null)
        {
            return false;
        }

        // Internal entries are for staff; public entries also for the inspection's customer.
        return visibility == TimelineVisibility.Internal
            ? await _authorizationService.AuthorizeAsync(user, InspectionPermissions.ManageInspections)
            : await _authorizationService.AuthorizeAsync(user, InspectionPermissions.ViewInspections);
    }

    public async Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
    {
        if (kind == TimelineConstants.Kinds.Event)
        {
            // Events are written by the module itself, never from the composer.
            return false;
        }

        return await _inspections.FindAsync(subject.Id, cancellationToken) is not null
            && await _authorizationService.AuthorizeAsync(user, InspectionPermissions.ManageInspections);
    }
}
```

For many records at once, override `GetVisibleAsync(user, subjectIds, visibility)` with one query; the default calls `CanViewAsync` for each id.

## Entry Handler

Refuse an entry in `AddingAsync` by adding an error; act on it in `AddedAsync`.

```csharp
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;

public sealed class InspectionTimelineHandler : ITimelineEntryHandler
{
    private readonly IInspectionStore _inspections;

    public InspectionTimelineHandler(IInspectionStore inspections)
    {
        _inspections = inspections;
    }

    public async Task AddingAsync(TimelineEntry entry, IList<string> errors, CancellationToken cancellationToken = default)
    {
        if (entry.Subject.Kind == "Inspection"
            && entry.Kind == TimelineConstants.Kinds.Comment
            && await _inspections.IsClosedAsync(entry.Subject.Id, cancellationToken))
        {
            errors.Add("A closed inspection takes no more comments.");
        }
    }

    public async Task AddedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.Subject.Kind == "Inspection" && entry.Visibility == TimelineVisibility.Public)
        {
            await _inspections.TouchAsync(entry.Subject.Id, entry.CreatedUtc, cancellationToken);
        }
    }
}
```

## Importing History

```csharp
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;

public sealed class LegacyNotesImporter
{
    private readonly ITimelineService _timeline;

    public LegacyNotesImporter(ITimelineService timeline)
    {
        _timeline = timeline;
    }

    public async Task ImportAsync(string inspectionId, IEnumerable<(int Id, string UserId, string Text, DateTime AtUtc)> notes)
    {
        foreach (var note in notes)
        {
            var entry = new TimelineEntry
            {
                Subject = new RecordRef("Inspection", inspectionId),
                Kind = TimelineConstants.Kinds.Note,
                Visibility = TimelineVisibility.Internal,
                Author = new PartyRef(PartyKinds.User, note.UserId),
                Body = note.Text,
                CreatedUtc = note.AtUtc,
            };

            // The same source note always gets the same id, so a second import adds nothing.
            entry.ItemId = TimelineEntryIds.FromSource("legacy-inspection-note", note.Id.ToString());

            await _timeline.ImportAsync(entry);
        }
    }
}
```

## A Composer Button for Your Kind

```csharp
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTimelineAccessProvider<InspectionTimelineAccess>();
        services.AddTimelineEntryHandler<InspectionTimelineHandler>();
        services.AddScoped<LegacyNotesImporter>();

        services.AddTimelineComposerKind(new TimelineComposerKind
        {
            Kind = "Finding",
            Visibility = TimelineVisibility.Public,
            ButtonText = LocalizationSource.Create<Startup>("Add finding"),
            SubjectKinds = { "Inspection" },
        });
    }
}
```

`IInspectionStore` and `InspectionPermissions` stand for your module's own types. A button that must also change the record (a reply that sets a status) overrides the `TimelineComposer__Inspection` shape and posts to the module's own action.

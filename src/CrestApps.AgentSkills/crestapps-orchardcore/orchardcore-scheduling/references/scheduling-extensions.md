# Extending Scheduling

Reference `CrestApps.OrchardCore.Scheduling.Abstractions` (namespaces `CrestApps.OrchardCore.Scheduling`, `.Models`, `.Services`) and depend on the `CrestApps.OrchardCore.Scheduling` feature, or put the registrations in a startup with `[RequireFeatures("CrestApps.OrchardCore.Scheduling")]` for an optional integration.

## Book from Another Module

```csharp
public sealed class PortalBookingService
{
    private readonly IAppointmentService _appointmentService;
    private readonly ISchedulingAvailabilityService _availability;
    private readonly AppointmentTypeStore _types;

    public PortalBookingService(IAppointmentService appointmentService, ISchedulingAvailabilityService availability, AppointmentTypeStore types)
    {
        _appointmentService = appointmentService;
        _availability = availability;
        _types = types;
    }

    public async Task<IReadOnlyList<AppointmentSlot>> GetSlotsAsync(string typeId, DateTime fromUtc, DateTime toUtc)
    {
        var type = await _types.FindByIdAsync(typeId);

        return type is null ? [] : await _availability.FindSlotsAsync(type, fromUtc, toUtc);
    }

    public Task<AppointmentResult> HoldAsync(string typeId, DateTime startUtc, string userId, string name, string email)
        => _appointmentService.BookAsync(new AppointmentRequest
        {
            AppointmentTypeId = typeId,
            StartUtc = startUtc,
            UserId = userId,
            CustomerName = name,
            CustomerEmail = email,
            Source = "Portal",
            Actor = userId,
            Hold = true,          // held for the type's HoldMinutes; confirm with ConfirmAsync
            SelfService = true,   // apply the type's booking policies
        });
}
```

`AppointmentTypeStore` lives in `CrestApps.OrchardCore.Scheduling.Core`; reference that project when you need it. Confirm a hold with `ConfirmAsync(appointment, actor)`; an unconfirmed hold expires and its calendar event is cancelled.

## React to Appointment Changes

Observers run after the change commits.

```csharp
public sealed class NoShowObserver : IAppointmentObserver
{
    private readonly ILogger _logger;

    public NoShowObserver(ILogger<NoShowObserver> logger)
    {
        _logger = logger;
    }

    public Task OnChangedAsync(AppointmentChange change, CancellationToken cancellationToken = default)
    {
        if (change.Kind == AppointmentChangeKind.NoShow)
        {
            _logger.LogInformation("{Customer} missed {Type} at {Start:u}.", change.Appointment.CustomerName, change.AppointmentType?.Name, change.Appointment.StartUtc);
        }

        return Task.CompletedTask;
    }
}

// Startup: services.AddScoped<IAppointmentObserver, NoShowObserver>();
```

Work Orders' `AppointmentWorkOrderObserver` is the reference implementation: on `Confirmed` it creates a work order (source `Appointment`, source id = appointment) for the work order type whose `AppointmentTypeIds` lists the appointment's type, moves it on `Rescheduled` and cancels it on `Cancelled`.

## Narrow Offered Slots

A filter receives the slots the resources' hours give and returns a subset (it may lower `Remaining` but never add slots). `ExcludeAppointmentId` is the appointment being moved, so count it as gone.

```csharp
public sealed class EquipmentStockSlotFilter : IAppointmentSlotFilter
{
    private readonly IKitInventory _kits;

    public EquipmentStockSlotFilter(IKitInventory kits)
    {
        _kits = kits;
    }

    public async Task<IReadOnlyList<AppointmentSlot>> FilterAsync(AppointmentSlotFilterContext context, IReadOnlyList<AppointmentSlot> slots, CancellationToken cancellationToken = default)
    {
        if (context.AppointmentType is null || slots.Count == 0 || !await _kits.NeedsKitAsync(context.AppointmentType.ItemId, cancellationToken))
        {
            return slots;
        }

        var kept = new List<AppointmentSlot>();

        foreach (var slot in slots)
        {
            var available = await _kits.CountFreeAsync(slot.StartUtc, slot.EndUtc, context.ExcludeAppointmentId, cancellationToken);

            if (available > 0)
            {
                slot.Remaining = Math.Min(slot.Remaining, available);
                kept.Add(slot);
            }
        }

        return kept;
    }
}

// Startup: services.AddScoped<IAppointmentSlotFilter, EquipmentStockSlotFilter>();
```

Dispatch's `DispatchCapacitySlotFilter` keeps a work-order appointment slot only while a technician with the work order type's skills, on a published shift, can take the job, counting bookings not yet assigned.

## Add a Section to the Appointment Type Editor

The appointment type editor (`scheduling/types/edit/{id?}`) and the resource editor (`scheduling/resources/edit/{id?}`) are built with `IDisplayManager<AppointmentType>` and `IDisplayManager<SchedulingResource>`. The built-in fields are one driver each (`AppointmentTypeFields_Edit`, `SchedulingResourceFields_Edit`, at `Content:1`). Both records are `CatalogItem`s, so keep your data in `Properties`; the controller saves the record after all drivers run.

```csharp
public sealed class IntakeFormSettings
{
    public string FormId { get; set; }
}

public class IntakeFormViewModel
{
    public string FormId { get; set; }
}

public sealed class IntakeFormDisplayDriver : DisplayDriver<AppointmentType>
{
    internal readonly IStringLocalizer S;

    public IntakeFormDisplayDriver(IStringLocalizer<IntakeFormDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AppointmentType type, BuildEditorContext context)
        => Initialize<IntakeFormViewModel>("AppointmentTypeIntakeForm_Edit", model =>
        {
            model.FormId = type.GetOrCreate<IntakeFormSettings>().FormId;
        }).Location("Content:5");

    public override async Task<IDisplayResult> UpdateAsync(AppointmentType type, UpdateEditorContext context)
    {
        var model = new IntakeFormViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (type.AllowSelfService && string.IsNullOrWhiteSpace(model.FormId))
        {
            context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.FormId)}", S["Self-service types need an intake form."]);
        }
        else
        {
            type.Put(new IntakeFormSettings { FormId = model.FormId?.Trim() });
        }

        return Edit(type, context);
    }
}

// Startup: services.AddDisplayDriver<AppointmentType, IntakeFormDisplayDriver>();
```

The view `Views/AppointmentTypeIntakeForm.Edit.cshtml` binds `IntakeFormViewModel` with the driver's prefix (`asp-for` handles it). Any model error keeps the record from being saved, and the editor cancels the session, so records your driver wrote during the update are discarded too. The `Scheduling` recipe step copies `properties` with the record, so your settings travel with it.

## Pitfalls

- Never write appointments through `AppointmentStore` outside a recipe: no locks, no calendar event, no observers.
- Don't call `ICalendarEventManager` for appointment events; they have `Editable = false` and belong to Scheduling.
- If your module locks its own records and then books, take your lock first and let Scheduling take its ordered set; never take Scheduling's `SCHEDULING_RESOURCE_` keys yourself.
- Busy time you want Scheduling to respect belongs in an `ICalendarBusyTimeSource` (see `orchardcore-calendars`); capacity rules that aren't time on a calendar belong in an `IAppointmentSlotFilter`.

# Extending the Work Order Page and Editor

The work order page (`work-orders/view/{id}`) builds `IDisplayManager<WorkOrderPanels>` with the `Detail` display type and renders the panels' Content zone. The editor (`work-orders/new/{typeId}` and `work-orders/edit/{id}`) builds `IDisplayManager<WorkOrder>`; the built-in fields are the `WorkOrderFields_Edit` shape at `Content:1`. The pages, routes, permissions and stored fields are unchanged by your drivers.

Reference `CrestApps.OrchardCore.WorkOrders.Abstractions` (for `WorkOrder`, `WorkOrderPanels`) from your module.

## A Panel on the Work Order Page

`WorkOrderPanels` carries the `WorkOrder` and `CanManage` (whether the user may manage work orders).

```csharp
using CrestApps.OrchardCore.WorkOrders.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class SitePhotosPanelDisplayDriver : DisplayDriver<WorkOrderPanels>
{
    public override IDisplayResult Display(WorkOrderPanels model, BuildDisplayContext context)
        => Initialize<SitePhotosPanelViewModel>("WorkOrderSitePhotos", view =>
        {
            view.Number = model.WorkOrder.Number;
            view.Attachments = model.WorkOrder.Attachments;
            view.CanManage = model.CanManage;
        }).Location("Content:10");
}

public class SitePhotosPanelViewModel
{
    public string Number { get; set; }

    public IList<string> Attachments { get; set; } = [];

    public bool CanManage { get; set; }
}
```

`Views/WorkOrderSitePhotos.cshtml`:

```cshtml
@model SitePhotosPanelViewModel

<div class="card mb-3">
    <div class="card-header">@T["Site photos for {0}", Model.Number]</div>
    <ul class="list-group list-group-flush">
        @foreach (var path in Model.Attachments)
        {
            <li class="list-group-item">@path</li>
        }
    </ul>
</div>
```

Register it in your startup:

```csharp
services.AddDisplayDriver<WorkOrderPanels, SitePhotosPanelDisplayDriver>();
```

Dispatch's own panel (`WorkOrderVisits`) is placed at `Content:5`; place yours before or after it.

## A Section in the Work Order Editor

Bind your own view model with the driver's `Prefix`, and report errors under it. Any model error keeps the work order from being saved, and the editor cancels the YesSql session, so anything your driver wrote during `UpdateAsync` is discarded too. A new work order has no `Number` yet.

```csharp
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

// Your module's own storage for the extra data.
public interface IPermitStore
{
    Task<string> GetAsync(string workOrderId);

    Task SaveAsync(string workOrderId, string permitNumber);
}

public sealed class PermitNumberDisplayDriver : DisplayDriver<WorkOrder>
{
    private readonly IPermitStore _permits;

    internal readonly IStringLocalizer S;

    public PermitNumberDisplayDriver(IPermitStore permits, IStringLocalizer<PermitNumberDisplayDriver> stringLocalizer)
    {
        _permits = permits;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(WorkOrder workOrder, BuildEditorContext context)
        => Initialize<PermitNumberViewModel>("WorkOrderPermit_Edit", async model =>
        {
            model.PermitNumber = await _permits.GetAsync(workOrder.ItemId);
        }).Location("Content:5");

    public override async Task<IDisplayResult> UpdateAsync(WorkOrder workOrder, UpdateEditorContext context)
    {
        var model = new PermitNumberViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (workOrder.Priority != WorkOrderPriority.Urgent && string.IsNullOrWhiteSpace(model.PermitNumber))
        {
            context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.PermitNumber)}", S["A permit number is required."]);
        }
        else
        {
            await _permits.SaveAsync(workOrder.ItemId, model.PermitNumber?.Trim());
        }

        return Edit(workOrder, context);
    }
}

public class PermitNumberViewModel
{
    public string PermitNumber { get; set; }
}
```

`Views/WorkOrderPermit_Edit.cshtml` (one field a line, inside a card, like the built-in sections):

```cshtml
@model PermitNumberViewModel

<div class="card mb-3">
    <div class="card-body">
        <div class="mb-3">
            <label asp-for="PermitNumber" class="form-label">@T["Permit number"]</label>
            <input asp-for="PermitNumber" class="form-control" />
            <span asp-validation-for="PermitNumber"></span>
        </div>
    </div>
</div>
```

Register it:

```csharp
services.AddScoped<IPermitStore, PermitStore>();
services.AddDisplayDriver<WorkOrder, PermitNumberDisplayDriver>();
```

## Bill Somewhere Else

`WorkOrderService.CompleteAsync` bills through the registered `IWorkOrderBilling` with the highest `Priority` (Invoicing's is 10, Transactions' is the default 0). It passes a `WorkOrderBillingRequest`: `WorkOrder`, `Amount` (before tax), `Currency`, `Lines` (`WorkOrderBillingLine`: `Kind`, `Description`, `Quantity`, `UnitPrice`, `ProductId`, `TaxCategoryCode`), `Payer` (a `PartyRef`: the account, else the customer user, else the contact), `PaymentTermId` and `TaxCategoryCode`. The returned `WorkOrderBillingResult` is stored on the work order (`InvoiceTransactionId`, `InvoiceId`).

```csharp
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;

// Your module's client for an external accounting system.
public interface IAccountingClient
{
    Task<(string Id, string Number)> CreateInvoiceAsync(WorkOrderBillingRequest request, CancellationToken cancellationToken);
}

public sealed class AccountingWorkOrderBilling : IWorkOrderBilling
{
    private readonly IAccountingClient _client;

    public AccountingWorkOrderBilling(IAccountingClient client)
    {
        _client = client;
    }

    // Wins over Invoicing (10) and Transactions (0).
    public int Priority => 20;

    public async Task<WorkOrderBillingResult> InvoiceAsync(WorkOrderBillingRequest request, CancellationToken cancellationToken = default)
    {
        var (id, number) = await _client.CreateInvoiceAsync(request, cancellationToken);

        return new WorkOrderBillingResult { InvoiceId = id, InvoiceNumber = number };
    }

    // The older amount-only overload, still part of the interface.
    public async Task<string> InvoiceAsync(WorkOrder workOrder, decimal amount, string currency, CancellationToken cancellationToken = default)
        => (await InvoiceAsync(new WorkOrderBillingRequest { WorkOrder = workOrder, Amount = amount, Currency = currency }, cancellationToken)).TransactionId;
}

// Startup (a feature that depends on CrestApps.OrchardCore.WorkOrders):
// services.AddScoped<IWorkOrderBilling, AccountingWorkOrderBilling>();
```

A billing that only posts an amount can implement just `InvoiceAsync(workOrder, amount, currency)` (returning the transaction id); the request overload's default implementation calls it.

## Other Seams

- `IWorkOrderPartsService.UseAsync(workOrder, line, quantity, actor)` takes parts off another stock system.
- Business hours for the timers come from the shared Business Hours schedules that a policy names (`HoursScheduleId`); there is no Work Orders hours provider any more. Add a schedule source to Business Hours instead.
- `IAgreementObserver` (Agreements) hears when any agreement is about to end (`OnExpiringAsync`) or passes 80% and 100% of its hours (`OnHoursUsedAsync`); check `agreement.TryGet<WorkOrderAgreementTerms>(out var terms)` to act only on agreements with visits. `IAgreementTermsValidator` checks your own terms on the agreement.
- Notes: write internal notes with `WorkOrderService.AddNoteAsync`; to add other entries (with field changes or attachments), call `ITimelineService.AddAsync` with `Subject = new RecordRef(WorkOrdersConstants.RecordKind, workOrder.ItemId)`.

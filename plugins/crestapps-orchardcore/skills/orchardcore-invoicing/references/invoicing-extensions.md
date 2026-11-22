# Building on Invoicing

Code samples for modules that bill through Invoicing. Reference `CrestApps.OrchardCore.Invoicing.Abstractions` and `CrestApps.OrchardCore.Invoicing.Core` only.

## Bill your module's work as an invoice

Look for the invoice you made already (by `SourceType` and `SourceId`), so billing twice returns the same invoice. Bill a party, not a copied name: the billing profile fills the contact at issue. This mirrors `InvoicingWorkOrderBilling`.

```csharp
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;

public sealed class EventRegistrationBilling
{
    public const string SourceType = "EventRegistration";

    private readonly IInvoiceManager _invoices;
    private readonly IInvoiceCommandService _commands;
    private readonly ILogger _logger;

    public EventRegistrationBilling(IInvoiceManager invoices, IInvoiceCommandService commands, ILogger<EventRegistrationBilling> logger)
    {
        _invoices = invoices;
        _commands = commands;
        _logger = logger;
    }

    public async Task<Invoice> BillAsync(string registrationId, string registrationNumber, string userId, decimal fee, CancellationToken cancellationToken)
    {
        var existing = await _invoices.PageAsync(1, 1, new InvoiceQuery { SourceType = SourceType, SourceId = registrationId }, cancellationToken);

        if (existing.Entries.FirstOrDefault() is { } billed)
        {
            return billed;
        }

        var invoice = await _invoices.NewAsync(cancellationToken: cancellationToken);

        invoice.Currency = "USD";
        invoice.SourceType = SourceType;
        invoice.SourceId = registrationId;
        invoice.Reference = registrationNumber;
        invoice.BillTo = new DocumentParty { Party = new PartyRef(PartyKinds.User, userId) };
        invoice.Lines.Add(new DocumentLine
        {
            Id = IdGenerator.GenerateId(),
            Kind = DocumentLineKind.Service,
            Name = "Conference registration",
            Quantity = 1m,
            UnitPrice = fee,
            TaxCategoryCode = "services",
            SourceType = SourceType,
            SourceId = registrationId,
        });

        var validation = await _invoices.ValidateAsync(invoice, cancellationToken);

        if (!validation.Succeeded)
        {
            return null;
        }

        await _invoices.CreateAsync(invoice, cancellationToken);

        var issued = await _commands.IssueAsync(invoice.ItemId, cancellationToken);

        if (!issued.Succeeded)
        {
            // Kept as a draft for staff to finish; nothing is owed until it is issued.
            _logger.LogWarning("Registration {Id} was kept as a draft invoice: {Errors}", registrationId, string.Join(" ", issued.Errors));

            return invoice;
        }

        return issued.Invoice;
    }
}
```

`issued.NeedsApproval` is `true` when the total reaches **Approve invoices from**: submit it with `SubmitForApprovalAsync` or leave it for staff.

## Credit an invoice

```csharp
public sealed class RegistrationCancellation
{
    private readonly ICreditNoteCommandService _creditNotes;

    public RegistrationCancellation(ICreditNoteCommandService creditNotes)
    {
        _creditNotes = creditNotes;
    }

    public async Task<IReadOnlyList<string>> CreditInFullAsync(string invoiceId, CancellationToken cancellationToken)
    {
        // The draft credits every line still creditable; its disposition starts as ReduceBalance while something is
        // owed, else CustomerCredit when the payer can hold credit, else Refund.
        var draft = await _creditNotes.CreateDraftAsync(invoiceId, cancellationToken);

        if (!draft.Succeeded)
        {
            return draft.Errors.ToArray();
        }

        var issued = await _creditNotes.IssueAsync(draft.CreditNote.ItemId, cancellationToken);

        return issued.Errors.ToArray();
    }
}
```

## React to invoice events

Every `IInvoiceObserver` member has a default that does nothing. You hear a change after it is saved; you may be called while the command still holds the invoice's lock, so defer slow work or other invoice commands with `ShellScope.AddDeferredTask` or `DeferredWork`. A credit that settles an invoice raises `CreditNoteIssuedAsync`, not `PaidAsync`.

```csharp
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;

public sealed class RegistrationInvoiceObserver : IInvoiceObserver
{
    public Task PaidAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice.SourceType != EventRegistrationBilling.SourceType)
        {
            return Task.CompletedTask;
        }

        // Confirm the registration invoice.SourceId.
        return Task.CompletedTask;
    }

    public Task VoidedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// services.AddScoped<IInvoiceObserver, RegistrationInvoiceObserver>();
```

Workflows and webhooks are observers already; don't add your own for the same moments.

## Add to the printed invoice

`IInvoiceDocumentContributor` adds to the `BusinessDocument` the PDF, the public page and the e-mail render (see `orchardcore-business-documents`). Never render your own invoice view or PDF.

```csharp
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;

public sealed class RegistrationNumberContributor : IInvoiceDocumentContributor
{
    public Task ContributeAsync(InvoicingDocument source, BusinessDocument document, CancellationToken cancellationToken = default)
    {
        if (source.SourceType == EventRegistrationBilling.SourceType)
        {
            document.Fields.Add(new BusinessDocumentField("Registration", source.Reference));
        }

        return Task.CompletedTask;
    }
}

// services.AddScoped<IInvoiceDocumentContributor, RegistrationNumberContributor>();
```

## Tax lines with your own engine

Providers run by `Order` (lower first); the first that handles the document wins. Taxation's `TaxationDocumentTaxProvider` has `Order` 0, so use a negative order to run ahead of it.

```csharp
using CrestApps.OrchardCore.Invoicing.Services;

public sealed class ExternalTaxEngineProvider : IDocumentTaxProvider
{
    public int Order => -10;

    public async Task<bool> ApplyTaxesAsync(DocumentCalculationContext context, CancellationToken cancellationToken = default)
    {
        if (context.KeepTypedTaxes || string.IsNullOrEmpty(context.Currency))
        {
            return false;
        }

        foreach (var line in context.Lines)
        {
            // Ask the engine for the line's taxes and set line.Taxes (DocumentLineTax: Code, Name, Rate, TaxType, ...).
        }

        return true;
    }
}

// services.AddDocumentTaxProvider<ExternalTaxEngineProvider>();
```

## Offer unbilled work (Invoicing Billing)

An `IBillableSource` offers work to put on draft invoices and remembers which line billed it. Lines it produces carry `SourceType` `billable:{Type}`.

```csharp
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;

public sealed class MileageBillableSource : IBillableSource
{
    public string Type => "mileage";

    public Task<IReadOnlyList<BillableCandidate>> ListUnbilledAsync(BillableQuery query, CancellationToken cancellationToken = default)
    {
        // Return the trips of query.Party (or query.UserId) in query.Currency between query.From and query.Through
        // not billed yet.
        IReadOnlyList<BillableCandidate> candidates = [];

        return Task.FromResult(candidates);
    }

    public Task<string> MarkBilledAsync(string id, string invoiceId, string lineId, CancellationToken cancellationToken = default)
    {
        // Record the invoice line on the trip; return why it can't be billed, or null.
        return Task.FromResult<string>(null);
    }

    public Task ReleaseAsync(string id, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// In a [RequireFeatures(InvoicingConstants.Features.Billing)] startup:
// services.AddScoped<IBillableSource, MileageBillableSource>();
```

`BillableCandidate` carries `Source`, `Id`, `Date`, `Kind`, `Description`, `Quantity`, `Unit`, `UnitPrice`, `Currency` and `TaxCategoryCode`.

## Record usage

```csharp
// IUsageService records once per meter and event id, so a retry is never counted twice.
var result = await _usage.RecordAsync(new UsageRecordRequest
{
    Meter = "api-calls",
    Party = new PartyRef(PartyKinds.Account, accountId),
    Quantity = 1250m,
    OccurredUtc = occurredUtc,
    EventId = eventId,
}, cancellationToken);

// result.Succeeded, result.AlreadyRecorded (the event id was seen before), result.Errors
```

`GetTotalAsync(meterId, party, fromUtc, toUtc)` adds up a customer's records in a period as the meter's `Aggregation` says. Over HTTP use `POST api/commerce/v1/usage`.

## Show a customer their invoices

Use `IInvoiceVisibilityService.CanViewAsync(invoice, userId)` and `CreateQueryAsync(userId)` (issued invoices billed to a party the user has billing access to). A user with no party sees nothing rather than every invoice.

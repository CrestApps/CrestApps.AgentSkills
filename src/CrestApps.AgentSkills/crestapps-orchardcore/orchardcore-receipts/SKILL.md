---
name: orchardcore-receipts
description: Skill for the CrestApps Orchard Core Receipts module (CrestApps.OrchardCore.Receipts), the reusable builder of branded, printable proof-of-purchase documents. Covers IReceiptService.BuildAsync with ReceiptRequest, ReceiptDocument, ReceiptLineItem, ReceiptTaxLine and ReceiptStatus (Paid, Pending, Failed, Due), IReceiptHtmlRenderer.RenderAsync for email bodies, printing receipts as Business Documents of the Receipt kind with ReceiptBusinessDocumentMapper, document templates and the BusinessDocument-Receipt alternate, the shared ReceiptDocument.cshtml partial, ReceiptSettings branding under Settings > Commerce > Receipts with the ManageReceiptSettings permission, and how Transactions, Orders and Point of Sale consume it for receipts, invoices and packing slips. Use it when requests mention printable receipts, invoice branding, receipt layout, a test-payment badge, or rendering a receipt from a custom module.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Receipts

Receipts turns purchase data a module already holds into a branded, printable document. It stores nothing: the consumer's ledger stays the source of truth and the receipt is built on demand. Transactions (payment receipts and invoices), Orders (invoices and packing slips) and Point of Sale use it.

Related skills: `orchardcore-business-documents` (the document model, templates, renderers and number series receipts print with), `orchardcore-transactions` (printable payment requests and receipts, the Send Payment Receipts feature and the `Financial.Receipt` number series), `orchardcore-invoicing` (real invoices and credit notes), `orchardcore-orders`, `orchardcore-commerce` (store setup step that fills the store details).

## Feature

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Receipts` | Receipts | Business Documents (`CrestApps.OrchardCore.BusinessDocuments`) |

`ReceiptsConstants.Feature.Area`; settings group `receipts` (`ReceiptsConstants.SettingsGroupId`). Subscriptions depends on it. Install `CrestApps.OrchardCore.Receipts` in the web project; a module that only builds receipts references `CrestApps.OrchardCore.Receipts.Abstractions` (no commerce dependencies).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [ "CrestApps.OrchardCore.Receipts" ],
      "disable": []
    }
  ]
}
```

## Layers

- `CrestApps.OrchardCore.Receipts.Abstractions`: `IReceiptService`, `IReceiptHtmlRenderer` (namespace `CrestApps.OrchardCore.Receipts`) and `ReceiptRequest`, `ReceiptDocument`, `ReceiptLineItem`, `ReceiptTaxLine`, `ReceiptStatus` (namespace `CrestApps.OrchardCore.Receipts.Models`).
- `CrestApps.OrchardCore.Receipts.Core`: `DefaultReceiptService`, `ReceiptSettings`, `ReceiptsPermissions`, `ReceiptsConstants` (`BusinessDocumentKind` = `Receipt`) and `ReceiptBusinessDocumentMapper`.
- `CrestApps.OrchardCore.Receipts`: settings driver, admin menu, permission provider, `DefaultReceiptHtmlRenderer`, and `Views/ReceiptDocument.cshtml`.

## Building a receipt

```csharp
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Models;
using Microsoft.AspNetCore.Mvc;

namespace Contoso.Registrations.Controllers;

public sealed class RegistrationReceiptController : Controller
{
    private readonly IReceiptService _receiptService;

    public RegistrationReceiptController(IReceiptService receiptService)
    {
        _receiptService = receiptService;
    }

    [HttpGet("registrations/{id}/receipt")]
    public async Task<IActionResult> Receipt(string id)
    {
        // Load your own record and check its owner first.
        var document = await _receiptService.BuildAsync(new ReceiptRequest
        {
            BilledToName = "Ada Lovelace",
            BilledToEmail = "ada@example.test",
            Reference = "REG-1024",
            SourceLabel = "Registration",
            IssuedAt = DateTime.Today,
            Currency = "USD",
            LineItems =
            [
                new ReceiptLineItem { Description = "Season fee", Quantity = 1, UnitAmount = 100m, Amount = 100m },
            ],
            TaxLines =
            [
                new ReceiptTaxLine { Description = "HST", Amount = 13m },
            ],
            TaxAmount = 13m,
            Total = 113m,
            Status = ReceiptStatus.Paid,
            IsTest = false,
        });

        return View(document);
    }
}
```

`BuildAsync` returns a `ValueTask<ReceiptDocument>`: the request merged with the branding (`HeaderTitle`, `BusinessName` falling back to the site name, `LogoUrl`, `BusinessAddress`, `ContactEmail`, `ContactPhone`, `Website`, `FooterText`, `ShowTestBadge`) and a computed `Subtotal` (`Total - TaxAmount`). `ReceiptStatus` is `Paid`, `Pending`, `Failed` or `Due` (an invoice not yet paid). `IsTest` comes from the gateway mode (`GatewayMode.Testing`) and shows the test badge when the setting allows.

Render the shared layout from your own view with a full-path partial (module views compile under `~/Areas/{ModuleId}/Views`):

```html
@using CrestApps.OrchardCore.Receipts.Models
@model ReceiptDocument

<partial name="~/Areas/CrestApps.OrchardCore.Receipts/Views/ReceiptDocument.cshtml" model="Model" />
```

Printing outputs only the receipt; the print stylesheet hides the site header and admin menu. For an email body, resolve `IReceiptHtmlRenderer` and call `await RenderAsync(document)`: tables with inline styles, safe for mail clients. Resolve both as optional (`GetService<IReceiptService>()`) when Receipts is not a hard dependency.

### Printed as business documents

Receipts are business documents of the `Receipt` kind (see `orchardcore-business-documents`). `ReceiptBusinessDocumentMapper.Map(receipt)` turns a `ReceiptDocument` into a `BusinessDocument`: the issuer with its address lines, contact details and website, the **Billed to** party, the reference (titled with `SourceLabel`), the date and the ISO **Currency** code as fields, a Description / Qty / Amount table, the subtotal, tax lines (muted) and the total (**Total paid** when paid), the notes as a section, the footer, a status badge (`Paid` success, `Failed` danger, `Due` warning, otherwise `Pending`), and a **Test payment** watermark for a test payment when the badge is on. Amounts are written with `IMoneyFormatter` (for example `$108.00`).

- The `ReceiptDocument.cshtml` partial maps the receipt, applies the receipt document template and renders the shared `BusinessDocument` shape. A theme restyles receipts alone with `BusinessDocument-Receipt.cshtml`.
- `IReceiptHtmlRenderer.RenderAsync` renders through `IBusinessDocumentHtmlRenderer` with the template applied. The synchronous `Render` is `[Obsolete]`: it skips the template and cannot work with an asynchronous renderer. A custom renderer that only implements `Render` still works through the default interface method.
- The receipt settings' logo and footer win over the template's; the template adds its accent color and paper size, and its logo and footer only when the settings have none.
- Pick the look under **Settings > Business documents > Document templates** (a template **Used for** Receipts, or a default for every kind). For a PDF of a receipt, map it and call `IBusinessDocumentPdfRenderer` (Business Documents PDF feature).

```csharp
var document = await _receiptService.BuildAsync(request);
var businessDocument = _mapper.Map(document);           // ReceiptBusinessDocumentMapper

await _templates.ApplyAsync(businessDocument);          // IBusinessDocumentTemplateManager
var pdfRenderer = _serviceProvider.GetService<IBusinessDocumentPdfRenderer>();
var pdf = pdfRenderer is null ? null : await pdfRenderer.RenderAsync(businessDocument);
```

## Settings

**Settings > Commerce > Receipts**, permission `ManageReceiptSettings` (registered for the `receipts` group, so a role with only that permission can open it). Stored as `ReceiptSettings`:

```json
{
  "steps": [
    {
      "name": "Settings",
      "ReceiptSettings": {
        "HeaderTitle": "Payment receipt",
        "BusinessName": "Contoso Club",
        "LogoUrl": "/media/logo.png",
        "BusinessAddress": "100 King Street West, Toronto ON M5X 1C7",
        "ContactEmail": "billing@contoso.example",
        "ContactPhone": "+1 416 555 0100",
        "Website": "https://contoso.example",
        "FooterText": "Thank you for your payment.",
        "ShowTestPaymentBadge": true
      }
    }
  ]
}
```

Empty `HeaderTitle` prints *Payment receipt*. Empty `BusinessName`, `LogoUrl`, `BusinessAddress`, `ContactEmail` and `ContactPhone` fall back to the Store Profile (`StoreProfileSettings`, see `orchardcore-commerce`; the address on one line as its country writes it), then the site name for the business name. The fallback reads the settings directly, so it works whether or not the Store Profile feature is on. The store setup guide fills these from the store details step.

## Consumers

- Transactions: printable receipt per payment and payment request (shown to customers as the invoice, a `PaymentRequest` business document) per transaction whenever Receipts is on; **Send Payment Receipts** (`CrestApps.OrchardCore.Transactions.Receipts`) emails a receipt numbered `R-1001`... after every payment, through the store's `payment-receipt` and `payment-refund` email templates (**Settings > Commerce > Emails**).
- Orders: invoice and packing slip pages for an order and each shipment.
- Point of Sale: printed and emailed till receipts.
- Invoicing issues its own `Invoice` and `CreditNote` business documents (see `orchardcore-invoicing`); it does not go through Receipts.

## Upgrading from 2.x

- Receipts now depends on Business Documents. On upgrade, `ReceiptsBusinessDocumentsFeatureMigrations` records Business Documents as enabled; until it is, receipts render with their own look and no template query runs (safe on PostgreSQL inside the payment transaction). Once enabled, a template that fails to apply is an error.
- Replace `IReceiptHtmlRenderer.Render(document)` with `await RenderAsync(document)`.
- Customizations of `ReceiptDocument.cshtml` markup should move to a `BusinessDocument-Receipt.cshtml` theme view; the partial now renders the shared shape.
- New in 3.0. Subscriptions no longer carries its own receipt view model or view; the subscriber dashboard that printed receipts was removed with the old signup flow.
- `IReceiptService.BuildAsync` returns `ValueTask<ReceiptDocument>`; `ReceiptStatus.Due` and `IReceiptHtmlRenderer` were added for invoices and emails.

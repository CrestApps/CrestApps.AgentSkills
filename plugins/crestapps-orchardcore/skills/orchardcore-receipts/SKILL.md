---
name: orchardcore-receipts
description: Skill for the CrestApps Orchard Core Receipts module (CrestApps.OrchardCore.Receipts), the reusable builder of branded, printable proof-of-purchase documents. Covers IReceiptService.BuildAsync with ReceiptRequest, ReceiptDocument, ReceiptLineItem, ReceiptTaxLine and ReceiptStatus (Paid, Pending, Failed, Due), IReceiptHtmlRenderer for email bodies, the shared ReceiptDocument.cshtml partial, ReceiptSettings branding under Settings > Commerce > Receipts with the ManageReceiptSettings permission, and how Transactions, Orders and Point of Sale consume it for receipts, invoices and packing slips. Use it when requests mention printable receipts, invoice branding, receipt layout, a test-payment badge, or rendering a receipt from a custom module.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Receipts

Receipts turns purchase data a module already holds into a branded, printable document. It stores nothing: the consumer's ledger stays the source of truth and the receipt is built on demand. Transactions (payment receipts and invoices), Orders (invoices and packing slips) and Point of Sale use it.

Related skills: `orchardcore-transactions` (printable invoices and receipts, the Send Payment Receipts feature and `R-`/`INV-` numbering), `orchardcore-orders`, `orchardcore-commerce` (store setup step that fills the store details).

## Feature

| Feature ID | Name | Category |
| --- | --- | --- |
| `CrestApps.OrchardCore.Receipts` | Receipts | Commerce |

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
- `CrestApps.OrchardCore.Receipts.Core`: `DefaultReceiptService`, `ReceiptSettings`, `ReceiptsPermissions`.
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

Printing outputs only the receipt; the print stylesheet hides the site header and admin menu. For an email body, resolve `IReceiptHtmlRenderer` and call `Render(document)`: one table with inline styles, safe for mail clients. Resolve both as optional (`GetService<IReceiptService>()`) when Receipts is not a hard dependency.

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

Empty `HeaderTitle` prints *Payment receipt*; empty `BusinessName` uses the site name. The store setup guide fills these from the store details step.

## Consumers

- Transactions: printable receipt per payment and invoice per transaction whenever Receipts is on; **Send Payment Receipts** (`CrestApps.OrchardCore.Transactions.Receipts`) emails a receipt numbered `R-1001`... after every payment.
- Orders: invoice and packing slip pages for an order and each shipment.
- Point of Sale: printed and emailed till receipts.

## Upgrading from 2.x

- New in 3.0. Subscriptions no longer carries its own receipt view model or view; the subscriber dashboard that printed receipts was removed with the old signup flow.
- `IReceiptService.BuildAsync` returns `ValueTask<ReceiptDocument>`; `ReceiptStatus.Due` and `IReceiptHtmlRenderer` were added for invoices and emails.

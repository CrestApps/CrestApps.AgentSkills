# Showing a customer's invoices and receipts from another module

`ITransactionCustomerPageService`, the builders, the view models and `TransactionsConstants` live in `CrestApps.OrchardCore.Transactions.Abstractions`, so a portal module can render a customer's documents without referencing the Transactions module. The service applies the same ownership rules as the shipped pages.

```csharp
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;

namespace Contoso.Portal.Controllers;

public sealed class PortalPaymentsController : Controller
{
    private readonly ITransactionCustomerPageService _pages;
    private readonly IShapeFactory _shapeFactory;

    public PortalPaymentsController(ITransactionCustomerPageService pages, IShapeFactory shapeFactory)
    {
        _pages = pages;
        _shapeFactory = shapeFactory;
    }

    [HttpGet("portal/payments/{itemId}/invoice")]
    public async Task<IActionResult> Invoice(string itemId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var result = await _pages.BuildInvoiceAsync(User, itemId, HttpContext.RequestAborted);

        if (result.Status == TransactionCustomerPageStatus.Forbidden)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            return NotFound();
        }

        // The view renders the shape with @await DisplayAsync(Model).
        return View(await _shapeFactory.CreateAsync<TransactionInvoiceViewModel>(TransactionsConstants.Shapes.Invoice, result.Model.CopyTo));
    }
}
```

- `BuildStatementAsync(User, TransactionStatementRequest { Page, PageSize, Search, OutstandingOnly })` leaves `Pager` empty so you can attach a pager that links to your own route.
- `BuildReceiptAsync(User, itemId, paymentId)` takes the transaction and payment event ids.
- Models: `TransactionStatementViewModel` (`Entries` of `TransactionStatementEntry` with `Transaction`, `PayUrl`, `InvoiceUrl`, `Receipts`; `TotalCount`, `Search`, `OutstandingOnly`, `Pager`), `TransactionInvoiceViewModel` (`Transaction`, `Invoice` as `ReceiptDocument`, `IsAdministrator`, `PayUrl`), `TransactionReceiptViewModel` (`Transaction`, `Payment`, `Receipt`, `IsAdministrator`).
- `ITransactionCustomerLinkService` (`GetStatementUrlAsync`, `GetInvoiceUrlAsync`, `GetReceiptUrlAsync`) builds links for messages, honoring `TransactionCustomerPagesOptions.LinkTarget`.

Rules every caller gets: not signed in is a challenge; neither **View own transactions** nor **Manage transactions** is `Forbidden`; another account's transaction, a guest transaction, an invoice never issued, an event that is not a payment, or a site without Receipts is `NotFound`. A manager sees any invoice or receipt (`IsAdministrator = true`).

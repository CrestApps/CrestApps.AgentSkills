---
name: orchardcore-companies
description: Skill for the CrestApps Orchard Core Companies module (CrestApps.OrchardCore.Companies), B2B selling on the CrestApps commerce suite. Covers Company with CompanyBuyer roles and order limits, CompanyLocation, CompanyTerms (NetDays, DepositPercent, CreditLimit, RequirePurchaseOrder), the company checkout step and purchase order numbers, the company-terms tender that puts the rest of an order on account as a transaction, price lists through IBuyerPriceProvider and CompanyPriceProvider, approvals and monthly budgets with CompanyApprovalService and CompanyApprovalPaymentGate, quotes, OCI punchout from a procurement system, the company API endpoints, ICompanyStore and CompanyService, and the ManageCompanies permission. Use it when requests mention business customers, B2B buyers, net terms, credit limits, purchase orders, company price lists, order approvals, budgets or punchout.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Companies

Companies sells to businesses. A company has buyers (some of them admins), the places it ships to, payment terms (a deposit at checkout, the rest invoiced in net days under a credit limit), a price list, order limits and a monthly budget that send orders for approval, and a punchout key for its procurement system. It is built entirely on the suite's seams: a checkout step, a tender, a payment gate, an order event handler, a buyer price provider and a cart action.

Related skills: `orchardcore-orders` (orders, quotes, `Order.Approval`, purchase order numbers), `orchardcore-checkout` (steps, tenders, gates), `orchardcore-transactions` (what the company owes), `orchardcore-products` (`IPriceResolver`), `orchardcore-customers` (business customers and tax ids), `orchardcore-addresses`.

## Feature

| Feature ID | Depends on | Seams it fills |
| --- | --- | --- |
| `CrestApps.OrchardCore.Companies` (`CompaniesConstants.Feature`) | Commerce, `CrestApps.OrchardCore.Orders`, Addresses, Transactions | `ICheckoutHandler`, `ICheckoutTenderProvider`, `ICheckoutPaymentGate`, `IOrderEventHandler`, `IBuyerPriceProvider`, `ICartActionProvider` |

Install `CrestApps.OrchardCore.Companies` in the web project. Data lives in the `Companies` YesSql collection.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Companies"
      ],
      "disable": []
    }
  ]
}
```

## Model

| `Company` | |
| --- | --- |
| `Name`, `TaxId`, `BillingEmail` | The tax number prints on its invoices |
| `Buyers` | `CompanyBuyer`: `UserId`, `UserName`, `Email`, `Role` (`Buyer`, `Admin`), `OrderLimit`. A user buys for one company (`CompanyBuyerIndex`) |
| `Locations` | `CompanyLocation`: `LocationId`, `Name`, `Address`, offered at checkout |
| `Terms` | `CompanyTerms`: `NetDays`, `DepositPercent`, `CreditLimit`, `RequirePurchaseOrder`; `OffersTerms` when `NetDays` is set |
| `PriceListId` | The `PriceList` its buyers pay |
| `MonthlyBudget` | What its buyers may order per calendar month, store currency |
| `Punchout` | `CompanyPunchout`: `Enabled`, `UserId`, `KeyHash` (SHA-256 hex), `KeyCreatedUtc` |
| `IsEnabled` | A disabled company's buyers buy for themselves |

Services: `ICompanyStore.FindByBuyerAsync(userId)`, `CompanyService.GetCurrentAsync()` (the signed-in user's enabled company and buyer, cached per request) and `GetForUserAsync(userId)`, `IPriceListStore`.

## Terms at checkout

- `CompanyCheckoutHandler` adds the `company` step (order 3, after Contact and Shipping) to a company buyer's order checkout. `CompanyCheckoutStepService.SaveAsync(session, purchaseOrderNumber, requestApproval)` validates the purchase order number (required when `RequirePurchaseOrder`, 64 characters max) and puts it and `CompanyId` on the order. An order that already carries a number starts with the step saved.
- `CompanyTermsTenderProvider` (`ICheckoutTenderProvider`, key `company-terms`) puts what the deposit does not cover on account: `GrandTotal x (100 - DepositPercent) / 100`, capped by what is left of the credit limit, store currency only, never for a register sale. The account part becomes a transaction (source `company-terms`) due in `NetDays`. `CompanyTermsService.GetOwedAsync(company)` and `GetAvailableCreditAsync(company)`.

## Price lists

`PriceList`: `Name`, `PercentOff` (0 to 100), `Currency`, `Entries` (`PriceListEntry`: `Sku`, `UnitPrice`). `CompanyPriceProvider` answers `IBuyerPriceProvider` (Products.Core) for the signed-in buyer: the entry for the product or variant SKU (case-insensitive, converted with `IShopperCurrency`), else `PercentOff`. `DefaultPriceResolver` charges the lowest buyer price below the store's; volume and role discounts do not stack on it, recurring prices and custom amounts are left alone. The admin **Prices by SKU** box takes `SKU,price` lines (comma, semicolon or tab), up to 5,000.

A buyer price provider of your own:

```csharp
using CrestApps.OrchardCore.Products.Core.Services;

public sealed class MemberPriceProvider : IBuyerPriceProvider
{
    public ValueTask<decimal?> GetUnitPriceAsync(BuyerPriceContext context, CancellationToken cancellationToken = default)
    {
        // context.ContentItem, VariantId, Sku, UnitPrice (store price in context.Currency), Quantity.
        // Return null to leave the price alone; the lowest answer below the store price wins.
        return ValueTask.FromResult<decimal?>(null);
    }
}

// services.AddScoped<IBuyerPriceProvider, MemberPriceProvider>();
```

## Approvals and budgets

`CompanyApprovalService.GetReasonAsync(company, buyer, total, currency)` returns why an order needs approval, or `null`: company admins never need it; a buyer over their `OrderLimit`; or an order that takes this month's paid orders past `MonthlyBudget` (other currencies converted).

- **Asking**: the company step reports the reason; with `requestApproval`, `RequestAsync` sets `Order.Approval` to `Pending`, gives the order an `OrderInvoice` so it outlives its checkout, and emails the company admins.
- **Blocking**: `CompanyApprovalPaymentGate` refuses payment for an order without a covering approval (`OrderApproval.Covers(total, currency)`), whatever path the buyer took.
- **Deciding**: `DecideAsync(order, approve, adminName, note)` records it and emails the buyer; a rejection cancels the order with `OrderCancelReason.Rejected`.

Quotes are an Orders feature; a company buyer's quote carries their purchase order number.

## Punchout (OCI)

| Route | |
| --- | --- |
| `GET`/`POST punchout/oci/{companyId}` | Takes `PASSWORD` or `key`, `HOOK_URL`, `~TARGET`. Refuses (401) an unknown or disabled company, a wrong key (hash compared in fixed time), a `HOOK_URL` that is not absolute `https` (or `http` on loopback) or has user info, a punchout user who is not a buyer, or an `Administrator`. Signs the buyer in and keeps the hook in the data-protected `punchout` cookie |
| `GET punchout/oci/transfer` (`PunchoutTransfer`) | Posts the cart as OCI fields to `HOOK_URL` in the target frame |

`PunchoutCartActionProvider` adds **Transfer to your procurement system** to the cart. `PunchoutService.ToOciFields(pricing)` writes `NEW_ITEM-DESCRIPTION[n]` (40 characters; longer titles in `NEW_ITEM-LONGTEXT_n:132[]`), `QUANTITY`, `UNIT` (`EA`), `PRICE`, `PRICEUNIT`, `CURRENCY`, `VENDORMAT` (SKU) and `EXT_PRODUCT_ID`. Open the punchout in a top-level window (the sign-in cookie is `SameSite=Lax`), and allow the hook origin in a CSP `form-action`.

## API

| Endpoint | |
| --- | --- |
| `GET account/company` | Buyer's company, role, limit, terms, owed, locations |
| `GET account/company/approvals` | Company admin: orders waiting (`ApiCompanyApproval`) |
| `POST account/company/approvals/{orderId}` | `{ approve, note }`; 204, or 409 when not waiting |
| `PUT checkout/{id}/company` | `{ purchaseOrderNumber, requestApproval }`; 400 with the reason on `approval` |
| `POST` / `DELETE checkout/{id}/account` | Put the rest on account, or take it off |

`ApiOrder` carries `companyId`, `purchaseOrderNumber`, `quote` and `approval`.

## Screens and permissions

- **Commerce > Companies** (admin route `companies`) and **Commerce > Price lists** (`companies/price-lists`), both on Orchard Core's `AdminList`: `ManageCompanies` (Administrator by default).
- Storefront `account/company` (`AccountCompany`) for buyers, with approvals for company admins.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| No company step | Buyer not in an enabled company, or the checkout is not an order |
| On-account option missing | `NetDays` unset, credit limit used up, a different currency, or a register sale |
| Payment refused *needs approval* | Order limit or monthly budget; ask for approval or raise the limit |
| Punchout 401 | Key, hook URL scheme, or the punchout user is not a buyer |

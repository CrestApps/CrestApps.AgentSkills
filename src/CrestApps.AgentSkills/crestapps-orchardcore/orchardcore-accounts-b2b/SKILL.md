---
name: orchardcore-accounts-b2b
description: Skill for the CrestApps Orchard Core Accounts B2B module (CrestApps.OrchardCore.Accounts.B2B), business selling on an Account content item. Covers B2BAccountPart and B2BAccount, buyers as account members and ship-to addresses as account locations, payment terms with a deposit, net days and credit limit through the account-terms tender, purchase order numbers, quotes, price lists through IBuyerPriceProvider, order limits, approvals and budgets, OCI punchout, the account/business page and API, the PriceLists recipe and deployment steps, the B2BAccounts report source, and the migration from the removed Companies module. Use this skill when requests mention B2B, wholesale or business buyers, net terms, purchase orders, price lists, order approvals or punchout. Strong matches include B2BAccountPart, B2BAccount, AccountBuyer, AccountTerms, AccountTermsTenderProvider, B2BAccountService, IB2BAccountStore, PriceList, B2BPriceProvider, B2BApprovalService, PunchoutService and ManageB2BAccounts.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Accounts B2B

## Sell to Businesses on Their Account

You are an Orchard Core expert. Generate accurate setup, recipes and extension guidance for the CrestApps Accounts B2B module. The business is an `Account` content item (see `orchardcore-accounts`); this feature adds `B2BAccountPart` to it. Its buyers are the account's members, the places it ships to are its locations, and it pays on terms, buys on a price list and sends large orders for approval.

### Guidelines

- Install `CrestApps.OrchardCore.Accounts.B2B` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- There is no separate Abstractions or Core package: `B2BAccountPart`, `B2BAccount`, `B2BAccountService`, `IB2BAccountStore`, `IPriceListStore` and `AccountTermsService` live in the module (`CrestApps.OrchardCore.Accounts.B2B.Models` and `.Services`). The price seam `IBuyerPriceProvider` is in `CrestApps.OrchardCore.Products.Core`.
- Add or remove a buyer by adding or removing the account **member**; buyers are read, never stored. Order limits are kept per user id on the part.
- Checkout steps, tenders and payment gates are covered by `orchardcore-checkout`; orders, quotes and `Order.Approval` by `orchardcore-orders`; terms invoices by `orchardcore-transactions`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.Accounts.B2B` (category Commerce) |
| Depends on | `CrestApps.OrchardCore.Accounts`, `CrestApps.OrchardCore.Accounts.Members`, `CrestApps.OrchardCore.Accounts.Locations`, `CrestApps.OrchardCore.Commerce`, `CrestApps.OrchardCore.Orders`, `CrestApps.OrchardCore.Transactions` |
| Lit up by | `CrestApps.OrchardCore.Shipping` (locations as ship-to), `CrestApps.OrchardCore.Checkout.Api` (API endpoints), `OrchardCore.Recipes.Core`, `OrchardCore.Deployment`, `CrestApps.OrchardCore.Recipes` (schemas), `OrchardCore.DataPipelines.Core` (report source) |
| Content part | `B2BAccountPart` (added to `Account` at position 10) |
| YesSql | `B2BAccountIndex` (default collection), `PriceList` in the `PriceLists` collection with `PriceListIndex` |
| Tender key and transaction source | `account-terms` |
| Permission | `ManageB2BAccounts` (Manage B2B accounts), Administrator |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Products",
        "CrestApps.OrchardCore.Carts",
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Shipping",
        "CrestApps.OrchardCore.Accounts.B2B"
      ],
      "disable": []
    }
  ]
}
```

## Model

| `B2BAccountPart` | Meaning |
|---|---|
| `Enabled` | The account buys as a business; while off, its buyers buy for themselves. |
| `TaxId`, `BillingEmail` | `TaxId` prints on invoices and makes `B2BCheckoutTaxProfileHandler` tax the buyer as `CustomerTaxType.B2B` (not at a register). |
| `OrderLimits` | `BuyerOrderLimit` (`UserId`, `Limit` in the store currency). Ignored for users who are no longer members. |
| `Terms` | `AccountTerms` (`NetDays`, `DepositPercent`, `CreditLimit`, `RequirePurchaseOrder`); `OffersTerms` is true when `NetDays >= 0`. |
| `PriceListId` | The `PriceList` its buyers pay, or none. |
| `MonthlyBudget` | Most its buyers may order in a calendar month, or none. |
| `Punchout` | `AccountPunchout` (`Enabled`, `UserId`, `KeyHash` SHA-256 hex, `KeyCreatedUtc`). |

`B2BAccount` wraps an account content item: `AccountId`, `Name` (title), `IsEnabled` (part on and version published), the part's values, `Buyers` (an `AccountBuyer` per member with `Role`, `OrderLimit`, `ContactContentItemId`) and `Locations`. Read it with:

- `IB2BAccountStore.FindByIdAsync(accountId)`, `FindByBuyerAsync(userId)` (through `IAccountMemberService`), `ListByPriceListAsync(priceListId)`, `SaveAsync(account)`.
- `B2BAccountService.GetForUserAsync(userId)` and `GetCurrentAsync()` (`(Account, Buyer)` of the signed-in user), which return only an enabled account and cache it for the request.

## Checkout, Terms and Purchase Orders

- **Business account step.** `B2BCheckoutHandler` adds a `business` step (order 3, after contact and shipping) to an account buyer's checkout. `B2BCheckoutStepService.SaveAsync(session, purchaseOrderNumber, requestApproval)` requires the PO number when `RequirePurchaseOrder` (64 characters at most) and writes it and `AccountId` on the order. An order that already has a PO number (such as a quote) starts with the step done.
- **Terms tender.** `AccountTermsTenderProvider` (key `account-terms`) puts `GrandTotal x (100 - DepositPercent) / 100` on terms, capped by the credit left, in the store currency only and never for a register sale. The tender reference is `"{accountId}:{sessionId}"`. The amount becomes a transaction due in `NetDays`, source `account-terms`, owned by the buyer, with parent reference `Account` and the account id.
- **Owed.** `AccountTermsService.ListInvoicesAsync(account)`, `GetOwedAsync(account)` and `GetAvailableCreditAsync(account)` read by parent reference, so what an account owes does not change when a buyer leaves.
- **Ship-to.** With Shipping on, `AccountLocationsShippingAddressSource` offers the account's locations on the Shipping step, labelled with the account name.
- **Orders.** `Order.AccountId` (`OrderIndex.AccountId`, `OrderQuery.AccountId`) is the account an order was placed for. Quotes are an Orders feature; a buyer's quote carries the PO number and the account.

## Price Lists

`PriceList` (`ItemId`, `Name`, `PercentOff` 0-100 or none, `Currency`, `Entries` of `PriceListEntry` `Sku` and `UnitPrice`, `CreatedUtc`, `ModifiedUtc`). `B2BPriceProvider` (an `IBuyerPriceProvider`) answers for the signed-in buyer's account: the SKU's entry (case-insensitive), converted with `IShopperCurrency` when the buyer shops in another currency, else `PercentOff` off the price. `DefaultPriceResolver` charges the lowest provider price below the store's; volume and role discounts do not stack on it, and recurring prices are left alone. Deleting a list clears it from every account.

Admin **Prices by SKU** takes `SKU,price` lines (comma, semicolon or tab), at most 5,000; a later line for a SKU wins.

## Approvals and Budgets

`B2BApprovalService.GetReasonAsync(account, buyer, total, currency)` returns why an order needs approval, or `null`: account admins never need it; a buyer's order over `OrderLimit`; or an order that takes this month's paid orders past `MonthlyBudget`. `RequestAsync` sets `Order.Approval` pending and emails the account admins (commerce email kind for approval requests); `DecideAsync(order, approve, adminName, note)` records it and emails the buyer, and a rejection cancels the order (`OrderCancelReason.Rejected`). `B2BApprovalPaymentGate` (`ICheckoutPaymentGate`) refuses payment without a covering approval, and `OrderApproval.Covers(total, currency)` makes a grown order need approval again.

## Punchout (OCI)

| Route | Does |
|---|---|
| `GET` or `POST punchout/oci/{accountId}` | Takes the key as `PASSWORD` or `key`, `HOOK_URL` and `~TARGET`; on success signs in the punchout buyer and sets a data-protected `punchout` cookie. Refuses (401) a wrong key, a non-business account, a non-https hook (http only on loopback), a non-member buyer or an `Administrator`. |
| `GET punchout/oci/transfer` (`PunchoutTransfer`) | Posts the OCI fields to the hook URL. |

`PunchoutCartActionProvider` adds **Transfer to your procurement system** to the cart; `PunchoutService.ToOciFields(pricing)` writes `NEW_ITEM-*` fields per line. Open punchout in a top-level window (the sign-in cookie is `SameSite=Lax`) and allow the hook's origin in a CSP `form-action`.

## Storefront, API and Admin

| Surface | Route | Access |
|---|---|---|
| **Business account** page (account menu item `account-business`) | `account/business`, `POST account/business/approvals/{orderId}` | An account buyer; approvals for its admins |
| Commerce API (`api/commerce/v1`, needs `CrestApps.OrchardCore.Checkout.Api`) | `GET account/business`, `GET account/business/approvals`, `POST account/business/approvals/{orderId}`, `PUT checkout/{id}/business`, `POST` and `DELETE checkout/{id}/account` | Bearer token, buyer |
| **Commerce > B2B Accounts** | Content list of account types with the part, `q=b2b:true` | `ManageB2BAccounts` (the list itself needs `ListContent`) |
| **Commerce > Price lists** | Admin `b2b/price-lists` (`Index`, `Create`, `Edit`, `Delete`) | `ManageB2BAccounts` |
| Account editor **Business** tab, page **Business** and **Invoices** tabs | `AccountPageTabs.Business`, `AccountPageTabs.Invoices` | `ManageB2BAccounts`; choosing buyers needs `ManageAccountMembers` |

The `b2b:` content list term filters any content list to accounts that buy as a business (`b2b:true`) or not (`b2b:false`). `ApiOrder` carries `accountId`, `purchaseOrderNumber`, `quote` and `approval`.

## Recipes and Deployment

Accounts (with `B2BAccountPart`) move with the `Accounts` or `Content` steps. Price lists have their own `PriceLists` step (needs `OrchardCore.Recipes.Core`), matched by `ItemId`, then by name (case-insensitive), so reruns update. A list without `Name` is a recipe error; an `ItemId` that is not a valid Orchard Core id is replaced with a new one; entries without a SKU are dropped. `PercentOff` is not range-checked by the step (the price provider ignores values outside 0-100).

```json
{
  "steps": [
    {
      "name": "PriceLists",
      "PriceLists": [
        {
          "ItemId": "4b7g3ke0q7mnvf3fbzbbyemqtn",
          "Name": "Wholesale",
          "PercentOff": 10,
          "Currency": "USD",
          "Entries": [ { "Sku": "HAT-22069", "UnitPrice": 24.5 } ]
        }
      ]
    }
  ]
}
```

The **Price Lists** deployment step (`PriceListsDeploymentStep`, category **Commerce**, no options) exports every list in this shape. A B2B account with terms and a price list, and a deployment plan, are in [references/b2b-recipes-and-extensions.md](references/b2b-recipes-and-extensions.md).

## Report Builder

With `OrchardCore.DataPipelines.Core` on, the **B2B accounts** source (`B2BAccounts`) adds the `B2BAccounts`, `B2BBuyers`, `PriceLists` and `PriceListEntries` data sets, each requiring `ManageB2BAccounts`. The Orders data set's `AccountId` joins to `B2BAccounts`. The punchout key is never exposed. See `orchardcore-report-builder`.

## Extension Points

| Seam | Use it to |
|---|---|
| `IBuyerPriceProvider` (Products.Core) | Add another buyer-specific price, such as a contract price per account. |
| `B2BAccountService`, `IB2BAccountStore` | Read the current buyer's account, its terms and buyers. |
| `AccountTermsService` | Read what an account owes and its available credit. |
| `DisplayDriver` on the account (`ContentDisplayDriver`, `AccountPageTabs`) | Add a card to the Business tab or a tab of your own. |

```csharp
public sealed class ContractPriceProvider : IBuyerPriceProvider
{
    private readonly B2BAccountService _accounts;

    public ContractPriceProvider(B2BAccountService accounts)
    {
        _accounts = accounts;
    }

    public async ValueTask<decimal?> GetUnitPriceAsync(BuyerPriceContext context, CancellationToken cancellationToken = default)
    {
        var (account, _) = await _accounts.GetCurrentAsync(cancellationToken);

        // Accounts on 60-day terms get 3% off everything; the lowest provider price wins.
        return account?.Terms.NetDays >= 60
            ? Math.Round(context.UnitPrice * 0.97m, 2)
            : null;
    }
}

// Startup: services.AddScoped<IBuyerPriceProvider, ContractPriceProvider>();
```

More examples are in [references/b2b-recipes-and-extensions.md](references/b2b-recipes-and-extensions.md).

## Migrating from the Companies Module

The Companies module was removed; a business is now an account with `B2BAccountPart`.

| Companies | Accounts B2B |
|---|---|
| `Order.CompanyId`, `OrderIndex.CompanyId`, `OrderQuery.CompanyId` | `AccountId` |
| `ApiOrder.companyId` | `accountId` |
| Routes `account/company`, `checkout/{id}/company` | `account/business`, `checkout/{id}/business` |
| The company terms tender | Tender key `account-terms` |
| Permission `ManageCompanies` | `ManageB2BAccounts` |
| Report Builder **Companies** source | **B2B accounts** (`B2BAccounts`, with `B2BAccounts` and `B2BBuyers` data sets) |
| Company buyers and locations | Account members (`ManageAccountMembers`) and account locations |

The Orders migration (`UpdateFrom2Async`) adds `OrderIndex.AccountId`; the old company column is left unread and dropped in a later release. No migration in the module converts company records into accounts, so recreate businesses as accounts (for example with the `Accounts` step) and set `Order.AccountId` on orders that must keep their business.

## Gotchas

| Symptom | Check |
|---|---|
| No Business step at checkout | The buyer is not a member of an account, `Enabled` is off, or the account version is not published. |
| Terms not offered | `NetDays` unset, credit limit used up, a currency other than the store's, or a register sale. |
| Price list ignored | The buyer's account has no `PriceListId`, the SKU is not listed and `PercentOff` is empty, or the list currency cannot be converted. |
| Approval email not sent | No account member has the `Admin` role (the request is emailed to admin buyers' emails), or e-mail is not configured. |
| Punchout returns 401 | Wrong key, `HOOK_URL` not https, punchout user not a member or is an Administrator, or the account does not buy as a business. |
| API routes 404 | `CrestApps.OrchardCore.Checkout.Api` is not enabled; the paths are under `api/commerce/v1`. |
| User cannot be added as buyer | They are already a member of another account. |
| Odd terms after an import | Recipes skip the editor's checks (`NetDays` 0-365, `DepositPercent` 0-100, no negative limits, punchout buyer a non-admin member); keep imported values in range. |

Related skills: `orchardcore-accounts`, `orchardcore-orders`, `orchardcore-checkout`, `orchardcore-transactions`, `orchardcore-customers`, `orchardcore-addresses`, `orchardcore-products`, `orchardcore-report-builder`.

---
name: orchardcore-customers
description: Skill for the CrestApps Orchard Core Customers module (CrestApps.OrchardCore.Customers, Customers.CheckoutDetails, Customers.StoreCredit, Customers.Loyalty, Customers.Referrals, Customers.Api) and the Customers.Abstractions buyer identity foundation. Covers the Customer record linked to a user or a guest, CustomerOwner and CustomerOwnerKind, ICustomerContactResolver, ICustomerService, the address book and IShippingAddressBook, the checkout Contact step, Checkout details fields, store credit, loyalty points and referrals as tenders, merging verified guests, privacy erase, export and retention with ICustomerPrivacyHandler and the site-wide IPersonalDataService, the Customers, StoreCreditAccounts, LoyaltyAccounts and ReferralProfiles recipe and deployment steps, the Report Builder data source and the Customers API. Use it when requests mention commerce customers, guest buyers, address books, store credit, rewards points, referral links, personal data requests, or the checkout contact step.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Customers

A customer is everyone who buys: a person or a business, with an account or as a guest. A customer is not a user; sign-in, registration and passwords stay with Orchard Core Users, and `Customer.UserId` links the two. Orders, transactions and subscriptions point at one customer whatever later happens to the account.

Related skills: `orchardcore-checkout` (steps, tenders), `orchardcore-orders` (orders, gift cards), `orchardcore-addresses` (the `Address` value and editor), `orchardcore-transactions` (guest transactions, reminders), `orchardcore-commerce` (personal data requests, API conventions, webhooks), `orchardcore-taxation` (tax-exempt customers).

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Customers` | Customers | Commerce, `OrchardCore.Users`, Addresses |
| `CrestApps.OrchardCore.Customers.CheckoutDetails` | Checkout details | Customers, Orders, Contents, Content Types |
| `CrestApps.OrchardCore.Customers.StoreCredit` | Store Credit | Customers, Orders |
| `CrestApps.OrchardCore.Customers.Loyalty` | Loyalty Points | Customers, Orders |
| `CrestApps.OrchardCore.Customers.Referrals` | Referrals | Customers, Orders |
| `CrestApps.OrchardCore.Customers.Api` | Customers API | Customers |

Constants: `CustomersConstants.Features`, `ContactStepKey` (`Contact`), `CheckoutDetailsContentType`, settings groups `customers`, `loyalty`, `referrals`. Install `CrestApps.OrchardCore.Customers` in the web project.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Customers",
        "CrestApps.OrchardCore.Customers.StoreCredit",
        "CrestApps.OrchardCore.Customers.Loyalty",
        "CrestApps.OrchardCore.Customers.Referrals"
      ],
      "disable": []
    }
  ]
}
```

## The buyer identity foundation

`CrestApps.OrchardCore.Customers.Abstractions` is used even without the Customers feature:

- `CustomerOwner` (`CustomerOwner.ForUser(userId)`, `CustomerOwner.ForGuest(guestCustomerId)`) and `CustomerOwnerKind` (`Authenticated`, `Guest`), in `CrestApps.OrchardCore.Customers.Models`. Transactions and subscriptions store `OwnerId` plus `OwnerKind`.
- `ICustomerContactResolver.ResolveAsync(owner, guestContact)` and `ICustomerOwnerNotifier.NotifyAsync(owner, guestContact, message)`: a user through `INotificationService`, a guest by email. `AddCustomersCore()` registers the defaults; the Customers feature replaces the resolver with `CustomerRecordContactResolver` (name from the customer record, email from the account).
- `ICommercePersonalDataService` (`ExportAsync`, `AnonymizeAsync`, `ExpireAsync` by owner and email) with `ICommercePersonalDataContributor` per feature (`IdentifyAsync`, `ExportAsync`, `AnonymizeAsync`, `TakesPartInRetentionExpiry`, `true` by default and `false` for Fundraising, Memberships, Marketplace and Reviews). It is one contributor (`CommercePersonalDataContributor`) of the site-wide `IPersonalDataService` (`CrestApps.OrchardCore.PersonalData.Abstractions`, namespace `CrestApps.OrchardCore.PersonalData`), which also reaches the CRM, Workforce, Scheduling and other modules. A new module implements `IPersonalDataContributor` and registers it with `services.AddPersonalDataContributor<T>()`; use `ICommercePersonalDataContributor` only when it needs the commerce subject (guest owners, checkout sessions, payment-provider customers).
- `IShippingAddressBook` (`CrestApps.OrchardCore.Addresses.Abstractions`) is how Shipping reads and saves the buyer's address book; the Customers feature implements it on the customer record (`CustomerShippingAddressBook`), so Shipping never references Customers.

## The customer record

`Customer` (`CrestApps.OrchardCore.Customers.Core.Models`, a `CatalogItem` in collection `Customers`): `Kind` (`Person`, `Business`), `Status` (`Active`, `Disabled`, `Anonymized`; merged guests are `Merged`), `UserId`, `DisplayName`, `FirstName`, `LastName`, `CompanyName`, `TaxId`, `IsTaxExempt`, `TaxExemptionReason`, `Email`, `Phone`, `AcceptsMarketing`, `ExternalIds`, activity dates, and up to 20 `Addresses` (`CustomerAddress`: `AddressId`, `Label`, `Address`) with `DefaultBillingAddressId` and `DefaultShippingAddressId`. `GetName()`, `GetDefaultBillingAddress()`, `ToAddress()` (a copy for an order).

| Service (`CrestApps.OrchardCore.Customers.Core.Services`) | Members |
| --- | --- |
| `ICustomerStore` | `FindByIdAsync`, `FindByUserIdAsync`, `ListGuestsByEmailAsync`, `CreateAsync`, `UpdateAsync` (concurrency checked), paging with `CustomerQuery` |
| `ICustomerService` | `GetOrCreateForUserAsync(userId)` (locked per user), `FindForUserAsync`, `CreateGuestAsync(name, email, phone)`, `TouchAsync` |
| `ICustomerHandler` | `CreatedAsync(customer)` |
| `ICustomerMergeService` / `ICustomerMergeHandler` | `MergeVerifiedGuestsAsync(userId)` / `MergingAsync(CustomerMergeContext)` |
| `ICustomerPrivacyService` / `ICustomerPrivacyHandler` | `EraseAsync`, `ExportAsync`, `ExpireAsync` / `ErasingAsync`, `ExportingAsync`, `Order` |

```csharp
using CrestApps.OrchardCore.Customers.Core.Services;

public sealed class RegistrationCustomerLink
{
    private readonly ICustomerService _customers;

    public RegistrationCustomerLink(ICustomerService customers)
    {
        _customers = customers;
    }

    public async Task<string> GetCustomerIdAsync(string userId)
        => (await _customers.GetOrCreateForUserAsync(userId)).ItemId;
}
```

## Checkout Contact step

With Orders, `CustomerContactCheckoutHandler` adds the **Contact** step to every order checkout (after the hidden order step): name and email required, phone, and a billing address per `CustomerCheckoutSettings.BillingAddress` (`Optional` = 0, the default, `Off` = 1, `Required` = 2; **Settings > Commerce > Customers**). It stores `CustomerContactStep` in `SavedSteps` and a `CheckoutContactInfo` on the session; on completion the order is linked to the signed-in customer or a new guest customer, and a guest's billing address becomes their default. A signed-in buyer with `Off` never sees the step.

**Checkout details** creates a field-less `CheckoutDetails` content type; add fields to it (gift message, delivery date) and they are asked on the Contact step, validated like any editor, kept on `Order.CheckoutDetails` and shown on the order pages.

## Store credit, loyalty points and referrals

All three pay toward checkouts as tenders (after tax) for signed-in buyers only. Summary:

| Feature | Tender key | Settings | Service |
| --- | --- | --- | --- |
| Store Credit | `store-credit` | none | `StoreCreditService.ChangeAsync(customerId, currency, amount, reason, actor, sessionId)` |
| Loyalty Points | `loyalty-points` | `LoyaltySettings` (**Settings > Commerce > Rewards**) | `LoyaltyService.ChangeAsync(customerId, points, kind, reason, actor, orderId)` |
| Referrals | rewards in credit or points | `ReferralSettings` (**Settings > Commerce > Referrals**) | `ReferralService`, link `refer/{code}` |

Details, settings JSON and the account pages are in [references/store-credit-loyalty-referrals.md](references/store-credit-loyalty-referrals.md).

## Merging guests, privacy and retention

- `ICustomerMergeService.MergeVerifiedGuestsAsync` runs on sign-in, registration and email confirmation, only for a confirmed email: each active guest with that email is merged through every `ICustomerMergeHandler` (Orders moves orders and their guest transactions), addresses are copied, and the guest is marked `Merged`. Resolve the merge service lazily in user event handlers (it depends on the user service).
- `EraseAsync(customer, by)` runs every `ICustomerPrivacyHandler.ErasingAsync` (lower `Order` first; `PersonalDataCustomerPrivacyHandler` is `-100`), then `Customer.Anonymize`. The Orders module's `OrderCustomerPrivacyHandler` anonymizes orders and their checkout sessions (`ICheckoutSessionAnonymizer`); `PersonalDataCustomerPrivacyHandler` (formerly `CommercePersonalDataCustomerPrivacyHandler`) identifies the customer by account, email and phone and calls `IPersonalDataService`, so an erasure reaches every module with a contributor (commerce, CRM, Workforce, Scheduling, Bookings, Marketing and more). `ExportAsync` returns a `JsonObject` with each handler's section; the personal data section is now `personalData` (each record with `kind`, `title`, `fields`) instead of `commerce`. Each action is logged on `Customer.PrivacyLog`.
- Handlers read `CustomerPrivacyContext.Action` (`CustomerPrivacyAction`: `Exported`, `Erased`, `Expired`) to tell an admin erasure from the retention expiry. On `Expired`, `IPersonalDataService.ExpireAsync` runs only contributors whose `TakesPartInRetentionExpiry` is `true` (the commerce records and stock alerts), so a guest's CRM, Workforce and Scheduling records are left alone; only an explicit erasure reaches them.
- `CustomerRetentionBackgroundTask` (daily 03:30): `CustomerPrivacySettings.InactiveGuestMonths` (24) expires inactive guests; `GuestOrderMonths` (12) anonymizes old guest records through each module's `IGuestRetentionHandler` (Orders implements it as `OrderGuestRetentionHandler`: paid or canceled guest orders with their checkout sessions); zero turns a window off; 500 of each per run.
- Admin `POST customers/{customerId}/erase` and `GET customers/{customerId}/export` need `ManageCustomers`.

```csharp
using CrestApps.OrchardCore.Customers.Core.Services;

public sealed class RegistrationPrivacyHandler : ICustomerPrivacyHandler
{
    public Task ErasingAsync(CustomerPrivacyContext context, CancellationToken cancellationToken = default)
    {
        // context.Customer and context.UserId (the account before it is unlinked): clear your registrations.
        return Task.CompletedTask;
    }

    public Task ExportingAsync(CustomerExportContext context, CancellationToken cancellationToken = default)
    {
        context.Export["registrations"] = new System.Text.Json.Nodes.JsonArray();

        return Task.CompletedTask;
    }
}

// services.AddScoped<ICustomerPrivacyHandler, RegistrationPrivacyHandler>();
```

## Recipes

The `Customers` step adds or updates guest and business customers (matched by `ItemId`, then by a guest's email; account customers come from their user). The **Customers** deployment step (`CustomersDeploymentStep`) writes guest and business customers in the same shape; it is personal data, so an administrator must add it to a plan, and customers with a user account or that were erased or merged are left out.

Store credit, loyalty points and referral codes have their own steps, each with a deployment step (**Store credit**, **Loyalty points**, **Referral codes**). Records are matched by `ItemId`, then by customer (and currency for store credit), and carry their balance or points and activity. Import customers first.

| Feature | Recipe step | Array | Matched by |
| --- | --- | --- | --- |
| Store Credit | `StoreCreditAccounts` | `Accounts` | `ItemId`, then `CustomerId` and `Currency` |
| Loyalty Points | `LoyaltyAccounts` | `Accounts` | `ItemId`, then `CustomerId` |
| Referrals | `ReferralProfiles` | `Profiles` | `ItemId`, then `CustomerId`; a `Code` another customer uses is refused |

```json
{
  "steps": [
    {
      "name": "Customers",
      "Customers": [
        {
          "Kind": "Business",
          "CompanyName": "Contoso Supplies",
          "TaxId": "123456789RT0001",
          "Email": "purchasing@contoso.example",
          "Addresses": [
            {
              "AddressId": "contoso-office",
              "Label": "Office",
              "Address": { "Company": "Contoso Supplies", "AddressLine1": "100 King Street West", "City": "Toronto", "Region": "ON", "PostalCode": "M5X 1C7", "Country": "CA" }
            }
          ],
          "DefaultBillingAddressId": "contoso-office",
          "DefaultShippingAddressId": "contoso-office"
        }
      ]
    },
    {
      "name": "StoreCreditAccounts",
      "Accounts": [
        { "CustomerId": "4cs00000000000000000000001", "Currency": "USD", "Balance": 25, "Activity": [] }
      ]
    },
    {
      "name": "Settings",
      "CustomerCheckoutSettings": { "BillingAddress": 2 },
      "CustomerPrivacySettings": { "InactiveGuestMonths": 24, "GuestOrderMonths": 12 }
    }
  ]
}
```

## Report Builder data source

With **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) enabled, as the Report Builder does, Customers registers the **Customers** data source (`Customers`). Each data set is listed only while its feature is on. Tax ids, street lines, the privacy log and referral codes are never exposed; join the Orders data source on **Customer ID** for what customers bought. See `orchardcore-report-builder`.

| Data set | Feature | Permission |
| --- | --- | --- |
| `Customers` | Customers | `ViewCustomers` |
| `LoyaltyAccounts`, `LoyaltyActivity` | Loyalty Points | `ViewCustomers` |
| `StoreCreditAccounts`, `StoreCreditActivity` | Store Credit | `ViewCustomers` |
| `Referrals`, `ReferralRewards` | Referrals | `ManageCustomers` |

## API

Customers API (`api/commerce/v1`): `me` and `me/addresses` (`ManageOwnCustomerProfile`), `customers` and `customers/{customerId}` (`ViewCustomers`), with Orders and the Checkout API `PUT checkout/{id}/contact`, and with Store Credit or Loyalty `checkout/{id}/store-credit` and `checkout/{id}/points`. With Commerce Webhooks, a `customer.created` event is published.

## Screens and permissions

| Permission | Grants |
| --- | --- |
| `ManageCustomers` | Add, edit, export, erase; Customers settings; give or take credit and points; Rewards and Referrals settings. Implies `ViewCustomers` |
| `ViewCustomers` | List and open customers |
| `ManageOwnCustomerProfile` | **My Account** (`my-account`, a `[CustomerAdminPage]`) and storefront `account/details`, `account/addresses`; Authenticated by default |

**Commerce > Customers** lists customers on Orchard Core's `AdminList` with *Active*, *With an account*, *Guests*, *Businesses*, *Disabled* filters. Modules add sections to a customer's admin page with a `DisplayDriver<Customer>` for display type `DetailAdmin`, placed in `Content`.

## Upgrading from 2.x

New in 3.0. Transactions moved from a null owner to `CustomerOwner` with `OwnerKind`; a guest transaction is never shown to an account sharing its id.

- `CommercePersonalDataCustomerPrivacyHandler` is now `PersonalDataCustomerPrivacyHandler` over the site-wide `IPersonalDataService`; the export section is `personalData`, not `commerce`.
- `OrderCustomerMergeHandler`, `OrderCustomerPrivacyHandler` and `OrderGuestRetentionHandler` live in the Orders module (registered when Customers is on).
- `PhoneNumberNormalization.ToE164OrTrimmed`, used by the customer store, the Contact step and the store profile, now stores the E.164 form only when it keeps everything typed; a number with an extension (`x12`, `ext. 12`, `#12`), a second number or a note is kept as typed.

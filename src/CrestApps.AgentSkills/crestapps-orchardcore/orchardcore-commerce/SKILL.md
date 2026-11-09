---
name: orchardcore-commerce
description: Skill for the CrestApps Orchard Core Commerce suite hub. Covers picking the right commerce features, the dependency-only Commerce feature (CrestApps.OrchardCore.Commerce) with its shared Commerce admin menu, storefront account menu, CustomerAdminPage attribute, financial-document policy and personal data service, the store setup guide with IStoreSetupStep and staff role presets, the Online store and Commerce starter recipes, the Storefront theme, the commerce JSON API at api/commerce/v1 (bearer tokens, guest tokens, Idempotency-Key, X-Market), outgoing Commerce Webhooks on the Webhooks building block (IWebhookPublisher, AddWebhookEvent, HMAC-SHA256 signatures), the Store Profile, store dashboard widgets and Report Builder data sources, web farm hosting and the extension seam map. Use it when requests mention Orchard Core commerce, an online store, headless commerce, commerce webhooks, store setup, the store profile, CommerceConstants, personal data requests or which commerce module to extend.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Commerce Suite

The CrestApps commerce modules (new in CrestApps.OrchardCore 3.0) let an Orchard Core site sell goods, downloads, plans and sites. They are separate modules with one-way dependencies, so a site enables only what it needs. This skill is the hub: it maps the suite, covers the shared **Commerce** feature, store setup, the JSON API and webhooks, and says which seam to implement.

## How the suite fits together

| Layer | Module (feature ID) | Depends on | Skill |
| --- | --- | --- | --- |
| Shell | Commerce (`CrestApps.OrchardCore.Commerce`, dependency only) | none | `orchardcore-commerce` |
| Shell | Commerce Webhooks (`CrestApps.OrchardCore.Commerce.Webhooks`) | Commerce, Webhooks | `orchardcore-commerce`, `orchardcore-webhooks` |
| Shell | Store Profile (`CrestApps.OrchardCore.Commerce.StoreProfile`) | Commerce, Addresses | `orchardcore-commerce` |
| Catalog | Products (`CrestApps.OrchardCore.Products`), Product Listing and sub-features | Commerce | `orchardcore-products` |
| Catalog | Markets (`CrestApps.OrchardCore.Markets`) | Commerce, Products | `orchardcore-markets` |
| Buying | Carts (`CrestApps.OrchardCore.Carts`) | Commerce, Products | `orchardcore-carts` |
| Buying | Checkout (`CrestApps.OrchardCore.Checkout`), coupons, tenders | Commerce | `orchardcore-checkout` |
| Buying | Orders (`CrestApps.OrchardCore.Orders`), Gift Cards, Returns | Carts, Checkout | `orchardcore-orders` |
| Fulfilment | Inventory (`CrestApps.OrchardCore.Inventory`) | Commerce, Products | `orchardcore-inventory` |
| Fulfilment | Shipping (`CrestApps.OrchardCore.Shipping`) | Products, Orders, Addresses | `orchardcore-shipping` |
| Money | Payments and Stripe, Pay Later, Transactions, Receipts | Checkout | `orchardcore-payments`, `orchardcore-pay-later`, `orchardcore-transactions`, `orchardcore-receipts` |
| Selling plans | Subscriptions, Memberships | Products, Checkout | `orchardcore-subscriptions`, `orchardcore-memberships` |
| Buyers | Customers; Accounts B2B (`CrestApps.OrchardCore.Accounts.B2B`, replaces Companies) | Commerce | `orchardcore-customers` |
| Cross-cutting | Taxation (`CrestApps.OrchardCore.Taxation`), TaxJar | Commerce | `orchardcore-taxation` |
| Cross-cutting | Addresses (`CrestApps.OrchardCore.Addresses`) | none | `orchardcore-addresses` |

A purchase always flows the same way: a source (cart order, plan, transaction) starts a checkout session through `ICheckoutEngine` with a `ReferenceType` and `ReferenceId`; the buyer completes the steps; a payment provider takes the money into the durable payment ledger; the checkout settles and handlers finish the purchase. Details of that path are in `orchardcore-checkout` and `orchardcore-payments`.

### Features for each kind of store

| Store | Enable |
| --- | --- |
| Goods you ship | Products, Product Listing, Carts, Orders, Customers, Inventory, Shipping, Checkout, a payment provider; optionally Taxation, Product Reviews, Reports |
| Files to download | Products, Product Listing, Carts, Orders, Customers, Digital Downloads, Checkout, a payment provider |
| Recurring plans | Products, Product Listing, Checkout, Transactions, Subscriptions, a payment provider |
| Headless storefront | The above plus the API features `Products.Api`, `Carts.Api`, `Checkout.Api`, `Orders.Api`, `Customers.Api`, `Shipping.Api`, `Inventory.Api` |

**Test payments** (`CrestApps.OrchardCore.Checkout.TestProvider`) confirms every payment without moving money and only registers in Development unless `CrestApps:Checkout:TestProvider:AllowOutsideDevelopment` is `true`.

### Guidelines

- Install each module package (for example `CrestApps.OrchardCore.Products`, `CrestApps.OrchardCore.Orders`) in the web/startup project.
- Never enable `CrestApps.OrchardCore.Commerce` directly; it is `EnabledByDependencyOnly` and comes on with any commerce module.
- Depend on the feature that owns a seam in your manifest, and register implementations in a `Startup` behind `[RequireFeatures]` when the dependency is optional.
- Money is `decimal` everywhere. Read amounts posted by forms in the invariant culture.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

## The Commerce feature

Commerce is a composition shell: no persistence, indexes, migrations or domain models. It owns:

- **The Commerce admin menu.** Add screens under it from your module with an `AdminNavigationProvider`, keeping `.AddClass("commerce").Id("commerce")` so the node merges and keeps its icon.
- **The storefront account** at `account` (route `CommerceAccount`), the `commerce-account` menu (`CommerceConstants.Account.MenuName`) and the `CommerceAccountPage` frame shape.
- **`[CustomerAdminPage]`** (`CrestApps.OrchardCore.Commerce.CustomerAdminPageAttribute`), which lets any signed-in user open an admin page meant for customers. The marker opens the page, not the data: always check the customer's own permission and filter to their records.
- **The financial-document policy** default, `ReceiptsOnlyFinancialDocumentPolicy` (`IFinancialDocumentPolicy`, in `CrestApps.OrchardCore.Transactions.Abstractions`).
- **`ICommercePersonalDataService`** for GDPR and CCPA export, anonymize and retention expiry, composed from every `ICommercePersonalDataContributor`. `CommercePersonalDataContributor` plugs it into the site-wide `IPersonalDataService` (`CrestApps.OrchardCore.PersonalData.Abstractions`), so one privacy request reaches commerce, the CRM, Workforce and Scheduling together. New modules implement `IPersonalDataContributor` (`services.AddPersonalDataContributor<T>()`); records without a typed export list go in `CommercePersonalDataExport.Records` as `CommercePersonalDataRecord` entries.
- **`IStoreSetupStep`** and the **Commerce > Set up your store** guide (`commerce/setup`, permission `SetUpStore`).

```csharp
[assembly: Feature(
    Id = "MyCompany.GiftWrap",
    Name = "Gift Wrap",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
    ]
)]
```

```csharp
public sealed class AdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .AddClass("commerce")
                .Id("commerce")
                .Add(S["Gift wrap"], S["Gift wrap"].PrefixPosition(), item => item
                    .Action("Index", "Admin", "MyCompany.GiftWrap")
                    .Permission(GiftWrapPermissions.ManageGiftWrap)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
```

### Add a page to the storefront account

```csharp
public sealed class AccountMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public AccountMenu(IStringLocalizer<AccountMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (name == CommerceConstants.Account.MenuName)
        {
            builder.Add(S["Gift wrap"], "60", item => item
                .Id("account-gift-wrap")
                .AddClass("fa-solid fa-gift")
                .Action("Index", "AccountGiftWrap", new { area = "MyCompany.GiftWrap" }));
        }

        return ValueTask.CompletedTask;
    }
}
```

The account view renders `await New.CommerceAccountPage(Title: T["Gift wrap"], Content: Model)`, which draws the account menu beside the content.

### Commerce admin lists

Commerce lists follow Orchard Core's `AdminList` standard (see `orchardcore-admin-lists`): an `AdminListContext<T>` with a display driver for both `SummaryAdmin` and `SummaryAdminGrid`. Toolbar filter selects come from `IShapeFactory.CreateAdminListFiltersAsync(listName, formId, filters)`, which builds the shared `CommerceAdminListFilters` shape; a theme overrides one list's filters with `CommerceAdminListFilters-{ListName}.cshtml`.

## Store setup and staff roles

**Commerce > Set up your store** shows one step per enabled module. Add your own step:

```csharp
public sealed class GiftWrapSetupStep : IStoreSetupStep
{
    internal readonly IStringLocalizer S;

    public GiftWrapSetupStep(IStringLocalizer<GiftWrapSetupStep> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Name => "gift-wrap";

    public int Order => 65;

    public Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new StoreSetupStepDescription
        {
            Title = S["Offer gift wrapping"],
            Description = S["Choose the price of gift wrapping."],
            IsOptional = true,
            IsDone = null,
            Url = "~/Admin/gift-wrap",
            UrlText = S["Gift wrap settings"],
        });
}
```

Register it with `services.AddScoped<IStoreSetupStep, GiftWrapSetupStep>();`. Built-in steps are 10 apart (store details 10, currency 20, Stripe 30, offline payments 35, shipping 40, taxes 50, products 60, test order 80, staff roles 90).

Staff role presets live in `CommerceConstants.StaffRoles`: `Store manager`, `Warehouse`, `Customer support`, `Marketing`. Each module grants permissions to them through `PermissionStereotype`s returned by `GetDefaultStereotypes()`; declare your module's staff permissions the same way.

## Store profile

**Store Profile** (`CrestApps.OrchardCore.Commerce.StoreProfile`, `CommerceConstants.Features.StoreProfile`; depends on Commerce and Addresses) adds **Settings > Commerce > Store** (`Admin/Settings/store-profile`, permission `ManageStoreProfile`, granted to administrators and store managers). It keeps the store's `Name` (the site name when empty), `LogoUrl`, `TaxId`, `Email`, `Phone` (E.164 when it parses) and `Address` in `StoreProfileSettings` (Commerce.Abstractions, namespace `CrestApps.OrchardCore.Commerce.Stores`). The **Store profile** deployment step exports it; Orchard Core's `Settings` recipe step imports it. The Online store recipe enables the feature.

```json
{
  "steps": [
    {
      "name": "Settings",
      "StoreProfileSettings": {
        "Name": "Contoso Outfitters",
        "Email": "hello@contoso.example",
        "Phone": "+14165550100",
        "Address": { "AddressLine1": "100 King Street West", "City": "Toronto", "Region": "ON", "PostalCode": "M5X 1C7", "Country": "CA" }
      }
    }
  ]
}
```

Receipts (business name, logo, address, email, phone), the Shipping ship-from address and the Taxation tax origin fall back to the profile when their own settings are empty. The fallback reads the settings directly (`ISiteService.GetStoreProfileAsync()`), so it works whether or not the feature is on. `IStoreLocationService` (Commerce.Core, `services.AddStoreLocations()`) returns the profile, the store address (`GetAddressAsync`, `GetAddressTextAsync`) and every place the store works from (`ListLocationsAsync`: the store's own address as `store`, then the `NamedAddress`es each `IStoreLocationSource` adds).

## Dashboard widgets and Report Builder data sources

- With Orchard Core's Admin Dashboard and Reports on, the commerce modules ship ready-made report widgets (Sales Overview, Top Selling Products, Orders to Ship, Low Stock, Product Reviews, Shipping Times, Digital Downloads, Subscription Revenue and more); any report can also be a widget, and the **Sales Summary** and **Subscription Summary** widgets keep their figures for 30 minutes by default. See `orchardcore-reports`.
- With **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) on, as the Report Builder does, each module registers its own data source: `Products`, `Carts`, `Checkout`, `Orders`, `Customers`, `Transactions`, `Inventory`, `Shipping`, `Subscriptions`, `Reviews`, `Downloads`, `Marketplace`, `PointOfSale` and `B2BAccounts`. Each data set uses the permission of the module's own screens, and identifier fields join across sources. The `CommerceReportBuilderReports`, `InventoryReportBuilderReports`, `ShippingReportBuilderReports`, `SubscriptionsReportBuilderReports`, `ReviewsReportBuilderReports` and `DownloadsReportBuilderReports` recipes add editable copies of the built-in reports to the **Store** report group. See `orchardcore-report-builder`.

## Recipes and the Storefront theme

| Recipe | Kind | What it does |
| --- | --- | --- |
| `OnlineStore` (Online store) | Setup recipe | Enables content features, Products, Listing, Carts, Checkout, Orders, Customers, Transactions, Receipts, Inventory, Shipping, Taxation, Stripe, Pay Later, the Storefront theme; defines a `Product` type and a `/shop` listing home page |
| `CommerceStarter` (Commerce starter) | Recipe | Enables Products, Listing, Checkout, Transactions, Pay Later, Subscriptions, Roles, Workflows; creates a `Member` role, a `MembershipPlan` type and a `/plans` listing |
| Product listing samples | Recipes in Products | Online store, real estate and vehicle samples (see `orchardcore-products`) |

The **Storefront theme** (`CrestApps.OrchardCore.StorefrontTheme`, base theme TheTheme) restyles through `--storefront-accent`, `--storefront-header-bg` and `--storefront-footer-bg`. Every storefront page renders through a named shape, so override it in a theme or with Orchard Core Templates instead of replacing controller views. See [references/storefront-and-operations.md](references/storefront-and-operations.md) for the shape list, background tasks, backups, web farms and throttling.

## The commerce JSON API

Each module maps endpoints into `api/commerce/v1` from its own API feature. Guests need no token; everything else uses an OpenID Connect bearer token from the site's own server (the `Api` authentication scheme). Cookies are never used.

| Header | Use |
| --- | --- |
| `Authorization: Bearer ...` | Signed-in buyer or staff/server token |
| `X-Cart-Token` | Guest cart, returned when the cart is created |
| `X-Checkout-Token` | Guest checkout, returned by `POST checkout` |
| `Idempotency-Key` | Safe retries on place, pay, refund and stock calls (24 hours, per caller and endpoint) |
| `X-Market` or `?market=` | The buyer's market on every call when Markets is on |

Errors are RFC 9457 problems; `409` carries a `code` such as `out-of-stock`, `not-available`, `invoice-changed`, `concurrency`, `unsupported` or `idempotency-in-progress`. See [references/commerce-api.md](references/commerce-api.md) for setup (OpenID, CORS, OpenAPI), the guest checkout call by call and the endpoint list.

## Commerce Webhooks

`CrestApps.OrchardCore.Commerce.Webhooks` posts signed JSON to subscribed URLs. Subscriptions, signing, the outbox and the admin (**Configuration > Webhooks**, permission **Manage webhooks**, stored name `ManageCommerceWebhooks`) belong to the generic Webhooks building block (`CrestApps.OrchardCore.Webhooks`, see `orchardcore-webhooks`), which Commerce Webhooks turns on; Commerce Webhooks is the switch the commerce modules gate their events on. Deliveries are queued in the same unit of work as the change, sent after commit, retried with backoff (1 minute up to 24 hours, failed after the eighth attempt) and logged. Built-in events are in `CommerceWebhookEvents`: `order.created`, `order.paid`, `order.canceled`, `order.refunded`, `customer.created`, `shipment.shipped`, `shipment.delivered`, `stock.changed`, `stock.low`, `stock.sold-out`, `webhook.ping`.

Publish your own event from a startup that only runs while webhooks are on:

```csharp
[RequireFeatures(CommerceConstants.Features.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IOrderEventHandler, GiftWrapWebhookHandler>();
        services.AddWebhookEvent("giftwrap.requested", "Gift wrap requested", "Gift wrap");
    }
}

public sealed class GiftWrapWebhookHandler : OrderEventHandlerBase
{
    private readonly IWebhookPublisher _publisher;

    public GiftWrapWebhookHandler(IWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => _publisher.PublishAsync("giftwrap.requested", new { orderId = context.Order.ItemId }, cancellationToken);
}
```

`IWebhookPublisher` and `AddWebhookEvent` are in `CrestApps.OrchardCore.Webhooks.Abstractions` (namespace `CrestApps.OrchardCore.Webhooks`); the publisher sends only events an enabled feature declared. Pass a stable event id with `PublishAsync(eventName, payload, eventId)` when the same event can be raised twice. Signatures, receivers and the delivery model are in [references/commerce-webhooks.md](references/commerce-webhooks.md).

## Which seam to implement

| To | Implement | Lives in | Skill |
| --- | --- | --- | --- |
| Give a buyer their own price (such as a business account's price list) | `IBuyerPriceProvider` | Products.Core | `orchardcore-products` |
| Refuse to sell a product here | `IProductSaleRestriction` | Products.Core | `orchardcore-markets` |
| Price in the shopper's currency | `IShopperCurrency` (replace the default) | Products.Core | `orchardcore-markets` |
| Report availability | `IProductAvailabilityProvider` | Products.Core | `orchardcore-inventory` |
| Keep, price or refuse cart line details | `ICartLineHandler` | Carts.Core | `orchardcore-carts` |
| Add a cart page action | `ICartActionProvider` | Carts.Core | `orchardcore-carts` |
| Follow a checkout, gate payment, add a tender or payment method | `ICheckoutHandler`, `ICheckoutPaymentGate`, `ICheckoutTenderProvider`, `ICheckoutPaymentProvider` | Checkout.Abstractions | `orchardcore-checkout`, `orchardcore-payments` |
| React to order events, grant what was bought | `IOrderEventHandler`, `OrderGrantHandlerBase`, `OrderLineGrantHandlerBase<TPart>` (reads each line's product version with `IContentManager.GetPurchasedAsync(line)`) | Orders.Core | `orchardcore-orders` |
| Send a templated customer email | `ICommerceEmailSender`, `AddCommerceEmailKind` (namespace `CrestApps.OrchardCore.Commerce.Emails`) | Commerce.Abstractions, Commerce.Core | `orchardcore-orders` |
| Put part of a checkout on an invoice owed later | `DeferredBalanceTenderProviderBase` | Checkout.Core | `orchardcore-checkout` |
| Add a shipping rate or carrier | `IShippingRateProvider`, `ICarrierProvider` | Shipping.Core | `orchardcore-shipping` |
| Add tax rules or an external tax engine | `ITaxRuleProvider`, `ITaxDeterminationProvider` | Taxation.Abstractions | `orchardcore-taxation` |
| Suggest addresses | `IAddressAutocompleteProvider` | Addresses.Core | `orchardcore-addresses` |
| Export and erase your records of a person | `IPersonalDataContributor` (or `ICommercePersonalDataContributor` for guest owners and checkout sessions) | PersonalData.Abstractions (Customers.Abstractions) | `orchardcore-customers` |
| Keep config records in a named catalog with recipe and deployment steps | `NamedCatalogRecipeStep<T>` or `CatalogRecipeStep<T>`, `NamedCatalogDeploymentSource<TStep, T>` or `CatalogDeploymentSource<TStep, T>`, `AuditedCatalogEntryHandlerBase<T>` | CrestApps.OrchardCore.Core | `orchardcore-taxation` |
| Stamp created and updated dates, purge old records, or work durable items across nodes | `ITimestampedModel`; `IRetentionPolicy` with `services.AddRetentionPolicy<T>()`; `ILeasedWorkQueue<T>` with `services.AddLeasedWorkQueue<T>()` | CrestApps.OrchardCore.Abstractions, YesSql.Core | `orchardcore-webhooks` |
| Store a secret in settings | `ProtectSetting` and `UnprotectSettingOrDefault` on `IDataProtectionProvider`; `ApplySecret` in a settings driver; `ProtectedSettingsDeploymentSerializer` keeps secrets out of plans | CrestApps.OrchardCore.Core | `orchardcore-shipping` |
| Sign a time-limited link | `SignedTokenService`, `SiteUrlGenerator`, `services.AddSignedLinks()` | CrestApps.OrchardCore.Core | `orchardcore-orders` |

### Traps

- `IOrderService` is built with every `IOrderEventHandler`. A handler that needs a service depending on `IOrderService` must resolve it from `IServiceProvider` when the event fires, or it creates a cycle.
- Checkout and refunds commit part-way through a request. Re-read a document by id after a payment or refund instead of saving a copy loaded earlier.
- Prefix cart and order line detail keys with your module name, such as `giftwrap.message`.
- A product whose line needs details must be added on its page: add its part name to `CartOptions.ChooseOnProductPageParts`.
- A deployment step that overrides `Category` or `Title` must mark them `[JsonIgnore]`; otherwise saving a plan with it fails with a 500 (the Coupons and Shipping Zones steps had this bug before 3.0's #1228 fix).
- Admin create and edit forms put their buttons in Orchard Core's sticky bottom action bar (`<div class="action-bar-bottom">`); theme overrides and UI tests that look for the old `ocat-wrapper` row must update. Storefront pages and modals are unchanged.

## Permissions

| Permission | Feature | Allows |
| --- | --- | --- |
| `SetUpStore` | Commerce | The store setup guide |
| `ManageCommerceWebhooks` (shown as **Manage webhooks**) | Webhooks | Webhook subscriptions, deliveries and signing secrets under **Configuration > Webhooks** |
| `ManageStoreProfile` | Store Profile | **Settings > Commerce > Store** |

Each domain module's permissions are listed in its own skill.

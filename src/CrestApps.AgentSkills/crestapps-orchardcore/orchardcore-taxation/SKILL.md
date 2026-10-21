---
name: orchardcore-taxation
description: Skill for the CrestApps Orchard Core Taxation framework (CrestApps.OrchardCore.Taxation and the TaxJar feature CrestApps.OrchardCore.Taxation.TaxJar). Covers tax as a determination, TaxationPart, tax categories, types, jurisdictions, tables and source-aware rules (Percentage, FixedAmount, PerUnit, Progressive, Threshold and more), ThresholdBasis for import duty, reverse charge and exempt treatments, nexus with MerchantTaxRegistration, ITaxService and TaxCalculationContext, ITaxableItemProvider, ITaxClassificationProvider with taxonomy inheritance, ITaxDeterminationProvider, ITaxRuleProvider, tax-inclusive pricing, TaxSnapshot and ITaxRefundCalculator, TaxationOptions rounding, Tax origin settings, the TaxCategory, TaxType, TaxJurisdiction, TaxTable and TaxRule recipe and deployment steps, the EU VAT standard rates recipe, TaxJarOptions through IOptionsMonitor, and the VAT one-stop-shop report. Use it when requests mention Orchard Core sales tax, VAT, GST, tax rules, TaxJar or tax on orders.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Taxation

Taxation (new in CrestApps.OrchardCore 3.0) is a provider-agnostic engine built on one principle: tax is a determination, not a property. Items carry only a classification; the engine determines taxes from what is sold, who buys it, where it is sourced, when, and how it is calculated, and returns auditable tax lines with the rule and table versions used.

## Where it fits in the commerce suite

| Module | Relationship |
| --- | --- |
| Commerce (`orchardcore-commerce`) | Required shell; **Commerce > Taxation** menu |
| Checkout (`orchardcore-checkout`) | Taxes the invoice through `ICheckoutTaxService` after discounts, and keeps a `TaxSnapshot`; tax profile handlers add origin, destination and exemptions |
| Products (`orchardcore-products`) | `ProductTaxableItemProvider` turns products into taxable items |
| Orders (`orchardcore-orders`) | Copies tax lines to `Order.TaxLines`; VAT OSS report |
| Subscriptions (`orchardcore-subscriptions`) | Re-taxes each renewal with the rules in effect |
| Customers (`orchardcore-customers`) | Tax-exempt customers and business tax ids |
| Addresses (`orchardcore-addresses`) | Country lists; region codes that jurisdictions match |
| Markets (`orchardcore-markets`) | Tax-inclusive markets |
| Payments (`orchardcore-payments`) | Tax-agnostic; refunds take tax from the snapshot |

## Features and assemblies

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Taxation` | Taxation | Commerce |
| `CrestApps.OrchardCore.Taxation.TaxJar` | TaxJar | Taxation |

| Assembly | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Taxation.Abstractions` | Interfaces, models (namespace `CrestApps.OrchardCore.Taxation.Models`), `TaxationPart`, `TaxTypeNames`, `TaxCalculationMethodNames` |
| `CrestApps.OrchardCore.Taxation.Core` | Engine, calculation methods, sourcing, stores, `TaxationOptions`, `Add*` registration extensions |
| `CrestApps.OrchardCore.Taxation` | The module: admin screens, part editors, recipes |

### Guidelines

- Install `CrestApps.OrchardCore.Taxation` in the web/startup project.
- Classify items with `TaxationPart` (category and classification codes); never store a rate on a product.
- Create categories before configuring part settings; the dropdowns read the categories catalog.
- Don't mutate a rule already used by transactions; publish a new version so snapshots stay reproducible.
- Keep table-driven rules (`TaxTable`, `Progressive`, `Threshold`) tax-exclusive; they can't be un-grossed.
- Read `TaxJarOptions` through `IOptionsMonitor<TaxJarOptions>.CurrentValue`; settings saves refresh it without a tenant restart.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

## Setting up taxes

**Commerce > Taxation** has **Categories**, **Types**, **Jurisdictions**, **Tables** and **Rules** (permission `ManageTaxation`), each an `AdminList`. Order matters: rules reference jurisdictions, types, categories and tables.

1. **Categories**: `Name`, `Code` (matched by rules), optional `ParentCode` (a rule for a parent applies to children).
2. **Types**: reporting labels such as `SalesTax`, `VAT`, `GST`, `ImportDuty`; seeded on first run; never change the amount.
3. **Jurisdictions**: hierarchical (`Level`: `Country`, `State`, `Province`, `Region`, `County`, `City`, `Special`, `Other`), matched to an address by non-empty `Country`, `Region`, `County`, `City`, `PostalCode`, with effective dates.
4. **Tables** (optional): versioned bracket rows (`Minimum`, `Maximum`, `Rate`, `FixedAmount`) with an effective window; validated for overlaps. A rule needing a table with none in effect is skipped.
5. **Rules**: pick a calculation method (the rule's fixed `Source`), then jurisdiction, category (or any), tax type, display name (`TaxName`), customer type, priority, effective dates, thresholds and flags (**Enabled**, **Included in price**, **Compound**, **Reverse charge**, **Applies to shipping**).
6. **Classify content**: attach `TaxationPart` and set the defaults.
7. **Settings > Commerce > Tax origin** (`TaxOriginSettings`): the store's address for origin-sourced taxes.

A rule without a category taxes everything in its jurisdiction and stacks with category rules (for example a 30% tobacco excise on top of 7.5% sales tax). A rule without a jurisdiction applies everywhere.

## TaxationPart

| Property | Notes |
| --- | --- |
| `Taxable` | Whether the item is taxable |
| `TaxCategoryCode` | Such as `Electronics` |
| `TaxClassificationCode` | Finer, such as `Television` |
| `ExternalTaxCode` | Code for a third-party provider |

```csharp
internal sealed class TelevisionMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public TelevisionMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Television", type => type
            .WithPart("TaxationPart", part => part
                .WithDisplayName("Taxation")
                .WithSettings(new TaxationPartSettings
                {
                    DefaultTaxCategoryCode = "Electronics",
                    DefaultTaxClassificationCode = "Television",
                    AllowClassificationOverride = true,
                })));

        return 1;
    }
}
```

Classification precedence: the item's own `TaxationPart` category, then `ITaxClassificationProvider`s by ascending `Order` (the built-in one reads the `TaxationPart` on a tagged taxonomy term, with `OrchardCore.Taxonomies`), then the type default.

## Calculating tax

```csharp
public sealed class QuoteTaxService
{
    private readonly ITaxService _taxService;
    private readonly IClock _clock;

    public QuoteTaxService(ITaxService taxService, IClock clock)
    {
        _taxService = taxService;
        _clock = clock;
    }

    public Task<TaxCalculationResult> CalculateAsync(decimal price)
        => _taxService.CalculateAsync(new TaxCalculationContext
        {
            Currency = "USD",
            TransactionDateUtc = _clock.UtcNow,
            Destination = new Address { Country = "US", Region = "CA", City = "Los Angeles", PostalCode = "90001" },
            Customer = new CustomerTaxProfile { CustomerType = CustomerTaxType.B2C },
            Items =
            [
                new TaxableItem
                {
                    Id = "sku-123",
                    Kind = TaxableItemKind.Physical,
                    Quantity = 1m,
                    UnitPrice = price,
                    Currency = "USD",
                    TaxCategoryCode = "Electronics",
                },
            ],
        });
}
```

`Address` is the shared value from `CrestApps.OrchardCore.Addresses.Models`. Every item must be in the context's currency. `TaxCalculationResult` has `TaxableAmount`, `TaxAmount`, `TotalAmount` and `Lines` of `TaxLine` (`TaxCode`, `TaxName`, `TaxType`, jurisdiction, `Rate`, `TaxableAmount`, `TaxAmount`, `CalculationMethod`, `IncludedInPrice`, `IsCompound`, `Treatment`, `TreatmentReason`, `RuleId`, `RuleVersion`, `TableId`, `TableVersion`). Exempt, zero-rated and reverse-charge supplies still emit a zero line with a `Treatment`; only a rule without nexus emits nothing.

Convert content items with `ITaxableItemResolver.ResolveAsync(contentItem)`. Capture a transaction with `ITaxSnapshotFactory.Create(context, result)`, and refund from it with `ITaxRefundCalculator.CalculateFullRefund(snapshot)` or `CalculateProportionalRefund(snapshot, amount)`, never with today's rules.

## Key behaviors

| Topic | Behavior |
| --- | --- |
| Inclusive prices | `TaxableItem.PriceIncludesTax` or `TaxCalculationContext.DefaultPriceType`; percentage and fixed-amount taxes are un-grossed from the gross price |
| Thresholds | `MinimumAmount` and `MaximumAmount` compared with the item (`ThresholdBasis.Item`) or the whole order before tax and shipping (`ThresholdBasis.Order`), as for import duty |
| Reverse charge | For a `B2B` customer, a zero line marked reverse charge |
| Nexus | Without `MerchantTaxRegistration` records every rule collects. With records, only covered jurisdictions and types collect, once `ThresholdAccumulatedAmount` reaches `ThresholdAmount` (the host maintains the running total) |
| Sourcing | `ITaxSourcingStrategy` by item kind: `Origin`, `Destination`, `CustomerResidence`, `CustomerBusiness`, `ServiceLocation`, `EventLocation` |
| Rounding | `TaxationOptions`: `DecimalPlaces` (2), `MidpointRounding` (AwayFromZero), `RoundingLevel` (`Line`, `Tax`, `Jurisdiction`, `Transaction`), `CurrencyDecimalPlaces` |
| Determinism | Same context and rule and table versions give the same result; time comes from `IClock` |

## Extending

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTaxableItemProvider<BookingTaxableItemProvider>();
        services.AddTaxCalculationMethod<EnvironmentalFeeMethod>();
        services.AddTaxSourcingStrategy<VenueSourcingStrategy>();
        services.AddTaxDeterminationProvider<AvalaraTaxDeterminationProvider>();
    }
}
```

| Interface | Responsibility |
| --- | --- |
| `ITaxableItemProvider` (`Order`, `CanCreate(object)`, `CreateAsync(object)`) | Turn your objects into `ITaxableItem` |
| `ITaxCalculationMethod` (`Name`, `Inputs`, `Compute`) | A method; `Inputs` (`TaxCalculationMethodInputs`) tells the rule editor which fields to show |
| `ITaxSourcingStrategy` | Where an item is taxed |
| `ITaxRuleProvider` (`GetApplicableRulesAsync(TaxRuleQuery)`) | Rules from another source |
| `ITaxClassificationProvider` (`Order`, `GetClassificationAsync(contentItem)`) | Inherited classification |
| `ITaxJurisdictionResolver`, `ITaxExemptionResolver`, `IMerchantTaxRegistrationProvider` | Address mapping, exemptions, nexus |
| `ITaxableBaseCalculator`, `ITaxRoundingStrategy` | Base and rounding |
| `ITaxDeterminationProvider` (`Order`, `CanHandle`, `DetermineAsync`) | Take over the whole calculation, lowest `Order` first; a provider that throws falls back to the built-in engine |

Inherit classification from an ERP:

```csharp
public sealed class ErpTaxClassificationProvider : ITaxClassificationProvider
{
    private readonly IErpCatalog _erp;

    public ErpTaxClassificationProvider(IErpCatalog erp)
    {
        _erp = erp;
    }

    public int Order => 50;

    public async ValueTask<TaxClassification> GetClassificationAsync(ContentItem contentItem, CancellationToken cancellationToken = default)
    {
        var code = await _erp.GetTaxCodeAsync(contentItem.ContentItemId, cancellationToken);

        return code is null ? null : new TaxClassification { TaxCategoryCode = code };
    }
}
```

## TaxJar

`CrestApps.OrchardCore.Taxation.TaxJar` registers `TaxJarTaxDeterminationProvider` (`Order` 100, named `HttpClient` `TaxJar`). **Settings > Commerce > TaxJar** (group `taxjar`, `ManageTaxation`) stores `TaxJarSettings` with the API key protected (purpose `CrestApps.OrchardCore.Taxation.TaxJar.ApiKey`), sandbox flag and `Countries` (default `US`). `TaxJarOptions` is read through `IOptionsMonitor` and refreshed by `IOptionsUpdateNotifier.RequestUpdate<TaxJarOptions>()` on save. It handles a context only with a token, exclusive prices and a destination in `Countries`; a TaxJar error falls back to the store's rules.

## Recipes, deployment and reports

Recipe and deployment steps `TaxCategory`, `TaxType`, `TaxJurisdiction`, `TaxTable`, `TaxRule` match entries by `ItemId`, refuse duplicate names, and never import `CreatedUtc`, `ModifiedUtc`, `Author`, `OwnerId` or `Version`. Examples, the EU VAT standard rates recipe and the taxonomy worked example are in [references/taxation-recipes.md](references/taxation-recipes.md). With `CrestApps.OrchardCore.Recipes` on, JSON Schema is contributed for these steps and `TaxationPart`.

With Orders and Reports on, **Tax on orders** and **VAT one-stop shop (OSS)** (`order-vat-oss`, `ManageOrders`) sum `Order.TaxLines` with `TaxType` `VAT` by EU destination country and rate, refunds taken off proportionally.

## Upgrading within the 3.0 line

- `TaxRule.Source` (the calculation method name, fixed at creation) replaced `TaxRule.CalculationMethod`.
- Tax types became a managed catalog with its own recipe step; existing rule values are kept.
- `TaxRule.TaxName` is optional and falls back to `Name`.
- The no-op checkout tax service is a fallback (`TryAddScoped`), so enabling Taxation always taxes checkouts.

---
name: orchardcore-taxation
description: Skill for the CrestApps Orchard Core Taxation module and its features Tax Tables, Sales Tax (CrestApps.OrchardCore.Taxation), Income Tax, United States Income Tax, Canada Income Tax and TaxJar. Covers tax as a determination, TaxationPart, categories, types, jurisdictions, tables and sales tax rules, import duty, reverse charge, nexus, ITaxService, ITaxDeterminationProvider, snapshots and refunds, the Tax calculation rounding setting, ITaxBreakdownCalculator for invoices, IPartyTaxProfileService, ITaxTableUsage, income tax rules and sources, IIncomeTaxProvider, election forms, withholding methods, Form W-4, the CRA and Revenu Quebec formulas, country packs, the recipe steps including IncomeTaxRules, the EU VAT and 2026 income tax recipes, TaxJar and the VAT one-stop-shop report. Use it when requests mention sales tax, VAT, GST, tax rules, income tax withholding, payroll taxes, TaxJar or tax on orders. Strong matches include TaxRule, IncomeTaxRule, TaxCalculationSettings and AddIncomeTaxRuleSource.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Taxation

Taxation (new in CrestApps.OrchardCore 3.0) is a provider-agnostic engine built on one principle: tax is a determination, not a property. Items carry only a classification; the engine determines taxes from what is sold, who buys it, where it is sourced, when, and how it is calculated, and returns auditable tax lines with the rule and table versions used. Beside sales tax, the **Income Tax** feature works out taxes on income (withholding and statutory contributions) for Payroll or any module that pays income.

## Where it fits

| Module | Relationship |
| --- | --- |
| Commerce (`orchardcore-commerce`) | Sales Tax depends on Commerce Foundation; every tax screen is under **Commerce > Taxation** (a site with Payroll and no store still gets this item) |
| Invoicing (`orchardcore-invoicing`) | Invoices, estimates, credit notes and recurring plans take their tax from the sales tax rules through `ITaxBreakdownCalculator` and `IPartyTaxProfileService` |
| Payroll (`orchardcore-payroll`) | Depends on Income Tax; maps each pay statement to an `IncomeTaxRequest` and never owns a rule |
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
| `CrestApps.OrchardCore.Taxation.Tables` | Tax Tables | none (jurisdictions, tables, calculation methods; shared by Sales Tax and Income Tax) |
| `CrestApps.OrchardCore.Taxation` | Sales Tax (the id is kept from when it was named Taxation) | Tax Tables, `CrestApps.OrchardCore.Commerce.Foundation` |
| `CrestApps.OrchardCore.Taxation.TaxJar` | TaxJar | Sales Tax |
| `CrestApps.OrchardCore.Taxation.IncomeTax` | Income Tax | Tax Tables |
| `CrestApps.OrchardCore.Taxation.IncomeTax.UnitedStates` | United States Income Tax | Income Tax |
| `CrestApps.OrchardCore.Taxation.IncomeTax.Canada` | Canada Income Tax | Income Tax |

Income Tax never references Commerce, so a payroll-only site runs without a store.

| Assembly | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Taxation.Abstractions` | Interfaces, models (namespace `CrestApps.OrchardCore.Taxation.Models`), `TaxationPart`, `TaxTypeNames`, `TaxCalculationMethodNames`, the income tax contracts and `AddIncomeTax*` extensions |
| `CrestApps.OrchardCore.Taxation.Core` | Engine, calculation methods, sourcing, stores, `TaxationOptions`, `AddTaxationCore`, `AddTaxTablesCore`, `AddIncomeTaxCore`, `AddPartyTaxProfiles`, the US and Canada packs |
| `CrestApps.OrchardCore.Taxation` | The module: admin screens, part editors, recipes |

### Guidelines

- Install `CrestApps.OrchardCore.Taxation` in the web/startup project.
- Classify items with `TaxationPart` (category and classification codes); never store a rate on a product.
- Create categories before configuring part settings; the dropdowns read the categories catalog.
- Don't mutate a rule already used by transactions; publish a new version so snapshots stay reproducible.
- Keep table-driven rules (`TaxTable`, `Progressive`, `Threshold`) tax-exclusive; they can't be un-grossed.
- Read `TaxJarOptions` through `IOptionsMonitor<TaxJarOptions>.CurrentValue`; settings saves refresh it without a tenant restart.
- Tax a document (invoice, quote) through `ITaxBreakdownCalculator` with the site's `ITaxRoundingLevelProvider`, so it comes to the same tax as a checkout of the same lines.
- A module whose records point at tax tables registers an `ITaxTableUsage`, so a table in use can't be deleted.
- Never edit an income tax rule a past pay used; add a new dated version (`EffectiveFrom`/`EffectiveTo`, versions of one tax code can't overlap).
- `ManageIncomeTaxRules` is security critical and given only to Administrator by default; grant it on purpose.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

## Setting up taxes

**Commerce > Taxation** has **Jurisdictions** and **Tables** (Tax Tables), **Categories**, **Types** and **Sales tax rules** (Sales Tax), all for `ManageTaxation`, and **Income tax rules** (Income Tax, `ManageIncomeTaxRules`), each an `AdminList`. Order matters: rules reference jurisdictions, types, categories and tables.

| Location | Feature | Permission |
| --- | --- | --- |
| **Commerce > Taxation > Jurisdictions**, **Tables** | Tax Tables | `ManageTaxation` |
| **Commerce > Taxation > Categories**, **Types**, **Sales tax rules** | Sales Tax | `ManageTaxation` |
| **Commerce > Taxation > Income tax rules** | Income Tax | `ManageIncomeTaxRules` |
| **Settings > Commerce > Tax calculation** (group `tax-calculation`) | Sales Tax | `ManageTaxation` |
| **Settings > Commerce > Tax origin** (group `tax-origin`) | Sales Tax | `ManageTaxation` |
| **Settings > Commerce > TaxJar** (group `taxjar`) | TaxJar | `ManageTaxation` |

1. **Categories**: `Name`, `Code` (matched by rules), optional `ParentCode` (a rule for a parent applies to children).
2. **Types**: reporting labels such as `SalesTax`, `VAT`, `GST`, `ImportDuty`; seeded on first run; never change the amount.
3. **Jurisdictions**: hierarchical (`Level`: `Country`, `State`, `Province`, `Region`, `County`, `City`, `Special`, `Other`), matched to an address by non-empty `Country`, `Region`, `County`, `City`, `PostalCode`, with effective dates.
4. **Tables** (optional): versioned bracket rows (`Minimum`, `Maximum`, `Rate`, `FixedAmount`) with an effective window; validated for overlaps. A rule needing a table with none in effect is skipped.
5. **Sales tax rules**: pick a calculation method (the rule's fixed `Source`), then jurisdiction, category (or any), tax type, display name (`TaxName`), customer type, priority, effective dates, thresholds and flags (**Enabled**, **Included in price**, **Compound**, **Reverse charge**, **Applies to shipping**).
6. **Classify content**: attach `TaxationPart` and set the defaults.
7. **Settings > Commerce > Tax origin** (`TaxOriginSettings`): the store's address for origin-sourced taxes; when empty, checkout taxes and the VAT OSS report fall back to the Store Profile address (see `orchardcore-commerce`).

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
| Rounding level | One site setting, **Settings > Commerce > Tax calculation** (`TaxCalculationSettings.RoundingLevel`), read by checkout and invoices through `ITaxRoundingLevelProvider`. `TaxRoundingLevel`: `Line`, `Tax`, `Jurisdiction`, `Transaction`, `Rate` (a new site's default: summed per tax and rate, rounded once, spread back largest remainder first, as EN 16931 asks). A site that had tax rules before the setting keeps `Line` (`TaxCalculationSettingsMigrations`). `TaxCalculationContext.RoundingLevel` overrides it per call; a host without site settings uses `TaxationOptions.RoundingLevel` (`Line`) |
| Precision | `TaxationOptions`: `DecimalPlaces` (empty by default, so tax rounds to each currency's ISO 4217 precision from `CurrencyScale`: 0 for JPY, 3 for KWD; set it only to force one precision), `MidpointRounding` (AwayFromZero), `CurrencyDecimalPlaces` (per-currency override, always wins) |
| Determinism | Same context and rule and table versions give the same result; time comes from `IClock` |
| Telemetry | Meter `Taxation` counts `crestapps.taxation.determination.fallbacks` when an external determination provider fails and the store's rules answer |

Two lines of 10.05 at 7.5% come to 1.51 rounding per tax and rate (on 20.10) and 1.50 rounding each line. Changing the level changes what the checkout charges from then on; orders and issued invoices keep their amounts.

```json
{
  "steps": [
    {
      "name": "Settings",
      "TaxCalculationSettings": {
        "RoundingLevel": "Rate"
      }
    }
  ]
}
```

## Document tax breakdown

`ITaxBreakdownCalculator.Calculate(TaxBreakdownRequest)` (`DefaultTaxBreakdownCalculator`) works out a document's tax from the taxes the rules found for its items; Invoicing uses it for invoices, estimates, credit notes and recurring plans.

- `TaxBreakdownRequest`: `Currency`, `PricesIncludeTax`, `Treatment` and `ExemptionReason` (the bill-to's), `RoundingLevel`, `CashRoundingIncrement`, `Items` of `TaxBreakdownItem` (`Id`, `Amount` after document discounts, `BaseAmount` before them, `IsCharge`, `Treatment`, `TreatmentReason`, `Taxes` of `TaxBreakdownRate` with `Code`, `Name`, `Rate` in percent, `IsCompound`, `TaxType`, `JurisdictionId`).
- `TaxBreakdownResult`: `Rows` (one per treatment, code, name, rate and compound flag, including a zero row with its reason for each untaxed treatment), `Items` (each item's net and share of tax, spread largest remainder first with `MoneyAllocation`), `TaxTotal`, `RoundingAmount` (cash rounding), `Total`.
- At levels other than `Rate`, each item's taxes are rounded the way the checkout does it (`TaxLineRounding`), so both agree at every level.

## Tax profile of a party

A document billed to a party (account, customer, user, contact) has no checkout to read the buyer from. `IPartyTaxProfileService.GetAsync(PartyRef)` builds a `CustomerTaxProfile`: each `IPartyTaxProfileProvider.BuildAsync(profile, party)` fills what is missing. The default `BillingProfilePartyTaxProfileProvider` reads the party's billing profile (an `Account` is `B2B` at its billing address; the tax id fills `TaxRegistrationNumber` and `VatNumber` through `CustomerTaxProfile.SetTaxNumber`; an exempt party is exempt). Customers adds `CustomerPartyTaxProfileProvider` (business customers and their exemption certificates).

`AddTaxationCore()` registers the service; a feature that taxes documents without Sales Tax calls `services.AddPartyTaxProfiles()`, and a module adds a provider with `services.AddPartyTaxProfileProvider<T>()`.

## Income tax

Income Tax works out withholding at every level (federal, state or province, local) and statutory contributions on earnings (Social Security, Medicare, unemployment; CPP, EI, QPP, QPIP). The seam is `IIncomeTaxProvider.CalculateAsync(IncomeTaxRequest)` returning `IncomeTaxResult` (`Lines` of `IncomeTaxLine`, `Warnings`). The built-in provider (`IncomeTaxRulesProvider`, stored name `TaxTables`) works out the `IncomeTaxRule`s in effect on the pay date, kept in the `IncomeTax` YesSql collection.

| Rule source | Method | Use |
| --- | --- | --- |
| `FlatRate` | `Percentage` | A rate with optional `WageBase`, `WageThreshold`, `YearlyExemption`, `YearlyMaximum`, or the payer's own rate (`RateFromRegistration`) |
| `ShareOfTax` | `Percentage` | A multiple of another tax of the same pay (`ShareOfTaxCode`, `ShareMultiplier`) |
| `TaxTable` | `TaxTable`, `Progressive`, `Threshold` | Brackets from a tax table, optionally annualized with elections |
| `FixedAmount` | `FixedAmount` | The same amount each pay |
| `UnitedStatesFormW4` | same | IRS Publication 15-T percentage method (United States Income Tax) |
| `CanadaFederal`, `CanadaProvincial`, `QuebecTP1015` | same | CRA T4127 and Revenu Québec TP-1015.F (Canada Income Tax) |

```json
{
  "steps": [
    {
      "name": "IncomeTaxRules",
      "Rules": [
        {
          "TaxCode": "US-TX-SUI",
          "Name": "Texas unemployment (SUI)",
          "Jurisdiction": "US-TX",
          "Category": "Unemployment",
          "Payer": "Employer",
          "EffectiveFrom": "2026-01-01",
          "EffectiveTo": "2026-12-31",
          "Enabled": true,
          "Source": "FlatRate",
          "Method": "Percentage",
          "Rate": 2.7,
          "RateFromRegistration": true,
          "WageBase": 9000
        }
      ]
    }
  ]
}
```

Run `IncomeTaxUnitedStatesFederal2026` (US-FIT, Social Security, Medicare, Additional Medicare, FUTA, backup withholding) or `IncomeTaxCanada2026` (every province and territory, CPP, CPP2, EI, QPP, QPP2, QPIP) from **Tools > Recipes**; check the amounts against the agency's current publications. Rule fields, the request, country packs and a custom withholding method are in [references/income-tax.md](references/income-tax.md).

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
| `ITaxTableUsage` (`GetUsagesAsync(TaxTable)`) | Name the records that use a table so it can't be deleted (income tax rules register one) |
| `ITaxRoundingLevelProvider` | The rounding level documents and checkout use (default reads `TaxCalculationSettings`) |
| `IPartyTaxProfileProvider` (`AddPartyTaxProfileProvider<T>()`) | Add what you know about a party's tax status |
| `IIncomeTaxProvider`, `IIncomeTaxWithholdingMethod` | An income tax engine, or a country's withholding formula (see [references/income-tax.md](references/income-tax.md)) |

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

Recipe and deployment steps `TaxCategory`, `TaxType`, `TaxRule` (Sales Tax) and `TaxJurisdiction`, `TaxTable` (Tax Tables) match entries by `ItemId`, refuse duplicate names, and never import `CreatedUtc`, `ModifiedUtc`, `Author`, `OwnerId` or `Version`. The `IncomeTaxRules` step and deployment step (Income Tax, entries key `Rules`) match by `ItemId`; the handler clears fields the rule's `Source` doesn't own and sets the source's method, so a recipe may name the source alone. `TaxCalculationSettings`, `TaxOriginSettings` and `TaxJarSettings` travel with Orchard's `Settings` step.

The `MerchantTaxRegistration` step (`MerchantTaxRegistrations` array; matched by `ItemId`, then name; needs a name and `JurisdictionId`) imports the store's registrations, and the `ExemptionCertificate` step (`ExemptionCertificates` array; keeps its `ItemId`, which customers' `ExemptionCertificateIds` list; otherwise matched by `CustomerId` and `CertificateNumber`; must expire after it takes effect) imports customers' certificates. Import customers and jurisdictions first. The **Tax registrations** and **Exemption certificates** deployment steps (*Taxation*) write the same shape.

```json
{
  "steps": [
    {
      "name": "MerchantTaxRegistration",
      "MerchantTaxRegistrations": [
        { "Name": "California sales tax", "JurisdictionId": "us-ca", "TaxType": "SalesTax", "RegistrationNumber": "123-456", "IsActive": true, "ThresholdAmount": 100000 }
      ]
    },
    {
      "name": "ExemptionCertificate",
      "ExemptionCertificates": [
        { "Name": "Resale", "CertificateNumber": "R-1001", "CustomerId": "4cs00000000000000000000001", "ExemptionType": "Resale", "Status": "Active", "TaxTypes": [ "SalesTax" ], "JurisdictionIds": [ "us-ca" ] }
      ]
    }
  ]
}
```

Examples, the EU VAT standard rates recipe and the taxonomy worked example are in [references/taxation-recipes.md](references/taxation-recipes.md). With `CrestApps.OrchardCore.Recipes` on, JSON Schema is contributed for these steps, the settings and `TaxationPart`.

With Orders and Reports on, **Tax on orders** and **VAT one-stop shop (OSS)** (`order-vat-oss`, `ManageOrders`) sum `Order.TaxLines` with `TaxType` `VAT` by EU destination country and rate, refunds taken off proportionally.

## Permissions

| Permission | Feature | Grants |
| --- | --- | --- |
| `ManageTaxation` | Tax Tables (also used by Sales Tax and TaxJar) | Jurisdictions, tables, categories, types, sales tax rules, tax calculation, origin and TaxJar settings |
| `ManageIncomeTaxRules` | Income Tax | **Income tax rules**; security critical, Administrator only by default |

## Troubleshooting

| Symptom | Check |
| --- | --- |
| No **Income tax rules** menu | Income Tax feature off, or the role lacks `ManageIncomeTaxRules` (not given to Editor or Manager by default) |
| Can't delete a tax table | An `ITaxTableUsage` reports it in use, such as an income tax rule's `TaxTableId` or `Tables` |
| Invoice tax differs from the checkout by a cent | Different rounding levels; both should read **Settings > Commerce > Tax calculation** |
| Income tax rule not applied | Payer not registered in the jurisdiction that collects it, outside its dates, wrong `IncomeKinds`, the person isn't in its jurisdiction, or `ByPlaceOfWork` with no work jurisdiction known |
| `RateFromRegistration` rule warns | The payer's registration has no rate on the pay date and the rule has no `Rate` |
| A pack's rules show only shared fields | That pack's feature is off; its source isn't offered |

## Upgrading within the 3.0 line

- The feature `CrestApps.OrchardCore.Taxation` is now named **Sales Tax** and depends on the new **Tax Tables** feature and on Commerce Foundation; jurisdictions and tables (and their recipe steps) belong to Tax Tables.
- **Rules** is now **Sales tax rules** in the menu.
- Rounding moved to the **Tax calculation** site setting; existing sites with rules keep `Line`, new sites use `Rate`.
- `TaxRule.Source` (the calculation method name, fixed at creation) replaced `TaxRule.CalculationMethod`.
- Tax types became a managed catalog with its own recipe step; existing rule values are kept.
- `TaxRule.TaxName` is optional and falls back to `Name`.
- `TaxationOptions.DecimalPlaces` is now nullable and empty by default (currency precision); a site that relied on the old fixed 2 for JPY or KWD must set it explicitly.
- The no-op checkout tax service is a fallback (`TryAddScoped`), so enabling Taxation always taxes checkouts.

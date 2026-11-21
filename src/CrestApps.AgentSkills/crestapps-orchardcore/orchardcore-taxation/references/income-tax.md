# Income tax

Income Tax (`CrestApps.OrchardCore.Taxation.IncomeTax`) depends only on Tax Tables. It never references Commerce or Payroll: Payroll's taxes step maps a pay statement to an `IncomeTaxRequest` and the result back to tax lines, and keeps accumulators, liabilities, filings and year end.

## The request and the result

| `IncomeTaxRequest` member | Carries |
| --- | --- |
| `PayerId`, `EarnerId` | Ids for a provider that keeps its own records |
| `RegisteredJurisdictions`, `Registrations` | Where the payer is registered; each `IncomeTaxRegistration` (`Jurisdiction`, `AccountId`, `Rate` on the pay date) |
| `IncomeKind`, `ContractorWithholding` | `Employment`, `Contractor`, `Pension`, `Other`; whether a contractor is withheld on |
| `HomeJurisdictions`, `WorkJurisdictions` | Such as `CA`, `CA-ON`, `US`, `US-TX` |
| `Elections` | `IncomeTaxElection` (`Jurisdiction`, `Values`) in effect |
| `PayDate`, `PeriodsPerYear`, `Currency` | When, how often, in what |
| `Income`, `SupplementalIncome`, `SupplementalMethod` | Regular and supplemental income per tax category; `Aggregate` or `Flat` |
| `RegularIncomePerPeriod` | For a payment with no regular income of its own (a bonus alone) |
| `YearIncome`, `YearSupplementalIncome` | Year to date before this payment |
| `TaxesToDate` | `IncomeTaxToDate` per tax code (`YearTaxableIncome`, `YearTax`, `PeriodTaxableIncome`, `PeriodFlatIncome`) |

`IncomeTaxLine`: `TaxCode`, `Name`, `Jurisdiction`, `CollectedBy`, `Category`, `Payer` (`Employee`, `Employer`), `TaxableIncome`, `FlatIncome`, `Amount`.

Income tax categories are an open list (`IncomeTaxCategories`: `IncomeTax`, `SocialInsurance`, `Unemployment`; United States adds `ContractorWithholding`, Canada adds `InsurableEarnings`). Add one with `services.AddIncomeTaxCategory(name, displayName)`.

## Another tax engine

```csharp
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Localization;

public sealed class VendorIncomeTaxProvider : IIncomeTaxProvider
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<VendorIncomeTaxProvider>("Vendor payroll tax engine");

    private readonly IVendorTaxClient _client;

    public VendorIncomeTaxProvider(IVendorTaxClient client)
    {
        _client = client;
    }

    public string Name => "Vendor";

    [JsonIgnore]
    public LocalizationSource DisplayName => _displayName;

    public async Task<IncomeTaxResult> CalculateAsync(IncomeTaxRequest request, CancellationToken cancellationToken = default)
    {
        var result = new IncomeTaxResult();

        foreach (var tax in await _client.CalculateAsync(request, cancellationToken))
        {
            result.Lines.Add(new IncomeTaxLine
            {
                TaxCode = tax.Code,
                Name = tax.Name,
                Jurisdiction = tax.Jurisdiction,
                Category = tax.Category,
                Payer = tax.EmployerPaid ? IncomeTaxPayer.Employer : IncomeTaxPayer.Employee,
                TaxableIncome = tax.Wages,
                Amount = tax.Amount,
            });
        }

        return result;
    }
}

// services.TryAddEnumerable(ServiceDescriptor.Scoped<IIncomeTaxProvider, VendorIncomeTaxProvider>());
```

Payroll uses the provider named in **Settings > Payroll** (`PayrollSettings.TaxProvider`); empty uses the only one, or the only one besides the built-in rules (`TaxTables`).

## Income tax rules

`IncomeTaxRule` (catalog entry, `IncomeTax` collection, **Commerce > Taxation > Income tax rules**). A rule applies when the payer is registered in the jurisdiction that collects it, it covers the income kind, the person is in its jurisdiction (for one below a country), and in none of its excepted jurisdictions.

| Field | Use |
| --- | --- |
| `TaxCode`, `Name` | Such as `US-FIT`; versions of one code can't overlap |
| `Jurisdiction`, `CollectedBy` | A jurisdiction code at any level; another agency that collects it (`CA-ON` collected by `CA`) |
| `ByPlaceOfWork` | Match by where the person works (Canada's province of employment) |
| `ExceptJurisdictions` | People it doesn't apply to (`CA-QC` for CPP) |
| `Category`, `IncomeKinds`, `Payer` | Income taxed, kinds of income (empty is all), who pays |
| `Source`, `Method` | The kind of rule (fixed after creation) and its calculation or withholding method |
| `Rate`, `RateFromRegistration`, `FixedAmount`, `TaxTableId` | The amount; the payer's own registration rate |
| `WageBase`, `WageThreshold`, `YearlyExemption`, `YearlyMaximum` | Year limits |
| `ShareOfTaxCode`, `ShareMultiplier` | A share of another tax (1.4 times EI) |
| `Tables`, `Parameters` | A withholding method's tables (by key) and numbers |
| `Annualize`, `UsesElections`, `AllowanceAmount` | Year tables; the person's election |
| `SupplementalRate`, `SupplementalExcessRate`, `SupplementalExcessThreshold` | Flat rates on supplemental income |
| `EffectiveFrom`, `EffectiveTo`, `Enabled` | Dated versions |

Contributions and flat taxes are worked out first, then withholding methods that use them (`UsesOtherTaxes`), then shares. Each source owns its amount fields (`IncomeTaxRuleSources.FlatRateFields` and others); the handler clears the rest on save or import and validates what the source needs. A rule without a source gets one on load (`IncomeTaxRuleSources.Infer`).

**Add income tax rule** opens a dialog of sources (`IncomeTaxRuleSource_Link` shape, alternate `IncomeTaxRuleSource_Link__{Name}`). The editor shows the shared fields (`IncomeTaxRuleDisplayDriver`) and the source's card (`IncomeTaxFlatRateDisplayDriver`, `IncomeTaxShareOfTaxDisplayDriver`, `IncomeTaxTableDisplayDriver`, `IncomeTaxFixedAmountDisplayDriver`, or `IncomeTaxWithholdingDisplayDriver` for a pack's method).

## Country packs

```csharp
// In a country pack's Startup.
services.AddIncomeTaxCategory("InsurableEarnings", LocalizationSource.Create<Startup>("Insurable earnings"));
services.AddElectionFieldSet(MyElectionFields.Create());
services.AddIncomeTaxWithholdingMethod<RuritaniaWithholdingMethod>();
services.AddIncomeTaxRuleSource(
    "RuritaniaWithholding",
    LocalizationSource.Create<Startup>("Ruritania income tax"),
    LocalizationSource.Create<Startup>("The tax agency's formula, with each person's withholding form."),
    method: "RuritaniaWithholding",
    position: 60);
```

`AddIncomeTaxRuleSource(name, displayName, description, method, position, fields, usesWithholdingEditor)`: a source with a withholding method needs no driver (`IncomeTaxWithholdingDisplayDriver` shows its tables and parameters). A pack needing other fields derives from `IncomeTaxRuleSourceDisplayDriver` (`CrestApps.OrchardCore.Taxation.Core.Drivers`), passes `usesWithholdingEditor: false` and lists its `fields:`.

An `ElectionFieldSet` has a `Jurisdiction` (`*`, a code, or a prefix such as `CA-*`), a `DisplayName` (`[JsonIgnore]`) and `Fields` of `ElectionField(Name, Label, Kind)` (`ElectionFieldKind`). The generic set (`*`) asks for `FilingStatus`, `Allowances`, `ExtraWithholding` and `Exempt`.

```csharp
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Localization;

public sealed class RuritaniaWithholdingMethod : IIncomeTaxWithholdingMethod
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<RuritaniaWithholdingMethod>("Ruritania withholding");

    public string Name => "RuritaniaWithholding";

    [JsonIgnore]
    public LocalizationSource DisplayName => _displayName;

    public IReadOnlyList<IncomeTaxWithholdingInput> Tables { get; } =
    [
        new("Brackets", LocalizationSource.Create<RuritaniaWithholdingMethod>("Yearly brackets")),
    ];

    public IReadOnlyList<IncomeTaxWithholdingInput> Parameters { get; } =
    [
        new("PersonalAmount", LocalizationSource.Create<RuritaniaWithholdingMethod>("Personal amount")),
    ];

    public async Task<decimal> CalculateAsync(IncomeTaxWithholdingContext context, CancellationToken cancellationToken = default)
    {
        var yearly = context.Income * context.PeriodsPerYear - context.Parameter("PersonalAmount");

        if (yearly <= 0)
        {
            return 0m;
        }

        var yearTax = await context.ComputeTableAsync("Brackets", yearly, cancellationToken);

        return Math.Round(yearTax / context.PeriodsPerYear + context.Amount("ExtraWithholding"), 2, MidpointRounding.AwayFromZero);
    }
}
```

`IncomeTaxWithholdingContext` gives `Rule`, `Request`, `Election`, `Income`, `SupplementalIncome` (when `HandlesSupplementalIncome`), `PeriodsPerYear`, `OtherTaxes` (when `UsesOtherTaxes`), `ComputeTableAsync`, `LowestRateAsync`, `RateAtAsync`, and helpers `Value`, `Amount`, `Parameter`, `OtherTax`, `PersonIn`.

## Shipped packs

| Feature | Election forms | Methods | Recipe |
| --- | --- | --- | --- |
| United States Income Tax | Form W-4 (`US`) | `UnitedStatesFormW4` (Publication 15-T, Worksheet 1A) | `IncomeTaxUnitedStatesFederal2026`: `US-FIT`, `US-SS-EE`, `US-SS-ER`, `US-MED-EE`, `US-MED-ER`, `US-MED-ADD`, `US-FUTA`, `US-BWH` |
| Canada Income Tax | TD1 (`CA`), provincial TD1 (`CA-*`), TP-1015.3 (`CA-QC`) | `CanadaFederal`, `CanadaProvincial` (CRA T4127 Option 1 with the bonus method), `QuebecTP1015` | `IncomeTaxCanada2026`: every province and territory with the July 1, 2026 changes, CPP, CPP2, EI (employer 1.4 times), QPP, QPP2, QPIP |

State and local taxes are not shipped: add them as rules (for example a `FlatRate` state unemployment rule with `RateFromRegistration`, or a `TaxTable` state income tax) or use a tax engine's provider.

# Taxation Recipes and Examples

Each step takes a plural array (`TaxCategories`, `TaxTypes`, `TaxJurisdictions`, `TaxTables`, `TaxRules`). Entries are matched by `ItemId` and updated, or created. Rule and jurisdiction fields use the model property names (`Country` and `Region` on a jurisdiction, `Source` for the calculation method on a rule). The same five names exist as deployment steps under **Configuration > Deployment** with the same JSON shape.

## A complete catalog

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Taxation"
      ],
      "disable": []
    },
    {
      "name": "TaxCategory",
      "TaxCategories": [
        { "ItemId": "electronics", "Name": "Electronics", "Code": "ELEC" },
        { "ItemId": "tobacco", "Name": "Tobacco", "Code": "TOBACCO" }
      ]
    },
    {
      "name": "TaxType",
      "TaxTypes": [
        { "ItemId": "sales-tax", "Name": "SalesTax" },
        { "ItemId": "excise-tax", "Name": "ExciseTax", "Description": "Excise duty on regulated goods." }
      ]
    },
    {
      "name": "TaxJurisdiction",
      "TaxJurisdictions": [
        { "ItemId": "us-ca", "Name": "California", "Code": "US-CA", "Level": "State", "Country": "US", "Region": "CA" }
      ]
    },
    {
      "name": "TaxTable",
      "TaxTables": [
        {
          "ItemId": "luxury-brackets",
          "Name": "Luxury brackets",
          "Rows": [
            { "Minimum": 0, "Maximum": 1000, "Rate": 0.05 },
            { "Minimum": 1000, "Rate": 0.10 }
          ]
        }
      ]
    },
    {
      "name": "TaxRule",
      "TaxRules": [
        { "ItemId": "ca-sales", "Name": "CA sales tax", "TaxType": "SalesTax", "JurisdictionId": "us-ca", "Source": "Percentage", "Rate": 0.075, "AppliesToShipping": true },
        { "ItemId": "ca-tobacco", "Name": "CA tobacco excise", "TaxType": "ExciseTax", "JurisdictionId": "us-ca", "CategoryCode": "TOBACCO", "Source": "Percentage", "Rate": 0.30 },
        { "ItemId": "ca-luxury", "Name": "CA luxury tax", "JurisdictionId": "us-ca", "Source": "Progressive", "TaxTableId": "luxury-brackets" }
      ]
    }
  ]
}
```

Calculation method names (`TaxCalculationMethodNames`): `Percentage`, `FixedAmount`, `PerUnit`, `PerWeight`, `PerVolume`, `TaxTable`, `Progressive`, `Threshold`. A table method requires `TaxTableId`.

## Import duty on the whole order

```json
{
  "steps": [
    {
      "name": "TaxRule",
      "TaxRules": [
        {
          "ItemId": "ca-import-duty",
          "Name": "Canada import duty",
          "Source": "Percentage",
          "TaxType": "ImportDuty",
          "TaxName": "Import duty",
          "JurisdictionId": "ca",
          "Rate": 0.05,
          "MinimumAmount": 150,
          "ThresholdBasis": "Order",
          "Enabled": true
        }
      ]
    }
  ]
}
```

Every item of an order to the jurisdiction whose items total 150 or more before tax and shipping pays the duty; the checkout summary shows it once, as *Import duty 5%*.

## Per-category tax through a taxonomy

1. Create categories `ELEC` and `TOBACCO`.
2. Enable `OrchardCore.Taxonomies`, create a *Product Categories* taxonomy whose term type has `TaxationPart`, and set each term's tax category.
3. Add a Taxonomy field to the product type and tag products; set no code on the product.
4. Add a general rule (no category) and a category rule; tagged tobacco products match both.

```json
{
  "steps": [
    {
      "name": "content",
      "data": [
        {
          "ContentType": "ProductCategory",
          "DisplayText": "Tobacco",
          "TaxationPart": { "Taxable": true, "TaxCategoryCode": "TOBACCO" }
        }
      ]
    }
  ]
}
```

## EU VAT standard rates

The Taxation module ships `EuVatStandardRates` (`Recipes/eu-vat.recipe.json`, not a setup recipe). Run it from **Configuration > Recipes**: it enables Taxation and adds the 27 EU member states as country jurisdictions (Greece as `GR`) with a `Percentage` `VAT` rule at each standard rate, applied to shipping. Fixed `ItemId`s make it safe to re-run. Check current rates and add reduced-rate rules by category before relying on it.

## Prices that include tax

For VAT-style storefronts, set `TaxCalculationContext.DefaultPriceType` to `TaxPriceType.Inclusive` (the Markets module does this for markets whose prices include tax) or mark rules **Included in price**. Lines then show *VAT 19% (included)*. TaxJar is skipped for inclusive contexts.

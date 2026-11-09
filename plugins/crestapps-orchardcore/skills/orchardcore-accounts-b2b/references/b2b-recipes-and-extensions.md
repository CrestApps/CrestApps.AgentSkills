# Accounts B2B Recipes and Extensions

## A Business Account with Terms and a Price List

The account is an ordinary `Account` content item, so it travels in the `Accounts` step (from `orchardcore-accounts`) with its `B2BAccountPart`. Import the price list first so `PriceListId` points at it. Members' `UserId` values must be users on the target site.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Accounts.B2B"
      ],
      "disable": []
    },
    {
      "name": "PriceLists",
      "PriceLists": [
        {
          "ItemId": "4b7g3ke0q7mnvf3fbzbbyemqtn",
          "Name": "Wholesale",
          "PercentOff": 10,
          "Currency": "USD",
          "Entries": [
            { "Sku": "HAT-22069", "UnitPrice": 24.5 },
            { "Sku": "MUG-10001", "UnitPrice": 6 }
          ]
        }
      ]
    },
    {
      "name": "Accounts",
      "Accounts": [
        {
          "ContentItemId": "4xf4a7cq978mj04mg6nfygm3dd",
          "ContentType": "Account",
          "DisplayText": "Northwind Traders",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Northwind Traders" },
          "AccountPart": {},
          "AccountMembersPart": {
            "Members": [
              { "UserId": "4kq3w9m0vz1h8r6x2t5c7b4n0d", "UserName": "ada", "Email": "ada@northwind.example", "Role": "Admin" },
              { "UserId": "4m8n2b6v0c4x8z2l6k0j4h8g2f", "UserName": "sam", "Email": "sam@northwind.example", "Role": "Member" }
            ]
          },
          "AccountLocationsPart": {
            "Locations": [
              {
                "LocationId": "warehouse",
                "Name": "Warehouse",
                "Address": { "Country": "US", "AddressLine1": "200 Dock Road", "City": "Sparks", "Region": "NV", "PostalCode": "89431" }
              }
            ]
          },
          "B2BAccountPart": {
            "Enabled": true,
            "TaxId": "US-12-3456789",
            "BillingEmail": "ap@northwind.example",
            "OrderLimits": [
              { "UserId": "4m8n2b6v0c4x8z2l6k0j4h8g2f", "Limit": 2500 }
            ],
            "Terms": {
              "NetDays": 30,
              "DepositPercent": 20,
              "CreditLimit": 50000,
              "RequirePurchaseOrder": true
            },
            "PriceListId": "4b7g3ke0q7mnvf3fbzbbyemqtn",
            "MonthlyBudget": 20000
          }
        }
      ]
    }
  ]
}
```

- Sam's orders over 2,500 and any order past the 20,000 monthly budget wait for Ada (an `Admin` member) to approve.
- 20% is paid at checkout; 80% goes on terms, due in 30 days, up to the credit left.
- Leave `Punchout` out of recipes: the key is stored only as a hash and is generated on the account editor.

## Deployment Plan

```json
{
  "steps": [
    {
      "name": "DeploymentPlans",
      "Plans": [
        {
          "Name": "B2B setup",
          "Steps": [
            { "Type": "PriceListsDeploymentStep", "Step": {} },
            { "Type": "AccountsDeploymentStep", "Step": { "IncludeRecords": false } }
          ]
        }
      ]
    }
  ]
}
```

Orders and terms invoices are not exported by these steps; they move with the Orders and Transactions modules.

## Read What the Current Buyer's Account Owes

```csharp
public sealed class AccountCreditService
{
    private readonly B2BAccountService _accounts;
    private readonly AccountTermsService _terms;

    public AccountCreditService(B2BAccountService accounts, AccountTermsService terms)
    {
        _accounts = accounts;
        _terms = terms;
    }

    public async Task<string> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var (account, buyer) = await _accounts.GetCurrentAsync(cancellationToken);

        if (account is null || !account.Terms.OffersTerms)
        {
            return null;
        }

        var (owed, currency) = await _terms.GetOwedAsync(account, cancellationToken);
        var available = await _terms.GetAvailableCreditAsync(account, cancellationToken);

        return $"{account.Name} owes {owed} {currency}; {available} {currency} of credit left. Your limit: {buyer?.OrderLimit?.ToString() ?? "none"}.";
    }
}

// Startup: services.AddScoped<AccountCreditService>();
```

`GetCurrentAsync` returns nulls when the signed-in user is not a member of an account that buys as a business.

## Add a Card to the Business Tab

```csharp
internal sealed class AccountCreditCardDisplayDriver : ContentDisplayDriver
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountCreditCardDisplayDriver(IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<B2BAccountPart>();

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin ||
            !contentItem.As<B2BAccountPart>().Enabled ||
            !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, B2BAccountsPermissions.ManageB2BAccounts))
        {
            return null;
        }

        return Initialize<AccountCreditCardViewModel>("AccountCreditCard", model => model.AccountId = contentItem.ContentItemId)
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Business, "5"));
    }
}

// Startup: services.AddScoped<IContentDisplayDriver, AccountCreditCardDisplayDriver>();
```

Each section checks its own permission; the built-in Business and Invoices cards require `ManageB2BAccounts` and show only while the account buys as a business.

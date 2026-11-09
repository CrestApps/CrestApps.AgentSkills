# Accounts Examples

## Full Accounts Recipe Step

Accounts are imported before their `Records`, so each record lands in its account. Each record names its account in `ContainedPart`. The members' `UserId` values must exist on the target site.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Accounts",
        "CrestApps.OrchardCore.Accounts.Members",
        "CrestApps.OrchardCore.Accounts.Locations"
      ],
      "disable": []
    },
    {
      "name": "Accounts",
      "Accounts": [
        {
          "ContentItemId": "4xf4a7cq978mj04mg6nfygm3dd",
          "ContentItemVersionId": "4v7cfa9x3jhpc8w7m6d0x4w1qe",
          "ContentType": "Account",
          "DisplayText": "Northwind Traders",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Northwind Traders" },
          "AccountPart": {},
          "AccountMembersPart": {
            "Members": [
              {
                "UserId": "4kq3w9m0vz1h8r6x2t5c7b4n0d",
                "UserName": "ada",
                "Email": "ada@northwind.example",
                "Role": "Admin",
                "ContactContentItemId": "4c2k1m9qz8a7b6c5d4e3f2g1h0"
              }
            ]
          },
          "AccountLocationsPart": {
            "Locations": [
              {
                "LocationId": "hq",
                "Name": "Head office",
                "Address": {
                  "Country": "US",
                  "AddressLine1": "1 Main Street",
                  "City": "Reno",
                  "Region": "NV",
                  "PostalCode": "89501"
                }
              }
            ]
          }
        }
      ],
      "Records": [
        {
          "ContentItemId": "4c2k1m9qz8a7b6c5d4e3f2g1h0",
          "ContentItemVersionId": "4c2k1m9qz8a7b6c5d4e3f2g1hv",
          "ContentType": "Contact",
          "DisplayText": "Ada Lovelace",
          "Latest": true,
          "Published": true,
          "ContainedPart": {
            "ListContentItemId": "4xf4a7cq978mj04mg6nfygm3dd",
            "ListContentType": "Account"
          }
        }
      ]
    }
  ]
}
```

- `Role` is the `AccountMemberRole` name (`Member` or `Admin`).
- `Records` is optional; leave it out to import accounts only.

## Deployment Plan with the Accounts Step

```json
{
  "steps": [
    {
      "name": "DeploymentPlans",
      "Plans": [
        {
          "Name": "Accounts",
          "Steps": [
            {
              "Type": "AccountsDeploymentStep",
              "Step": { "IncludeRecords": true }
            }
          ]
        }
      ]
    }
  ]
}
```

With `IncludeRecords` false the exported step has no `Records` array.

## Find an Account's Contact by Email

Register an `IAccountContactFinder` in the module that knows where a contact keeps its email. Return the one matching contact, or `null` when none or several match.

```csharp
public sealed class MemberContactFinder : IAccountContactFinder
{
    private readonly ISession _session;

    public MemberContactFinder(ISession session)
    {
        _session = session;
    }

    public async Task<string> FindContactByEmailAsync(string accountId, string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var items = await _session.Query<ContentItem, ContainedPartIndex>(index => index.ListContentItemId == accountId)
            .With<ContentItemIndex>(index => index.Latest && index.ContentType == "Member")
            .ListAsync(cancellationToken);

        var matches = items
            .Where(item => string.Equals(item.As<MemberPart>()?.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ContentItemId)
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }
}

// MemberPart is your own ContentPart with a string Email property.
// Startup: services.AddScoped<IAccountContactFinder, MemberContactFinder>();
```

The Account Members feature calls every registered finder when an account is saved and keeps the first non-empty answer on `AccountMember.ContactContentItemId`.

## Read the Account a User Signs In For

`IAccountMemberService` is registered by the Account Members feature; depend on `CrestApps.OrchardCore.Accounts.Members`.

```csharp
public sealed class AccountGreetingService
{
    private readonly IAccountMemberService _accountMemberService;

    public AccountGreetingService(IAccountMemberService accountMemberService)
    {
        _accountMemberService = accountMemberService;
    }

    public async Task<string> GetGreetingAsync(string userId, CancellationToken cancellationToken = default)
    {
        var account = await _accountMemberService.FindAccountAsync(userId, cancellationToken);

        if (account is null)
        {
            return null;
        }

        var member = account.Get<AccountMembersPart>()?.FindMember(userId);
        var headOffice = account.Get<AccountLocationsPart>()?.FindLocation("hq");

        return member?.Role == AccountMemberRole.Admin
            ? $"{account.DisplayText} (admin), {headOffice?.Address?.City}"
            : account.DisplayText;
    }
}
```

## List an Account's Records

Records sit in the account through `ContainedPart`, so query `ContainedPartIndex`:

```csharp
var records = await session.Query<ContentItem, ContainedPartIndex>(index => index.ListContentItemId == accountId)
    .With<ContentItemIndex>(index => index.Latest)
    .ListAsync();
```

Records referenced by id (orders, work orders, terms invoices) are found through their own stores, for example `OrderQuery.AccountId` or `ITransactionStore.GetByParentReferenceAsync` with `AccountsConstants.ReferenceTypes.Account`.

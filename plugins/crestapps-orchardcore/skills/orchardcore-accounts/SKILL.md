---
name: orchardcore-accounts
description: Skill for the CrestApps Orchard Core Accounts module (CrestApps.OrchardCore.Accounts) with its Account Members and Account Locations features. Covers the Account content type and AccountPart, the account picker and search endpoint, the records an account holds, IAccountChildTypeRule, account page sections placed with AccountPageTabs, editor tabs, members and IAccountMemberService, locations, IAccountContactFinder, AccountPartySource, the Account import column, the Accounts recipe and deployment steps, the admin menu, and the move from the Omnichannel CRM. Use this skill when requests mention accounts, the businesses or households that contacts, orders and work orders belong to, an account page tab, account members or locations. Strong matches include AccountPart, AccountsConstants, AccountPageTabs, IAccountContentTypeProvider, AccountRecordKinds, IAccountChildTypeRule, IAccountContactFinder, IAccountMemberService, AccountMembersPart, AccountLocationsPart and ManageAccountMembers.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Accounts

## One Record for Every Business and Household

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Accounts module. An account is the business or household that other records belong to. It is one content type, `Account`, that the CRM, Commerce (Accounts B2B) and Field Service extend instead of keeping an organization record of their own.

### Guidelines

- Install `CrestApps.OrchardCore.Accounts` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- Contracts live in `CrestApps.OrchardCore.Accounts.Abstractions` (`AccountsConstants`, `AccountPageTabs`, `AccountsAdminMenuOptions`, the parts and the `IAccount*` services) and services in `CrestApps.OrchardCore.Accounts.Core` (`AccountListSynchronizer`, `AccountMemberService`, `AccountPartySource`). Reference those from your module, not the module project.
- Any type that carries `AccountPart` is an account type. Never test for the type name `Account`; use `AccountRecordKinds.IsAccount(definition)` or `IAccountContentTypeProvider.IsAccountTypeAsync(contentType)`.
- A record belongs to an account through Orchard Core's `ContainedPart` (`ListContentItemId` is the account's content item id). Records kept outside the content store name the account by id instead (`Order.AccountId`, `WorkOrder.AccountId`).
- Link to the search with `Url.RouteUrl(AccountsConstants.RouteNames.Search)`, never a hard-coded path.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Accounts | `CrestApps.OrchardCore.Accounts` | `CrestApps.OrchardCore.Resources`, `OrchardCore.Contents`, `OrchardCore.ContentTypes`, `OrchardCore.Lists`, `OrchardCore.Title` |
| Account Members | `CrestApps.OrchardCore.Accounts.Members` | `CrestApps.OrchardCore.Accounts`, `OrchardCore.Users` |
| Account Locations | `CrestApps.OrchardCore.Accounts.Locations` | `CrestApps.OrchardCore.Accounts`, `CrestApps.OrchardCore.Addresses` |

All three are in the **Content Management** category.

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
    }
  ]
}
```

Optional integrations light up in startups marked `[RequireFeatures]`: `OrchardCore.Recipes.Core` (the `Accounts` step and the list sync after `ContentDefinition` steps), `OrchardCore.Deployment` (the deployment step), `CrestApps.OrchardCore.Recipes` (step schemas) and `CrestApps.OrchardCore.ContentTransfer` (the `Account` column).

## The Account Type

The Accounts migration defines the attachable `AccountPart` and creates the `Account` type (creatable, listable, securable) only when no type of that name exists, so a site where the CRM created it keeps its settings and items.

| Part | Position | Added by |
|---|---|---|
| `TitlePart` | 1 | Accounts (the account's name) |
| `AccountPart` | 2 | Accounts (a marker with no data; settings `AccountPartSettings.OfferedContentTypes`) |
| `ListPart` | 3 | Accounts (the records it holds, page size 20, with a header) |
| `AccountMembersPart` | 4 | Account Members |
| `AccountLocationsPart` | 5 | Account Locations |
| `B2BAccountPart` | 10 | Accounts B2B (see `orchardcore-accounts-b2b`) |

Members, locations and B2B parts are attached only to the type named `Account`; give them to another account type (such as `Household`) by hand.

### What Belongs to an Account

The module knows no child types. Each module whose records sit in an account registers an `IAccountChildTypeRule` (`CanBelongToAccount`, `MustNotBelongToAccount`). A type is a child when a rule allows it, no rule excludes it, and it is not an account type. The CRM allows contacts and opportunities and excludes leads.

`AccountListSynchronizer` keeps each account type's `ListPart.ContainedContentTypes` in line with the rules when a part is attached or detached, when a type is imported, and after a recipe's `ContentDefinition` or `ReplaceContentDefinition` step. A type is offered once (recorded in `OfferedContentTypes`), so an admin who removes it keeps it out; an excluded type is always removed.

```csharp
public sealed class MemberAccountChildTypeRule : IAccountChildTypeRule
{
    public bool CanBelongToAccount(ContentTypeDefinition definition)
        => AccountRecordKinds.HasPart(definition, "MemberPart");

    public bool MustNotBelongToAccount(ContentTypeDefinition definition)
        => false;
}

// Startup: services.AddScoped<IAccountChildTypeRule, MemberAccountChildTypeRule>();
```

### Picker and Search

- **Account picker.** Every child type gets an **Account** field (`AccountPicker_Edit`, `Parts:0`) from a content display driver, not a part. It writes, moves or removes the record's `ContainedPart`, leaves alone a record kept in a list that is not an account, refuses an account the editor may not view, and shows the account as a badge on the admin list row.
- **Search.** `GET /Admin/api/crestapps/accounts/search?query=acme` returns up to 50 `{ value, text }` items (content item id and title), ordered by title, from the latest versions. It needs `ListContent`, and searches only account types the user has `ViewContent` on. Route name `CrestApps.Accounts.Search`.
- **Parties.** `AccountPartySource` registers kind `Account` (`PartyKinds.Account`) with `IPartyDirectory`, so asset owners and work order pickers find and name accounts (see `orchardcore-teams` for parties).

## The Account Page

The page is Orchard Core's `DetailAdmin` display of the account (**View** on its row). The `ContentZone__AccountPage` alternate (from `AccountPageShapeTableProvider`) draws the tabs as a side list (a dropdown on a phone), and the selected section is kept in the URL hash (`#account-contacts`).

| Tab | `AccountPageTabs` | Position | Added by |
|---|---|---|---|
| Overview | `Overview` | 0 | Account Members (members, `Content:5`) and Account Locations (addresses, `Content:6`) |
| Contacts, Opportunities, Leads, Activities | `Contacts`, `Opportunities`, `Leads`, `Activities` | 10, 20, 30, 40 | Omnichannel CRM (`orchardcore-omnichannel-management`) |
| Orders | `Orders` | 50 | Orders (`Order.AccountId`, see `orchardcore-orders`) |
| Business, Invoices | `Business`, `Invoices` | 60, 70 | Accounts B2B (`orchardcore-accounts-b2b`) |
| Service | `Service` | 80 | Work Orders (work orders `1`, service agreements `3`) and Assets (`2`) |
| Records | `Records` | 100 | Accounts (Orchard Core's list, via `placement.json`) |

- `AccountPageTabs.Location(tab, position = "1")` returns `Content:{position}#{tab};{tabPosition}`; a tab name the class does not know gets position 90 (after Service).
- `AccountPageTabs.EditorLocation(tab)` returns `Parts:{position}#{tab};{tabPosition}`. The editor has `Members` (5), `Locations` (6) and `Business` (60) tabs.
- Each section checks its own permissions and shows only when its feature is on.

```csharp
internal sealed class AccountProjectsDisplayDriver : ContentDisplayDriver
{
    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();

    public override IDisplayResult Display(ContentItem contentItem, BuildDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin)
        {
            return null;
        }

        return Initialize<AccountProjectsViewModel>("AccountProjects", model => model.AccountId = contentItem.ContentItemId)
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location("Projects"));
    }
}

// Startup: services.AddScoped<IContentDisplayDriver, AccountProjectsDisplayDriver>();
```

## Members and Locations

**Account Members** (`AccountMembersPart.Members`, each `AccountMember` with `UserId`, `UserName`, `Email`, `Role` `Member` or `Admin`, `ContactContentItemId`):

- The **Members** editor card needs `ManageAccountMembers`; it takes a user name or email and refuses a missing user or one who is already a member of another account (a user belongs to one account at most).
- `AccountMemberIndex` (`AccountContentItemId`, `UserId`, `Published`, `Latest`) backs `IAccountMemberService.FindAccountAsync(userId)` (the published account) and `FindOtherAccountIdAsync(userId, exceptAccountId)`.
- On save, each member is linked to the account's contact with the same email through the registered `IAccountContactFinder`s (the CRM registers one). No match, or more than one, leaves `ContactContentItemId` empty.
- Keep your own per-member settings by user id in a part of your own; read them with `account.Get<AccountMembersPart>()?.FindMember(userId)`.

**Account Locations** (`AccountLocationsPart.Locations`, each `AccountLocation` with a stable `LocationId`, `Name` and an `Address` entered with the shared address editor, see `orchardcore-addresses`). `FindLocation(locationId)` reads one. Accounts B2B offers them as shipping addresses; Work Orders uses one to fill an empty service address.

Full `IAccountContactFinder` and member lookup examples are in [references/accounts-examples.md](references/accounts-examples.md).

## Admin Menu and Permissions

- A top-level **Accounts** item (position `after.5`, id `accounts`) opens Orchard Core's content list for every account type, with `ListContent`. It is hidden when there is no account type, or when a module sets `AccountsAdminMenuOptions.ShowAccountsMenu = false`. The CRM does this and lists accounts under **Interaction Center > Accounts**.
- Accounts use Orchard Core's content permissions; the `Account` type is securable, so roles can be given rights to accounts alone.

| Permission | Display name | Feature | Default role |
|---|---|---|---|
| `ManageAccountMembers` | Manage the users who sign in for accounts | Account Members | Administrator |

```csharp
services.Configure<AccountsAdminMenuOptions>(options => options.ShowAccountsMenu = false);
```

## Import, Export, Recipes and Deployment

- **Content Transfer.** Every child type gets an `Account` column (also read as `Account Name` or `AccountName`). Import matches the account by exact title among the latest account versions; no match or more than one leaves the record where it was. Export writes the title. See `orchardcore-content-transfer`.
- **`Accounts` recipe step** (needs `OrchardCore.Recipes.Core`). Imports `Accounts` then `Records` with `IContentManager.ImportAsync`, like the `Content` step, so an existing `ContentItemId` is updated and reruns change nothing. An `Accounts` item without `AccountPart`, or any item without `ContentItemId` or `ContentType`, is a recipe error and skipped.

```json
{
  "steps": [
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
          "AccountPart": {}
        }
      ],
      "Records": []
    }
  ]
}
```

- **Accounts deployment step** (`AccountsDeploymentStep`, category **Content**, `IncludeRecords` true by default) exports every latest account with all its parts, plus the items in the accounts' lists when `IncludeRecords` is on, as one `Accounts` step.
- With CrestApps Recipes on, `AccountsRecipeStepSchema` and `AccountsDeploymentStepSchema` validate both (see `orchardcore-crestapps-recipes`).
- Accounts are ordinary content items, so the `Content` step and **All Content** deployment also move them.

A full step with members, locations and records, and a deployment plan, are in [references/accounts-examples.md](references/accounts-examples.md).

## Moving from the Omnichannel CRM

| Before | Now |
|---|---|
| `CrestApps.OrchardCore.Omnichannel.Core.Models.AccountPart`, `AccountPartSettings` | `CrestApps.OrchardCore.Accounts.Models` (package `CrestApps.OrchardCore.Accounts.Abstractions`) |
| `OmnichannelConstants.ContentParts.Account`, `OmnichannelConstants.ContentTypes.Account` | `AccountsConstants.ContentParts.Account`, `AccountsConstants.ContentTypes.Account` |
| `Admin/api/crestapps/omnichannel/crm/accounts/search` | `Admin/api/crestapps/accounts/search`, route `CrestApps.Accounts.Search` |
| CRM's `AccountContentImportHandler`, account picker and view model | The Accounts module |

Stored accounts are unaffected (parts are stored by name). The CRM (`CrestApps.OrchardCore.Omnichannel.Crm`) depends on Accounts, and `CrmAccountsFeatureMigrations` records the Accounts feature as enabled after the request ends, retrying at the next start until it is on.

## Removing an Account

When an account's last version is removed, `AccountPartHandler` detaches its records (removes their `ContainedPart`) after the request, in batches of 100. Records that hold the id (orders, work orders, B2B terms invoices) keep the removed id, and nothing stops removing an account that still owes money on B2B terms. Add a `ContentPartHandler<AccountPart>` to react yourself.

## Gotchas

| Symptom | Check |
|---|---|
| No **Account** field on a type | No `IAccountChildTypeRule` allows it, a rule excludes it, or the type is an account type. |
| A type keeps disappearing from the account list | An admin removed it after it was offered (`OfferedContentTypes`), or a rule's `MustNotBelongToAccount` is true. |
| Search returns nothing | The user lacks `ListContent`, or `ViewContent` on the account type. Titles match with `Contains`. |
| Search 404 on a site with a custom admin prefix | The pattern is literally `Admin/api/crestapps/accounts/search`; use the named route. |
| Import leaves records outside their account | Two accounts share the title, or the title does not match exactly. |
| Members or Locations tab missing on a second account type | Those migrations attach the parts to `Account` only. |
| Member has no contact | No `IAccountContactFinder` registered (CRM off), or zero or several contacts in the account have that email. |
| `Accounts` step does nothing | `OrchardCore.Recipes.Core` is not enabled, or the items lack `AccountPart`. |
| **Accounts** menu missing | `ShowAccountsMenu` is false (CRM on) or no type has `AccountPart`. |

Related skills: `orchardcore-accounts-b2b`, `orchardcore-omnichannel-management`, `orchardcore-orders`, `orchardcore-work-orders`, `orchardcore-assets`, `orchardcore-addresses`, `orchardcore-content-transfer`, `orchardcore-teams`.

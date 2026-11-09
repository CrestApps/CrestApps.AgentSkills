---
name: orchardcore-companies
description: Pointer skill for the removed CrestApps Orchard Core Companies module (CrestApps.OrchardCore.Companies). In CrestApps.OrchardCore 3.0 a business is an Account with B2BAccountPart from Accounts B2B (CrestApps.OrchardCore.Accounts.B2B), so Company, CompanyBuyer, CompanyLocation, CompanyTerms, the company-terms tender, CompanyPriceProvider, CompanyApprovalService and ManageCompanies are gone. Use this skill only to map old Companies names to Accounts B2B; for real work use orchardcore-accounts-b2b and orchardcore-accounts. Use this skill when requests mention the Companies module, Order.CompanyId, the account/company storefront route, ManageCompanies, or upgrading a site that sold to companies.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "2.0"
---

# CrestApps Orchard Core Companies (removed)

The Companies module no longer exists. Selling to businesses moved to **Accounts B2B**: a business is an `Account` content item (Accounts module) with `B2BAccountPart`, its buyers are the account's members and the places it ships to are the account's locations.

- For B2B terms, purchase orders, price lists, approvals, budgets and punchout, use the `orchardcore-accounts-b2b` skill.
- For the account record, the account page, members, locations and the `Accounts` recipe step, use the `orchardcore-accounts` skill.

## Name map

| Companies (removed) | Accounts B2B |
|---|---|
| `CrestApps.OrchardCore.Companies` | `CrestApps.OrchardCore.Accounts.B2B` (needs Accounts, Account Members and Account Locations) |
| `Company` | `Account` content item with `B2BAccountPart` |
| Company buyers and locations | Account members and account locations |
| `Order.CompanyId`, `OrderIndex.CompanyId`, `OrderQuery.CompanyId` | `AccountId` |
| `ApiOrder.companyId` | `accountId` |
| Routes `account/company`, `checkout/{id}/company` | `account/business`, `checkout/{id}/business` |
| Company terms tender | Tender key `account-terms` |
| Permission `ManageCompanies` | `ManageB2BAccounts` |
| Report Builder **Companies** source | **B2B accounts** (`B2BAccounts`, with `B2BAccounts` and `B2BBuyers` data sets) |

## Upgrade notes

- No migration converts company records into accounts. Recreate each business as an account (for example with the `Accounts` recipe step) and set `Order.AccountId` on orders that must keep their business.
- The Orders migration adds the order index's account column; the old company column is no longer read.

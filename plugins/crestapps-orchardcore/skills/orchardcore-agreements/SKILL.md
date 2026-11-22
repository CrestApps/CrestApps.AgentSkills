---
name: orchardcore-agreements
description: Skill for the CrestApps Orchard Core Agreements module (CrestApps.OrchardCore.Agreements), support and maintenance contracts with accounts. Covers the Agreement record with its SA- number, status, period and covered hours (a block or a monthly allowance with rounding), Content > Agreements, the account Service tab, the ManageAgreements and ViewAgreements permissions, the Agreements recipe and deployment step, module terms in Properties with IAgreementTermsValidator, IAgreementUsageProvider and IAgreementBalanceService, the hourly lifecycle task with IAgreementObserver, account manager notifications, and the Agreement Usage and Burn-down reports. Use this skill when requests mention service agreements, support contracts, prepaid or retainer hours, entitlements or agreement expiry. Strong matches include Agreement, AgreementHours, AgreementStatus, AgreementBalance, AgreementUsage, IAgreementManager, IAgreementObserver and TicketAgreementTerms.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Agreements

## Contracts With Accounts, Shared by Every Module

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Agreements module. An agreement is a contract with an account (period, status, the hours it covers). One agreement carries the terms of several modules at once: Tickets keeps the desks and ticket types it covers and their service-level policy, Work Orders keeps visit schedules, and both count the time they spend against its hours.

### Guidelines

- Install `CrestApps.OrchardCore.Agreements` in the web/startup project. It depends on Accounts (`orchardcore-accounts`).
- Reference `CrestApps.OrchardCore.Agreements.Abstractions` (models, `IAgreementManager`, `IAgreementBalanceService`, `IAgreementUsageProvider`, `IAgreementObserver`, `IAgreementTermsValidator`, `AgreementsConstants`, `AgreementsPermissions`) or `CrestApps.OrchardCore.Agreements.Core` (`AddAgreementsCore()`, store, `AgreementHandler`, `AgreementBalanceService`, `AgreementLifecycle`, `AgreementPeriods`, `AgreementBurnDown`) from your module.
- Write agreements through `IAgreementManager` (an `ICatalogManager<Agreement>`) so numbering, validation and every terms validator run.
- Keep your module's terms in `agreement.Properties` (`agreement.Put(new MyTerms { ... })`), not in a separate store.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Feature | ID | Depends on |
|---|---|---|
| Agreements | `CrestApps.OrchardCore.Agreements` | `CrestApps.OrchardCore.Accounts`, `CrestApps.OrchardCore.Resources` |

Category **Organization**.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Agreements"
      ],
      "disable": []
    }
  ]
}
```

## The Agreement

`Agreement` is a `CatalogItem` in the `Agreements` collection (`AgreementIndex`).

| Member | Meaning |
|---|---|
| `Number` | `SA-00042`, from the locked `Agreements.Agreement` number sequence |
| `Name`, `Notes` | What people call it, notes for the people who work under it |
| `Status` | `Draft`, `Active`, `Paused`, `Expired`, `Canceled`; only an active agreement in its period is in force (`IsInForceOn(date)`) |
| `AccountId` | The account content item it is with |
| `StartDate`, `EndDate` (`DateOnly`), `TimeZoneId` | The period in its time zone; no end runs until canceled |
| `Hours` | `AgreementHours { Kind, Hours, RoundingMinutes }`: `Kind` is `None`, `Block` (used until it runs out) or `MonthlyAllowance` (each calendar month); `RoundingMinutes` is 0, 6, 15 or 30 |
| `ExpiringNoticeDays` | 0 to 365, default 30 |
| `ExpiringNotifiedUtc`, `HoursWarnings` | What the lifecycle already announced this period |
| `Properties` | Terms other modules keep on it |

`AgreementHandler` checks the name, dates, notice, hours (more than none and at most 100,000 when it covers hours) and rounding, and calls every `IAgreementTermsValidator`, for the editor and recipes alike. Only a draft can be deleted; a copy starts as a draft.

## Admin UI and Permissions

| Menu or page | Routes | Permission |
|---|---|---|
| **Content → Agreements** | `Admin/agreements`, `Admin/agreements/create`, `Admin/agreements/edit/{id}`, `Admin/agreements/clone/{id}`, `Admin/agreements/delete/{id}` | `ViewAgreements` to list, `ManageAgreements` to change |
| Account page, **Service** tab | `AccountAgreementsDisplayDriver` lists the account's agreements with hours used and links to a new one | `ViewAgreements` |

| Permission | Display name | Default role |
|---|---|---|
| `ManageAgreements` | Manage agreements with accounts | Administrator |
| `ViewAgreements` | View agreements with accounts and their hours (implied by `ManageAgreements`) | Administrator |

The editor shows the general values, the **Covered hours** panel with how the hours stand now (`AgreementHoursDisplayDriver`), and panels other modules add with a `DisplayDriver<Agreement>` (Tickets' **Tickets** panel, Work Orders' **Visits** panel).

## Recipes and Deployment

The `Agreements` step (`AgreementsConstants.RecipeStepName`) creates or updates agreements by `ItemId`, with every value above and other modules' `Properties`. The **Agreements** deployment step exports them all; both publish JSON schemas for CrestApps Recipes. Enums are written as names, dates as `yyyy-MM-dd`.

```json
{
  "steps": [
    {
      "name": "Agreements",
      "Agreements": [
        {
          "ItemId": "4agr0000000000000000000001",
          "Name": "Gold support",
          "Status": "Active",
          "AccountId": "4d33nzx1h8tcms96qw9qsjn9xj",
          "StartDate": "2026-11-01",
          "EndDate": "2027-10-31",
          "TimeZoneId": "America/New_York",
          "ExpiringNoticeDays": 30,
          "Hours": { "Kind": "MonthlyAllowance", "Hours": 10, "RoundingMinutes": 15 },
          "Properties": {
            "TicketAgreementTerms": {
              "DeskIds": [ "49vyvps9npqdzz9mz7xfwqgdzx" ],
              "TicketTypes": [],
              "ServiceLevelPolicyId": "47bpbxt94eb7qbn8wjb9rg9eb3"
            }
          }
        }
      ]
    }
  ]
}
```

Import the account (a `Content` step) and any desks or policies the terms name before this step.

## Hours Used and Left

`IAgreementBalanceService.GetBalanceAsync(agreement, date = null)` finds the period that holds the day (the whole agreement for a block, the calendar month in its time zone for an allowance), asks every `IAgreementUsageProvider` for `AgreementUsage` entries (`Source`, `SourceRef`, `Description`, `UserId`, `UserName`, `StartUtc`, `Minutes`), rounds each entry up and returns `AgreementBalance`: `PeriodStart`, `PeriodEnd`, `CoveredMinutes`, `UsedMinutes`, `LeftMinutes`, `OverageMinutes` (billable: work goes on past the cover), `UsedShare` and `Entries`. `ListInForceAsync(accountId, date = null)` returns an account's agreements in force on a day.

| Provider | Counts |
|---|---|
| `TicketAgreementUsageProvider` (Tickets + Time Tracker) | Time logged in a desk's ticket stage on tickets charged to the agreement, not the "not billed" stage, not non-billable tickets |
| `WorkOrderAgreementUsageProvider` (Work Orders) | Billable labor of completed work orders |

## Lifecycle, Observers and Notifications

- The **Agreements** background task (hourly at minute 10) runs `AgreementLifecycle`: an active agreement past its end becomes `Expired`; one within its notice calls `IAgreementObserver.OnExpiringAsync` once; one whose used hours pass 80% and 100% (`AgreementsConstants.WarningPercents`) calls `OnHoursUsedAsync(agreement, percent, balance)` once each, starting again each period.
- With `OrchardCore.Notifications` on, `AgreementAccountManagerNotifier` tells the account's manager (`AccountTeamPart.OwnerStaffId`, through the staff directory, to their user) with the notification types `AgreementHours` (**Agreement hours**) and `AgreementEnding` (**Agreements ending**).

## Reports

With the Reports feature on (`AgreementReportsStartup`), two reports under **Reports → Billing & Usage** need `ViewAgreements`, with an **Agreement** filter:

- **Agreement Usage**: covered, used, left and over per agreement, share used (colored from 80% and at 100%), the day the hours ran out or are expected to (`AgreementBurnDown.ProjectRunOut`), and where the hours went by usage provider.
- **Agreement Burn-down**: hours left at the end of each day against an even pace, for the chosen agreement or the six that used the most.

Tickets adds **Account Health** (under **Executive**) with each account's agreement hours used. See `orchardcore-tickets`.

## Extending Agreements

| Seam | Register with | Use it to |
|---|---|---|
| Terms in `Properties` + `DisplayDriver<Agreement>` | `services.AddDisplayDriver<Agreement, T>()` | Add your panel to the editor |
| `IAgreementTermsValidator` | `services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgreementTermsValidator, T>())` | Return `ValidationResult`s for your terms; runs for the editor and recipes |
| `IAgreementUsageProvider` (`Name`, `ListAsync(agreement, fromUtc, toUtc)`) | `services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgreementUsageProvider, T>())` | Report time your module spent under an agreement |
| `IAgreementObserver` (`OnExpiringAsync`, `OnHoursUsedAsync`) | `services.AddScoped<IAgreementObserver, T>()` | Act when an agreement is about to end or passes a share of its hours |
| `ICatalogEntryHandler<Agreement>` | `TryAddEnumerable` | Keep your records in step |

```csharp
public sealed class InspectionAgreementTerms
{
    public int VisitsPerYear { get; set; }
}

public sealed class InspectionAgreementTermsValidator : IAgreementTermsValidator
{
    public Task<IReadOnlyList<ValidationResult>> ValidateAsync(Agreement agreement, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ValidationResult> results =
            agreement.TryGet<InspectionAgreementTerms>(out var terms) && terms.VisitsPerYear is < 0 or > 365
                ? [new ValidationResult("Visits per year must be between 0 and 365.", [nameof(InspectionAgreementTerms.VisitsPerYear)])]
                : [];

        return Task.FromResult(results);
    }
}

public sealed class InspectionAgreementUsageProvider : IAgreementUsageProvider
{
    private readonly IInspectionStore _inspections;

    public InspectionAgreementUsageProvider(IInspectionStore inspections)
    {
        _inspections = inspections;
    }

    public string Name => "Inspections";

    public async Task<IReadOnlyList<AgreementUsage>> ListAsync(Agreement agreement, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var visits = await _inspections.ListCompletedAsync(agreement.ItemId, fromUtc, toUtc, cancellationToken);

        return [.. visits.Select(visit => new AgreementUsage
        {
            Source = "Inspection",
            SourceRef = visit.Id,
            Description = visit.Title,
            UserId = visit.InspectorId,
            StartUtc = visit.StartedUtc,
            Minutes = visit.Minutes,
        })];
    }
}

// Startup:
// services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgreementTermsValidator, InspectionAgreementTermsValidator>());
// services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgreementUsageProvider, InspectionAgreementUsageProvider>());
```

`IInspectionStore` stands for your module's own store.

## Consumers

| Module | Terms | What it does |
|---|---|---|
| Tickets | `TicketAgreementTerms { DeskIds, TicketTypes, ServiceLevelPolicyId }`, **Tickets** panel | A new ticket with an account takes the earliest agreement in force that covers its desk and type; its policy comes first; time on tickets counts against the hours. See `orchardcore-tickets`. |
| Work Orders | `WorkOrderAgreementTerms`, **Visits** panel | Visit schedules create work orders; billable labor counts against the hours. See `orchardcore-work-orders`. |

## Troubleshooting

| Symptom | Check |
|---|---|
| Agreement not applied to new tickets | Not `Active`, outside its period, the terms don't cover the desk or type, or the ticket has no account. |
| Balance shows no used hours | No usage provider (enable Time Tracker for tickets), or time was logged in a non-billed stage. |
| Can't delete an agreement | Only drafts can be deleted; cancel it instead. |
| No expiry or 80% notifications | The **Agreements** background task is off, Notifications is off, or the account has no manager with a user. |
| Recipe refused an agreement | Hours over 100,000 or none with a covering kind, rounding not 0/6/15/30, end before start, notice over 365 days, or a terms validator refused it. |

Related skills: `orchardcore-accounts`, `orchardcore-tickets`, `orchardcore-work-orders`, `orchardcore-time-tracker`, `orchardcore-service-levels`, `orchardcore-reports`.

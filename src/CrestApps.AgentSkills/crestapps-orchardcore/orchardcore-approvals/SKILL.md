---
name: orchardcore-approvals
description: Skill for the CrestApps Orchard Core Approvals module (CrestApps.OrchardCore.Approvals), the approvals building block any module extends. Covers registering an approval type with AddApprovalType and an ApprovalTypeHandlerBase handler, asking with IApprovalService.RequestAsync and ApprovalSubmission, policies chosen by type, amount, currency and scope with ordered stages, approver rules (Permission, Role, Users, Manager, Staff) and custom IApproverRuleProvider, requester and subject rules, ISourceAccess, the Waiting for me inbox, notification types, IApprovalObserver, display drivers, the ApprovalPolicies and ApprovalRequests steps, the Approval requests data set, workflow events and tasks, the reminder recipe and permissions. Use this skill when requests mention approvals, approval policies, stages, approvers, pay run approval or adding an approval step to a module. Strong matches include IApprovalService, IApprovalTypeHandler, ApprovalPolicy, ApprovalRequest, ApproverRule and RequestApprovalTask.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Approvals

## Approvals for Any Module

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Approvals module. A module registers an **approval type** (Payroll registers `PayRun`), asks for approval of one of its records, and reacts when the request is approved, rejected or withdrawn. A **policy** chosen by type and conditions says who decides, in which **stages**, and how many must approve each stage. Every decision is kept with who, when and why. Approvals records decisions; reminders and escalations are workflows.

### Guidelines

- Install `CrestApps.OrchardCore.Approvals` in the web/startup project. Reference `CrestApps.OrchardCore.Approvals.Abstractions` (models, contracts, `AddApprovalType`, `AddApproverRuleProvider`, `AddApprovalObserver`) from your module's `.Core` or `.Abstractions`; `CrestApps.OrchardCore.Approvals.Core` holds the store, resolver, service and inbox (`AddApprovalsCore()`).
- Make your feature depend on `CrestApps.OrchardCore.Approvals` (as Payroll does) so the type is always registered with the service.
- The type name is stored on requests and policies and never changes. Only Payroll's `PayRun` type ships today; tickets, time sheets and expenses are added the same way.
- Register an `ISourceAccess` whose `SourceKind` is your type name so approvers and viewers are limited to the records they may act on (`AccessScope`, such as an employer).
- Do the record's state change in the handler's closing hooks (`ApprovedAsync`, `RejectedAsync`, `CanceledAsync`); they run in the decision's unit of work.
- Pass `SubjectVersion` and compare it in `DecidingAsync` so an approval is for the version that was asked. Asking again with another version withdraws the pending request and asks again.
- `LocalizationSource` properties (such as `ApprovalType.DisplayName`, `IApproverRuleProvider.DisplayName`) are `[JsonIgnore]`; repeat it on overrides.
- All recipe JSON must be wrapped in `{ "steps": [...] }`. All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Feature | ID | Depends on |
|---|---|---|
| Approvals | `CrestApps.OrchardCore.Approvals` | CrestApps Resources (uses Staff, Notifications, Workflows, Audit Trail, Recipes, Deployment, Data Pipelines Core when they are on) |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Approvals"
      ],
      "disable": []
    }
  ]
}
```

## How a Request Moves

1. A module calls `IApprovalService.RequestAsync(ApprovalSubmission, requester)` (one request at a time per subject, under a lock).
2. The policy is the one named in `Policy` (id or technical name, used whatever its conditions; refused if it doesn't serve the type), or the first enabled policy by `Order` that serves the type, whose standard conditions match and whose handler's `MatchesAsync` accepts (`IApprovalPolicyResolver`). No match is an error.
3. The request copies the policy's stages (later policy edits never change it), then the handler's `RequestingAsync` fills the title and page, adds approvers, stores data with `Put`, or refuses.
4. The current stage's approvers are told (notification type `Approval`). A person a rule of the stage names may decide, after the type's source access (Manage level) and the handler's `CanAccessAsync` and `DecidingAsync` agree.
5. When a stage has its `RequiredApprovals`, the next starts (`StageApprovedAsync`). One rejection rejects the request (`RejectedAsync`); the last stage approves it (`ApprovedAsync`). A withdrawn or replaced request calls `CanceledAsync`.

Built-in rules, checked at every stage: the requester can't approve (`AllowRequesterToApprove` off), a person in `SubjectUserIds` can't decide (`AllowSubjectToDecide` off), and a person decides at most one stage (`AllowSamePersonAcrossStages` off). Rejecting needs a note. Decisions run under a per-request lock on the committed request (`IApprovalRequestManager.FindCommittedAsync`) with a concurrency check.

`ApprovalStatus`: `Pending`, `Approved`, `Rejected`, `Canceled`.

## Register a Type and Ask for Approval

```csharp
using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.SourceAccess;
using OrchardCore.Localization;

// In your module's Startup (feature depends on CrestApps.OrchardCore.Approvals):
services.AddApprovalType<ExpenseApprovalTypeHandler>("Expense", type =>
{
    type.DisplayName = LocalizationSource.Create<ExpenseApprovalTypeHandler>("Expense report");
    type.Description = LocalizationSource.Create<ExpenseApprovalTypeHandler>("An expense report approved before it is reimbursed.");
    type.Conditions = ApprovalConditions.Amount | ApprovalConditions.Scope;
});
services.AddSourceAccess<ExpenseSourceAccess>();
```

```csharp
var result = await _approvalService.RequestAsync(new ApprovalSubmission
{
    SubjectKind = "Expense",
    SubjectId = report.ItemId,
    SubjectStaffMemberId = report.StaffMemberId,
    SubjectUserIds = [report.UserId],
    SubjectVersion = report.Version,
    AccessScope = report.DepartmentId,
    Title = $"Expenses of {report.PersonName}, {report.Month}",
    Amount = report.Total,
    Currency = report.Currency,
}, User);

if (!result.Succeeded)
{
    // result.Errors: no policy matches, the handler refused, ...
}
```

`ApprovalType.Conditions` opts into the standard policy conditions: `Amount` (amount range and currency) and `Scope` (a **Scope** list filled by the handler's `GetScopesAsync`, matched to `ApprovalSubmission.AccessScope`). Conditions the type doesn't use are cleared when a policy is saved.

The full handler, a custom approver rule provider, a policy condition driver and request page drivers are in [references/approval-type-examples.md](references/approval-type-examples.md).

## Handler Hooks (`ApprovalTypeHandlerBase`)

| Hook | When |
|---|---|
| `RequestingAsync(ApprovalRequestingContext)` | Before the request is stored. Set `Request.Title`, `SubjectUrl`, `SubjectUserIds`, `SubjectStaffMemberId`; `AddApprover(rule)` adds a rule to every stage; `Request.Put(...)`; `Refuse(reason)`. |
| `DecidingAsync(ApprovalDecidingContext)` | Before a decision is recorded; `Refuse` when the record changed (`Request.SubjectVersion`). |
| `CanAccessAsync(request, user)` | Refuse a named approver the record isn't theirs to act on. |
| `StageApprovedAsync(request)` | A stage other than the last is approved. |
| `GetScopesAsync()` | The scopes the policy editor offers. |
| `MatchesAsync(policy, submission)` | A condition of your own (stored on the policy with `Put`). |
| `ApprovedAsync`, `RejectedAsync`, `CanceledAsync` | The request closes. |
| `GetSubjectUrlAsync`, `GetSubjectDisplayAsync` | The request page link and description. |

## Approver Rules

A stage's `Approvers` are `ApproverRule { Kind, Value }` (`ApproverRuleKinds`):

| `Kind` | Value | Names | Registered |
|---|---|---|---|
| `Permission` | Permission name | Anyone granted it | Always |
| `Role` | Role name | Anyone in the role | Always |
| `Users` | User ids, comma separated | Those users | Always |
| `Manager` | none | Managers up the chain of `SubjectStaffMemberId` | With Staff on |
| `Staff` | Staff member ids, comma separated | Their users | With Staff on |

Add your own with `IApproverRuleProvider` and `services.AddApproverRuleProvider<T>()`; its value editor is the `ApproverRuleValue_Edit` shape with the alternate `ApproverRuleValue_Edit__{Kind}` (`Views/ApproverRuleValue.Edit-{Kind}.cshtml`, model's `Editor` is `ApproverRuleValueEditorViewModel` with `FieldName` and `Value`). A rule whose provider is off names nobody. Old numeric kinds (0 Permission, 1 Role, 2 Manager, 3 Staff) still load through `ApproverRuleKindJsonConverter`.

## Policies

`ApprovalPolicy` (`INamedCatalogManager<ApprovalPolicy>`): `Name` (technical name modules and workflows ask for), `Title`, `Description`, `Type` (one type, fixed after creation), legacy `Types`, `Enabled` (chosen for requests that name none), `Order`, `MinimumAmount`, `MaximumAmount`, `Currency`, `AccessScope`, `Stages` (each `Title`, `Approvers`, `RequiredApprovals`; at most 10 stages of 10 rules), `AllowRequesterToApprove`, `AllowSubjectToDecide`, `AllowSamePersonAcrossStages`, `DueAfterHours`. A policy without `Stages` is one stage of its `Approvers` and `RequiredApprovals` (`GetStages()`). A policy with no `Type` and zero or several `Types` is a **General** policy that serves any type.

Payroll seeds `PayRunApproval` (type `PayRun`, one stage of `Permission` `ApprovePayRuns`, due after 48 hours) and names it when it submits a pay run.

```json
{
  "steps": [
    {
      "name": "ApprovalPolicies",
      "Policies": [
        {
          "Name": "LargeExpenseApproval",
          "Title": "Expenses over 1,000 USD",
          "Type": "Expense",
          "Enabled": true,
          "Order": 10,
          "MinimumAmount": 1000,
          "Currency": "USD",
          "Stages": [
            { "Title": "Manager", "Approvers": [ { "Kind": "Manager" } ], "RequiredApprovals": 1 },
            { "Title": "Finance", "Approvers": [ { "Kind": "Role", "Value": "Finance" } ], "RequiredApprovals": 1 }
          ],
          "AllowRequesterToApprove": false,
          "AllowSubjectToDecide": false,
          "AllowSamePersonAcrossStages": false,
          "DueAfterHours": 72
        }
      ]
    }
  ]
}
```

Policies are matched by id, then by `Name`. The `ApprovalRequests` step (entries `Requests`, each needs `SubjectKind` and `SubjectId`) imports history only and skips an existing `ItemId`. Deployment steps **Approval policies** and **Approval requests** write the same shapes (category Approvals).

## Admin Menu and Who Sees What

| Location | Permission |
|---|---|
| **Approvals > Requests** (tabs **Waiting for me**, **My requests**, **All requests**; **Type** filter) | None to decide what a rule names you for; `ViewApprovalRequests` for **All requests** |
| **Approvals > Policies** (**Add approval policy** dialog picks the type) | `ManageApprovalPolicies` |

| Permission | Allows |
|---|---|
| `ManageApprovalPolicies` | Add, edit and delete policies. Security critical. |
| `ViewApprovalRequests` | See every request the source access allows. Deciding still needs a rule that names you. |
| `ManageApprovalRequests` | Withdraw anyone's pending request the source access allows at Manage level. Security critical; implies View. |

The requester may withdraw their own pending request. Anyone else gets "not found". **Waiting for me** reads pending requests the person's source access allows in batches (`ApprovalInbox.BatchSize`, 500) and checks each current stage, so its count is exact.

## Notifications, Observers and Data

- With `OrchardCore.Notifications` on, the notification center is the inbox: type `Approval` (**Approvals to decide**) for the current stage's approvers, removed when the stage or request closes, and `ApprovalDecided` (**Approval decisions**) for the requester. People choose email or SMS per type in their notification preferences. See `orchardcore-notifications`.
- `IApprovalObserver.OnChangedAsync(ApprovalChange)` (`services.AddApprovalObserver<T>()`) runs after every change: `ApprovalChangeKind` `Requested`, `PartlyApproved`, `StageApproved`, `Approved`, `Rejected`, `Canceled`, with the `Decision`, `Actor` and `ActorId`.
- Audit Trail (when on, category **Approvals**) records approval policies added, changed and removed.
- With Data Pipelines Core on, the `Approvals` data source has the `ApprovalRequests` data set: one row per request with type, stage, policy, status, amount, approvals, requester, deciders and dates, limited by source access.

## Workflow Activities

| Activity | Kind | Notes |
|---|---|---|
| Approval Requested, Approval Partly Approved, Approval Stage Approved, Approval Approved, Approval Rejected, Approval Canceled | Events | Optional **Approval type** (`ApprovalType`) filter. Correlation id is the request id. Input `RequestId`, `SubjectKind`, `SubjectId`, `Title`, `PolicyId`, `PolicyTitle`, `Status`, `Change`, `RequesterName`, `ApproverUserNames`, `Decider`, `Note`, `DueUtc`, `Stage`, `Amount`, `Currency`. |
| Request Approval | Task | `SubjectKind`, `Policy`, `SubjectId`, `AccessScope`, `Title`, `SubjectUrl`, `Amount`, `Currency` (Liquid). Outcomes `Done`, `Failed`; writes `ApprovalRequestId`. |
| Approve Request, Reject Request | Tasks | Decide as the user the workflow runs for. Outcomes `Done`, `NotAllowed`, `NotFound`. |
| Get Approval Request | Task | Outcomes `Pending`, `Approved`, `Rejected`, `Canceled`, `NotFound`. |

The recipe **Automation: remind approvers** (`ApprovalsReminder`) adds a disabled workflow (**Remind approvers of pending requests**: Approval Requested Event, a Timer Event on `0 9 * * 1-5`, Get Approval Request Task and Notify User Task). Run it from **Tools > Recipes**, adjust the time in the workflow and enable it. There is no background task of its own.

## Troubleshooting

| Symptom | Check |
|---|---|
| "No approval policy matches" | No enabled policy serves the type with matching amount, currency or scope; or name the policy in `ApprovalSubmission.Policy`. |
| A named approver can't decide | They asked for it or are a subject (built-in rules), already decided an earlier stage, fail the type's source access at Manage level, or `CanAccessAsync` refused. |
| Policy editor warns fewer people can decide than needed | The stage's rules name fewer users today than `RequiredApprovals`. |
| Requests of a type show a do-nothing handler | The feature that registers the type is off; its requests and policies are kept. |
| Approval refused as out of date | The record's version changed since it was asked; the type asks again on the next submit. |
| Reminder workflow never runs | The `ApprovalsReminder` workflow is added disabled; enable it. |

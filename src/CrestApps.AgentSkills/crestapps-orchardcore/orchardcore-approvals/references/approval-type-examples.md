# Approval Type Examples

A worked expense report approval: the type handler, its source access, a custom approver rule provider, a policy condition of the type's own, and a request page driver. Namespaces: `CrestApps.OrchardCore.Approvals` (registration), `CrestApps.OrchardCore.Approvals.Models`, `CrestApps.OrchardCore.Approvals.Services`, `CrestApps.OrchardCore.SourceAccess`.

## The handler

```csharp
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;

public sealed class ExpenseApprovalTypeHandler : ApprovalTypeHandlerBase
{
    private readonly IExpenseReportManager _reports;
    private readonly IDepartmentDirectory _departments;

    public ExpenseApprovalTypeHandler(IExpenseReportManager reports, IDepartmentDirectory departments)
    {
        _reports = reports;
        _departments = departments;
    }

    public override async Task RequestingAsync(ApprovalRequestingContext context, CancellationToken cancellationToken = default)
    {
        if (await _reports.FindByIdAsync(context.Request.SubjectId, cancellationToken) is not { } report || report.IsPaid)
        {
            context.Refuse("The expense report can no longer be approved.");

            return;
        }

        context.Request.Title ??= $"Expenses of {report.PersonName}, {report.Month}";
        context.Request.SubjectUrl ??= $"~/Admin/expenses/{report.ItemId}";
        context.Request.Put(new ExpenseApprovalData { DepartmentId = report.DepartmentId });

        // Whoever the policy names, the department's finance controller decides too.
        context.AddApprover(new ApproverRule { Kind = ApproverRuleKinds.Role, Value = "Finance Controller" });
    }

    public override async Task DecidingAsync(ApprovalDecidingContext context, CancellationToken cancellationToken = default)
    {
        var report = await _reports.FindByIdAsync(context.Request.SubjectId, cancellationToken);

        if (report?.Version != context.Request.SubjectVersion)
        {
            context.Refuse("The expense report changed since approval was asked. Submit it again.");
        }
    }

    public override async Task<IReadOnlyList<ApprovalScope>> GetScopesAsync(CancellationToken cancellationToken = default)
        => (await _departments.ListAsync(cancellationToken))
            .Select(department => new ApprovalScope(department.Id, department.Name))
            .ToList();

    public override Task ApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => _reports.MarkApprovedAsync(request.SubjectId, cancellationToken);

    public override Task RejectedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => _reports.MarkRejectedAsync(request.SubjectId, request.Decisions[^1].Note, cancellationToken);

    public override Task CanceledAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => _reports.MarkDraftAsync(request.SubjectId, cancellationToken);
}

public sealed class ExpenseApprovalData
{
    public string DepartmentId { get; set; }
}
```

`AddApprover` adds the rule to every stage the request copied from its policy. A request is a catalog item: keep your data with `Put` and read it with `TryGet<T>(out var data)` in handlers, providers and drivers.

## Source access

The approver must pass the type's source access at the `Manage` level for the request's `AccessScope`; viewers with `ViewApprovalRequests` are limited at the `View` level.

```csharp
using System.Security.Claims;
using CrestApps.OrchardCore.SourceAccess;

public sealed class ExpenseSourceAccess : ISourceAccess
{
    private readonly IDepartmentDirectory _departments;

    public ExpenseSourceAccess(IDepartmentDirectory departments)
    {
        _departments = departments;
    }

    // The same name as the approval type.
    public string SourceKind => "Expense";

    public async Task<SourceAccessScope> GetScopeAsync(ClaimsPrincipal user, SourceAccessLevel level, CancellationToken cancellationToken = default)
    {
        var departmentIds = await _departments.GetDepartmentIdsOfAsync(user, cancellationToken);

        return departmentIds.Count == 0 ? SourceAccessScope.None : SourceAccessScope.Of(departmentIds);
    }
}
```

## A custom approver rule provider

```csharp
using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using OrchardCore.Localization;

public sealed class DepartmentHeadApproverRuleProvider : IApproverRuleProvider
{
    private readonly IDepartmentDirectory _departments;

    public DepartmentHeadApproverRuleProvider(IDepartmentDirectory departments)
    {
        _departments = departments;
    }

    public string Name => "DepartmentHead";

    [JsonIgnore]
    public LocalizationSource DisplayName { get; } = LocalizationSource.Create<DepartmentHeadApproverRuleProvider>("The head of the expense's department");

    public bool NeedsValue => false;

    // Who it names depends on each request's data.
    public bool DependsOnRequest => true;

    public async Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => (await GetUserIdsAsync(rule, request, cancellationToken)).Contains(user.FindFirstValue(ClaimTypes.NameIdentifier));

    public async Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
        => request.TryGet<ExpenseApprovalData>(out var data)
            ? await _departments.GetHeadUserIdsAsync(data.DepartmentId, cancellationToken)
            : [];
}

// services.AddApproverRuleProvider<DepartmentHeadApproverRuleProvider>();
```

Deciding calls `IsNamedAsync`; notices, **Waiting for me** and the approver count call `GetUserIdsAsync`. When `NeedsValue` is true, add `Views/ApproverRuleValue.Edit-DepartmentHead.cshtml` (alternate of `ApproverRuleValue_Edit`); its model's `Editor` (`ApproverRuleValueEditorViewModel`) gives the `FieldName` to post and the current `Value`.

## A policy condition of the type's own

```csharp
using CrestApps.OrchardCore.Approvals.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class ExpensePolicyCategoryDisplayDriver : DisplayDriver<ApprovalPolicy>
{
    public override bool CanHandleModel(ApprovalPolicy policy)
        => policy.Type == "Expense";

    public override IDisplayResult Edit(ApprovalPolicy policy, BuildEditorContext context)
        => Initialize<ExpensePolicyCategoryViewModel>("ExpensePolicyCategory_Edit", model =>
        {
            model.Category = policy.TryGet<ExpensePolicyCategory>(out var category) ? category.Category : null;
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(ApprovalPolicy policy, UpdateEditorContext context)
    {
        var model = new ExpensePolicyCategoryViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        policy.Put(new ExpensePolicyCategory { Category = model.Category });

        return Edit(policy, context);
    }
}

public sealed class ExpensePolicyCategory
{
    public string Category { get; set; }
}

public class ExpensePolicyCategoryViewModel
{
    public string Category { get; set; }
}
```

The handler checks it:

```csharp
public override async Task<bool> MatchesAsync(ApprovalPolicy policy, ApprovalSubmission submission, CancellationToken cancellationToken = default)
{
    if (!policy.TryGet<ExpensePolicyCategory>(out var condition) || string.IsNullOrEmpty(condition.Category))
    {
        return true;
    }

    var report = await _reports.FindByIdAsync(submission.SubjectId, cancellationToken);

    return report?.Category == condition.Category;
}
```

The policy editor's own shapes are `ApprovalPolicyFields_Edit` (`Content:1`), `ApprovalPolicyConditions_Edit` (`Content:2`, types with conditions) and `ApprovalPolicyStages_Edit` (`Content:10`). Each card of the **Add approval policy** dialog is `ApprovalPolicyType_Link` with the alternate `ApprovalPolicyType_Link__{Name}`.

## Show the record on the request page

The request page renders the request with display type `DetailAdmin`; list rows use `SummaryAdmin` and `SummaryAdminGrid`.

```csharp
using CrestApps.OrchardCore.Approvals.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class ExpenseApprovalDisplayDriver : DisplayDriver<ApprovalRequest>
{
    public override bool CanHandleModel(ApprovalRequest request)
        => request.SubjectKind == "Expense";

    public override IDisplayResult Display(ApprovalRequest request, BuildDisplayContext context)
        => View("ExpenseApproval_DetailAdmin", request)
            .Location("DetailAdmin", "Content:5");
}

// services.AddDisplayDriver<ApprovalRequest, ExpenseApprovalDisplayDriver>();
```

## Ask from a workflow

Add **Request Approval** with **Approval type** `Expense`, the subject id as Liquid (for example `{{ Workflow.Input.ReportId }}`), and optional policy, scope, title, page, amount and currency. Branch on `Done` or `Failed`; the new request id is in the output `ApprovalRequestId`. Start other workflows from **Approval Approved** or **Approval Rejected** with the **Approval type** filter set to `Expense`.

## Observe every change

```csharp
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;

public sealed class ExpenseApprovalObserver : IApprovalObserver
{
    public Task OnChangedAsync(ApprovalChange change, CancellationToken cancellationToken = default)
    {
        if (change.Request.SubjectKind != "Expense" || change.Kind != ApprovalChangeKind.StageApproved)
        {
            return Task.CompletedTask;
        }

        // For example, post to the team's channel that finance now has it.
        return Task.CompletedTask;
    }
}

// services.AddApprovalObserver<ExpenseApprovalObserver>();
```

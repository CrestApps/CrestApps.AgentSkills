# Data Formulas Examples

All examples target Orchard Core 4.0. The module project references `OrchardCore.DataSources.Abstractions` and `OrchardCore.DataSources.Core` (version `4.*`); the web/startup project references `OrchardCore.DataPipelines`.

## Formula recipes

| Goal | Formula |
|------|---------|
| Line total with tax | `ROUND([Price] * [Quantity] * 1.15, 2)` |
| Full name | `TRIM([FirstName] & ' ' & [LastName])` |
| Month bucket | `DATETRUNC('month', [CreatedUtc])` or `TEXT([CreatedUtc], 'yyyy-MM')` |
| Days open | `DATEDIFF('day', [OpenedUtc], NOW())` |
| Overdue flag | `[DueDate] < TODAY() AND NOT [Paid]` |
| Fallback text | `COALESCE([Nickname], [UserName], 'unknown')` |
| Banding | `IFS([Total] >= 1000, 'Large', [Total] >= 100, 'Medium', 'Small')` |
| Code mapping | `SWITCH([Country], 'CA', 'Canada', 'US', 'United States', 'Other')` |
| Email domain | `SPLIT([Email], '@', 2)` |
| Filter condition | `[Status] != 'Cancelled' AND IN([Region], 'East', 'West')` |
| Average order (aggregate consumers only) | `SUM([Total]) / COUNT()` |

## Scalar, context-aware and aggregate functions

```csharp
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.Modules;

[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class FormulasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Scalar: computed from the values of its arguments, one row at a time.
        services.AddExpressionFunction(ExpressionFunction.CreateScalar(
            name: "TAX",
            category: ExpressionFunctions.NumberCategory,
            signature: "TAX(amount)",
            description: "Returns the sales tax of an amount.",
            minArguments: 1,
            maxArguments: 1,
            returnType: _ => DataFieldType.Decimal,
            invoke: args => args[0] switch
            {
                long amount => Math.Round(amount * 0.15m, 2),
                decimal amount => Math.Round(amount * 0.15m, 2),
                _ => null,
            }));

        // Context-aware: reads ExpressionContext.Now, like the built-in NOW() and TODAY().
        services.AddExpressionFunction(new ExpressionFunction
        {
            Name = "AGE",
            Category = ExpressionFunctions.DateCategory,
            Signature = "AGE(date)",
            Description = "Returns the number of whole years from a date to now.",
            MinArguments = 1,
            MaxArguments = 1,
            ReturnType = _ => DataFieldType.Integer,
            Invoke = (args, context) =>
            {
                if (args[0] is not DateTime date)
                {
                    return null;
                }

                var years = context.Now.Year - date.Year;

                if (context.Now < date.AddYears(years))
                {
                    years--;
                }

                return (long)years;
            },
        });

        // Aggregate over all the values of the group: can only be computed over ExpressionContext.Group.
        services.AddExpressionFunction(ExpressionFunction.CreateAggregate(
            name: "RANGE",
            signature: "RANGE(number)",
            description: "Returns the difference between the largest and the smallest value of the group.",
            requiresNumericArgument: true,
            returnType: _ => DataFieldType.Decimal,
            aggregate: values =>
            {
                var numbers = values.Where(value => value is not null).Select(Convert.ToDecimal).ToList();

                // A group without values has no range.
                return numbers.Count == 0 ? null : numbers.Max() - numbers.Min();
            }));

        // Aggregate of a DataAggregate: computed as the rows arrive, like the built-in SUM.
        services.AddExpressionFunction(ExpressionFunction.CreateAggregate(
            name: "TOTAL",
            signature: "TOTAL(number)",
            description: "Same as SUM.",
            requiresNumericArgument: true,
            returnType: types => types.Count == 1 && types[0] == DataFieldType.Integer ? DataFieldType.Integer : DataFieldType.Decimal,
            function: DataAggregate.Sum));
    }
}
```

## A provider with several functions, replacing a built-in

```csharp
using System.Text.RegularExpressions;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;

public sealed partial class TextFunctionsProvider : IExpressionFunctionProvider
{
    public IEnumerable<ExpressionFunction> GetFunctions()
    {
        yield return ExpressionFunction.CreateScalar(
            name: "SLUG",
            category: ExpressionFunctions.TextCategory,
            signature: "SLUG(text)",
            description: "Converts text to lower case words separated by hyphens.",
            minArguments: 1,
            maxArguments: 1,
            returnType: _ => DataFieldType.Text,
            invoke: args => args[0] is string text
                ? NonWord().Replace(text.Trim().ToLowerInvariant(), "-").Trim('-')
                : null);

        // Same name as the built-in TRIM: this one replaces it, whatever the order of the startups.
        yield return ExpressionFunction.CreateScalar(
            name: "TRIM",
            category: ExpressionFunctions.TextCategory,
            signature: "TRIM(text)",
            description: "Removes leading and trailing spaces, and collapses the spaces between words.",
            minArguments: 1,
            maxArguments: 1,
            returnType: _ => DataFieldType.Text,
            invoke: args => args[0] is string text ? Spaces().Replace(text.Trim(), " ") : null);
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonWord();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
```

```csharp
// Singleton; the catalog builds once, on first use, and validates every function.
services.AddExpressionFunctionProvider<TextFunctionsProvider>();
```

## Listing the functions, such as for an editor

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources.Expressions;

public sealed class FormulaHelpService
{
    private readonly IExpressionFunctionCatalog _catalog;
    private readonly IStringLocalizer S;

    public FormulaHelpService(IExpressionFunctionCatalog catalog, IStringLocalizer<FormulaHelpService> localizer)
    {
        _catalog = catalog;
        S = localizer;
    }

    public IEnumerable<(string Category, string Signature, string Description)> GetScalarFunctions()
        => _catalog.Functions
            .Where(function => !function.IsAggregate)
            .Select(function => (function.Category, function.Signature, S[function.Description].Value));
}
```

## A data source that honors conditions

An orders module exposes a YesSql `MapIndex` (see `orchardcore-yessql-indexes`) as a data set, and translates the conditions it can into the index query. Everything it skips is still applied by the consumer, so the rule is: never filter **more** than the condition asks.

```csharp
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using YesSql;
using YesSql.Indexes;
using YesSql.Services;

namespace Orders.DataSources;

public sealed class OrderIndex : MapIndex
{
    public string OrderId { get; set; }

    public string CustomerName { get; set; }

    public decimal Total { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }
}

public sealed class OrdersDataSource : RecordDataSource, IDataSource
{
    public OrdersDataSource(ISession session, IAuthorizationService authorizationService, IStringLocalizer<OrdersDataSource> localizer)
    {
        DataSets = [new OrdersDataSet(session, authorizationService, localizer)];
    }

    public static string Name => "Orders";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrdersDataSource>("Orders");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrdersDataSource>("The orders of the shop.");

    protected override IEnumerable<IRecordDataSet> DataSets { get; }
}

public sealed class OrdersDataSet : RecordDataSet<OrderIndex>
{
    private const int PageSize = 500;

    // Set to true only when the database compares text ignoring case (check its collation).
    private static readonly bool StoreIgnoresTextCase = false;

    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;

    public OrdersDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer localizer)
        : base(new DataSetDescriptor("Orders", localizer["Orders"], localizer["One row per order."]) { DefaultDateField = "CreatedUtc" })
    {
        _session = session;
        _authorizationService = authorizationService;

        var orderId = AddField("OrderId", localizer["Order"], DataFieldType.Text, order => order.OrderId);
        orderId.IsIdentifier = true;
        orderId.IsKeyFilterable = true;

        AddField("CustomerName", localizer["Customer"], DataFieldType.Text, order => order.CustomerName);
        AddField("Total", localizer["Total"], DataFieldType.Decimal, order => order.Total);
        AddField("CreatedUtc", localizer["Created"], DataFieldType.DateTime, order => order.CreatedUtc);

        // A field that grows with every change, for "Read only new rows".
        AddField("ModifiedUtc", localizer["Modified"], DataFieldType.DateTime, order => order.ModifiedUtc);
    }

    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => context.User is not null && await _authorizationService.AuthorizeAsync(context.User, OrdersPermissions.ViewOrders);

    protected override async IAsyncEnumerable<OrderIndex> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conditions = query.Conditions;

        // In/Equals lists, intersected. Exact for a join key (IsJoinKey keeps the case of identifiers).
        var orderIds = DataConditions.Keys(conditions, "OrderId")?.OfType<string>().ToArray();

        // Inclusive ranges: a GreaterThan becomes >=, which is looser, so never removes a row the filter keeps.
        var (createdFrom, createdTo) = DataConditions.DateRange(conditions, "CreatedUtc");

        // Includes the GreaterThan condition of "Read only new rows".
        var (modifiedFrom, modifiedTo) = DataConditions.DateRange(conditions, "ModifiedUtc");

        var lastId = 0L;

        while (true)
        {
            // Keyset paging on the index id: a stable order.
            var after = lastId;
            var page = _session.QueryIndex<OrderIndex>(index => index.Id > after);

            if (orderIds is not null)
            {
                page = page.Where(index => index.OrderId.IsIn(orderIds));
            }

            if (createdFrom is { } createdStart)
            {
                page = page.Where(index => index.CreatedUtc >= createdStart);
            }

            if (createdTo is { } createdEnd)
            {
                page = page.Where(index => index.CreatedUtc <= createdEnd);
            }

            if (modifiedFrom is { } modifiedStart)
            {
                page = page.Where(index => index.ModifiedUtc >= modifiedStart);
            }

            if (modifiedTo is { } modifiedEnd)
            {
                page = page.Where(index => index.ModifiedUtc <= modifiedEnd);
            }

            page = ApplyTotal(page, conditions);
            page = ApplyCustomerName(page, conditions);

            var orders = (await page.OrderBy(index => index.Id).Take(PageSize).ListAsync()).ToList();

            if (orders.Count == 0)
            {
                yield break;
            }

            foreach (var order in orders)
            {
                yield return order;
            }

            lastId = orders[^1].Id;
        }
    }

    private static IQueryIndex<OrderIndex> ApplyTotal(IQueryIndex<OrderIndex> page, IEnumerable<DataCondition> conditions)
    {
        foreach (var condition in conditions.Where(condition => condition.Field == "Total"))
        {
            var first = Number(condition.Values, 0);
            var second = Number(condition.Values, 1);

            page = condition.Operator switch
            {
                DataFilterOperator.GreaterThan when first is { } value => page.Where(index => index.Total > value),
                DataFilterOperator.GreaterThanOrEqual when first is { } value => page.Where(index => index.Total >= value),
                DataFilterOperator.LessThan when first is { } value => page.Where(index => index.Total < value),
                DataFilterOperator.LessThanOrEqual when first is { } value => page.Where(index => index.Total <= value),
                DataFilterOperator.Between when first is { } low && second is { } high => page.Where(index => index.Total >= low && index.Total <= high),
                DataFilterOperator.Between when first is { } low => page.Where(index => index.Total >= low),
                DataFilterOperator.Between when second is { } high => page.Where(index => index.Total <= high),

                // Anything else (NotEquals, a missing value...) is left to the consumer.
                _ => page,
            };
        }

        return page;
    }

    private static IQueryIndex<OrderIndex> ApplyCustomerName(IQueryIndex<OrderIndex> page, IEnumerable<DataCondition> conditions)
    {
        // Text conditions ignore case: applying them case-sensitively would drop rows the filter keeps.
        if (!StoreIgnoresTextCase)
        {
            return page;
        }

        foreach (var condition in conditions.Where(condition => condition.Field == "CustomerName" && condition.Values.Count > 0))
        {
            if (condition.Values[0] is not string text)
            {
                continue;
            }

            page = condition.Operator switch
            {
                DataFilterOperator.Contains => page.Where(index => index.CustomerName.Contains(text)),
                DataFilterOperator.StartsWith => page.Where(index => index.CustomerName.StartsWith(text)),
                DataFilterOperator.EndsWith => page.Where(index => index.CustomerName.EndsWith(text)),
                _ => page,
            };
        }

        return page;
    }

    private static decimal? Number(IList<object> values, int index)
        => values.Count > index && values[index] is { } value ? Convert.ToDecimal(value, CultureInfo.InvariantCulture) : null;
}
```

```csharp
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<OrdersDataSource>();
    }
}
```

What a pipeline sends to this data set:

| Step setting | Conditions received |
|--------------|---------------------|
| Filter `Total` is greater than `100` | `Total GreaterThan 100m` |
| Filter `CreatedUtc` is in the last `30` days | `CreatedUtc Between (start, end)` |
| Filter `CreatedUtc` is less than or equal to `2026-01-31` (date only) | `CreatedUtc LessThanOrEqual 2026-02-01T00:00` (the whole day) |
| Filter `CustomerName` contains `smith` | `CustomerName Contains "smith"` (skipped here unless the store ignores case) |
| Filter `CustomerName` doesn't equal `Test` | nothing (applied by the step only) |
| **Read only new rows** by `ModifiedUtc` | `ModifiedUtc GreaterThan` the last watermark |

To also let consumers aggregate in the database, implement `IAggregateRecordDataSet` and apply the conditions of the `DataAggregateQuery` **exactly**, answering `null` otherwise (see `orchardcore-data-sources`).

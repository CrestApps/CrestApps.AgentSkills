---
name: orchardcore-data-formulas
description: Skill for the formula language and filters of Orchard Core Data Pipelines Core. Covers formula syntax, operators and precedence, missing values and type rules, the full catalog of data pipeline formula functions (text, number, date, logical and aggregate) with signatures, using formulas from code with ExpressionCompiler, CompiledExpression, Aggregates and DataPipelineFormulas, adding or replacing formula functions with ExpressionFunction.CreateScalar, CreateAggregate, AddExpressionFunction, IExpressionFunctionProvider and IExpressionFunctionCatalog, the DataFilterOperator filter operators, the Filter rows transform, source-side row filtering, DataFilterPredicates, and how a data source applies query conditions with DataCondition, DataConditions.Keys, DataConditions.DateRange and IsJoinKey. Use it for data pipeline formulas, calculated fields, custom formula functions, data source filters, or pushing filter conditions into a custom data source.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Data Formulas and Filters

The Data Pipelines Core feature (Orchard Core 4.0) includes a formula language, used by the **Filter rows** and **Add calculated fields** steps of data pipelines (see `orchardcore-data-pipelines`) and by features such as reports, and the filter operators that consumers offer to data sources (see `orchardcore-data-sources`). A formula computes a value from a row, such as `[Price] * [Quantity]` or `UPPER(TRIM([Country]))`.

Code lives in the `OrchardCore.DataSources.Expressions` and `OrchardCore.DataSources` namespaces (packages `OrchardCore.DataSources.Core` and `OrchardCore.DataSources.Abstractions`, version `4.*`). Register functions in a module that depends on `OrchardCore.DataPipelines.Core`, or in a `[RequireFeatures("OrchardCore.DataPipelines.Core")]` startup.

## Syntax

| Element | Syntax |
|---------|--------|
| Field | Technical name in brackets, `[TitlePart.Title]`. A `]` in a name is doubled: `[Size [cm]]]` for `Size [cm]`. |
| Text | Single or double quotes, `'CA'` or `"CA"`. Double a quote to escape it: `'It''s'`. |
| Number | Digits with a dot, `42`, `0.25`. Without a dot it is an integer. |
| Constants | `TRUE`, `FALSE`, `NULL` |
| Function | `ROUND([Amount], 2)` |
| Parentheses | `([Price] + [Shipping]) * 1.15` |

Keywords and function names are case-insensitive; field names are not.

## Operators

From the lowest precedence to the highest:

1. `OR` or `||`
2. `AND` or `&&`
3. `NOT` or `!`
4. Comparisons `=` or `==`, `!=` or `<>`, `<`, `<=`, `>`, `>=`
5. `&` joins two values as text (a missing value joins as empty text)
6. `+`, `-`
7. `*`, `/`, `%`
8. Unary `-`, `+`

Rules:

- A missing value in arithmetic gives a missing result, and so does a division by zero. Integer arithmetic that overflows continues with decimals.
- `+` joins text when one side is text.
- Date plus or minus a number adds or removes days; date minus date gives the number of days between them.
- A missing value equals another missing value or an empty text; `<`, `<=`, `>`, `>=` are false when either side is missing.
- A date compared with a text converts the text to a date: `[CreatedUtc] >= '2025-01-01'`. Text comparisons ignore case.
- A value is true when it is `TRUE`, a non-zero number, or a text other than empty, `false` or `0`.
- Functions return a missing value rather than failing when an argument is missing or of the wrong type.
- A formula is checked before it runs: every field must exist, every function must get the right number of arguments, and operators must suit their operand types. Pipeline steps report an invalid formula as an error of the step.

## Functions

Built-in functions come from `BuiltInExpressionFunctionProvider` (`ExpressionFunctions.All`). The return type is the `DataFieldType` the compiler infers.

### Text

| Function | Returns | Description |
|----------|---------|-------------|
| `UPPER(text)` | Text | Converts to upper case. |
| `LOWER(text)` | Text | Converts to lower case. |
| `TRIM(text)` | Text | Removes leading and trailing spaces. |
| `PROPER(text)` | Text | Capitalizes the first letter of each word. |
| `LEN(text)` | Integer | Number of characters. |
| `LEFT(text, count)` | Text | The first characters. |
| `RIGHT(text, count)` | Text | The last characters. |
| `MID(text, start, [count])` | Text | Characters from the middle; the first character is at 1. |
| `SUBSTRING(text, start, [count])` | Text | Same as `MID`. |
| `REPLACE(text, find, replacement)` | Text | Replaces every occurrence, ignoring case. |
| `CONCAT(value1, value2, ...)` | Text | Joins values into one text; missing values are skipped. |
| `FIND(text, search)` | Integer | Position of the search text (from 1), or 0 when not found; ignores case. |
| `CONTAINS(text, search)` | Boolean | True when the text contains the search text, ignoring case. |
| `STARTSWITH(text, search)` | Boolean | True when the text starts with it, ignoring case. |
| `ENDSWITH(text, search)` | Boolean | True when the text ends with it, ignoring case. |
| `SPLIT(text, separator, index)` | Text | The part at the index (from 1). |
| `TEXT(value, [format])` | Text | Converts to text, optionally with a .NET format such as `'N2'` or `'yyyy-MM'`. |

### Number

| Function | Returns | Description |
|----------|---------|-------------|
| `ABS(number)` | Type of the number | Absolute value. |
| `ROUND(number, [decimals])` | Integer, or Decimal with `decimals` | Rounds to a number of decimals (0 by default); midpoints away from zero. |
| `FLOOR(number)` | Integer | Rounds down to a whole number. |
| `CEILING(number)` | Integer | Rounds up to a whole number. |
| `POWER(number, exponent)` | Decimal | Raises to a power. |
| `SQRT(number)` | Decimal | Square root (missing for a negative number). |
| `MOD(number, divisor)` | Common number type | Remainder of a division. |
| `SIGN(number)` | Integer | -1, 0 or 1. |
| `NUMBER(value)` | Decimal | Converts to a decimal number. |
| `INTEGER(value)` | Integer | Converts to a whole number, dropping any fraction. |
| `LEAST(value1, value2, ...)` | Common type | The smallest value. |
| `GREATEST(value1, value2, ...)` | Common type | The largest value. |

### Date

| Function | Returns | Description |
|----------|---------|-------------|
| `NOW()` | DateTime | The current date and time. |
| `TODAY()` | Date | The current date. |
| `YEAR(date)` | Integer | The year. |
| `QUARTER(date)` | Integer | The quarter (1 to 4). |
| `MONTH(date)` | Integer | The month (1 to 12). |
| `DAY(date)` | Integer | The day of the month. |
| `WEEK(date)` | Integer | The ISO week number. |
| `WEEKDAY(date)` | Integer | The day of the week, 1 (Monday) to 7 (Sunday). |
| `HOUR(date)` | Integer | The hour (0 to 23). |
| `MINUTE(date)` | Integer | The minute (0 to 59). |
| `DATE(year, month, day)`, `DATE(value)` | Date | Builds a date from its parts, or converts a value to a date. |
| `DATETIME(value)` | DateTime | Converts a value to a date-time. |
| `DATEADD('part', number, date)` | Type of `date` | Adds years, quarters, months, weeks, days, hours, minutes or seconds. |
| `DATEDIFF('part', start, end)` | Integer | Counts the parts from start to end. |
| `DATETRUNC('part', date)` | Type of `date` | Truncates to the start of its year, quarter, month, week, day, hour or minute. |

`part` is `year` (`years`, `yyyy`, `yy`), `quarter` (`quarters`, `q`, `qq`), `month` (`months`, `mm`, `m`), `week` (`weeks`, `wk`, `ww`), `day` (`days`, `dd`, `d`), `hour` (`hours`, `hh`), `minute` (`minutes`, `mi`, `n`) or `second` (`seconds`, `ss`, `s`). Weeks start on Monday.

`NOW()` and `TODAY()` read `ExpressionContext.Now`: the site time zone; in a pipeline run, when the run started, so every row gets the same value.

### Logical

| Function | Returns | Description |
|----------|---------|-------------|
| `IF(condition, then, [else])` | Common type of the results | One value when the condition is true, another when not. |
| `IIF(condition, then, [else])` | Same | Same as `IF`. |
| `IFS(condition1, value1, condition2, value2, ..., [else])` | Same | The value of the first true condition. |
| `SWITCH(value, match1, result1, match2, result2, ..., [else])` | Same | The result paired with the first match. |
| `COALESCE(value1, value2, ...)` | Common type | The first value that isn't missing. |
| `IFNULL(value, fallback)` | Common type | The fallback when the value is missing. |
| `ISNULL(value)` | Boolean | True when the value is missing. |
| `ISBLANK(value)` | Boolean | True when the value is missing or empty text. |
| `IN(value, option1, option2, ...)` | Boolean | True when the value equals any option. |
| `NOT(condition)` | Boolean | True when the condition isn't true. |

"Common type" is `ExpressionFunctions.CommonType`: the type itself when all agree, Decimal for mixed numbers, DateTime for mixed dates, Text otherwise.

### Aggregate

Aggregate functions combine the values of a group of rows. They are available only to consumers that evaluate formulas over groups (such as reports). A formula can't mix aggregated values with the fields of a single row, nor nest an aggregate in another.

| Function | Returns | Description |
|----------|---------|-------------|
| `SUM(number)` | Integer for an integer argument, else Decimal | Adds the values of the group. |
| `AVG(number)` | Decimal | Averages the values. |
| `AVERAGE(number)` | Decimal | Same as `AVG`. |
| `MEDIAN(number)` | Decimal | The middle value. |
| `MIN(value)` | Type of the value | The smallest value. |
| `MAX(value)` | Type of the value | The largest value. |
| `COUNTD(value)` | Integer | Distinct values (numbers by value, text exactly). |
| `COUNT([value])` | Integer | The rows of the group, or the rows where the value is present. |

The formulas of data pipeline steps read one row at a time and reject aggregates; use the **Group and summarize** step.

## Using formulas from code

```csharp
using OrchardCore.DataSources.Expressions;

// _functionCatalog is the IExpressionFunctionCatalog singleton: built-in functions plus module functions.
var scope = new FieldListExpressionScope(batch.Fields);

// Throws an ExpressionException, with a localized message, when the formula is invalid.
var expression = ExpressionCompiler.Compile("[Price] * [Quantity]", scope, _functionCatalog, S);

foreach (var row in batch.Rows)
{
    var total = expression.Evaluate(new ExpressionContext { Row = row, Now = now });
}
```

- `ExpressionCompiler.Compile(formula, scope, localizer)` without a catalog uses only the built-in functions (`ExpressionFunctionCatalog.BuiltIn`), for code without services.
- `FieldListExpressionScope` resolves fields of a `DataField` list by technical name. A custom `IExpressionScope.TryResolve(key, out ExpressionFieldBinding)` can bind other names (`Key`, `DataType`, `Index`).
- `CompiledExpression`: `DataType` (result type), `IsAggregate`, `Aggregates`, `CanAggregateAsRowsArrive`, `References` (fields read), `Evaluate(ExpressionContext)`.
- `ExpressionContext`: `Row` (scalar formulas), `Group` (all rows of a group, in memory), `AggregateValues` (precomputed aggregates), `Now`.
- `IExpressionFunctionCatalog.Functions` lists every function by category and name with `Signature` and English `Description` (localize them to show), `Find(name)` ignores case.
- In a pipeline step, `DataPipelineFormulas.TryCompile(formula, fields, catalog, S, out error)` returns `null` with an error for an invalid or aggregate formula, `DataPipelineFormulas.Evaluate(expression, row, now, out error)` returns `null` with an error for a row that fails (such as a conversion error), and `await DataPipelineFormulas.GetRunTimeAsync(context.Run)` gives `now` (see `orchardcore-data-pipeline-steps`).

### Aggregate formulas over groups of any size

Evaluating with `Group` holds a group in memory. Compute the aggregates as rows arrive instead: `CompiledExpression.Aggregates` lists the aggregate calls (such as the `SUM` and `COUNT` of `SUM([Amount]) / COUNT()`), each with its `DataAggregate Function` and the `Argument` delegate to evaluate per row. Compute them with `DataAggregator` and evaluate the formula with the results in `AggregateValues`, in the same order. `CanAggregateAsRowsArrive` is false when a call uses an aggregate function created without a `DataAggregate` (it can only be computed over `Group`).

```csharp
// expression is an aggregate formula; groupIndex the position of the field to group by.
var context = new ExpressionContext { Now = now };

async IAsyncEnumerable<object[]> Arguments(IAsyncEnumerable<object[]> rows)
{
    await foreach (var row in rows)
    {
        context.Row = row;

        // The group, then the argument of each aggregate call.
        yield return [row[groupIndex], .. expression.Aggregates.Select(aggregate => aggregate.Argument(context))];
    }
}

var measures = expression.Aggregates.Select((aggregate, index) => new DataMeasure(aggregate.Function, index + 1)).ToArray();

await foreach (var group in DataAggregator.AggregateAsync(Arguments(rows), [0], measures, spillOptions, cancellationToken))
{
    var value = expression.Evaluate(new ExpressionContext { Now = now, AggregateValues = group[1..] });
}
```

## Adding a formula function

```csharp
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.Modules;

[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class FormulasStartup : StartupBase
{
    private const decimal TaxRate = 0.15m;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddExpressionFunction(ExpressionFunction.CreateScalar(
            name: "TAX",
            category: ExpressionFunctions.NumberCategory,
            signature: "TAX(amount)",
            description: "Returns the sales tax of an amount.",
            minArguments: 1,
            maxArguments: 1,
            returnType: _ => DataFieldType.Decimal,
            // Return a missing value, rather than throwing, for a missing or wrongly typed argument.
            invoke: args => args[0] switch
            {
                long amount => Math.Round(amount * TaxRate, 2),
                decimal amount => Math.Round(amount * TaxRate, 2),
                _ => null,
            }));
    }
}
```

`[Price] + TAX([Price])` then works, and `TAX` is listed in the editors of **Filter rows** and **Add calculated fields**.

- Arguments arrive with their CLR types (`long`, `decimal`, `string`, `bool`, `DateTime`, `null`). `returnType` infers the result type from the argument types.
- Names are case-insensitive, start with a letter or underscore, hold only letters, digits and underscores, and can't be `AND`, `OR`, `NOT`, `TRUE`, `FALSE` or `NULL`. Invalid functions (bad name, no return type, no implementation, bad argument counts) fail when the catalog is first used.
- **Replacing built-ins**: a registered function replaces a built-in of the same name whatever the startup order (built-ins load first), and a later registration replaces an earlier one.
- `category`: `ExpressionFunctions.TextCategory`, `NumberCategory`, `DateCategory`, `LogicalCategory` or `AggregateCategory` (or your own). `description` is English; the function list localizes it.
- A function that needs the evaluation context (such as `NOW()`, which reads `context.Now`) is created with an object initializer and `Invoke = (args, context) => ...`.

Aggregate functions take one argument:

- `ExpressionFunction.CreateAggregate(name, signature, description, requiresNumericArgument, returnType, DataAggregate function)` computes one of the `DataAggregate` values, like the built-ins, so formulas calling it compute as rows arrive.
- `ExpressionFunction.CreateAggregate(name, signature, description, requiresNumericArgument, returnType, aggregate: values => ...)` receives every value of the group (`null` for missing) and can only be computed over `Group`.
- `requiresNumericArgument: true` makes a non-numeric argument a compile error.

For several functions, implement `IExpressionFunctionProvider.GetFunctions()` and register it with `services.AddExpressionFunctionProvider<TProvider>()` (singleton). Examples, including a context-aware function, a provider and a replaced built-in, are in [references/data-formulas-examples.md](references/data-formulas-examples.md).

## Filters

### Filter operators

`DataFilterOperator` conditions compare a field with values (`DataCondition`: `Field`, `Operator`, `Values`, `IsJoinKey`):

| Operator | Matches a value that | Applies to |
|----------|----------------------|------------|
| `Equals`, `NotEquals` | Equals, or differs from, the first value | Every type |
| `Contains`, `NotContains` | Contains, or doesn't contain, the first value, ignoring case | Text |
| `StartsWith`, `EndsWith` | Starts or ends with the first value, ignoring case | Text |
| `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual` | Compares with the first value | Numbers and dates |
| `Between` | Lies between the first and second values, inclusive; an empty bound is open | Numbers and dates |
| `In`, `NotIn` | Equals any, or none, of the values | Every type |
| `IsEmpty`, `IsNotEmpty` | Is missing or empty text, or isn't | Every type |
| `InLastDays`, `InNextDays` | Is a date within the last or next N days, counting today (N is the first value) | Dates |

`operator.AppliesTo(DataFieldType)` (`DataFilterOperatorExtensions`) tells whether an operator applies, so editors offer only those; consumers still evaluate any operator, and the **Read a data source** step reports a filter whose operator doesn't apply to its field as an error.

### The Filter rows transform

**Filter rows** evaluates a formula condition per row (it must return a Boolean, such as `[Amount] >= 100 AND CONTAINS([Email], '@example.com')`). Rows whose value is `TRUE` go to **Matched**, the others to **Unmatched** (written only when connected). A row whose condition fails is unmatched and logged as a warning (the first 10). Use it for conditions that combine fields or use functions; use source filters for simple field comparisons, which the data source may apply in its store.

### Source-side row filtering

The filters of **Read a data source** (`DataSourceStepSettings.Filters`, each a `DataSourceFilter` with `Field`, `Operator` and text `Values`) are applied in two places:

1. The step builds a **loose** `DataCondition` for each filter it can pass down (`DataFilterPredicates.BuildCondition`), adds it to `DataSourceQuery.Conditions`, and the data source may apply it in its store.
2. The step applies every filter again, exactly, to the rows it receives (`DataFilterPredicates.Build`), so a data source that ignores a condition is still correct.

| Filter | Condition passed to the data source |
|--------|-------------------------------------|
| `Equals`, `In` | `In` with the values |
| `Contains`, `StartsWith`, `EndsWith` (text) | The same operator with the first value |
| `GreaterThan(OrEqual)`, `LessThan(OrEqual)`, `Between` (numbers) | The same operator |
| `GreaterThan(OrEqual)`, `LessThan(OrEqual)`, `Between` (dates) | `GreaterThanOrEqual`, `LessThanOrEqual`, `Between`; a date written without a time covers its whole day, so an upper bound moves to the next midnight |
| `InLastDays`, `InNextDays` | `Between` the computed start and end dates (1 to 36,500 days) |
| `IsNotEmpty` | `IsNotEmpty` |
| `NotEquals`, `NotIn`, `NotContains`, `IsEmpty` | Not passed down |
| **Read only new rows** | `GreaterThan` the largest value the last successful run read |

Values are typed for the field: `string`, `long`, `decimal`, `bool`, `DateTime` (UTC for date-time fields). A filter without its values is off.

### Applying conditions in a data source

- **Row queries are loose**: a data source may apply any subset of the conditions, but must never remove a row the full filter would keep. Skip a condition you can't translate exactly or more broadly. In particular, text conditions (`In` on text, `Contains`, `StartsWith`, `EndsWith`) compare **ignoring case**: apply them only when the store compares case-insensitively.
- `DataConditions.Keys(conditions, field)` returns the values the `In` and `Equals` conditions of a field allow (intersected), or `null` when none limits it.
- `DataConditions.DateRange(conditions, field)` returns the narrowest inclusive `(From, To)` range the comparison conditions of a field allow; a missing bound is `null`.
- A condition with `IsJoinKey` carries the keys a join needs (an `In` list of identifiers, case kept). It is sent only for fields marked `IsKeyFilterable`, which must then return exactly the records of those keys.
- **Aggregate queries are exact**: the `Conditions` of a `DataAggregateQuery` must be applied exactly (built with `DataFilterPredicates.BuildExactConditions`); answer `null` for any condition you can't apply exactly. See `orchardcore-data-sources`.

A consumer of your own (such as a report) builds conditions the same way: `DataFilterPredicates.BuildCondition(fieldName, op, type, rawValues, toUtc, today)` for a loose condition (or `null`), `BuildExactConditions(...)` for aggregate queries, and `Build(op, type, rawValues, today)` for the exact row predicate (`null` when the filter is off).

A data source that translates conditions into a YesSql index query is in [references/data-formulas-examples.md](references/data-formulas-examples.md).

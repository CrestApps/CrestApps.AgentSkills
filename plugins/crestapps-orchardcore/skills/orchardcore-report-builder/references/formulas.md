# Report Builder formula reference

Calculated fields (`ReportCalculatedField`: `Name`, `Label`, `Expression`) use Orchard Core's formula language from Data Pipelines Core (namespace `OrchardCore.DataSources.Expressions`, package `OrchardCore.DataSources.Core`), the same one data pipelines use. `ReportQueryPlanner` compiles each formula with `ExpressionCompiler` against the report's fields and the `IExpressionFunctionCatalog`. The builder lists functions from the same catalog (`/Admin/reports/builder/api/functions`), so module-added functions appear automatically.

## Syntax

| Element | Written as |
| --- | --- |
| Data set field | `[alias.Field]`, such as `[Order.Order.Total]` or `[Order.CreatedUtc]`. The alias is the data set's display name without spaces or punctuation (`Order`, `Order2` for a second copy). A `]` inside is doubled (`]]`) |
| Calculated field | `[Name]`, such as `[ProfitMargin]`; names start with a letter, then letters, digits, underscores |
| Row count | `[$count]` (**Number of rows**), already aggregated |
| Text | `'Large'` or `"Large"`; a quote inside is doubled (`'O''Brien'`) |
| Numbers | Dot decimal separator; whole numbers are integers, numbers with a dot are decimals |
| Keywords | `TRUE`, `FALSE`, `NULL`, `AND`, `OR`, `NOT` (keywords and function names are case-insensitive) |
| Calls | `NAME(argument, ...)` |
| Grouping | Parentheses, nested at most 64 levels |

## Operators (lowest to highest precedence)

1. `OR`, `||`
2. `AND`, `&&`
3. `NOT`, `!`
4. `=`, `==`, `!=`, `<>`, `<`, `<=`, `>`, `>=`
5. `&` (text concatenation)
6. `+`, `-`
7. `*`, `/`, `%`
8. unary `-`, `+`

Same-precedence operators apply left to right.

- `+` joins text when either side is text. Date plus or minus a number adds or subtracts days; date minus date gives days as a decimal.
- `/` always gives a decimal; integer overflow continues in decimals.
- `&` always gives text and treats missing as empty, so `[First] & ' ' & [Last]` works with a missing part.
- Comparisons compare numbers as numbers, dates as dates (text on the other side is read as a date), text ignoring case.
- Truthy values: `TRUE`, non-zero numbers, non-empty text other than `false` or `0`.
- Missing operands give a missing result for arithmetic; division or remainder by zero gives missing. `=` treats missing and empty text as equal; ordering comparisons with a missing side are false. A function given an unconvertible value returns missing. A formula never stops a report.

## Result types

| Type | From |
| --- | --- |
| Text | Text fields and literals, `&`, `+` with text, text functions |
| Integer | Whole-number fields and literals, `LEN`, `FIND`, date parts, `COUNT`, `COUNTD`, `ROUND` without decimals, `FLOOR`, `CEILING`, `INTEGER`, `SUM` of integers |
| Decimal | Decimal fields and literals, `/`, `AVG`, `MEDIAN`, `SUM` of decimals, `ROUND` with decimals, `POWER`, `SQRT`, `NUMBER`, date minus date |
| Boolean | Comparisons, `AND`, `OR`, `NOT`, `CONTAINS`, `STARTSWITH`, `ENDSWITH`, `ISNULL`, `ISBLANK`, `IN` |
| Date | `TODAY`, `DATE`, date fields |
| Date-time | `NOW`, `DATETIME`, date-time fields |

`IF`, `IFS`, `SWITCH`, `COALESCE`, `IFNULL`, `LEAST`, `GREATEST` and `MOD` return the common type of their results; `DATEADD` and `DATETRUNC` return their date argument's type. The type decides the aggregates, transforms, filter conditions and formats offered.

## Row-level and aggregate formulas

- A row-level formula runs once per joined row, before filters and grouping; it can be a dimension, a measure (with an aggregate on its column) or a filter.
- A formula calling `SUM`, `AVG`, `AVERAGE`, `MEDIAN`, `MIN`, `MAX`, `COUNT` or `COUNTD` is aggregated: evaluated once per group, after grouping, and again by charts, metrics, pivots and totals over their own groups, so ratios stay true ratios in totals.
- In an aggregated formula every field must be inside an aggregate; the aggregate's argument is a row-level expression, such as `SUM(IF([Order.Order.Status] = 'Paid', [Order.Order.Total], 0))`.
- Aggregates cannot be nested, and an aggregated field (including `[$count]`) cannot be aggregated again.
- A filter on an aggregated field must be a result filter (`Stage` `Result`), not a row filter.
- Reports aggregate rows as they arrive without keeping them, so a formula calling an aggregate function that needs all values of its group at once (`CompiledExpression.CanAggregateAsRowsArrive` false) is refused.

## Functions

| Group | Functions |
| --- | --- |
| Aggregate | `SUM(number)`, `AVG(number)`, `AVERAGE(number)`, `MEDIAN(number)`, `MIN(value)`, `MAX(value)`, `COUNT([value])`, `COUNTD(value)` |
| Logical | `IF(condition, then, [else])`, `IIF(...)`, `IFS(c1, v1, c2, v2, ..., [else])`, `SWITCH(value, m1, r1, ..., [else])`, `COALESCE(v1, v2, ...)`, `IFNULL(value, fallback)`, `ISNULL(value)`, `ISBLANK(value)`, `IN(value, o1, o2, ...)`, `NOT(condition)` |
| Text | `UPPER`, `LOWER`, `TRIM`, `PROPER`, `LEN`, `LEFT(text, count)`, `RIGHT(text, count)`, `MID(text, start, [count])`, `SUBSTRING(...)` (same as MID, 1-based), `REPLACE(text, find, replacement)` (ignores case), `CONCAT(...)` (skips missing), `FIND(text, search)` (1-based, 0 when absent), `CONTAINS`, `STARTSWITH`, `ENDSWITH` (ignore case), `SPLIT(text, separator, index)` (1-based), `TEXT(value, [format])` (invariant culture) |
| Number | `ABS`, `ROUND(number, [decimals])` (midpoints away from zero), `FLOOR`, `CEILING`, `POWER(number, exponent)`, `SQRT`, `MOD(number, divisor)`, `SIGN`, `NUMBER(value)`, `INTEGER(value)`, `LEAST(...)`, `GREATEST(...)` |
| Date | `NOW()`, `TODAY()` (tenant time zone), `YEAR`, `QUARTER`, `MONTH`, `DAY`, `WEEK` (ISO), `WEEKDAY` (1 Monday to 7 Sunday), `HOUR`, `MINUTE`, `DATE(year, month, day)` or `DATE(value)`, `DATETIME(value)`, `DATEADD('part', number, date)`, `DATEDIFF('part', start, end)`, `DATETRUNC('part', date)` |

Date parts (case-insensitive): `year` (`years`, `yyyy`, `yy`), `quarter` (`quarters`, `q`, `qq`), `month` (`months`, `mm`, `m`), `week` (`weeks`, `wk`, `ww`), `day` (`days`, `dd`, `d`), `hour` (`hours`, `hh`), `minute` (`minutes`, `mi`, `n`), `second` (`seconds`, `ss`, `s`; not for `DATETRUNC`). `DATEDIFF` counts calendar boundaries for years, quarters and months (`DATEDIFF('month', '2026-01-31', '2026-02-01')` is 1), whole weeks and days, and whole hours, minutes and seconds. Weeks start on Monday. Date-time fields are already in the tenant time zone.

## Examples

| Formula | Result |
| --- | --- |
| `[Order.Order.Total] * 0.2` | 20% of each order total |
| `IF([Order.Order.Total] >= 1000, 'Large', 'Small')` | A label to group by |
| `SWITCH([Activities.Status], 'Completed', 'Done', 'Canceled', 'Done', 'Open')` | Fewer status labels |
| `[Customer.Customer.FirstName] & ' ' & [Customer.Customer.LastName]` | Full name |
| `DATEDIFF('day', [Order.CreatedUtc], TODAY())` | Age in days |
| `DATETRUNC('month', [Order.CreatedUtc])` | First day of the month |
| `SUM([Order.Order.Total]) / COUNTD([Customer.ContentItemId])` | Revenue per customer, per group |
| `SUM(IF([Activities.AiEscalated], 1, 0)) / COUNT()` | Share escalated by AI per group |
| `COALESCE([Customer.Customer.Phone], [Customer.Customer.Email], 'No contact')` | First contact detail present |

## Adding a function

Register a function in Orchard Core's catalog, in a startup marked `[RequireFeatures("OrchardCore.DataPipelines.Core")]`, and it is available to every formula of the site (reports and data pipelines):

- `services.AddExpressionFunction(ExpressionFunction.CreateScalar(...))` for a function computed from one row's values.
- `services.AddExpressionFunction(ExpressionFunction.CreateAggregate(...))` for a function combined over a group; it makes a formula aggregated like the built-ins. Use the overload that names a `DataAggregate`, which can be computed as rows arrive; reports refuse formulas that call the overload that needs every value of the group at once.
- `services.AddExpressionFunctionProvider<TProvider>()` to register several at once with an `IExpressionFunctionProvider`.

A registered function replaces a built-in one of the same name. See the `orchardcore-data-sources` skill for the argument list of `CreateScalar` and `CreateAggregate`.

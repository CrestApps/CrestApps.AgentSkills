# Users Admin List Filters Examples

## Example 1: End-to-end `ssn` filter

A single-value `ssn:` token that filters users on a custom `UserProfileIndex.Ssn`, documented with a card in the **Available Filters** dialog.

### Index

```csharp
using OrchardCore.Users.Models;
using YesSql.Indexes;

namespace MyModule.Indexes;

public sealed class UserProfileIndex : MapIndex
{
    public string UserId { get; set; }
    public string Ssn { get; set; }
}

public sealed class UserProfileIndexProvider : IndexProvider<User>
{
    public override void Describe(DescribeContext<User> context)
    {
        context.For<UserProfileIndex>()
            .Map(user =>
            {
                var part = user.As<UserProfilePart>();

                if (part is null || string.IsNullOrEmpty(part.Ssn))
                {
                    return null;
                }

                return new UserProfileIndex
                {
                    UserId = user.UserId,
                    Ssn = part.Ssn,
                };
            });
    }
}
```

### Filter provider

```csharp
using MyModule.Indexes;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using YesSql.Filters.Query;

namespace MyModule.Services;

public sealed class SsnUsersAdminListFilterProvider : IUsersAdminListFilterProvider
{
    public void Build(QueryEngineBuilder<User> builder)
    {
        builder
            .WithNamedTerm("ssn", builder => builder
                .OneCondition((val, query) =>
                    query.With<UserProfileIndex>(i =>
                        i.Ssn != null && i.Ssn.Contains(val))));
    }
}
```

### Display driver (filter card)

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.ViewModels;

namespace MyModule.Drivers;

public sealed class SsnUsersAdminListDisplayDriver : DisplayDriver<UserIndexOptions>
{
    public override IDisplayResult Display(UserIndexOptions model, BuildDisplayContext context)
    {
        return View("UsersAdminFilters_Thumbnail__Ssn", model)
            .Location("Thumbnail", "Content:35");
    }
}
```

### View — `Views/Items/UsersAdminFilters-Ssn.Thumbnail.cshtml`

```html
@model ShapeViewModel<UserIndexOptions>
@{
    var term = Model.Value.FilterResult.FirstOrDefault(x => x.TermName == "ssn");
}

<div class="d-flex justify-content-between align-items-center gap-2">
    <h6 class="card-title fw-semibold mb-0">@T["SSN"]</h6>
    <span class="text-primary text-nowrap">
        <i class="fa-solid fa-sm fa-minus" title="@T["Accepts a single value"]" aria-hidden="true"></i>
    </span>
</div>
<div class="mt-1"><code class="small text-nowrap">@(term?.ToString() ?? "ssn:...")</code></div>
<p class="card-text small text-body-secondary mt-1 mb-0">@T["Filters on a user's social security number."]</p>
```

### Startup

```csharp
using MyModule.Drivers;
using MyModule.Indexes;
using MyModule.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using OrchardCore.Users.ViewModels;
using YesSql.Indexes;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Index that backs the filter.
        services.AddSingleton<IIndexProvider, UserProfileIndexProvider>();

        // Filter logic: adds the `ssn:` token to the users admin search box.
        services.AddScoped<IUsersAdminListFilterProvider, SsnUsersAdminListFilterProvider>();

        // Filter card: documents the token in the Available Filters dialog.
        services.AddDisplayDriver<UserIndexOptions, SsnUsersAdminListDisplayDriver>();
    }
}
```

## Example 2: Multiple-value term with logical operators

A `ssn:` token that supports `AND`, `OR`, `NOT`, and groups. Use `ManyCondition` and provide both the matching and the negated predicate.

```csharp
using MyModule.Indexes;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using YesSql.Filters.Query;

namespace MyModule.Services;

public sealed class SsnUsersAdminListFilterProvider : IUsersAdminListFilterProvider
{
    public void Build(QueryEngineBuilder<User> builder)
    {
        builder
            .WithNamedTerm("ssn", builder => builder
                .ManyCondition(
                    (val, query) => query.With<UserProfileIndex>(i =>
                        i.Ssn != null && i.Ssn.Contains(val)),
                    (val, query) => query.With<UserProfileIndex>(i =>
                        i.Ssn == null || i.Ssn.NotContains(val))));
    }
}
```

The matching card uses the `fa-bars` icon to signal **Multiple** capability:

```html
<span class="text-primary text-nowrap">
    <i class="fa-solid fa-sm fa-bars" title="@T["Supports logical operators and groups"]" aria-hidden="true"></i>
</span>
```

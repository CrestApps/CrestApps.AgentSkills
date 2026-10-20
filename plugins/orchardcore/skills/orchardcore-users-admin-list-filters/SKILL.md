---
name: orchardcore-users-admin-list-filters
description: Skill for adding custom filters to the Orchard Core users admin list (Security → Users). Covers IUsersAdminListFilterProvider named and default terms with OneCondition/ManyCondition against YesSql indexes over User, changing the bare-text term and exact match with UsersAdminListFilterOptions, registering the provider in Startup, and documenting the new filter in the redesigned Available Filters dialog with a DisplayDriver<UserIndexOptions> Thumbnail card. Use this skill when requests mention custom users admin list filters, IUsersAdminListFilterProvider, QueryEngineBuilder<User>, WithNamedTerm, UsersAdminListFilterOptions, the Filter syntax dialog, UserIndexOptions filter cards, UsersAdminFilters Thumbnail views, upgrading filter cards to Orchard Core 4.0, or closely related Orchard Core users admin list search work. Strong matches include OrchardCore.Users, IUsersAdminListFilterProvider, UserIndexOptions, UsersAdminList, and UsersAdminFilters-*.Thumbnail.cshtml.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Users Admin List Filters

## Add a custom filter to the users admin list

You are an Orchard Core expert. The users admin list (**Security → Users**) has a search box that parses tokens such as `name:`, `email:`, `status:`, `role:`, and `sort:`. Add your own token by implementing a filter provider, then document it for end users by adding a card to the **Available Filters** dialog. These are two independent extension points: the filter **logic** and the filter **card**. A filter still works without a card, but users will not discover it; a card without a matching provider documents a filter that does nothing. The users list works exactly like the content items admin list (see `orchardcore-contents-admin-list-filters`); only the model type differs.

### Guidelines

- Enable `OrchardCore.Users`. The users admin list ships with this feature.
- Implement `IUsersAdminListFilterProvider` to add searchable terms; register it with `services.AddScoped<IUsersAdminListFilterProvider, T>()`.
- Build terms against `QueryEngineBuilder<User>`. Filter against a YesSql index over `User` (`UserIndex` or a custom index), not against materialized user objects.
- Use `WithNamedTerm("token", ...)` for a token the user types as `token:value`. Use `WithDefaultTerm("token", ...)` for the term applied to bare text with no prefix; register at most one default term.
- Use `OneCondition` for a term that accepts a single value (the `fa-minus`/**Single** icon). Use `ManyCondition` for a term that supports the `AND`, `OR`, and `NOT` operators and groups (the `fa-bars`/**Multiple** icon), and supply both the matching and the negated predicate.
- Document the filter by implementing `DisplayDriver<UserIndexOptions>` and returning a `View(...)` in the `Content` zone of the `Thumbnail` display type; register it with `services.AddDisplayDriver<UserIndexOptions, T>()`.
- The shape type `UsersAdminFilters_Thumbnail__<Name>` resolves to `Views/Items/UsersAdminFilters-<Name>.Thumbnail.cshtml`. The card wrapper and grid are supplied for you; the view only renders the card's inner content.
- Use the same capability icons the built-in filters use so the shared legend at the bottom of the dialog stays accurate: `fa-check` (**Default**), `fa-minus` (**Single**), `fa-bars` (**Multiple**).
- Bare text is routed to the `name` term (`UsersAdminListFilterOptions.DefaultTermName`). Set `UsersAdminListFilterOptions.TermName` to route it to another term, and `UseExactMatch = true` to search bare text as one quoted term unless it uses `AND` or `OR`.
- In Orchard Core 4.0 the users list renders through the shared `AdminList` shape under the list name `Users` (`UsersAdminList.Name`), so it follows the site's List/Grid layout. For columns and row templates, see `orchardcore-admin-lists`; filters and filter cards are unaffected by the layout.
- Backing user data that is not already indexed (for example a profile field on a custom part) needs its own YesSql index before it can be filtered.

## Register the filter logic

Add named or default terms to the `QueryEngineBuilder<User>`. This example adds a single-value `ssn` term that filters on a custom `UserProfileIndex`.

```csharp
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using YesSql.Filters.Query;

namespace MyModule;

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

For a term that supports logical operators and groups, use `ManyCondition` and provide both the matching and the negated predicate:

```csharp
builder
    .WithNamedTerm("ssn", builder => builder
        .ManyCondition(
            (val, query) => query.With<UserProfileIndex>(i =>
                i.Ssn != null && i.Ssn.Contains(val)),
            (val, query) => query.With<UserProfileIndex>(i =>
                i.Ssn == null || i.Ssn.NotContains(val))));
```

Register the provider in the module's `Startup`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Users.Services;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUsersAdminListFilterProvider, SsnUsersAdminListFilterProvider>();
    }
}
```

To make bare text search your term instead of `name`:

```csharp
using OrchardCore.Users.Models;

services.Configure<UsersAdminListFilterOptions>(options =>
{
    options.TermName = "ssn";
    options.UseExactMatch = true;
});
```

## Register the filter card

Implement a display driver for `UserIndexOptions`. The position after `Content:` controls where the card appears in the grid.

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.ViewModels;

namespace MyModule;

public sealed class SsnUsersAdminListDisplayDriver : DisplayDriver<UserIndexOptions>
{
    public override IDisplayResult Display(UserIndexOptions model, BuildDisplayContext context)
    {
        // First argument is the shape type; second is the display type and position.
        return View("UsersAdminFilters_Thumbnail__Ssn", model)
            .Location("Thumbnail", "Content:35");
    }
}
```

Register the driver in `Startup`:

```csharp
services.AddDisplayDriver<UserIndexOptions, SsnUsersAdminListDisplayDriver>();
```

## The card template

The shape type `UsersAdminFilters_Thumbnail__Ssn` resolves to `Views/Items/UsersAdminFilters-Ssn.Thumbnail.cshtml`. Each card is wrapped in a Bootstrap card and laid out in the responsive grid automatically, so the view supplies only the inner content: a title with its capability icons, the filter token, and a short description.

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

Choose the icon that matches the term's capability, and match the `title` text to the shared legend:

| Icon | Font Awesome class | Meaning | Term method |
|------|--------------------|---------|-------------|
| ✓ | `fa-check` | **Default** — may be entered with or without the term name | `WithDefaultTerm` |
| − | `fa-minus` | **Single** — accepts a single value | `OneCondition` |
| ☰ | `fa-bars` | **Multiple** — supports `AND`, `OR`, `NOT`, and groups | `ManyCondition` |

If you also override the dialog's grid container (`UserIndexOptions.Thumbnail.cshtml`) or the card wrapper (`UsersAdminFilters-Card.Thumbnail.cshtml`), match the built-in markup, which emits `row row-cols-*` for the grid and `col > card h-100` for each card.

## Upgrading from 3.x

- The **Available Filters** dialog is now a `modal-xl` grid of cards, and a Thumbnail template renders only the card's inner content. Old cards still render but look out of place.
- Before (3.x), cards used `ocat-wrapper`, an `<h4 class="card-title ocat-label">`, the token in a `<pre>`, and the icons in a trailing `<div class="d-block text-end">`.
- After (4.0), put the title and capability icons on the first line, the token in a `<code>` line, and the description last, as shown in [The card template](#the-card-template).
- A theme or module that overrides the users list page must render the `List` context of its view model with the `<admin-list>` tag helper (see `orchardcore-admin-lists`); the `Users` collection of the view model now holds raw `User` records instead of rendered row shapes.

## Troubleshooting

- If typing `ssn:...` returns unfiltered results, confirm the `IUsersAdminListFilterProvider` is registered with `AddScoped` and the term name matches the token exactly (case-insensitive).
- If the term filters but no card appears in the **Available Filters** dialog, register the `DisplayDriver<UserIndexOptions>` with `AddDisplayDriver` and verify the view path is `Views/Items/UsersAdminFilters-<Name>.Thumbnail.cshtml`.
- If the card renders but the token line is empty, ensure the `TermName` passed to `FilterResult.FirstOrDefault` matches the token used in `WithNamedTerm`/`WithDefaultTerm`.
- Filter against a persisted YesSql index over `User`. Predicates over in-memory user properties are not translated to SQL and will not filter.

See [references/users-admin-list-filters-examples.md](references/users-admin-list-filters-examples.md) for a complete end-to-end example.

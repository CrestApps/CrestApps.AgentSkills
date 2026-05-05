---
name: orchardcore-url-rewriting
description: Skill for configuring URL rewriting in Orchard Core. Covers redirect vs rewrite rules, regex pattern matching, rule source implementation, display drivers for custom rule types, recipe-based rule configuration, and URL rewriting best practices. Use this skill when requests mention Orchard Core URL Rewriting, Configure URL Rewriting and Redirects, Enabling URL Rewriting, Available Rule Types, Redirect Rule Properties, Rewrite Rule Properties, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.UrlRewriting, OrchardCore.UrlRewriting.Models, OrchardCore.UrlRewriting.Services, OrchardCore.DisplayManagement.Handlers, OrchardCore.DisplayManagement.Views, OrchardCore.Modules, IUrlRewriteRuleSource, CustomRule. It also helps with Redirect Rule Properties, Rewrite Rule Properties, Redirect Type Reference, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core URL Rewriting - Prompt Templates

## Configure URL Rewriting and Redirects

You are an Orchard Core expert. Generate URL rewrite rules, redirect configurations, and custom rule sources for Orchard Core.

### Guidelines

- Enable `OrchardCore.UrlRewriting` for URL rewrite and redirect support.
- Rules are processed sequentially based on their position; the first matching rule wins.
- Use **Redirect Rules** (301/302/307/308) to send users to a new URL (browser address bar changes).
- Use **Rewrite Rules** to serve content from a different URL without changing the browser address bar.
- Patterns use regular expressions; use `^` and `$` for exact matches.
- Use `$1`, `$2`, etc. in substitution patterns to reference regex capture groups.
- Set `IsCaseInsensitive` to `true` for case-insensitive pattern matching.
- Configure `QueryStringPolicy` to `Append` (keep query strings) or `Drop` (discard them).
- Custom rule sources implement `IUrlRewriteRuleSource` and are registered with `services.AddRewriteRuleSource<T>()`.
- Use the admin UI drag-and-drop to reorder rules.
- Always seal classes.

### Enabling URL Rewriting

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.UrlRewriting"
      ],
      "disable": []
    }
  ]
}
```

### Available Rule Types

| Rule Type | Description | Use Case |
|-----------|-------------|----------|
| **Redirect Rule** | Sends an HTTP redirect response (301/302/307/308) to the client. The browser URL changes. | URL migrations, domain changes, SEO canonical URLs. |
| **Rewrite Rule** | Modifies the request URL server-side. The browser URL stays the same. | URL aliasing, serving content from different paths. |

### Redirect Rule Properties

| Property | Description |
|----------|-------------|
| `Id` | Unique identifier. Leave empty to create new; match existing ID to update. |
| `Name` | Descriptive name for the rule. |
| `Pattern` | Regex pattern to match against the request URL. |
| `SubstitutionPattern` | Target URL for the redirect. Supports `$1`, `$2` capture groups. |
| `IsCaseInsensitive` | When `true`, pattern matching ignores case. |
| `QueryStringPolicy` | `Append` keeps original query string; `Drop` discards it. |
| `RedirectType` | `MovedPermanently` (301), `Found` (302), `TemporaryRedirect` (307), or `PermanentRedirect` (308). |

### Rewrite Rule Properties

| Property | Description |
|----------|-------------|
| `Id` | Unique identifier. Leave empty to create new; match existing ID to update. |
| `Name` | Descriptive name for the rule. |
| `Pattern` | Regex pattern to match against the request URL. |
| `SubstitutionPattern` | Target URL for the rewrite. Supports `$1`, `$2` capture groups. |
| `IsCaseInsensitive` | When `true`, pattern matching ignores case. |
| `QueryStringPolicy` | `Append` keeps original query string; `Drop` discards it. |
| `SkipFurtherRules` | When `true`, no subsequent rules are processed if this rule matches. |

### Redirect Type Reference

| Value | HTTP Status | Description |
|-------|-------------|-------------|
| `Found` | 302 | Temporary redirect. |
| `MovedPermanently` | 301 | Permanent redirect; clients should update bookmarks. |
| `TemporaryRedirect` | 307 | Temporary redirect; preserves HTTP method (POST stays POST). |
| `PermanentRedirect` | 308 | Permanent redirect; preserves HTTP method. |

### Recipe: Create Redirect and Rewrite Rules

```json
{
  "steps": [
    {
      "name": "UrlRewriting",
      "Rules": [
        {
          "Source": "Redirect",
          "Name": "Redirect old-page to new-page",
          "Pattern": "^/old-page$",
          "SubstitutionPattern": "/new-page",
          "IsCaseInsensitive": true,
          "QueryStringPolicy": "Append",
          "RedirectType": "MovedPermanently"
        },
        {
          "Source": "Redirect",
          "Name": "Redirect legacy blog URLs",
          "Pattern": "^/blog/post/(\\d+)$",
          "SubstitutionPattern": "/articles/$1",
          "IsCaseInsensitive": true,
          "QueryStringPolicy": "Drop",
          "RedirectType": "MovedPermanently"
        },
        {
          "Source": "Rewrite",
          "Name": "Serve media from img path",
          "Pattern": "^/img/(.*)$",
          "SubstitutionPattern": "/media/$1",
          "IsCaseInsensitive": true,
          "QueryStringPolicy": "Drop",
          "SkipFurtherRules": true
        }
      ]
    }
  ]
}
```

### Custom Rule Source Implementation

```csharp
using OrchardCore.UrlRewriting.Models;
using OrchardCore.UrlRewriting.Services;

namespace MyModule.UrlRewriting;

public sealed class CustomRuleSource : IUrlRewriteRuleSource
{
    public const string SourceName = "CustomRule";

    public string Name => SourceName;

    public Task ConfigureAsync(RewriteRule rule, RewriteRuleContext context)
    {
        // Configure the rule based on custom properties.
        // Access custom properties from rule.Source.

        return Task.CompletedTask;
    }
}
```

### Custom Rule Display Driver

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.UrlRewriting.Models;

namespace MyModule.UrlRewriting;

public sealed class CustomRuleDisplayDriver : DisplayDriver<RewriteRule>
{
    public override IDisplayResult Edit(RewriteRule model, BuildEditorContext context)
    {
        return Initialize<CustomRuleViewModel>("CustomRule_Fields_Edit", viewModel =>
        {
            // Populate view model from model properties.
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(RewriteRule model, UpdateEditorContext context)
    {
        var viewModel = new CustomRuleViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        // Apply view model values to the model.

        return Edit(model, context);
    }
}

public class CustomRuleViewModel
{
    public string CustomProperty { get; set; }
}
```

### Registering a Custom Rule Source in Startup

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.UrlRewriting.Models;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRewriteRuleSource<CustomRuleSource>(CustomRuleSource.SourceName)
            .AddScoped<IDisplayDriver<RewriteRule>, CustomRuleDisplayDriver>();
    }
}
```

### Common Redirect Patterns

| Scenario | Pattern | Substitution | Type |
|----------|---------|--------------|------|
| Exact page redirect | `^/about-us$` | `/about` | `MovedPermanently` |
| Path prefix redirect | `^/old-blog/(.*)$` | `/blog/$1` | `MovedPermanently` |
| Trailing slash removal | `^(.+)/$` | `$1` | `MovedPermanently` |
| Legacy ID to slug | `^/post/(\\d+)$` | `/articles/$1` | `MovedPermanently` |

### Common Rewrite Patterns

| Scenario | Pattern | Substitution | SkipFurtherRules |
|----------|---------|--------------|------------------|
| Media alias | `^/img/(.*)$` | `/media/$1` | `true` |
| API versioning | `^/api/v1/(.*)$` | `/api/v2/$1` | `true` |
| Clean URLs | `^/page/(.*)$` | `/content/$1` | `false` |

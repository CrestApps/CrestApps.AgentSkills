---
name: orchardcore-fulltext-search
description: Skill for implementing full-text search in Orchard Core. Covers enabling search features, index creation and configuration, analyzer setup, search settings, permissions, custom full-text indexing with Liquid, search template customization, and FlowPart widget indexing. Use this skill when requests mention Orchard Core Full-Text Search, Implement Full-Text Search, Step 1 Enable Search Features, Step 2 Create a Search Index, Step 3 Configure Search Settings, Step 4 Set Index Permissions, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Search.Lucene, OrchardCore.Search.Elasticsearch, OrchardCore.Search, OrchardCore.Indexing, OrchardCore.Search.Lucene.Model. It also helps with Step 3 Configure Search Settings, Step 4 Set Index Permissions, Step 5 Select Search Provider, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Full-Text Search - Prompt Templates

## Implement Full-Text Search

You are an Orchard Core expert. Generate code and configuration for implementing full-text search on an Orchard Core website, including index setup, search settings, permissions, custom full-text fields, and template customization.

### Guidelines

- Orchard Core supports Lucene and Elasticsearch as full-text search providers; the setup steps are similar for both.
- Enable `OrchardCore.Search.Lucene` or `OrchardCore.Search.Elasticsearch` depending on the chosen provider.
- Enable `OrchardCore.Search` for the frontend search route (`/search`).
- Create a search index specifying which content types to include.
- Configure search settings to specify the default index and search fields.
- Set index permissions for user roles (e.g., allow Anonymous users to query the index).
- Select the search provider under Settings > Search > Site Search.
- Use Liquid in content type definitions to customize which fields contribute to the `FullText` index.
- Override search templates in your theme to customize the search UI.
- Lucene indexes are stored locally; Elasticsearch requires an external cluster.
- The `TheBlogTheme` recipe includes a pre-configured full-text search setup.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Step 1: Enable Search Features

Enable the required features for Lucene-based full-text search:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Search",
        "OrchardCore.Search.Lucene",
        "OrchardCore.Indexing"
      ],
      "disable": []
    }
  ]
}
```

For Elasticsearch-based full-text search:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Search",
        "OrchardCore.Search.Elasticsearch",
        "OrchardCore.Indexing"
      ],
      "disable": []
    }
  ]
}
```

### Step 2: Create a Search Index

Create a Lucene index profile for full-text search:

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "SiteSearch",
          "IndexName": "sitesearch",
          "ProviderName": "Lucene",
          "Type": "Content",
          "Properties": {
            "ContentIndexMetadata": {
              "IndexLatest": false,
              "IndexedContentTypes": [
                "Article",
                "BlogPost",
                "Page"
              ],
              "Culture": "any"
            },
            "LuceneIndexMetadata": {
              "AnalyzerName": "standard",
              "StoreSourceData": false
            }
          }
        }
      ]
    }
  ]
}
```

#### Index Configuration Options

| Option | Description |
|--------|-------------|
| Index Name | Identifies the index. Creates a folder at `/App_Data/Sites/{TenantName}/Lucene/{IndexName}`. |
| Analyzer Name | Controls text stemming and tokenization. Default `standard` is optimized for English. |
| Culture | `"any"` indexes all cultures; specify a culture code to index only that culture. |
| Content Types | Select which content types to parse and include in the index. |
| Index Latest Version | When `true`, indexes drafts in addition to published items. Useful for admin dashboards. |
| Store Source Data | (Elasticsearch only) When `true`, stores `_source` data for retrieval. |

### Step 3: Configure Search Settings

After creating the index, configure the frontend search settings:

1. Navigate to **Settings** > **Search** in the admin dashboard.
2. Select the default index for the `/search` page.
3. Set the index fields to search — typically `Content.ContentItem.FullText`.

### Step 4: Set Index Permissions

By default, indexes are permission-protected. To allow anonymous users to search:

1. Navigate to **Security** > **Roles** > **Anonymous**.
2. Under the search feature permissions section, enable the permission for your search index.

Each index has its own permission entry, allowing fine-grained access control.

### Step 5: Select Search Provider

1. Navigate to **Settings** > **Search** > **Site Search**.
2. Select the indexing provider (`Lucene` or `Elasticsearch`) for frontend search.

### Step 6: Customize Full-Text Indexing with Liquid

Each content type has a section in its definition to control which fields are indexed as part of `FullText`. By default, content items index the **display text** and **body part**.

To index additional fields, click **Use custom full-text** and enter Liquid expressions:

**Index a custom field (e.g., Subtitle):**

```liquid
{{ Model.Content.BlogPost.Subtitle.Text }}
```

**Index multiple custom fields:**

```liquid
{{ Model.Content.BlogPost.Subtitle.Text }}
{{ Model.Content.BlogPost.Tags.Text }}
```

**Index FlowPart widgets:**

```liquid
{{ Model.Content.FlowPart.Widgets | full_text_aspect }}
```

Or iterate explicitly:

```liquid
{% for contentItem in Model.Content.FlowPart.Widgets %}
  {{ contentItem | full_text_aspect }}
{% endfor %}
```

The `full_text_aspect` filter extracts the full-text content from each widget, making nested content searchable.

### Step 7: Register Custom Analyzers

Register a custom Lucene analyzer in a module's `Startup.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Search.Lucene.Model;
using OrchardCore.Search.Lucene.Services;
using OrchardCore.Modules;

namespace MyModule;

[Feature("MyModule.FrenchAnalyzer")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<LuceneOptions>(o =>
            o.Analyzers.Add(new LuceneAnalyzer("frenchanalyzer",
                new MyAnalyzers.FrenchAnalyzer(LuceneSettings.DefaultVersion))));
    }
}
```

### Customizing Search Templates

Override these templates in your theme to customize the search UI:

| Template File | Purpose |
|---------------|---------|
| `Views/Shared/Search.liquid` or `.cshtml` | General search layout. |
| `Views/Search-Form.liquid` or `.cshtml` | Search form layout. |
| `Views/Search-Results.liquid` or `.cshtml` | Search results layout. |

**Example custom search results template (Liquid):**

```liquid
{% if Model.ContentItems != null and Model.ContentItems.size > 0 %}
    <ul class="list-group">
        {% for item in Model.ContentItems %}
            <li class="list-group-item">
                {{ item | shape_build_display: "SearchSummary" | shape_render }}
            </li>
        {% endfor %}
    </ul>
{% elsif Model.Terms != null %}
    <p class="alert alert-warning">{{ "There are no such results." | t }}</p>
{% endif %}
```

This example changes the display type from `"Summary"` to `"SearchSummary"`, allowing you to create a dedicated shape template for search results.

### Complete Full-Text Search Recipe

A complete recipe that enables features, creates an index, and configures search:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Search",
        "OrchardCore.Search.Lucene",
        "OrchardCore.Indexing"
      ],
      "disable": []
    },
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "SiteSearch",
          "IndexName": "sitesearch",
          "ProviderName": "Lucene",
          "Type": "Content",
          "Properties": {
            "ContentIndexMetadata": {
              "IndexLatest": false,
              "IndexedContentTypes": [
                "Article",
                "BlogPost",
                "Page"
              ],
              "Culture": "any"
            },
            "LuceneIndexMetadata": {
              "AnalyzerName": "standard",
              "StoreSourceData": false
            }
          }
        }
      ]
    }
  ]
}
```

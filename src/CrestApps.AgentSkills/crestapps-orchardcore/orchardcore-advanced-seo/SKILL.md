---
name: orchardcore-advanced-seo
description: Skill for the CrestApps Orchard Core Advanced SEO module (CrestApps.OrchardCore.Seo), which publishes schema.org structured data as one JSON-LD graph per page and for NLWeb answers. Covers the Schema.org part (SchemaOrgPart, SchemaOrgPartSettings with SchemaType and property mappings from Content, Fixed, Editor or Liquid sources), the StructuredDataSettings site settings and their deployment step, the ManageAdvancedSeoSettings permission, Orchard Core SEO, Search, Products and Reviews integration, the getSchemaOrgOptions, configureContentTypeSchemaOrg and configureSiteStructuredData AI tools, describing pages in code with IPageStructuredData, the structured-data tag helper and the structured_data Liquid filter, and extending with IStructuredDataProvider, ContentPartStructuredDataProvider and SelfDescribingParts. Use this skill when requests mention schema.org, JSON-LD, structured data, rich results, Google required properties, product or article markup, or Advanced SEO in Orchard Core.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Advanced SEO (schema.org JSON-LD)

The Advanced SEO module describes the site and its content items with the [schema.org](https://schema.org) vocabulary in JSON-LD. One engine, `IStructuredDataManager`, has two outputs:

- **Pages.** Every front-end page gets a single `<script type="application/ld+json">` holding one `@graph`: the publisher (`Organization` or `Person`), the `WebSite`, and the content item the page displays (or what code said about the page).
- **Agents.** The same engine builds a content item's schema.org object for an answer (`StructuredDataPurpose.Answer`). `orchardcore-nlweb` returns these objects.

It builds on Orchard Core's SEO module (`orchardcore-seo` keeps meta tags, Open Graph, Twitter cards and `robots.txt`) and takes over only the JSON-LD.

## Guidelines

- One feature, ID `CrestApps.OrchardCore.Seo` (category **Content Management**), depending on `OrchardCore.Contents` and `OrchardCore.Liquid`. Install the `CrestApps.OrchardCore.Seo` package in the web/startup project; extension code only needs `CrestApps.OrchardCore.Seo.Abstractions`.
- Integrations switch on by themselves through `[RequireFeatures]` startups:

| Enabled module | What it adds |
|---|---|
| `OrchardCore.Seo` | Uses the SEO part's meta description and social image; merges the JSON-LD typed in the part's **Google Schema** field into the graph and stops Orchard Core writing it twice |
| `OrchardCore.Search` | A `SearchAction` on the `WebSite` node pointing at `/search?terms={search_term_string}` |
| `CrestApps.OrchardCore.AI` | The three AI tools below |
| Products | `Product` nodes with their `Offer` or `AggregateOffer` |
| Reviews | `aggregateRating` from approved reviews |

- Settings live under **Settings → Search → Search Engine Optimization → Advanced SEO** and need **Manage the advanced SEO settings** (`ManageAdvancedSeoSettings`, Administrator by default).
- A content item is described on its page only when its type has the **Schema.org** part (and the item's **Publish no structured data about this item** is off), or has a self-describing part such as `ProductPart`. Other pages still describe the site. NLWeb answers describe every item.
- URLs are absolute, built from the site's **Base URL** setting or the request. The script is built when the theme's `Layout` shape renders, so views and Liquid templates can still describe the page. Values are HTML-escaped so they cannot close the script element.

## The Schema.org part

`SchemaOrgPart` is attachable. Its type settings (`SchemaOrgPartSettings`) hold `SchemaType` and `Properties`, a list of `SchemaOrgPropertyMapping` (`Name`, `Source`, `Value`). They are part of the content definition, so `ContentDefinition` recipes and deployment plans carry them.

| `Source` | `Value` |
|---|---|
| `Content` | `Item:DisplayText`, `Item:Summary`, `Item:Body`, `Item:Url`, `Item:Author`, `Item:PublishedUtc`, `Item:ModifiedUtc`, `Item:CreatedUtc`, `Item:Language`, `Site:Publisher`, `Site:Name`, or a field as `Field:{Part}.{Field}` |
| `Fixed` | A constant for every item |
| `Editor` | None; editors enter the value on the item's **Schema.org** tab (stored in `SchemaOrgPart.Values`) |
| `Liquid` | A Liquid template receiving `ContentItem` (also the model), `Url` and `BaseUrl` |

- A dotted name such as `offers.price` sets a nested object, created with its type (`Offer`) when the vocabulary knows it.
- `SchemaOrgVocabulary` knows Google's rich result types and common types, each property's kind (text, URL, image, date, number, enumeration, object, JSON) and whether Google requires or recommends it. Values are written in that shape: media fields give absolute URLs, enumerations become `https://schema.org/InStock`, content pickers give names and URLs. Unknown types and properties are written as text, or JSON when the text is a JSON object or array.
- The settings editor suggests a source for each property and has **Add suggested properties**; the AI tools use the same rules (`SchemaOrgSuggestions`).

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "BlogPost",
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "SchemaOrgPart",
              "Name": "SchemaOrgPart",
              "Settings": {
                "SchemaOrgPartSettings": {
                  "SchemaType": "BlogPosting",
                  "Properties": [
                    { "Name": "headline", "Source": "Content", "Value": "Item:DisplayText" },
                    { "Name": "image", "Source": "Content", "Value": "Field:BlogPost.Image" },
                    { "Name": "author", "Source": "Content", "Value": "Item:Author" },
                    { "Name": "articleSection", "Source": "Fixed", "Value": "News" },
                    { "Name": "wordCount", "Source": "Editor" }
                  ]
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

## How a node is built

`StructuredDataManager` runs every `IStructuredDataProvider` in ascending `Order`; later providers override earlier properties.

| `StructuredDataProviderOrder` | Provider | Sets |
|---|---|---|
| `Defaults` (-1000) | Content item defaults | `name`, `url`, dates, `inLanguage`, `description` from the body |
| `Parts` (0) | Module providers | Domain data, such as product offers |
| `SeoMeta` (500) | Orchard Core SEO | `description`, `image` |
| `SchemaOrgPart` (1000) | Schema.org part | The mappings and editor values |
| `EditorJsonLd` (2000) | SEO part's **Google Schema** | Editor JSON-LD (wins over the mappings) |
| 3000 | Site | Publisher and `WebSite`; `publisher` and `isPartOf` when missing |

Afterwards `IPageStructuredData.MainEntity` is merged in, an untyped node becomes `WebPage` (or `Thing` without a URL), and a node without `@id` gets `{url}#{type}`.

## Site settings

`StructuredDataSettings` is a site setting set by the `Settings` step. The **Structured data settings** deployment step exports it in the same shape. With **CrestApps Recipes** enabled the section has a JSON schema.

```json
{
  "steps": [
    {
      "name": "Settings",
      "StructuredDataSettings": {
        "RenderOnPages": true,
        "PublisherType": "Organization",
        "PublisherName": "Contoso",
        "PublisherLogoUrl": "/media/logo.png",
        "SameAs": [ "https://www.linkedin.com/company/contoso" ],
        "IncludeWebSite": true,
        "IncludeSearchAction": true
      }
    }
  ]
}
```

## AI tools

With AI enabled, three selectable tools in the **Search Engine Optimization** category let an agent (such as the admin AI chat) set up structured data. Each also needs **Access AI tool**.

| Tool | Arguments | Permission checked |
|---|---|---|
| `getSchemaOrgOptions` | optional `contentType`, `schemaType` | None (read-only) |
| `configureContentTypeSchemaOrg` | `contentType`, `schemaType`, `properties` (`name`, `source`, `value`), `replace`, `addSuggested` | Edit content types |
| `configureSiteStructuredData` | `publisherType`, `publisherName`, `publisherLogoUrl`, `sameAs`, `renderOnPages`, `includeWebSite`, `includeSearchAction` | `ManageAdvancedSeoSettings` |

## Describe a page in code

`IPageStructuredData` (scoped) describes pages that are not content items: set `MainEntity`, `Add(node)` other nodes such as a `BreadcrumbList`, or set `Suppressed` to drop the script. `AddJsonLd(json)` (extension) treats a node without `@id` as the page and adds nodes with `@id` to the graph. `JsonLd.CreateNode`, `JsonLd.Reference` and `JsonLd.Merge` help build nodes.

```csharp
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo;
using Microsoft.AspNetCore.Mvc;

public sealed class AboutController : Controller
{
    private readonly IPageStructuredData _pageStructuredData;

    public AboutController(IPageStructuredData pageStructuredData)
        => _pageStructuredData = pageStructuredData;

    public IActionResult Index()
    {
        var page = JsonLd.CreateNode("AboutPage");
        page["name"] = "About Contoso";
        page["mainEntity"] = JsonLd.Reference("#founder");

        _pageStructuredData.MainEntity = page;
        _pageStructuredData.Add(new JsonObject
        {
            ["@type"] = "Person",
            ["@id"] = "#founder",
            ["name"] = "Ada Lovelace",
        });

        return View();
    }
}
```

From views, with no code-behind:

```cshtml
@addTagHelper *, CrestApps.OrchardCore.Seo

<structured-data>
{ "@type": "ContactPage", "name": "Contact us", "telephone": "@Model.Phone" }
</structured-data>
```

```liquid
{% capture page_json_ld %}{ "@type": "ContactPage", "name": "Contact {{ Site.SiteName }}" }{% endcapture %}
{{ page_json_ld | structured_data }}
```

## Extension points

| Type | Use it to |
|---|---|
| `IStructuredDataProvider` (`Order`, `BuildAsync(StructuredDataContext)`) | Describe your own content or pages; when `context.ContentItem` is `null`, set `context.MainEntity` for route-based pages |
| `ContentPartStructuredDataProvider<TPart>` | Run only for items with a part; override `BuildAsync(context, part)` |
| `StructuredDataOptions.SelfDescribingParts` | Describe items of your part without the Schema.org part |
| `IStructuredDataManager` | `BuildPageDocumentAsync(...)` for a page, `BuildEntityAsync(item, purpose)` for an agent |
| `StructuredDataContext` | `ContentItem`, `Purpose`, `BaseUrl`, `Url`, `MainEntity`, `Graph`, `SuggestType(type)`, `ToAbsoluteUrl(url)` |

Register providers in a startup marked `[RequireFeatures("CrestApps.OrchardCore.Seo")]` so nothing else has to be enabled:

```csharp
using CrestApps.OrchardCore.Seo;

internal sealed class EventStructuredDataProvider : ContentPartStructuredDataProvider<EventPart>
{
    protected override Task BuildAsync(StructuredDataContext context, EventPart part)
    {
        context.SuggestType("Event");
        context.MainEntity["startDate"] = part.StartUtc.ToString("o");

        if (context.Purpose == StructuredDataPurpose.Page)
        {
            context.Graph.Add(JsonLd.CreateNode("Place", $"{context.BaseUrl}/#venue"));
        }

        return Task.CompletedTask;
    }
}

[RequireFeatures("CrestApps.OrchardCore.Seo")]
public sealed class SeoStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IStructuredDataProvider, EventStructuredDataProvider>();
        services.Configure<StructuredDataOptions>(options => options.SelfDescribingParts.Add(nameof(EventPart)));
    }
}
```

## Troubleshooting

- No item node on a page: the type lacks the Schema.org part, the item opted out, or **Add structured data to pages** (`RenderOnPages`) is off.
- JSON-LD written twice: keep `OrchardCore.Seo` enabled together with this module so `SeoAspectJsonLdHandler` clears `SeoAspect.GoogleSchema`.
- Invalid JSON in the **Google Schema** field, the tag helper or the Liquid filter is logged and ignored.

## Related skills

- `orchardcore-seo` (Orchard Core's SEO part and meta tags).
- `orchardcore-nlweb` (natural-language answers built from these objects).
- `orchardcore-products` and `orchardcore-reviews` (product offers and ratings).

---
name: orchardcore-ai-tool-instances
description: Skill for CrestApps Orchard Core AI Tool Instances (CrestApps.OrchardCore.AI.ToolInstances), administrator-created functions built from registered sources. Covers the sources http-api-request (HttpApiRequestToolSettings, authentication types, declared parameters), sitemap-documentation, search-index-documentation, algolia-documentation, website-search (live site search API) and data-source-search (vector search over an AI data source), assigning instances to profiles, templates, chat interactions, post-session processing and the direct-config workflow task, exposing instances to MCP clients, the ManageAIToolInstances and AccessAITool_{functionName} permissions, the AIToolInstances recipe and deployment steps, IAIToolInstanceHandler secret stripping, and custom sources with AddAIToolInstanceSource and AIToolInstancesDisplayDriverBase. Use this skill when requests mention AI tool instances, HTTP API tools, documentation or website search tools, data source search tools, or docs for MCP clients.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Tool Instances

An AI tool instance is a function an administrator creates from a developer-registered **source**. The source describes a capability once (for example "call an HTTP API"); each named instance has its own description, settings, permission, and function name.

| Concept | Registered by | Cardinality |
|---|---|---|
| Tool | Developer, in code | One function per tool |
| Tool instance source | Developer, in code | Blueprint only, never callable directly |
| Tool instance | Administrator, in the admin | Many per source, one function each |

## Guidelines

- Enable `CrestApps.OrchardCore.AI.ToolInstances` (depends on `CrestApps.OrchardCore.AI`). It adds **Artificial Intelligence → Tool Instances**.
- The **Name** is unique and cannot change after creation, because it derives the function name: `tool_instance_` plus the sanitized name, with a short hash suffix when sanitizing changed the name or it is too long. The MCP allow-list accepts either the name or the function name.
- The **Description** is what the model uses to choose between instances of the same source. Describe the concrete purpose.
- Secrets are encrypted with ASP.NET Core Data Protection. Leaving a secret field empty when editing keeps the stored value.
- Instances can be used on profiles, chat interactions, and the direct-config workflow task, and can be exposed to MCP clients.

## Built-in sources

| Source | Registered when | Settings object |
|---|---|---|
| `http-api-request` | Always | `HttpApiRequestToolSettings` |
| `sitemap-documentation` | Always | `SitemapDocumentationToolSettings` |
| `search-index-documentation` | Always | `SearchIndexDocumentationToolSettings` |
| `algolia-documentation` | Always | `AlgoliaDocumentationToolSettings` |
| `website-search` | Always | `WebsiteSearchToolSettings` |
| `data-source-search` | `CrestApps.OrchardCore.AI.DataSources` is enabled | `DataSourceSearchToolSettings` |

The documentation, website, and data source sources sit in the **Knowledgebase** category.

### HTTP API request

Calls a configured endpoint and returns the response to the model.

- `BaseUrl`, optional `PathTemplate` (with `{parameterName}` tokens), `HttpMethod` (`GET`, `POST`, `PUT`, `PATCH`, `DELETE`), `TimeoutSeconds`, `DefaultHeaders`.
- `AllowModelProvidedPath`, `AllowModelProvidedQuery`, `AllowModelProvidedBody` (all default `true`). Turn off anything the instance should not expose.
- `AuthenticationType` `None`, `ApiKey` (`ApiKeyHeaderName`, `ApiKey`), `Bearer` (`BearerToken`), `Basic` (`Username`, `Password`), or `OAuth2` (`TokenEndpoint`, `ClientId`, `ClientSecret`, `Scope`).

**Declared parameters** (`AIToolInstanceParametersMetadata.Parameters`) shape the function schema the model sees. Each parameter has a `Name`, `Description`, `Type` (`String`, `Integer`, `Number`, `Boolean`, `Array`, `Object`), `Required`, `DefaultValue`, `AllowedValues`, and:

- `Fill` - `Model` (the model supplies it), `Fixed` (a pinned value, optionally `IsSecret` and encrypted), or `Context` (resolved server-side from `ContextKey`: `user.id`, `user.name`, `user.email`, `resource.id`, `now.utc`).
- `Binding` - where the value goes: `Query:name`, `Path:name`, `Header:name`, or `Body:name`.

Use `Context` fill for anything identifying the caller so the model cannot impersonate another user.

### Documentation and website search

| Source | Best for | Fields |
|---|---|---|
| Documentation search (sitemap) | Sites with `sitemap.xml` (Docusaurus, MkDocs) | `BaseUrl`, optional `SitemapUrl` (default `{BaseUrl}/sitemap.xml`), `MaxResults`, `MaxPages` |
| Documentation search (search index) | Sites publishing a prebuilt JSON index (MkDocs or Docusaurus Lunr, detected automatically) | `BaseUrl`, optional `IndexUrl` (default `{BaseUrl}/search/search_index.json`), `MaxResults` |
| Documentation search (Algolia DocSearch) | Sites wired to hosted DocSearch | `ApplicationId`, search-only `ApiKey` (encrypted), `IndexName`, `MaxResults` |
| Website search (live API) | WordPress and any site with its own search API | `BaseUrl`, `SearchPath` (`/wp-json/wp/v2/search`), `QueryParameter` (`search`), `ExtraQuery` (`_embed=1`), `ResultsPath`, `TitlePath` (`title`), `UrlPath` (`url`), `SnippetPath` (`_embedded.self[0].excerpt.rendered`), `MaxResults` |

The sitemap and search index sources crawl or download once, cache the corpus, and reuse it until the instance settings change. Website search keeps no corpus and queries the site live, so there is no cold start and the site does the ranking. Never use an Algolia write key.

### Data source search (vector)

Turns one existing AI data source into a callable vector search, so the model searches a knowledge base only when it decides to (unlike a data source attached to a profile, which retrieves on every turn).

- `DataSourceId` (required), `RetrievalMode` (`Chunk` or `Hierarchical`), `Strictness` (1-5), `TopNDocuments` (3-20), and an OData `Filter` validated on save.
- Empty strictness or document count reads the site defaults from **Settings → Artificial Intelligence → Data Sources**.
- The model supplies one to three search phrases, embedded with the same embedding deployment the index was built with. Results carry `[doc:N]` citations.
- There is no "restrict answers to retrieved data" option. Attach the data source to the profile when answers must be grounded. See `orchardcore-ai-data-sources`.

## Assign instances

| Surface | Where | Storage |
|---|---|---|
| AI Profile | **Capabilities** tab, **Tool Instances** | `AIToolInstanceMetadata` |
| AI Profile Template (Profile source) | **Capabilities** tab | Copied to profiles created from the template |
| Chat Interaction | **Capabilities** tab | Carried over by **Chat with Preset Settings** |
| Post-session processing | **Data Processing & Metrics → Capabilities** | Separate from the live selection |
| AI Completion using Direct Config task | **Capabilities** tab (needs Chat Interactions and Workflows) | The task's embedded interaction |
| MCP Server | **Settings → Artificial Intelligence → MCP Server**, **Tool instances** | `McpServerOptions.Tools` |

Selectors list only the instances the editing user may access. To expose documentation sites to external AI agents, select their instances on the MCP Server card (`orchardcore-ai-mcp-server`).

## Permissions

| Permission | Meaning |
|---|---|
| `ManageAIToolInstances` | Reach the list and manage your own instances |
| `ManageAIToolInstancesCreatedByOthers` | Also manage other users' instances; implies `ManageAIToolInstances` |
| `AccessAITool_{functionName}` | Generated per instance; required to select it, and checked again when a chat interaction sends a message |

The feature uses the default tool instance registry: access is enforced when an instance is selected in an editor (and re-checked for chat interactions at send time), not on every AI profile session. See `orchardcore-ai-tools` for the full access model.

## Recipe and deployment

With `OrchardCore.Deployment` enabled, the **AI Tool Instances** deployment step exports all or selected instances. Each source's `IAIToolInstanceHandler` blanks its secrets (HTTP API key, bearer token, password, client secret and cached OAuth tokens; the Algolia API key). Re-enter credentials after import.

The `AIToolInstances` step matches each entry by `ItemId`, then by `Name`; a match is updated, otherwise a new instance is created from `Source`, which must be registered on the target tenant.

```json
{
  "steps": [
    {
      "name": "AIToolInstances",
      "instances": [
        {
          "Source": "http-api-request",
          "Name": "order-lookup-api",
          "Description": "Looks up a customer's order status by order number.",
          "Properties": {
            "HttpApiRequestToolSettings": {
              "BaseUrl": "https://api.example.com/orders",
              "PathTemplate": "{orderNumber}",
              "HttpMethod": "GET",
              "AllowModelProvidedPath": false,
              "AllowModelProvidedQuery": false,
              "AllowModelProvidedBody": false,
              "TimeoutSeconds": 10
            },
            "AIToolInstanceParametersMetadata": {
              "Parameters": [
                {
                  "Name": "orderNumber",
                  "Description": "The order number to look up.",
                  "Type": "String",
                  "Fill": "Model",
                  "Required": true,
                  "Binding": "Path:orderNumber"
                }
              ]
            }
          }
        },
        {
          "Source": "sitemap-documentation",
          "Name": "orchardcore-docs",
          "Description": "Search the Orchard Core framework documentation.",
          "Properties": {
            "SitemapDocumentationToolSettings": {
              "BaseUrl": "https://docs.orchardcore.net"
            }
          }
        },
        {
          "Source": "website-search",
          "Name": "company-blog-search",
          "Description": "Search the company blog for product announcements.",
          "Properties": {
            "WebsiteSearchToolSettings": {
              "BaseUrl": "https://www.example.com"
            }
          }
        },
        {
          "Source": "data-source-search",
          "Name": "hr-policies",
          "Description": "Search the HR policy knowledge base.",
          "Properties": {
            "DataSourceSearchToolSettings": {
              "DataSourceId": "{{HrPoliciesDataSourceId}}",
              "RetrievalMode": "Chunk"
            }
          }
        }
      ]
    }
  ]
}
```

## Register a custom source

Register sources with `AddAIToolInstanceSource` from a startup gated on the feature. Do not call `AddToolInstances(...)` yourself; the feature already configures the registry.

```csharp
using CrestApps.Core.AI;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures(AIConstants.Feature.ToolInstances)]
public sealed class WeatherToolStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public WeatherToolStartup(IStringLocalizer<WeatherToolStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIToolInstanceSource<WeatherToolInstanceSource>("weather", entry =>
        {
            entry.DisplayName = S["Weather Lookup"];
            entry.Description = S["Looks up the forecast for a configured region."];
        });

        services.AddDisplayDriver<AIToolInstance, WeatherToolInstanceDisplayDriver>();
    }
}
```

`WeatherToolInstanceSource` implements `IAIToolInstanceSource`, turning an `AIToolInstance` into an `AITool` (use `instance.GetFunctionName()` for its name). Its `DisplayDriver<AIToolInstance>` returns `null` unless `instance.Source` is `weather`, binds a view model, and stores settings with `instance.Put(...)`. Place source fields after `Content:1`, where the shared name and description render.

If the source stores credentials, add an `IAIToolInstanceHandler` (namespace `CrestApps.OrchardCore.AI.Tools.Handlers`) whose `Exporting(ExportingAIToolInstanceContext context)` blanks them in `context.ExportData["Properties"]`, and register it with `services.TryAddEnumerable(ServiceDescriptor.Transient<IAIToolInstanceHandler, WeatherToolInstanceExportHandler>())`.

## Add the selector to another model

Derive from `AIToolInstancesDisplayDriverBase<TModel>` to add the instance picker to any `ExtensibleEntity`. Override `EditorShapeType` (default `EditToolInstances_Edit`), `EditorLocation` (default `Content:7#Capabilities;9`), `CanHandle`, and `GetSelectedInstanceNames` / `SetSelectedInstanceNames` (default storage `AIToolInstanceMetadata`) only when needed, and register the driver from a startup gated on `AIConstants.Feature.ToolInstances`.

## Admin list

**Tool Instances** renders through Orchard Core's `AdminList` shape (list name `AIToolInstances`). See `orchardcore-admin-lists` for layout and column extension.

## Upgrading from 2.x

- `website-search` and `data-source-search` (with AI Data Sources) are new sources, alongside the documentation search sources (sitemap, search index, Algolia DocSearch). Prefer them over pointing `http-api-request` at a static documentation index, which returns the whole index unfiltered.
- The Algolia search-only key is encrypted with Data Protection at rest and blanked in exports.
- Tool instances are not exposed to MCP clients until selected on the MCP Server card.
- The list page uses the `AdminList` shape; whole-row template overrides only apply to the List layout.

## Troubleshooting

| Symptom | Check |
|---|---|
| A source is missing from **Add Tool Instance** | Its feature is disabled (data source search needs `CrestApps.OrchardCore.AI.DataSources`). |
| Recipe import fails for an instance | `Source` is not registered on the target tenant. |
| The model never calls the instance | The description is vague, or the instance is not selected on the profile or interaction. |
| First documentation search is slow | The sitemap or index is crawled on first use; later searches use the cache. |
| A data source search filter is ignored | The index provider cannot filter (Azure AI Search knowledge base indexes declare no filters field). |

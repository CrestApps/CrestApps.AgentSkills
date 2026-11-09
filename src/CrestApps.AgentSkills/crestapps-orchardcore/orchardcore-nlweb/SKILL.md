---
name: orchardcore-nlweb
description: Skill for the CrestApps Orchard Core NLWeb module (CrestApps.OrchardCore.NLWeb), which lets people and AI agents ask the site questions in natural language with the NLWeb protocol with schema.org answers built by Advanced SEO. Covers the /ask and /await endpoints, protocol versions 0.54 and 0.55, 0.5 and the original query-string version, JSON and server-sent event responses, list, summarize and generate modes, NLWebSettings and the NLWeb settings deployment step, the QueryNLWeb and ManageNLWebSettings permissions, search index and AI data source retrievers, AI rewriting, ranking and generation, the ask AI tool for the MCP server, WebMCP and AI profiles, rate limiting, cross-origin calls, and extending the pipeline with INLWebRetriever, INLWebQueryProcessor, INLWebResultRanker, INLWebResponseGenerator and INLWebAskHandler. Use this skill when requests mention NLWeb, natural-language site search for agents, an /ask endpoint, schema.org answers, or giving MCP clients an ask tool in Orchard Core.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core NLWeb

[NLWeb](https://github.com/nlweb-ai/NLWeb) is an open protocol (being standardized in a W3C Community Group) that lets agents ask a web site questions in natural language. The site answers with the matching items as schema.org objects. The NLWeb module implements it for Orchard Core and describes every result with the same schema.org object the item's page publishes, through the Advanced SEO engine (`orchardcore-advanced-seo`).

## Guidelines

- One feature, ID `CrestApps.OrchardCore.NLWeb` (category **Search**), depending on `CrestApps.OrchardCore.Seo` (enabled with it). Install the `CrestApps.OrchardCore.NLWeb` package in the web/startup project. Extension code only needs `CrestApps.OrchardCore.NLWeb.Core`, which has no Orchard Core dependency.
- Capabilities switch on with the module they need (`[RequireFeatures]`):

| Enabled module | What it adds |
|---|---|
| *(always)* | `/ask`, `/await`, list mode, the content item resolver, settings, permissions |
| `OrchardCore.Search` | Keyword retriever over the selected search indexes (Lucene, Elasticsearch, Azure AI Search) |
| `CrestApps.OrchardCore.AI` | Follow-up query rewriting, AI ranking, `summarize` and `generate` modes, the `ask` AI tool |
| `CrestApps.OrchardCore.AI.DataSources` | Vector retriever over an AI data source's knowledge base index |
| `OrchardCore.RateLimits` | 30 questions a minute per IP address, in the `NLWeb` rate limit group |

- Settings are under **Settings → Search → NLWeb** and need **Manage the NLWeb settings** (`ManageNLWebSettings`, Administrator). Asking needs **Ask the site questions with NLWeb** (`QueryNLWeb`), held by Anonymous, Authenticated and Administrator by default.
- Answers include only published content items the caller may view (`ViewContent`) and of the allowed content types.
- With **Allow calls from other sites** (`AllowCrossOrigin`), endpoints send `Access-Control-Allow-Origin: *`; browsers then send no credentials, so cross-site calls get anonymous answers.

## Endpoints and protocol

Routes are at the tenant root.

| Version | Request | Response |
|---|---|---|
| 0.54 and 0.55 | `POST /ask` with `{ query: { text, site, itemType }, context: { prev, text, memory }, prefer: { streaming, response_format, mode, accept-language }, meta: { version, session_context } }` | `{ _meta: { response_type, version }, results: [ schema.org objects ], content: [ { type: "text", text } ] }` |
| 0.5 | `POST /ask`, same body with `return_response` instead of `prefer` | Same as 0.54 |
| 0.1 (original) | `GET /ask?query=&site=&prev=&mode=&streaming=&query_id=` (needs `AllowLegacyRequests`) | `{ query_id, results: [ { url, name, site, score, description, schema_object } ] }` |

- `_meta.response_type` is `answer`, `elicitation`, `promise` or `failure`; `_meta.summary` holds the summary in `summarize` mode; `session_context` is echoed back.
- Streaming (server-sent events) happens when `prefer.streaming` is `true`, or, without that preference, when `Accept` holds `text/event-stream`. Events: `start`, `result` (one per item), `content`, `elicitation` or `failure`, `complete`, `end`.
- Status codes: `200` for answers and protocol failures, `400` for non-JSON bodies or empty queries, `401`/`403` without `QueryNLWeb`, `429` when rate limited.
- `POST /await` always answers `UNKNOWN_PROMISE`, because every question is answered at once.
- `query.site` picks one of the selected indexes by name; otherwise the first index is used.

```bash
curl -X POST https://example.com/ask \
  -H "Content-Type: application/json" \
  -d '{"query":{"text":"vegetarian recipes with cinnamon","itemType":"Recipe"},"prefer":{"mode":"summarize"},"meta":{"version":"0.54"}}'
```

## How a question is answered

`INLWebAskService.AskAsync` produces a stream of `NLWebEvent`; the JSON, SSE and AI tool responses are all written from it.

1. `INLWebQueryProcessor` (by `Order`): the settings processor caps results, picks the mode and maps `site`; the AI rewriter decontextualizes follow-ups. A processor can end the question with a failure or an elicitation.
2. `INLWebRetriever`: each enabled retriever returns `CandidateFactor` (3) times `MaxResults` candidates; multiple lists are merged with reciprocal rank fusion.
3. `INLWebResultResolver`: loads published items, filters by view permission and content type, and describes each with `IStructuredDataManager.BuildEntityAsync(item, StructuredDataPurpose.Answer)`. When the question asks for a schema.org type, other types are dropped unless none match.
4. `INLWebResultRanker`: the AI ranker scores 0 to 100 and drops results under 25; a failing ranker leaves the order unchanged.
5. `INLWebResponseGenerator`: writes the text in `summarize` or `generate` mode.
6. `INLWebAskHandler`: told about every question (analytics, auditing).

## AI

AI steps use the deployment picked in the settings (`DeploymentName`) or the default utility deployment, and record usage under the `NLWeb` purpose. Their prompts are overridable prompt templates: `nlweb-decontextualize`, `nlweb-rank`, `nlweb-summarize`, `nlweb-generate`.

The `ask` AI tool takes the same `query`, `context` and `prefer` arguments as `POST /ask`, returns the NLWeb JSON response, and answers as the calling user. Select it on the **MCP Server** card (`orchardcore-ai-mcp-server`), on the **WebMCP** card (`orchardcore-ai-webmcp`), or in an AI profile.

## Settings and deployment

`NLWebSettings` is a site setting set by the `Settings` step; the **NLWeb settings** deployment step exports it. Index profiles, the AI deployment and the data source are referenced by name or id, not copied, so create them on the target site too. With **CrestApps Recipes** enabled the section has a JSON schema.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [ "CrestApps.OrchardCore.NLWeb", "OrchardCore.Search" ],
      "disable": []
    },
    {
      "name": "Settings",
      "NLWebSettings": {
        "IndexProfileNames": [ "Main", "Blog" ],
        "ContentTypes": [],
        "MaxResults": 10,
        "DefaultMode": "list",
        "AllowLegacyRequests": true,
        "AllowCrossOrigin": true,
        "RewriteQueries": true,
        "RankResults": false,
        "AllowSummarize": true,
        "AllowGenerate": true,
        "DeploymentName": null,
        "DataSourceId": null
      }
    }
  ]
}
```

An empty `ContentTypes` allows every type. `MaxResults` ranges from 1 to 50 in the editor.

## Extending the pipeline

Register any of the interfaces in your module; they are scoped services in the `CrestApps.OrchardCore.NLWeb.Services` namespace. A retriever returns `NLWebCandidate(id, score, source)` where `id` is a content item id the resolver can load.

```csharp
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Services;

internal sealed class FeaturedItemsNLWebRetriever : INLWebRetriever
{
    private readonly IFeaturedItemsService _featuredItems;

    public FeaturedItemsNLWebRetriever(IFeaturedItemsService featuredItems)
        => _featuredItems = featuredItems;

    public string Name => "featured";

    public Task<bool> IsEnabledAsync(NLWebAskContext context)
        => Task.FromResult(!string.IsNullOrWhiteSpace(context.QueryText));

    public async Task<IReadOnlyList<NLWebCandidate>> RetrieveAsync(NLWebAskContext context, int count, CancellationToken cancellationToken)
    {
        var ids = await _featuredItems.FindAsync(context.QueryText, count, cancellationToken);

        return ids.Select(id => new NLWebCandidate(id, null, Name)).ToList();
    }
}

// In a startup marked [RequireFeatures("CrestApps.OrchardCore.NLWeb")]:
services.AddScoped<INLWebRetriever, FeaturedItemsNLWebRetriever>();
```

| Interface | Members |
|---|---|
| `INLWebQueryProcessor` | `Order` (default 0), `ProcessAsync(context, ct)`; set `context.Failure` or `context.Elicitation` to stop |
| `INLWebRetriever` | `Name`, `IsEnabledAsync(context)`, `RetrieveAsync(context, count, ct)` |
| `INLWebResultRanker` | `RankAsync(context, ct)` over `context.Results` |
| `INLWebResponseGenerator` | `CanGenerate(mode)`, `GenerateAsync(context, ct)` returns the text |
| `INLWebAskHandler` | `AskedAsync(context)` |

`NLWebAskContext` exposes `Request`, `User`, `QueryText`, `ItemType`, `Site`, `Mode`, `MaxResults`, `CandidateFactor`, `Results`, `SessionContext` and a `Properties` bag. To change how items are described, add an `IStructuredDataProvider` (see `orchardcore-advanced-seo`) rather than a custom resolver.

## Related skills

- `orchardcore-advanced-seo` for the schema.org objects in every answer.
- `orchardcore-search-indexing`, `orchardcore-lucene`, `orchardcore-elasticsearch` and `orchardcore-azure-ai-search` for the indexes.
- `orchardcore-ai-data-sources` for the vector retriever; `orchardcore-ai-prompting` for overriding prompt templates.

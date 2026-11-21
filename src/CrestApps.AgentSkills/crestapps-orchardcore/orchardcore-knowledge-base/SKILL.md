---
name: orchardcore-knowledge-base
description: Skill for the CrestApps Orchard Core Knowledge Base module (CrestApps.OrchardCore.KnowledgeBase). Covers the KnowledgeArticle content type with KnowledgeArticlePart, Markdown body, help/{slug} paths and the Knowledge Base Categories taxonomy, who reads an article through Content Access Control's role picker, the /help help center, the help/api/suggest endpoint Tickets uses, Was this helpful? feedback with its rate limit, report and data set, the ViewKnowledgeBaseFeedback permission, the help center widgets and zones, the Knowledge Base recipe and KnowledgeBaseZones step, and AI answers over articles. Use this skill when requests mention a knowledge base, help center, FAQ, self-service articles, article feedback or ticket deflection. Strong matches include KnowledgeArticle, KnowledgeArticlePart, KnowledgeArticleSearch, KnowledgeArticleFeedback, HelpCenterSearchWidget, RelatedArticlesWidget and KnowledgeBaseConstants.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Knowledge Base

## Help Articles and a Help Center

You are an Orchard Core expert. Generate accurate setup, recipes, theming and extension guidance for the CrestApps Knowledge Base module. Articles are ordinary content items (drafted, versioned, localized), shown in a help center at `/help`, restricted per role by Content Access Control, rated by readers, and suggested by Tickets while a requester or agent writes.

### Guidelines

- Install `CrestApps.OrchardCore.KnowledgeBase` in the web/startup project. It has no dependency on Tickets: a site can publish a help center without a service desk; Tickets suggests articles when both are on.
- Who may read an article is Content Access Control's role picker on the type; never add a custom audience field.
- Articles, the **Knowledge Base Categories** taxonomy and the widgets are content items, so they travel with Orchard Core's `Content` and `ContentDefinition` steps.
- Use the `KnowledgeBaseZones` step instead of a `Settings` step for layer zones; the `Settings` step replaces the whole list.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Feature | ID | Depends on |
|---|---|---|
| Knowledge Base | `CrestApps.OrchardCore.KnowledgeBase` | `OrchardCore.Contents`, `OrchardCore.Title`, `OrchardCore.Autoroute`, `OrchardCore.Markdown`, `OrchardCore.Taxonomies`, `CrestApps.OrchardCore.ContentAccessControl`, `OrchardCore.RateLimits` |

Category **Content Management**. The **Knowledge Base** recipe (`KnowledgeBase`, `knowledge-base.recipe.json`) enables it with `OrchardCore.Widgets` and `OrchardCore.Layers` and places the widgets.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.KnowledgeBase",
        "OrchardCore.Widgets",
        "OrchardCore.Layers"
      ],
      "disable": []
    }
  ]
}
```

## Articles

The feature defines the **Knowledge Article** type (`KnowledgeArticle`, `KnowledgeBaseConstants.ArticleType`), listable and creatable, with in order: `TitlePart`, `RolePickerPart` with **Restrict content?** on (**Which roles can see this content**), `KnowledgeArticlePart`, a **Category** taxonomy field (`KnowledgeBaseConstants.CategoryField`, one term), a Markdown **Body** (Wysiwyg editor) and `AutoroutePart` (`help/{slug}`).

| `KnowledgeArticlePart` | Holds |
|---|---|
| `Summary` | One or two sentences for lists, searches and suggestions |
| `HelpfulCount`, `NotHelpfulCount` | Readers' answers to "Was this helpful?", used to sort the help center |

Categories are terms (`KnowledgeCategory` term type) of the **Knowledge Base Categories** taxonomy the feature creates, managed under **Content → Taxonomies** and nestable. `KnowledgeArticleIndex` holds the published article's title, summary, category term, published date and helpful count.

### Who Can Read an Article

| Roles picked | Readers |
|---|---|
| None (**Any role**) | Everyone, signed in or not |
| `Authenticated` | Anyone signed in |
| Staff roles (for example `Editor`) | People in one of those roles |

Administrators read every article. Content Access Control refuses every read permission (view, preview, list, own variants, `ViewContent_KnowledgeArticle`) to anyone outside the roles, so Orchard search hits leave restricted articles out. `KnowledgeArticleSearch` joins `KnowledgeArticleIndex` with `ContentAccessRoleIndex` in SQL, so the help center, categories, search and suggestions only load what the reader may open. Writing articles uses the type's content permissions. See `orchardcore-content-access-control`.

## Admin Locations and Permission

The module adds no admin menu. Articles and the categories taxonomy are under **Content → Content Items** and **Content → Taxonomies**; the feedback report is under **Reports**; widgets under **Design → Widgets**.

| Permission | Display name | Default roles |
|---|---|---|
| `ViewKnowledgeBaseFeedback` | View knowledge base feedback | Administrator, Editor |

## Help Center and Endpoints

| Route | Name | Does |
|---|---|---|
| `GET help` | `KnowledgeBaseHome` | Articles by category (five most helpful each, taxonomy order) beside the category list with counts. `?category={termContentItemId}` shows a category with its sub-categories and searches within it; `?q=` searches (every word, up to five, two letters or more, in title or summary, most helpful first) |
| `GET help/api/suggest?q=` | `KnowledgeBaseSuggest` | Up to five articles as JSON (`title`, `summary`, `url`) for the reader; `&reader=public` or `&reader=signedin` limits to what an anonymous or signed-in reader could open |
| `POST help/articles/{id}/feedback` | `KnowledgeBaseFeedback` | Records a yes/no answer, once per browser (cookie), under a lock on the article |
| `POST help/articles/{id}/feedback/comment` | `KnowledgeBaseFeedbackComment` | Keeps "what was missing" (up to 1,000 characters) on that browser's own No |

With Rate Limits on (always, it is a dependency), both feedback posts share the `knowledge-base-feedback` group: 20 answers per visitor address in 10 minutes. With Tickets on, the portal's **New request** form and the agent's reply box call the suggest endpoint as people type; an agent adds an article's link with one click, and a public reply offers only what its requester can read.

## Widgets and Zones

| Widget type | Part | Shows |
|---|---|---|
| `HelpCenterSearchWidget` (**Help center search**) | `HelpCenterSearchPart` | Search box posting to `/help`; within the category on a category page |
| `KnowledgeBaseCategoriesWidget` (**Knowledge base categories**) | `KnowledgeBaseCategoriesPart` | Categories with counts the reader may open |
| `RelatedArticlesWidget` (**Related articles**) | `RelatedArticlesPart` (`Count` 1 to 20, default 5) | On an article page: same category first, then articles sharing words, never itself |

The help center and the article page (`Content-KnowledgeArticle.Detail`) render three zones themselves (`KnowledgeBaseConstants.Zones`), so widgets show whatever the theme: `KnowledgeBaseTop`, `KnowledgeBaseSidebar` (`col-md-4`) and `KnowledgeBaseAfterContent`. With no widgets in the top or sidebar zone, the help center renders its own search box and categories. Override `Content-KnowledgeArticle.Detail` or move the widgets to theme zones under **Design → Widgets**.

## Recipes

The `KnowledgeBaseZones` step takes no properties: it adds the three zones to `LayerSettings.Zones` (**Design → Settings → Zones**), keeping the site's zones and adding none twice.

```json
{
  "steps": [
    {
      "name": "KnowledgeBaseZones"
    },
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4kbarticle000000000000000a",
          "ContentType": "KnowledgeArticle",
          "DisplayText": "Reset your password",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Reset your password" },
          "KnowledgeArticlePart": { "Summary": "Use Forgot password on the sign-in page to get a reset link." },
          "MarkdownBodyPart": { "Markdown": "1. Open the sign-in page.\n2. Choose **Forgot password**." },
          "AutoroutePart": { "Path": "help/reset-your-password" },
          "RolePickerPart": { "RoleNames": [] }
        }
      ]
    }
  ]
}
```

The **Knowledge Base** recipe runs `feature`, `KnowledgeBaseZones`, a `Layers` step with a **Help center** layer (`UrlCondition` equals `/help` or starts with `/help/`, case-insensitive) and a `Content` step with the three widgets at fixed ids. `KnowledgeBaseZones` has a schema (`KnowledgeBaseZonesRecipeStepSchema`) when CrestApps Recipes is on. An article's category is the `Category` taxonomy field on the type's own part: `"KnowledgeArticle": { "Category": { "TaxonomyContentItemId": "...", "TermContentItemIds": [ "..." ] } }`. `RolePickerPart.RoleNames` empty means everyone.

## Feedback, Report and Data Set

- Each answer is a `KnowledgeArticleFeedback` document in the `KnowledgeBase` collection (`ContentItemId`, `ContentItemVersionId`, `Title`, `CategoryId`, `Helpful`, `Comment`, `CreatedUtc`, `UserId`, `UserName`), indexed by `KnowledgeArticleFeedbackIndex` (exclusive to its document type, one record per document).
- With Reports on, **Knowledge Base Feedback** (`knowledge-base-feedback`, category **General**, last 30 days by default) shows a summary, by article (least helpful first), by category and the newest 200 comments. Needs `ViewKnowledgeBaseFeedback`.
- With Data Pipelines Core on, the **Knowledge Base** data source (`KnowledgeBase`) has the `KnowledgeArticleFeedback` data set. Needs `ViewKnowledgeBaseFeedback`.
- `KnowledgeArticleFeedbackPersonalDataContributor` exports a signed-in reader's answers and, on erasure, clears `UserId`/`UserName` and scrubs the comment; counts don't change.

## AI Answers From Articles

An AI data source over a content index of the `KnowledgeArticle` type lets AI profiles answer from articles with citations that link to them (`ContentItemAILinkGenerator`). Use Elasticsearch (`CrestApps.OrchardCore.AI.DataSources.Elasticsearch`) or Azure AI Search (`CrestApps.OrchardCore.AI.DataSources.AzureAI`); Lucene has no AI knowledge base index. Index only published versions with source data stored, keep the data source's key field the content item id, and use `Content.ContentItem.FullText` as the content field. Content Access Control's `IIndexedDocumentVisibility` keeps restricted articles (any role picked) out of the AI index, even for staff. For a public chat widget, grant `Anonymous` the profile's own `QueryAIProfile_{profileName}`, never `QueryAnyAIProfile`. Full setup is in [references/ai-search.md](references/ai-search.md).

## Troubleshooting

| Symptom | Check |
|---|---|
| An article doesn't show in `/help` | Not published, no category term and the site has categories (it still shows in search), or restricted to roles the reader lacks. |
| Restricted article missing from AI answers | By design: only articles for everyone reach an AI knowledge base. |
| Widgets don't appear | The layer rule doesn't match, or the theme doesn't render the zone and the widgets aren't in the Knowledge Base zones. |
| Running a `Settings` step dropped zones | Use `KnowledgeBaseZones`, which merges. |
| "Was this helpful?" answers stop counting | The browser already answered (cookie), or the address hit the `knowledge-base-feedback` limit. |
| Ticket composer shows no suggestions | Knowledge Base off, or no published article shares a word of four letters or more with the text. |
| Upgraded site lost the article audience | Migration 5 moved `Audience` to the role picker (`Public` none, `SignedIn` `Authenticated`, `Staff` the roles that held the old permission plus `Administrator`); the old permission is gone. |

Related skills: `orchardcore-tickets`, `orchardcore-content-access-control`, `orchardcore-taxonomies`, `orchardcore-widgets`, `orchardcore-layers`, `orchardcore-ai-data-sources`, `orchardcore-ai-chat`, `orchardcore-rate-limits`, `orchardcore-reports`.

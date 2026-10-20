---
name: orchardcore-contents-api
description: Skill for using Orchard Core content APIs. Covers the Contents REST API minimal endpoints (GET, POST with draft, DELETE) secured by the Api bearer scheme, AccessContentApi and content permission checks, Problem Details error responses, the ManageLiquidTemplates requirement for Liquid content mutations, GraphQL content queries and filtering, Liquid and Razor content loading helpers, content item lifecycle states, and admin list customization. Use this skill when requests mention Orchard Core Contents API, api/content, content REST endpoints, content lifecycle states, CommonPart, Authentication for REST API, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Contents, OrchardCore.Apis.GraphQL, OrchardCore.OpenId.Validation, IContentManager, IOrchardHelper, IContentsAdminListFilterProvider, AccessContentApi, and ManageLiquidTemplates.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Contents API - Prompt Templates

## Using Orchard Core Content APIs

You are an Orchard Core expert. Generate code and configuration for accessing content items through REST API, GraphQL, Liquid, and Razor in Orchard Core.

### Guidelines

- The `OrchardCore.Contents` module owns the REST endpoints and the `AccessContentApi` permission. GraphQL requires `OrchardCore.Apis.GraphQL`; Liquid and Razor helpers are separate display-management integrations.
- REST API endpoints use the `Api` authentication scheme. Configure an API authentication provider, such as OpenID token validation, for that scheme. Cookie sessions are not accepted.
- Mutating items that contain Liquid parts or fields also requires `ManageLiquidTemplates` in 4.0.
- Create a dedicated API user with appropriate permissions for API calls.
- GraphQL queries are available when the `OrchardCore.Apis.GraphQL` feature is enabled.
- Use Liquid `Content` object to load content items by alias, slug, ID, or version ID.
- Use the `Orchard` Razor helper for content loading in `.cshtml` views.
- Content items have lifecycle states: Draft, Published, Unpublished, Removed, Cloned, Scheduled.
- `IContentManager.PublishAsync()` only raises publishing events if the content item is a draft.
- Always wrap recipe JSON in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier except View Models.
- Use file-scoped namespaces in C# examples.

### Content Item Lifecycle States

| State         | Description                                                                              |
|---------------|------------------------------------------------------------------------------------------|
| Draft         | Being worked on but not yet published. Only visible to editors.                          |
| Published     | Live and visible to visitors. Has `Published = true` and `Latest = true`.                |
| Unpublished   | Removed from public view but stored in the system as a draft.                            |
| Removed       | Soft-deleted. Invisible to all but restorable via Audit Trail module.                    |
| Cloned        | Independent copy of an existing content item, created as a draft.                        |
| Scheduled     | Set to be published or archived at a future date (via Publish Later / Archive Later).    |

The `Latest` version is the most recent version, which may include unpublished changes.

### CommonPart Properties

Attach `CommonPart` to edit common properties of a content item:

| Property      | Type       | Description                         |
|---------------|------------|-------------------------------------|
| `CreatedUtc`  | `DateTime` | When the content item was created   |
| `Owner`       | `string`   | The owner of the content item       |

### REST API Endpoints

`OrchardCore.Contents` maps three minimal-API endpoints. All of them authenticate with the `Api` scheme (bearer tokens only, no cookies), skip anti-forgery, and return the content item JSON on success.

| Method and route | Endpoint name | Purpose |
|---|---|---|
| `GET /api/content/{contentItemId}` | `ApiGetContentItem` | Get the published version. `404` if it does not exist. |
| `POST /api/content` | `ApiCreateContentItem` | Create a new item, or update the item whose `ContentItemId` matches. Add `?draft=true` to save a draft instead of publishing. |
| `DELETE /api/content/{contentItemId}` | `ApiDeleteContentItem` | Remove the item and return it. `404` if it does not exist. |

#### POST /api/content

```json
{
  "ContentItemId": "optional-existing-id",
  "ContentType": "BlogPost",
  "DisplayText": "My New Post",
  "TitlePart": {
    "Title": "My New Post"
  },
  "HtmlBodyPart": {
    "Html": "<p>Post content here.</p>"
  }
}
```

- A new item needs a known `ContentType`; otherwise the response is `400`.
- For an existing item the payload is merged into the latest draft, with arrays replaced rather than merged.
- Validation failures return `400` with an RFC 9457 validation Problem Details body (`errors` keyed by member name), and nothing is saved.
- Content handlers and workflows run as they do in the editor.

### REST API Permission Matrix

Every endpoint requires `AccessContentApi`. It also checks content permissions against the item:

| Endpoint | Additional permissions |
|---|---|
| `GET /api/content/{contentItemId}` | `ViewContent` on the item |
| `POST /api/content` for a new item | `EditContent` and `PublishContent` on the new item, then `EditContent` again on the merged item |
| `POST /api/content` for an existing item | `EditContent` on the item before and after the merge |
| `DELETE /api/content/{contentItemId}` | `DeleteContent` on the item |

Missing authentication returns `401` and missing permissions `403`; in 4.0 both carry an `application/problem+json` body.

#### Liquid Content and `ManageLiquidTemplates`

In Orchard Core 4.0, a mutation of an item that contains a `LiquidPart`, a `LiquidField`, or a `FacebookPluginPart`, directly or recursively embedded (for example in a `BagPart` or `FlowPart`), also requires the security-critical `ManageLiquidTemplates` permission (`OrchardCore.Liquid.Permissions.ManageLiquidTemplates`, granted to Administrator and Editor by default). The check runs on the merged item, so an API user without it gets `403` and the whole mutation is cancelled; the Liquid component is not silently dropped. `HtmlBodyPart`, `HtmlField`, `MarkdownBodyPart`, and `MarkdownField` no longer execute Liquid, so posting Liquid syntax there stores it as plain source.

### Authentication for REST API

The endpoints only accept bearer tokens validated by `OrchardCore.OpenId.Validation`.

1. Enable `OrchardCore.OpenId.Server` and `OrchardCore.OpenId.Validation`, configure a flow (client credentials for services, authorization code + PKCE for apps), and set `OpenIdValidationSettings.Tenant` to the server tenant. See `orchardcore-openid`.
2. Create a dedicated API user or a client-credentials application with `RoleEntries`, and grant its role `AccessContentApi` plus the content permissions above.
3. Request the `roles` scope for user-based tokens so the role permissions apply.

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.Contents",
        "OrchardCore.OpenId.Server",
        "OrchardCore.OpenId.Validation"
      ]
    }
  ]
}
```

```bash
curl -X POST "https://cms.example.com/api/content?draft=true" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "ContentType": "BlogPost", "DisplayText": "Draft post", "TitlePart": { "Title": "Draft post" } }'
```

To explore and try the endpoints in a browser, enable `OrchardCore.OpenApi` with the Swagger UI and run the `OpenApiPkce` recipe (see `orchardcore-openapi`). With `OrchardCore.RateLimits`, an `Endpoint` policy on `/api` throttles these calls.

### GraphQL Queries

Enable `OrchardCore.Apis.GraphQL` for GraphQL content queries. Callers need `ExecuteGraphQL` (`OrchardCore.Apis.GraphQL.GraphQLPermissions`), and results are filtered by `ViewContent` / `ViewOwnContent` per content type and per item. See `orchardcore-graphql` for configuration and custom types.

#### Query All Items of a Content Type

```graphql
query {
  blogPost {
    contentItemId
    displayText
    publishedUtc
  }
}
```

#### Query a Specific Content Item

```graphql
query {
  blogPost(where: {
    contentItemId: "417qsjrgv97e74wvp149h4da53"
  }) {
    contentItemId
    displayText
    publishedUtc
  }
}
```

#### Available Content Item Fields

| Field                  | Description                          |
|------------------------|--------------------------------------|
| `contentItemId`        | Unique content item identifier       |
| `contentItemVersionId` | Version-specific identifier          |
| `contentType`          | The content type name                |
| `displayText`          | Display text of the content item     |
| `published`            | Whether the item is published        |
| `latest`               | Whether this is the latest version   |
| `modifiedUtc`          | Last modification timestamp          |
| `publishedUtc`         | Publication timestamp                |
| `createdUtc`           | Creation timestamp                   |
| `owner`                | Owner of the content item            |
| `author`               | Author of the content item           |

#### Query Content Parts

```graphql
{
  blogPost {
    displayText
    autoroutePart {
      path
    }
  }
}
```

#### Ordering Results

```graphql
query {
  blogPost(orderBy: { publishedUtc: DESC, displayText: ASC }) {
    contentItemId
    displayText
    publishedUtc
  }
}
```

#### Filtering by Publication Status

Query drafts, latest versions, or all versions:

```graphql
query {
  blogPost(status: DRAFT) {
    contentItemId
    displayText
  }
}
```

Status options: `PUBLISHED` (default), `DRAFT`, `LATEST`, `ALL`.

#### Filtering by Field Values

```graphql
query {
  blogPost(where: {
    displayText_in: ["My biggest Adventure", "My latest Hobbies"]
  }) {
    contentItemId
    displayText
  }
}
```

#### Filtering by Content Part Fields

```graphql
query {
  blogPost(where: {
    autoroutePart: {
      path_contains: "/about"
    }
  }) {
    contentItemId
    displayText
  }
}
```

#### Combining Filters with AND, OR, NOT

```graphql
query {
  blogPost(where: {
    OR: {
      AND: {
        displayText_in: ["Adventure", "Hobbies"],
        publishedUtc_gt: "2024"
      },
      contentItemId: "417qsjrgv97e74wvp149h4da53"
    }
  }) {
    contentItemId
    displayText
  }
}
```

#### Pagination

```graphql
query {
  blogPost(first: 10, skip: 20) {
    contentItemId
    displayText
  }
}
```

### Liquid Content Helpers

#### Load by Alias

```liquid
{% assign myContent = Content["alias:main-menu"] %}
```

#### Load by Slug (Autoroute)

```liquid
{% assign myContent = Content["slug:my-blog/my-blog-post"] %}
```

#### Load Latest Version by Alias

```liquid
{% assign myContent = Content.Latest["alias:main-menu"] %}
```

#### Load by Content Item ID

```liquid
{% assign myContent = Content.ContentItemId["417qsjrgv97e74wvp149h4da53"] %}
```

#### Load Multiple Items by ID

```liquid
{% assign posts = postIds | content_item_id %}
```

#### Load by Version ID

```liquid
{% assign myContent = Content.ContentItemVersionId["49gq8g6zndfc736x0az3zsp4w3"] %}
```

#### Render a Content Item Inline

```liquid
{% contentitem handle:"alias:featured-post", display_type:"Summary" %}
```

The default display type is `Detail`. An optional `alternate` argument is supported.

#### Console Logging for Debugging

```liquid
{{ Model.ContentItem | console_log }}
{{ Model.Content | console_log }}
```

### Razor Content Helpers

The `Orchard` helper (via `IOrchardHelper`) provides content loading methods:

| Method                           | Parameters                                                | Description                                     |
|----------------------------------|-----------------------------------------------------------|-------------------------------------------------|
| `GetContentItemIdByHandleAsync`  | `string handle`                                           | Returns content item ID from a handle           |
| `GetContentItemByHandleAsync`    | `string handle, bool latest = false`                      | Loads content item by handle                    |
| `GetContentItemByIdAsync`        | `string contentItemId, bool latest = false`               | Loads content item by ID                        |
| `GetContentItemsByIdAsync`       | `IEnumerable<string> contentItemIds, bool latest = false` | Loads multiple content items by IDs             |
| `GetContentItemByVersionIdAsync` | `string contentItemVersionId`                             | Loads content item by version ID                |
| `ConsoleLog`                     | `object content`                                          | Logs content to browser console for debugging   |

#### Razor Usage Example

```html
@{
    var featuredPost = await Orchard.GetContentItemByHandleAsync("alias:featured-post");
    var latestDraft = await Orchard.GetContentItemByIdAsync("some-id", latest: true);
    var posts = await Orchard.GetContentItemsByIdAsync(new[] { "id1", "id2", "id3" });
}

@if (featuredPost != null)
{
    <h2>@featuredPost.DisplayText</h2>
    <p>Published: @featuredPost.PublishedUtc</p>
}
```

#### Razor Console Logging

```html
@Orchard.ConsoleLog(Model.Content as object)
@Orchard.ConsoleLog(Model.ContentItem as object)
```

### Admin List Customization

#### Filtering by Stereotype

List content items sharing a stereotype by navigating to:

```
/Admin/Contents/ContentItems?stereotype=Test
```

In 4.0, `stereotype` and `contentTypeId` accept several values, repeated or comma-separated, for example `?stereotype=Widget,Page` or `?contentTypeId=Article&contentTypeId=BlogPost`.

#### Custom Full-Text Search

Implement `IContentsAdminListFilterProvider` for custom search behavior:

```csharp
namespace MyModule;

public sealed class ProductContentsAdminListFilterProvider : IContentsAdminListFilterProvider
{
    public void Build(QueryEngineBuilder<ContentItem> builder)
    {
        builder
            .WithNamedTerm("producttext", builder => builder
                .ManyCondition(
                    (val, query) => query.Any(
                        (q) => q.With<ContentItemIndex>(i =>
                            i.DisplayText != null && i.DisplayText.Contains(val)),
                        (q) => q.With<ProductIndex>(i =>
                            i.SerialNumber != null && i.SerialNumber.Contains(val))
                    ),
                    (val, query) => query.All(
                        (q) => q.With<ContentItemIndex>(i =>
                            i.DisplayText == null || i.DisplayText.NotContains(val)),
                        (q) => q.With<ProductIndex>(i =>
                            i.SerialNumber == null || i.SerialNumber.NotContains(val))
                    )
                )
            );
    }
}
```

Register the custom filter in `Startup`:

```csharp
services.Configure<ContentsAdminListFilterOptions>(options =>
{
    options.DefaultTermNames.Add("Product", "producttext");
});
```

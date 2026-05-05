---
name: orchardcore-contents-api
description: Skill for using Orchard Core content APIs. Covers the Contents REST API (GET, POST, DELETE), GraphQL content queries and filtering, Liquid content loading helpers, Razor content helper methods, content item lifecycle states, and admin list customization. Use this skill when requests mention Orchard Core Contents API, Using Orchard Core Content APIs, Content Item Lifecycle States, CommonPart Properties, REST API Endpoints, Authentication for REST API, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Contents, OrchardCore.Apis.GraphQL, OrchardCore.OpenId.Server, OrchardCore.OpenId.Validation, IContentManager, CommonPart, TitlePart, HtmlBodyPart, IOrchardHelper, IEnumerable, IContentsAdminListFilterProvider. It also helps with REST API Endpoints, Authentication for REST API, GraphQL Queries, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Contents API - Prompt Templates

## Using Orchard Core Content APIs

You are an Orchard Core expert. Generate code and configuration for accessing content items through REST API, GraphQL, Liquid, and Razor in Orchard Core.

### Guidelines

- The `OrchardCore.Contents` module provides REST API, GraphQL, Liquid, and Razor helpers for content access.
- REST API endpoints require authentication via OAuth 2 (OpenId Authorization Server and Token Validation features).
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

Enable the `OrchardCore.Contents` module and configure OpenId for authentication.

#### GET /api/content/{contentItemId}

Retrieve a content item by ID.

| Parameter       | Location | Required | Type   | Description                   |
|-----------------|----------|----------|--------|-------------------------------|
| `contentItemId` | path     | Yes      | string | The content item ID           |

**Response:** `200 OK` with the content item JSON.

#### POST /api/content

Create or update a content item. Send the content item JSON in the request body.

**Request body example:**

```json
{
  "ContentItemId": "optional-existing-id",
  "ContentType": "BlogPost",
  "DisplayText": "My New Post",
  "Published": true,
  "TitlePart": {
    "Title": "My New Post"
  },
  "HtmlBodyPart": {
    "Html": "<p>Post content here.</p>"
  }
}
```

**Response:** `200 OK` with the created/updated content item.

#### DELETE /api/content/{contentItemId}

Delete a content item by ID.

| Parameter       | Location | Required | Type   | Description                     |
|-----------------|----------|----------|--------|---------------------------------|
| `contentItemId` | path     | Yes      | string | The content item ID to delete   |

**Response:** `200 OK` on success.

### Authentication for REST API

API access requires OAuth 2 via OpenId. Enable and configure these features:

1. **OpenId Authorization Server** (`OrchardCore.OpenId.Server`)
2. **OpenId Token Validation** (`OrchardCore.OpenId.Validation`)

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Contents",
        "OrchardCore.OpenId.Server",
        "OrchardCore.OpenId.Validation"
      ],
      "disable": []
    }
  ]
}
```

Create a dedicated API user and configure appropriate permissions via **Security > Roles**.

### GraphQL Queries

Enable `OrchardCore.Apis.GraphQL` for GraphQL content queries.

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

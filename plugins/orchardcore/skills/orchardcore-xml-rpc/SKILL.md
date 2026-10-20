---
name: orchardcore-xml-rpc
description: Skill for enabling XML-RPC and MetaWeblog remote publishing in Orchard Core so desktop blogging clients can create and edit content. Covers the XML-RPC endpoint, the Remote Publishing (MetaWeblog) feature, supported MetaWeblog methods, the RSD discovery route for lists, content and media authorization including Liquid content and restricted media uploads, IXmlRpcHandler and IMetaWeblogDriver extension points, and reader and writer services. Use this skill when requests mention Orchard Core XML-RPC, MetaWeblog, remote publishing, Open Live Writer, desktop blogging clients, or the xmlrpc endpoint, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.XmlRpc, OrchardCore.RemotePublishing, IXmlRpcReader, IXmlRpcWriter, IXmlRpcHandler, MetaWeblogDriver, MetaWeblogHandler, and the /xmlrpc and /xmlrpc/metaweblog routes.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core XML-RPC - Prompt Templates

## Enable Remote Publishing

You are an Orchard Core expert. Enable and configure XML-RPC / MetaWeblog so external client applications such as Open Live Writer can create and edit content remotely.

### Guidelines

- The module ships two features:
  - `OrchardCore.XmlRpc` — provides the core XML-RPC endpoint and protocol support (reader/writer services).
  - `OrchardCore.RemotePublishing` — adds the MetaWeblog API on top of XML-RPC; depends on `OrchardCore.XmlRpc`.
- Enable `OrchardCore.RemotePublishing` when you want blogging-client support; it automatically brings in `OrchardCore.XmlRpc`.
- Endpoints (relative to the tenant prefix):
  - `/xmlrpc` — the XML-RPC entry point (`HomeController`).
  - `/xmlrpc/metaweblog` — the MetaWeblog API endpoint (`MetaWeblogController`), available only when Remote Publishing is enabled.
- The core feature registers `IXmlRpcReader` (`XmlRpcReader`) and `IXmlRpcWriter` (`XmlRpcWriter`) for parsing and emitting XML-RPC method calls and responses.
- `MethodCallModelBinder` binds incoming XML-RPC method-call payloads to controller action parameters.
- Authenticate the client with a site user that has permission to create and publish the target content; the MetaWeblog operations run under that user's identity (user name and password are sent in each call).
- The MetaWeblog implementation lives in the Lists module (`MetaWeblogHandler`): each blog is a content item with `ListPart`, and posts are its contained items. Title, Autoroute, Html/Markdown body, and Media modules contribute `IMetaWeblogDriver`s when Remote Publishing is enabled.
- Supported methods: `blogger.getUsersBlogs`, `metaWeblog.getRecentPosts`, `metaWeblog.newPost`, `metaWeblog.getPost`, `metaWeblog.editPost`, `blogger.deletePost`, and `metaWeblog.newMediaObject`.
- Each list exposes an RSD discovery document at `/xmlrpc/metaweblog/{contentItemId}/rsd`.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Remote Publishing

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.RemotePublishing"
      ],
      "disable": []
    }
  ]
}
```

Enabling `OrchardCore.RemotePublishing` implicitly enables `OrchardCore.XmlRpc` through its dependency.

### Enabling Only the Core XML-RPC Feature

If you need the raw XML-RPC endpoint without the MetaWeblog API:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.XmlRpc"
      ],
      "disable": []
    }
  ]
}
```

### Endpoint Routing (from the module)

```csharp
// Core XML-RPC feature.
routes.MapAreaControllerRoute(
    name: "XmlRpc",
    areaName: "OrchardCore.XmlRpc",
    pattern: "xmlrpc",
    defaults: new { controller = "Home", action = "Index" });

// Remote Publishing (MetaWeblog) feature.
routes.MapAreaControllerRoute(
    name: "MetaWeblog",
    areaName: "OrchardCore.XmlRpc",
    pattern: "xmlrpc/metaweblog",
    defaults: new { controller = "MetaWeblog", action = "Manifest" });
```

### Reader / Writer Services

```csharp
// Registered by OrchardCore.XmlRpc Startup.
services.AddScoped<IXmlRpcReader, XmlRpcReader>();
services.AddScoped<IXmlRpcWriter, XmlRpcWriter>();
```

- `IXmlRpcReader` parses an incoming XML-RPC request into method-call objects.
- `IXmlRpcWriter` serializes response objects back into the XML-RPC wire format.

### Authorization of Remote Mutations

- New and edited posts are authorized against the updated item with `EditContent`, or `PublishContent` when the client publishes; on failure the YesSql session is canceled and the whole call fails ("Not authorized to edit this content").
- Since Orchard Core 4.0, Liquid runs only in `LiquidPart`/`LiquidField`, and posts that contain Liquid components (directly or embedded) also require the `ManageLiquidTemplates` permission. Unauthorized mutations are rejected in full.
- `metaWeblog.newMediaObject` requires `ManageMedia` and `ManageMediaFolder` for the target folder, rejects `.`/`..` path segments, accepts only `MediaOptions.AllowedFileExtensions` (or `RestrictedFileExtensions` for users with `UploadRestrictedMedia`), and stores the file through `FileCreationService`, so upload security handlers such as `OrchardCore.Antivirus.ClamAV` can reject it.

### Extending Remote Publishing

Add MetaWeblog fields for your own part with a `MetaWeblogDriver`, and register it only when Remote Publishing is enabled:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.MetaWeblog;
using OrchardCore.Modules;
using OrchardCore.XmlRpc;
using OrchardCore.XmlRpc.Models;

namespace MyModule.RemotePublishing;

public sealed class SubtitleMetaWeblogDriver : MetaWeblogDriver
{
    public override void BuildPost(XRpcStruct rpcStruct, XmlRpcContext context, ContentItem contentItem)
    {
        var subtitle = (string)contentItem.Content.ArticlePart?.Subtitle?.Text;

        if (!string.IsNullOrEmpty(subtitle))
        {
            rpcStruct.Set("mt_excerpt", subtitle);
        }
    }

    public override void EditPost(XRpcStruct rpcStruct, ContentItem contentItem)
    {
        var excerpt = rpcStruct.Optional<string>("mt_excerpt");

        if (excerpt is not null)
        {
            contentItem.Content.ArticlePart.Subtitle.Text = excerpt;
        }
    }
}

[RequireFeatures("OrchardCore.RemotePublishing")]
public sealed class RemotePublishingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IMetaWeblogDriver, SubtitleMetaWeblogDriver>();
    }
}
```

For a new XML-RPC method set, implement `IXmlRpcHandler` (`SetCapabilities(XElement)` and `ProcessAsync(XmlRpcContext)`; check `context.RpcMethodCall.MethodName` and set `context.RpcMethodResponse`) and register it with `services.AddScoped<IXmlRpcHandler, MyHandler>()`.

### Configuring a Blogging Client (Open Live Writer)

1. Enable the `Remote Publishing` feature.
2. In the client, add a new account and choose the MetaWeblog API.
3. Point the endpoint at `https://your-site/xmlrpc/metaweblog`.
4. Sign in as a user who can create and publish the relevant content type.

The MetaWeblog API supports the typical operations blogging clients need: retrieving recent posts, creating and editing posts, and uploading media.

### Notes

- XML-RPC is a legacy protocol; prefer the GraphQL API (`OrchardCore.Apis.GraphQL`) or the content REST API for modern integrations, and reserve XML-RPC for desktop blogging clients that require MetaWeblog.
- Because the endpoint accepts remote content creation, only enable Remote Publishing when needed and ensure client credentials map to appropriately scoped roles.

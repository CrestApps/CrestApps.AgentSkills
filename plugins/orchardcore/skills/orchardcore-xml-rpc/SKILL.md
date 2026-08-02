---
name: orchardcore-xml-rpc
description: Skill for enabling XML-RPC and MetaWeblog remote publishing in Orchard Core so desktop blogging clients can create and edit content. Covers the XML-RPC endpoint, the Remote Publishing (MetaWeblog) feature, reader/writer services, and endpoint routing. Use this skill when requests mention Orchard Core XML-RPC, MetaWeblog, remote publishing, Open Live Writer, desktop blogging client, xmlrpc endpoint, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.XmlRpc, OrchardCore.RemotePublishing, IXmlRpcReader, IXmlRpcWriter, MethodCallModelBinder, MetaWeblogController, and the /xmlrpc and /xmlrpc/metaweblog routes. It also helps with configuring blogging clients, feature enablement, and the routing patterns captured in this skill.
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
- Authenticate the client with a site user that has permission to create and publish the target content; the MetaWeblog operations run under that user's identity.
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

### Configuring a Blogging Client (Open Live Writer)

1. Enable the `Remote Publishing` feature.
2. In the client, add a new account and choose the MetaWeblog API.
3. Point the endpoint at `https://your-site/xmlrpc/metaweblog`.
4. Sign in as a user who can create and publish the relevant content type.

The MetaWeblog API supports the typical operations blogging clients need: retrieving recent posts, creating and editing posts, and uploading media.

### Notes

- XML-RPC is a legacy protocol; prefer the GraphQL API (`OrchardCore.Apis.GraphQL`) or the content REST API for modern integrations, and reserve XML-RPC for desktop blogging clients that require MetaWeblog.
- Because the endpoint accepts remote content creation, only enable Remote Publishing when needed and ensure client credentials map to appropriately scoped roles.

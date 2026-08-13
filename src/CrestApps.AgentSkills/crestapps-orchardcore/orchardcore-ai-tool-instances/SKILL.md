---
name: orchardcore-ai-tool-instances
description: Skill for configuring and extending Orchard Core AI Tool Instances. Covers admin-created tools, tool instance sources, HTTP API Request instances, encrypted credentials, profile and chat assignment, permissions, custom source display drivers, and access-aware selectors. Use this skill when requests mention AI Tool Instances, CrestApps.OrchardCore.AI.ToolInstances, AddAIToolInstanceSource, or related Orchard Core AI implementation and troubleshooting.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Tool Instances

An AI tool instance is an administrator-created function built from a
developer-registered tool instance source. A source is a reusable blueprint;
each named instance has its own description, settings, permission, and
function name.

## Enable the feature

Enable `CrestApps.OrchardCore.AI.ToolInstances`. It depends on the AI Services
feature and adds **Artificial Intelligence → Tool Instances**.

Every instance requires a unique **Name** and a **Description**. The name
cannot be changed after creation because it is used to derive the sanitized
function name exposed to the model. The description should state the concrete
purpose of the instance, especially when several instances use the same source.

## Built-in HTTP API Request source

The feature registers the `http-api-request` source. It supports:

- Base URL and HTTP method such as `GET`, `POST`, `PUT`, `PATCH`, or `DELETE`.
- Optional timeout and static JSON headers.
- Model-provided relative paths, query parameters, and request bodies.
- `None`, API key, Basic, and OAuth 2.0 authentication.

Allow model-provided values only for the request parts that the instance is
designed to expose. API keys, tokens, passwords, and client secrets are
protected with ASP.NET Core data protection. Leaving a secret field empty
when editing preserves the stored value.

## Assign instances to AI capabilities

The **Tool Instances** selector is available on:

- AI Profile capabilities.
- AI Profile Template capabilities when the template source is `Profile`.
- Chat Interaction capabilities.
- Post-session processing capabilities.

Live and post-session selections are stored separately. An instance selected
for a live conversation is not automatically available during post-session
processing.

## Permissions

The feature provides:

- `ManageAIToolInstances` to manage instances owned by the current user.
- `ManageAIToolInstancesCreatedByOthers` to manage instances owned by others.
- A dynamic `AccessAITool_{functionName}` permission for each instance.

The management page requires `ManageAIToolInstances`. Ownership checks then
limit editing unless the second permission is also granted. The permission-aware
registry exposes an instance to the model only when the current user can access
its dynamic permission.

## Register a custom source

Register a source from a feature-gated startup. Do not call
`AddToolInstances` for this feature because its default registry can expose
instances without the permission-aware registry.

```csharp
using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.Core.AI.Tooling.Instances;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures(AIConstants.Feature.ToolInstances)]
public sealed class WeatherToolStartup : StartupBase
{
    private readonly IStringLocalizer S;

    public WeatherToolStartup(IStringLocalizer<WeatherToolStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIToolInstanceSource<WeatherToolInstanceSource>(
            "weather",
            options =>
            {
                options.DisplayName = S["Weather Lookup"];
                options.Description = S["Looks up the forecast for a configured region."];
            });
    }
}
```

Add a `DisplayDriver<AIToolInstance>` for source-specific fields and check
`instance.Source` before rendering or updating the editor. Use a source-specific
view model for model binding, store the validated settings on the instance, and
place source fields after the shared name and description fields.

## Add a selector to another model

Derive a display driver from
`AIToolInstancesDisplayDriverBase<TModel>` when another extensible entity must
select instances. The base driver filters instances through the current user's
access evaluator. Override `EditorShapeType`, `EditorLocation`,
`CanHandle`, `GetSelectedInstanceNames`, and `SetSelectedInstanceNames` only
when the default behavior does not fit the model.

Register the driver from a startup gated by
`AIConstants.Feature.ToolInstances`. Keep the selected instance names in the
model metadata that the completion context reads, or override both selection
methods when using a custom storage location.

## Configure documentation search instances

The built-in `http-api-request` source is suitable for a service that accepts a
query and returns a small filtered result. Do not point it directly at the
Orchard Core or CrestApps documentation search indexes: these sites use large
static, client-side indexes, ignore query parameters, and the source has no
response filtering or size limit. The Orchard Core Gallery is a package and
module discovery site, not a documentation search API.

To expose documentation lookup at runtime, use one of these approaches:

- Point an `http-api-request` instance at a search service that you own and
  populate from the documentation and gallery data, such as Azure AI Search or
  Elasticsearch.
- Implement a custom tool instance source that fetches and caches the static
  indexes server-side, then returns only matching entries.

The source data may include:

- `https://docs.orchardcore.net`
- `https://core.crestapps.com`
- `https://orchardcore.crestapps.com`
- `https://gallery.orchardcore.net` for package and module discovery

For either approach, use `GET` only, set `AllowModelProvidedBody = false`,
leave credential fields empty for public sources, set a short timeout, and use
precise instance descriptions so the model selects the correct source. Keep
documentation instances separate from privileged management or content tools,
and grant access through the generated `AccessAITool_{functionName}`
permissions.

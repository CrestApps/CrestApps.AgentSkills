# WebMCP examples

## Per-content-item tools on a product page

Attach the **WebMCP tools** part to a type and pick the tools one item exposes. The tools must be selectable AI tools or tool instances; users still need **Access AI tool** for each one and must be able to view the item.

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Product",
          "ContentTypePartDefinitionRecords": [
            { "PartName": "WebMcpPart", "Name": "WebMcpPart", "Settings": {} }
          ]
        }
      ]
    },
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4product0000000000000000001",
          "ContentType": "Product",
          "Published": true,
          "Latest": true,
          "WebMcpPart": { "Tools": [ "checkStock" ] }
        }
      ]
    }
  ]
}
```

## Admin assistant that fills in the form the user is editing

1. Enable `CrestApps.OrchardCore.AI.Chat` and `CrestApps.OrchardCore.AI.WebMcp`.
2. Keep **Add page tools** on (default) on the **WebMCP** card.
3. On the admin chat profile's **Capabilities** tab, tick **Allow page tools**.
4. Grant the editors' role **Use WebMCP tools in the browser**.

The chat now sees `page_list_form_fields` and `page_set_form_field` for the page the message is sent from. The user still clicks **Save**.

## A module-provided page tool with server settings

Resource definition (in the module's resource configuration):

```csharp
using CrestApps.OrchardCore.AI.WebMcp;
using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

internal sealed class ContosoResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest = new();

    static ContosoResourceConfiguration()
    {
        _manifest
            .DefineScript("ContosoWebMcpTools")
            .SetUrl("~/Contoso.Stores/scripts/webmcp-tools.min.js", "~/Contoso.Stores/scripts/webmcp-tools.js")
            .SetDependencies(WebMcpConstants.ResourceNames.Script);
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
```

Script (`webmcp-tools.js`), reading the setting a handler added to `context.Settings`:

```javascript
(() => {
    const storesUrl = window.crestAppsWebMcp?.config?.storesUrl;

    document.modelContext.registerTool({
        name: 'contoso.list_stores',
        description: 'Lists the Contoso stores.',
        inputSchema: { type: 'object', properties: {} },
        annotations: { readOnlyHint: true },
        execute: async () => (await fetch(storesUrl)).json(),
    });
})();
```

Register the handler that adds both (see the main skill) in a startup marked `[RequireFeatures("CrestApps.OrchardCore.AI.WebMcp")]`.

## Troubleshooting

| Symptom | Check |
|---|---|
| No tools in the browser | The user is signed in, has **Use WebMCP tools in the browser**, the page kind is enabled, and the page is HTTPS or `localhost` |
| A tool is missing from the list | It is selected on the card (or **Expose all tools**), it is selectable, and the user has **Access AI tool** for it |
| `400` on invoke | The `RequestVerificationToken` header is missing, or the body is not a JSON object |
| The chat ignores page tools | The profile has **Allow page tools**; consequential tools also need **Allow consequential page tools** |
| A new selection does not apply | The card save releases the tenant; values in `CrestApps:AI:WebMcp` configuration override the stored settings |

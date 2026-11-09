# WebMCP Examples

## Register a Page Tool with the Standard API

No CrestApps code is needed. The AI chat lists it with `getTools()` like every other tool on the page.

```javascript
const registration = new AbortController();

document.modelContext.registerTool({
    name: 'contoso.find_store',
    title: 'Find a store',
    description: 'Finds the Contoso store nearest to a postal code.',
    inputSchema: {
        type: 'object',
        properties: { postalCode: { type: 'string' } },
        required: ['postalCode'],
    },
    annotations: { readOnlyHint: true },
    execute: async ({ postalCode }) => findStore(postalCode),
}, { signal: registration.signal });

// Unregister when the view goes away.
registration.abort();
```

Mark a tool that cannot be undone, such as placing an order, with `annotations: { consequentialHint: true }`, and a tool that returns text the page did not write (reviews, emails) with `untrustedContentHint: true`.

## Wait for the CrestApps Scripts

```javascript
document.addEventListener('crestapps-webmcp-ready', async (event) => {
    const api = event.detail; // same object as window.crestAppsWebMcp

    if (!api.isSupported()) {
        return;
    }

    const names = await api.ready; // the AI tools webmcp.js registered
    console.log('Server tools registered:', names);
});
```

## Load the Scripts from npm

When the Razor class library is not referenced, serve the files from `@crestapps/webmcp` (pin the version in production):

```html
<script type="application/json" id="crestapps-webmcp-config">{"toolsUrl":"/api/ai/webmcp/tools","invokeUrlTemplate":"/api/ai/webmcp/tools/{name}/invoke","antiforgeryHeaderName":"RequestVerificationToken","antiforgeryToken":"..."}</script>
<script src="https://cdn.jsdelivr.net/npm/@crestapps/webmcp/dist/webmcp-polyfill.js"></script>
<script src="https://cdn.jsdelivr.net/npm/@crestapps/webmcp/dist/webmcp.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/@crestapps/webmcp/dist/webmcp-page-tools.min.js"></script>
```

Render the antiforgery values from the server per request; never hard-code them.

## Share a Partner Frame's Tools with the Chat

```html
<iframe src="https://partner.example/widget" allow="tools"></iframe>
```

The frame registers its tools with `exposedTo` naming the page's origin, and the page lists the frame in its configuration:

```csharp
configuration["pageToolOrigins"] = new[] { "https://partner.example" };
```

## Rate-Limit the Tool Endpoints

```csharp
app.MapWebMcpTools()
    .RequireRateLimiting("webmcp");
```

## Opt a Profile into Page Tools in Code

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

public sealed class StoreAssistantSetup
{
    private readonly IAIProfileManager _profileManager;

    public StoreAssistantSetup(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task EnablePageToolsAsync(string profileName, CancellationToken cancellationToken = default)
    {
        var profile = await _profileManager.FindByNameAsync(profileName, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{profileName}' was not found.");

        profile.AlterSettings<WebMcpProfileSettings>(settings =>
        {
            settings.AllowPageTools = true;
            settings.AllowConsequentialPageTools = false;
        });

        await _profileManager.UpdateAsync(profile, cancellationToken: cancellationToken);
    }
}
```

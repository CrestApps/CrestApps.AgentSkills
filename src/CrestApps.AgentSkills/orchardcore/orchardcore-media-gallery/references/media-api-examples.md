# Media API Examples

## Cookie mode from an admin view

Cookie mode is the default. The admin cookie authenticates the request; every
`POST` must carry the antiforgery token.

```html
@Html.AntiForgeryToken()

<script at="Foot">
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
    const base = '@Url.Content("~/api/media/")';

    async function listFolder(path) {
        const response = await fetch(`${base}GetDirectoryContent?path=${encodeURIComponent(path)}`);
        if (!response.ok) {
            throw new Error((await response.json()).detail);
        }

        return response.json(); // { folders: [...], files: [...] }
    }

    async function upload(path, fileInput) {
        const data = new FormData();
        for (const file of fileInput.files) {
            data.append('files', file);
        }

        const response = await fetch(`${base}Upload?path=${encodeURIComponent(path)}`, {
            method: 'POST',
            headers: { RequestVerificationToken: token },
            body: data,
        });

        const result = await response.json();
        return result.files.filter(f => f.error); // per-file failures
    }

    async function moveFiles(names, sourceFolder, targetFolder) {
        await fetch(`${base}MoveMediaList`, {
            method: 'POST',
            headers: { RequestVerificationToken: token, 'Content-Type': 'application/json' },
            body: JSON.stringify({ mediaNames: names, sourceFolder, targetFolder }),
        });
    }

    async function deleteFiles(paths) {
        await fetch(`${base}DeleteMediaList`, {
            method: 'POST',
            headers: { RequestVerificationToken: token, 'Content-Type': 'application/json' },
            body: JSON.stringify(paths),
        });
    }
</script>
```

Use `Url.Content("~/api/media/")` so tenant URL prefixes are kept.

## Bearer mode from a server-side client

Set `MediaApiSettings.AuthenticationScheme` to `Bearer` and enable
`OrchardCore.OpenId.Validation`. Antiforgery is not checked in this mode. The
token's user needs roles that grant the media permissions.

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

public sealed class MediaApiClient
{
    private readonly HttpClient _httpClient;

    public MediaApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient; // BaseAddress = https://cms.example.com/ (with tenant prefix)
    }

    public async Task CreateFolderAsync(string accessToken, string path, string name)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/media/CreateFolder?path={Uri.EscapeDataString(path)}&name={Uri.EscapeDataString(name)}");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task UploadAsync(string accessToken, string path, string fileName, Stream content)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(content), "files", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/media/Upload?path={Uri.EscapeDataString(path)}")
        {
            Content = form,
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string accessToken, IEnumerable<string> paths)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/media/DeleteMediaList")
        {
            Content = JsonContent.Create(paths),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
```

## Listening to media changes over SignalR

Requires `OrchardCore.Media.SignalR` and `ManageMedia`.

```html
<script asp-name="signalr" at="Foot"></script>
<script at="Foot" depends-on="signalr">
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('@Url.Content("~/hubs/media")')
        .withAutomaticReconnect()
        .build();

    connection.on('MediaChanged', change => {
        // change.action: fileUploaded, fileDeleted, fileMoved, fileCopied, directoryCreated, directoryDeleted
        console.log(change.action, change.path, change.newPath);
    });

    connection.start().then(() => connection.invoke('SubscribePath', 'images'));
</script>
```

Headless clients pass the bearer token with
`withUrl(url, { accessTokenFactory: () => token })`.

# Recording and Voice-Media Storage

`IRecordingMediaStore` is the provider-neutral, encrypted store for Contact Center call recordings and the master copy of every voice-media clip (hold music, IVR prompts, agent voicemail greetings). Recording governance (start, stop, pause, erasure, audit) belongs to the Contact Center Call Recording feature and voice-media provisioning belongs to Contact Center; see `orchardcore-contact-center`. This page covers where the encrypted bytes live.

## The Store Contract

```csharp
public interface IRecordingMediaStore
{
    Task<string> StoreAsync(RecordingMediaWriteRequest request, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string storageReference, CancellationToken cancellationToken = default);
}
```

- `RecordingMediaWriteRequest` has `StorageKey`, `InteractionId` (`null` for voice-media master copies), `Format`, and `Content`, which is a `Stream` in 3.0. Media streams to storage instead of being buffered in memory.
- Bytes are encrypted with the tenant data-protection key (purpose `TelephonyConstants.RecordingMediaProtectorPurpose`) in 64 KB authenticated frames. When the backing file can seek (local files and Azure blobs can), `OpenReadAsync` returns a seekable stream that reports the recording length and decrypts only the frames a read covers, so playback serves HTTP range requests without decrypting the whole file. A truncated or extended file fails to open.
- The default store is `LocalEncryptedRecordingMediaStore` over a file system store at `App_Data/Sites/{tenant}/RecordingMedia` (`TelephonyConstants.RecordingMediaFolderName`). Removing a tenant purges its media before removal completes.

Provider ingest (Telnyx `call.recording.saved`, Asterisk ARI recordings) downloads the finished recording into this store and then deletes the provider-hosted copy; see `orchardcore-telephony-telnyx` and `orchardcore-telephony-asterisk`.

### Writing to the Store

```csharp
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;

namespace MyCompany.OrchardCore.MyPbx;

public sealed class MyPbxRecordingIngestor
{
    private readonly IRecordingMediaStore _store;

    public MyPbxRecordingIngestor(IRecordingMediaStore store)
    {
        _store = store;
    }

    public async Task<string> IngestAsync(string interactionId, string providerRecordingId, Stream download, CancellationToken cancellationToken)
    {
        return await _store.StoreAsync(new RecordingMediaWriteRequest
        {
            StorageKey = providerRecordingId,
            InteractionId = interactionId,
            Format = "mp3",
            Content = download,
        }, cancellationToken);
    }
}
```

`StorageKey` is an opaque, unique key (the built-in ingests use the provider's recording id); the local store hashes it into a file name, and writing the same key again overwrites the file. Persist the returned storage reference and never store plaintext recordings elsewhere.

## Azure Blob Storage Backend

| | |
|---|---|
| Feature | Telephony - Azure Blob Storage (`CrestApps.OrchardCore.Telephony.Azure`) |
| Depends on | `CrestApps.OrchardCore.Telephony` |
| Configuration section | `CrestApps:Telephony:AzureRecordings` (under `OrchardCore`) |

When enabled with a `ConnectionString` and `ContainerName`, the feature replaces `IRecordingMediaStore` with the same encrypting wrapper over an Orchard Core `BlobFileStore`, so Azure only receives ciphertext. With either value missing the feature logs an error and the local store stays active.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Telephony": {
        "AzureRecordings": {
          "ConnectionString": "<storage-connection-string>",
          "ContainerName": "telephony-recordings",
          "BasePath": "{{ ShellSettings.Name }}",
          "CreateContainer": true
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Telephony__AzureRecordings__ConnectionString=<storage-connection-string>
OrchardCore__CrestApps__Telephony__AzureRecordings__ContainerName=telephony-recordings
```

| Key | Purpose |
|---|---|
| `ConnectionString` | Azure Storage connection string. Keep it in a secret store or environment variable |
| `ContainerName` | Container name. Supports Liquid, for example `recordings-{{ ShellSettings.Name }}`; lowercased and must be a valid Azure container name (3 to 63 lowercase letters, digits and single hyphens) |
| `BasePath` | Optional folder inside the container; Liquid supported |
| `CreateContainer` | Create the container on tenant start when it does not exist |

Tenant separation:

- **Container per tenant**. Set `ContainerName` to `recordings-{{ ShellSettings.Name }}` and leave `BasePath` empty. A tenant name that yields an invalid container name (for example one with an underscore) logs an error at startup; use a shared container for such tenants.
- **Shared container**. Keep a fixed `ContainerName` and set `BasePath` to `{{ ShellSettings.Name }}`.

Tenant removal purges that tenant's blobs but never deletes the container.

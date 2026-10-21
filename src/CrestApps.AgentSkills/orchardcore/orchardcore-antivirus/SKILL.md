---
name: orchardcore-antivirus
description: Skill for scanning uploaded files for malware in Orchard Core 4.0 with the OrchardCore.Antivirus module. Covers the OrchardCore.Antivirus.ClamAV feature (ClamAV Antivirus Scanner), the OrchardCore Antivirus ClamAV configuration section with Host, Port, ConnectTimeoutSeconds and TransferTimeoutSeconds, environment variables, running clamd with Docker, fail-closed behavior, AntivirusScanningException, connection reuse, buffering of non-seekable streams, which uploads are scanned, and clamd StreamMaxLength sizing. Use this skill when requests mention Orchard Core antivirus, ClamAV, clamd, malware scanning, virus scanning of uploads, or closely related Orchard Core setup or troubleshooting work. Strong matches include OrchardCore.Antivirus, OrchardCore.Antivirus.ClamAV, ClamAvOptions, AntivirusScanningException, and clamav/clamav Docker images.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Antivirus - Prompt Templates

## Scan Uploads with ClamAV

You are an Orchard Core expert. Generate configuration and deployment guidance for scanning uploaded files with ClamAV before Orchard Core stores them.

### Guidelines

- `OrchardCore.Antivirus.ClamAV` (**ClamAV Antivirus Scanner**, Security category) registers a ClamAV `IFileEventHandler` in the shared file upload security pipeline. The pipeline itself (`FileCreationService`, custom upload code, custom handlers) is covered by `orchardcore-file-upload-security`.
- The module ships with Orchard Core 4.0 and is part of `OrchardCore.Application.Cms.Core.Targets`. Only an app that references individual modules needs `<PackageReference Include="OrchardCore.Antivirus" Version="4.*" />` in the web project.
- It fails closed: a malware detection rejects the upload, and a missing `Host`, invalid settings, connection failure, timeout, or unexpected `clamd` response also rejects it.
- Only uploads that go through the pipeline are scanned; see the coverage list below.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.

### Enabling the Feature

Configure the connection before enabling the feature; until `clamd` is reachable every upload is rejected.

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.Antivirus.ClamAV"
      ]
    }
  ]
}
```

### Configuration

The section is `OrchardCore:Antivirus:ClamAV` (new in 4.0, no legacy `OrchardCore_` name). It is read through `IShellConfiguration`, so it can be set per tenant, and the package ships `ConfigurationSchema.json` for appsettings IntelliSense.

```json
{
  "OrchardCore": {
    "Antivirus": {
      "ClamAV": {
        "Host": "clamav",
        "Port": 3310,
        "ConnectTimeoutSeconds": 5,
        "TransferTimeoutSeconds": 30
      }
    }
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `Host` | none | Host name or IP of the `clamd` service. Required. |
| `Port` | `3310` | `clamd` TCP port, 1 to 65535. |
| `ConnectTimeoutSeconds` | `5` | Connection timeout, greater than zero. |
| `TransferTimeoutSeconds` | `30` | Timeout for streaming the file and reading the verdict, greater than zero. Raise it for large uploads. |

```text
OrchardCore__Antivirus__ClamAV__Host=clamav
OrchardCore__Antivirus__ClamAV__Port=3310
OrchardCore__Antivirus__ClamAV__ConnectTimeoutSeconds=5
OrchardCore__Antivirus__ClamAV__TransferTimeoutSeconds=30
```

The options are bound with `IOptions<ClamAvOptions>`, so a configuration change applies after the tenant is reloaded.

### Running clamd with Docker Compose

```yaml
services:
  clamav:
    image: clamav/clamav:stable
    ports:
      - "3310:3310"
  cms:
    image: my-orchard-site
    environment:
      OrchardCore__Antivirus__ClamAV__Host: clamav
    depends_on:
      - clamav
```

`clamd` limits the size of a scanned stream with its own `StreamMaxLength` setting. Keep it at least as large as the Media `MaxFileSize` and the largest deployment package you import; a larger file is rejected with a scanner error. A fresh `clamav` container can take a while to download and load its signatures before it accepts connections.

### How the Scanner Behaves

- The file is sent with the `clamd` `INSTREAM` command. `stream: OK` lets the upload continue; a `... FOUND` response rejects it with the message "The uploaded file '{name}' was rejected because ClamAV detected '{signature}'.".
- Connectivity, timeout, and protocol failures throw `AntivirusScanningException` (namespace `OrchardCore.Antivirus`, derives from `FileStoreException`). Built-in upload surfaces report it as a failed upload; custom upload code must catch `FileStoreException` and treat it as a rejection.
- One TCP connection is reused per configuration (host, port, and timeouts) and shared by tenants with the same settings. Scans over a connection run one at a time, so heavy concurrent uploads queue on the scanner; it reconnects automatically after a failure.
- A seekable upload stream is rewound and scanned in place. A non-seekable stream is first copied to a temporary file from `ITempDirectoryProvider` (deleted on close), which then replaces the upload stream; size the temporary volume for it (see `orchardcore-temp-file-storage`).

### What Is Scanned

Uploads that run through `FileCreationService`, which in Orchard Core 4.0 are:

- Media Library uploads through the `api/media` upload endpoint (admin Media Gallery and media fields).
- Files imported by the `media` recipe step.
- XML-RPC `metaWeblog.newMediaObject` uploads.
- Deployment package imports, local and remote, scanned before the archive is written to temporary storage.

TUS resumable uploads (`OrchardCore.Media.Tus`) and Data Pipelines **Save to the media library** write through the plain `IMediaFileStore.CreateFileFromStreamAsync()` in the current source and are not scanned. Custom upload code is scanned only when it follows `orchardcore-file-upload-security`.

### Troubleshooting

| Symptom | Check |
|---|---|
| Every upload fails after enabling ClamAV | `Host` is missing, or `clamd` is unreachable from the app container or still loading signatures. |
| Large uploads fail with a scanner error | Increase `TransferTimeoutSeconds` and the `clamd` `StreamMaxLength`. |
| Uploads are slow under load | Scans with the same configuration are serialized; scale `clamd` or reduce concurrent large uploads. |
| A custom upload is not scanned | It bypasses `FileCreationService`; see `orchardcore-file-upload-security`. |
| Settings change has no effect | Reload the tenant; the options are read once per shell. |

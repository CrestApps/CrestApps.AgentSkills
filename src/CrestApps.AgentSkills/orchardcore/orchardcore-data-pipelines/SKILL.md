---
name: orchardcore-data-pipelines
description: Skill for using the Orchard Core Data Pipelines module (OrchardCore.DataPipelines), the visual ETL designer. Covers the features and the FTP and SFTP modules, the designer with drafts, versions, previews and runs, every built-in step (Read a data source and its filters, Read a media file, Filter rows, Add calculated fields, Convert time zone, Select fields, Sort rows, Group and summarize, Join, Combine rows, Remove duplicates, Keep first rows, Create a file with several worksheets, Zip files, media library, download link, FTP, SFTP, web API, email and Save content items destinations), placeholders, incremental runs, scheduling, run history, the Run Data Pipeline Task and Data Pipeline Run Completed Event activities, permissions, secrets, shared links, SFTP host keys, the DataPipelines recipe and deployment steps, and DataPipelineOptions. Use it for data pipelines, scheduled data exports, importing CSV or Excel files as content items, or ETL in Orchard Core.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Data Pipelines

The Data Pipelines module ships with Orchard Core 4.0. Administrators design pipelines under **Tools > Data Pipelines** that read data, transform it and deliver it, without code: for example, read the orders of the last 30 days, join their customers, compute totals, write an Excel workbook and upload it to an SFTP server every night, or import a CSV file of the media library as content items.

Related skills:

- `orchardcore-data-sources` - the data sources, file formats and data operations pipelines read and write with.
- `orchardcore-data-formulas` - the formula language of **Filter rows** and **Add calculated fields**, and the filter operators.
- `orchardcore-data-pipeline-steps` - custom step types, editors, icons, state, handlers and shared links in code.

## Features and modules

| Feature / module | Description |
|------------------|-------------|
| `OrchardCore.DataPipelines.Core` (Data Pipelines Core) | Data sources, file formats, formulas, data operations and shared links, shared with other features such as reports. Enabled by dependency only. |
| `OrchardCore.DataPipelines` (Data Pipelines) | Designer, runs, and the steps that read, transform, create and zip files, share links and call web APIs. Depends on Data Pipelines Core and `OrchardCore.Users`. |
| `OrchardCore.DataPipelines.Ftp` (Data Pipelines - FTP) | **Upload to an FTP server** step (FluentFTP). Separate module so the core module doesn't depend on the library. |
| `OrchardCore.DataPipelines.Sftp` (Data Pipelines - SFTP) | **Upload to an SFTP server** step (SSH.NET). |

Steps and data sources light up when the feature that owns them is enabled:

| Feature | Adds |
|---------|------|
| `OrchardCore.Media` | **Read a media file**, **Save to the media library** |
| `OrchardCore.Email` | **Send by email**, **Share a download link** |
| `OrchardCore.Contents` | **Save content items**, the `Contents` data source |
| `OrchardCore.Workflows` | **Run Data Pipeline Task**, **Data Pipeline Run Completed Event** |
| `OrchardCore.Users` / `OrchardCore.Queries` | The `Users` and `Queries` data sources |

```xml
<!-- Web/startup project -->
<PackageReference Include="OrchardCore.DataPipelines" Version="4.*" />
<PackageReference Include="OrchardCore.DataPipelines.Ftp" Version="4.*" />
<PackageReference Include="OrchardCore.DataPipelines.Sftp" Version="4.*" />
```

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.DataPipelines",
        "OrchardCore.DataPipelines.Sftp"
      ]
    }
  ]
}
```

## Concepts

- A pipeline is a graph of steps connected by ports (extract, transform, load). Categories: **Source** (rows out), **Transform** (rows in and out), **File** (rows in, files out), **Destination** (files in; **Save content items** takes rows).
- A port carries **rows** (typed fields) or **files**; a connection joins ports of the same kind. An output can feed several steps (each gets all the data); some inputs (**Combine rows**, **Create a file**, **Zip files**) accept several connections, read one after the other.
- Data flows in batches and every step runs at the same time, so memory doesn't depend on the row count. Steps that must see many rows (**Sort rows**, **Join**, **Group and summarize**, **Remove duplicates**) hold up to `StepMemoryLimitMegabytes` and spill the rest to temporary files of the run, deleted when it ends.
- Each step describes the fields it produces, so the designer knows the fields available to every step and reports issues before a run: missing settings, unknown fields, unconnected required inputs, loops, steps whose feature is disabled.

## Designing pipelines

**Tools > Data Pipelines** has two pages: **Pipelines** (search by name; shows the published version, unpublished changes, disabled state and last run status; **Edit** opens the designer; **Actions** runs now, opens run history, starts over or deletes; **Add Pipeline** creates one from a name and description) and **Run History**.

The designer has:

- **Add a data source** in the toolbar: a dialog of the data sets the user may read, with descriptions, filtered by text and by data source. Choosing one adds a **Read a data source** step on a free spot.
- A **toolbox** by category (Source, Transform, File, Destination) with search; drag a step to the canvas or click it.
- The **canvas**: drag from an output port to an input port to connect. Clicking an unconnected output port lists the steps that accept its data (rows or files) and adds the chosen one already connected; an empty pipeline offers its first source. Zoom, fit, undo and redo (`Ctrl+Z`, `Ctrl+Y`).
- The **step panel** (over the canvas or pinned below it): **Settings** (the step editor plus an optional title shown on the canvas; changes apply as they are made), **Data** (preview) and **Fields** (fields received and produced with types; copy a reference such as `[TitlePart.Title]` for formulas).
- The **pipeline panel**: **Pipeline** (name, description, enabled), **Issues** (errors that block publishing, warnings worth a look) and **History**.

### Drafts and versions

- The designer edits a **draft**, auto-saved shortly after each change. When two users edit the same pipeline, the second save is rejected with an offer to reload or overwrite.
- Runs never execute the draft. **Publish** turns it into a numbered version (1, 2, ...). Errors block publishing; warnings ask for confirmation. Publishing again without changes creates a version only when another user publishes.
- **Discard draft** drops unpublished changes. **Versions** lists published versions: view one read-only, or restore it into the draft (it runs once published again).
- A published pipeline runs with the access of the **user who published it**: publishing vouches for what it reads and writes, which is why `ManageDataPipelines` is security critical.

### Previews

The **Data** tab runs the draft up to the selected step and shows its first rows. A preview reads with the previewing user's access, reads at most `PreviewRowLimit` rows (100) per source, never executes destinations (previewing a destination shows what it would receive; steps after a destination, such as **Rejected**, can't be previewed), never changes incremental state, and stops after two minutes.

### Runs in the designer

**Run** runs the published version. The **History** tab lists recent runs; selecting one overlays each step's status and row or file counts on the canvas, follows a queued or running run, and can cancel it.

## Steps

### Sources

| Step (type) | Ports | Settings |
|-------------|-------|----------|
| **Read a data source** (`DataSource`) | Out: rows | The **data set** (from any data source, chosen when the step is added; **Change** reopens the dialog and clears fields and filters), the **fields to read** in order (none reads all), **filters**, the **maximum rows** (0 = all), **parameters** of a data set that takes them (saved queries, one `name=value` per line), **Read only new rows**. Parameter and filter values accept placeholders such as `{Parameter:Since}`. |
| **Read a media file** (`ReadMediaFile`) | Out: rows | The media library **file** (CSV, Excel, JSON, JSON Lines), its **format** (inferred from the extension by default), whether the first row holds column names (CSV, Excel), the CSV **delimiter**, and the **worksheet** (first by default). Requires Media. |

**Read a data source** reads with the access of the user the run is for (a data set they may not read is reported as missing). **Read a media file** requires that user to manage the media of the file's folder.

#### Filtering the rows of a data source

Each filter compares a field with a value; all filters must match. The editor offers only the comparisons that apply to the field's type, and the step reports a comparison that no longer applies (such as after a field's type changed) as an error.

| Field type | Comparisons |
|------------|-------------|
| Text | equals, doesn't equal, contains, doesn't contain, starts with, ends with, is one of, isn't one of, is empty, isn't empty |
| Integer, Decimal | equals, doesn't equal, greater than (or equal), less than (or equal), is between, is one of, isn't one of, is empty, isn't empty |
| Date, DateTime | The number comparisons, plus is in the last / next N days (counting today) |
| Boolean | equals, doesn't equal, is one of, isn't one of, is empty, isn't empty |

- Value inputs follow the comparison: two for **is between** (an empty bound is open), none for **is empty**, a number of days for **is in the last/next**, a list for **is one of** (comma-separated, `"Smith, John"` quotes a value with a comma; a placeholder such as `{Parameter:Regions}` expands to each comma-separated value).
- Inputs are typed (number, date, date and time, yes/no). The text button next to a value writes it as text, for a placeholder or for a date without a time on a date-time field (covers the whole day). A saved value the typed input can't hold is shown as text.
- Dates are stored as `yyyy-MM-dd`, date-times as `yyyy-MM-ddTHH:mm`, compared in UTC. Text comparisons ignore case.
- The step passes the conditions it safely can to the data source, then applies every filter again to the rows it reads. See `orchardcore-data-formulas` for the operators and how data sources apply them.

### Transforms

| Step (type) | Ports | Behavior |
|-------------|-------|----------|
| **Filter rows** (`Filter`) | In: rows. Out: **Matched**, **Unmatched** (optional) | A true/false **condition** formula, such as `[Amount] >= 100 AND [Country] = 'CA'`. A row whose condition can't be evaluated doesn't match and is logged as a warning. |
| **Add calculated fields** (`CalculatedFields`) | In/Out: rows | Fields computed by formulas, such as `[Price] * [Quantity]`. A formula can use the fields calculated before it; a name that matches an input field replaces its value. A value that can't be calculated is empty and logged as a warning. |
| **Convert time zone** (`ConvertTimeZone`) | In/Out: rows | Converts date-time fields **from UTC** to a time zone's local time, or **to UTC** from local times (such as values read from a file). Time zone: the site's, the run user's profile choice (falls back to the site's), or a chosen one such as `Europe/Paris`. **Fields** (none = every date-time field); dates without a time aren't converted. Follows DST; a skipped or repeated local time takes the offset in effect before the change. |
| **Select fields** (`SelectFields`) | In/Out: rows | Fields to keep, in order, each with an optional new name and type (unconvertible values become empty). No field passes rows unchanged. |
| **Sort rows** (`Sort`) | In/Out: rows | Sort keys, most significant first, ascending or descending. Text ignores case, empty values first, stable. Reads every row before writing; spills past the memory limit and merges. |
| **Group and summarize** (`Aggregate`) | In/Out: rows | **Group by** fields (none = one group) and **values**, each with a name, a function (`Count`, `CountDistinct`, `Sum`, `Average`, `Min`, `Max`, `Median`) and a field (a count without a field counts rows). Output: group fields then values, groups in first-seen order. Medians and distinct counts stay in memory with their group. |
| **Join** (`Join`) | In: **Left**, **Right**. Out: rows | **Rows to keep**: inner, left, right or full join. Pairs of key fields (an empty key never matches), **ignore case** for text keys, **prefix of right fields** (`Right.` by default) for clashing names. Reads the right input first: when it fits in memory the left rows stream through it, so connect the smaller data set to the right; otherwise both sides are sorted in temporary files and merged. Output order is the same either way. |
| **Combine rows** (`Union`) | In: rows, several connections. Out: rows | Appends each connection's rows in turn. Fields match by name; missing fields are empty; differing types widen to one that holds both. |
| **Remove duplicates** (`Distinct`) | In/Out: rows | Fields to **compare** (none = all). Keeps the first row of each key, in order. |
| **Keep first rows** (`Limit`) | In/Out: rows | Rows to **keep** and to **skip** first. Once satisfied, it stops reading, and upstream steps stop too. |

Grouping, deduplicating and joining compare values exactly: numbers by value (`2` equals `2.0`), text with its case unless a join ignores it.

### Writing formulas

The editors of **Filter rows** and **Add calculated fields** list the input fields (click to insert `[Field]` at the caret of the formula last edited) and **Functions and operators** (syntax reminder plus a searchable function list; click to insert `ROUND()` with the caret inside). The list holds built-in and module functions **except aggregates**: step formulas read one row at a time. Use **Group and summarize** for counts, sums and averages. Syntax and function catalog: see `orchardcore-data-formulas`.

### Files

| Step (type) | Ports | Settings |
|-------------|-------|----------|
| **Create a file** (`CreateFile`) | In: rows, several connections. Out: files | **Format** (`csv`, `xlsx`, `json`, `jsonl` or any registered format), **file name** without extension (`{PipelineName}-{Date:yyyyMMdd-HHmmss}` by default), header row, column names from field labels instead of technical names, CSV **delimiter** (`tab` for tab-separated), **worksheet names**, indented JSON. Rows are written as they arrive. |
| **Zip files** (`ZipFiles`) | In: files, several connections. Out: files | **Archive name** without `.zip`. Files with the same name get a number appended. |

#### Writing several data sets to a file

- **Excel workbook**: each connection writes its own worksheet, in connection order, named after the title of the step it comes from. **Worksheet names** takes one name per line in connection order; an empty or missing line keeps the step title. Names Excel can't use are adjusted (invalid characters to `_`, 31 characters max, duplicates numbered).
- **Other formats**: every connection's rows go to one file, one connection after the other, mapped by field name to the fields of the first connection.
- A workbook uses about 16 MB of memory whatever the row count. A worksheet holds at most 1,048,576 rows including the header; a run that writes more fails and suggests CSV or several worksheets.

### Destinations

| Step (type) | Ports | Settings |
|-------------|-------|----------|
| **Save to the media library** (`SaveToMedia`) | In: files | **Folder** (empty = root), **replace** an existing file or save with a number appended. The run user must manage the folder's media, and the extension must be allowed (see below). Requires Media. |
| **Share a download link** (`ShareDownloadLink`) | In: files | **Recipients** (user names or emails of site users, comma-separated), **days the link works** (1 to 365, default 7), **maximum downloads** across all recipients (0 = unlimited), email **subject** and optional **message**. Requires Email. |
| **Upload to an FTP server** (`UploadToFtp`) | In: files | **Host**, **port** (21), **encryption** (explicit TLS default, implicit TLS, none), **validate certificate** (default), **user name**, **password**, **remote folder** (created if missing; empty = starting folder), **replace** existing files (otherwise the step fails), **timeout** (30 s). Requires `OrchardCore.DataPipelines.Ftp`. |
| **Upload to an SFTP server** (`UploadToSftp`) | In: files | **Host**, **port** (22), **user name**, **password** and/or **private key** (PEM or OpenSSH) with optional **passphrase**, **host key fingerprint**, **remote folder** (created if missing; empty = home), **replace**, **timeout** (30 s). Requires `OrchardCore.DataPipelines.Sftp`. |
| **Send to a web API** (`SendToWebApi`) | In: files | Absolute http(s) **URL**, **method** (`POST` or `PUT`), **body** (`multipart/form-data` with the file in a field, `file` by default, or the raw file with its media type), non-secret **headers**, **authentication** (none, basic, bearer token, or API key header, `X-API-Key` by default), **timeout** per request (100 s). One request per file; a non-2xx response fails the step. |
| **Send by email** (`SendByEmail`) | In: files | **To**, **Cc**, **Bcc** (comma or semicolon), **subject**, plain text or HTML **body**, **maximum attachment size** (10 MB; larger fails the step). One email with every file, through the site's default email provider; by default no email when there are no files. Requires Email. |
| **Save content items** (`SaveContentItems`) | In: rows. Out: **Rejected** rows | See below. Requires Contents. |

- Destinations record what they delivered (saved path, URL a file was sent to) in the run history.
- The FTP and web API steps warn when the connection isn't encrypted (credentials and files travel in clear text). The web API step sends requests from the server to the URL it is given.
- Web API headers can't hold credentials: `Authorization` and `Proxy-Authorization` come from the authentication settings (stored as secrets), and `Host`, `Content-Length`, `Transfer-Encoding` and `Connection` are set by the step. It uses the `DataPipelines` named `HttpClient`; configure it, such as with a proxy, with `services.AddHttpClient("DataPipelines")`.

### Saving content items

- **Rows**: update the item a row matches or create one; create only (skip rows that match an item); or update only (reject rows that match none).
- **Match on**: a row field compared with the **content item id** or the **display text** of the items' latest version. A new item matched by display text gets that display text.
- **Values**: parts and fields such as `TitlePart.Title` or `Product.Price`, each set from a row field. Writable values are the ones the `Contents` data source reads (see `orchardcore-data-sources`); lists such as content pickers take comma-separated ids.
- **Publish the items** or save drafts; **items per save** (100 by default, 1 to 10,000).
- Each item is validated as the editor would. Rows that can't be saved go to **Rejected** with the reason in an `Error` field (write them to a file, for example). The run user must be allowed to edit, and to publish when publishing, items of the type; new items are owned by that user.

### File names and texts

File names, FTP/SFTP remote folders, **Send by email** subject and body, the **Share a download link** email subject, and **Read a data source** parameter and filter values accept:

| Placeholder | Value |
|-------------|-------|
| `{PipelineName}` / `{PipelineId}` / `{RunId}` | The pipeline name, pipeline id, run id |
| `{Date:format}` | Run start in the site time zone with a .NET format such as `yyyy-MM-dd`; `{Date}` uses `yyyy-MM-dd` |
| `{Parameter:name}` | A run parameter (such as one the Run Data Pipeline Task passes), or nothing |
| `{FileNames}` | The attached file names (**Send by email** subject and body only) |

Unknown placeholders stay as they are. Characters a file name can't hold, such as slashes, become underscores.

## Runs

- Started by **Run now** (pipeline **Actions** menu) or **Run** (designer), which require `RunDataPipelines`, or by the **Run Data Pipeline Task**. Disabled or never-published pipelines don't run.
- Runs execute in the background: a run is queued and starts once the request that queued it ends; a background task starts queued runs every minute.
- A run reads and writes with the access of the user who published the pipeline; if that user no longer exists or is disabled, runs fail until the pipeline is published again.
- One run per pipeline at a time (others wait in the queue); at most `MaxConcurrentRuns` (4) per tenant.
- A running run that stops reporting progress for 10 minutes (such as after a restart) is marked failed. A failing step stops the run.
- Run files and spill files live under `App_Data/Sites/{TenantName}/DataPipelines/Runs/{RunId}`, deleted when the run ends.

### Incremental runs

- **Read only new rows** (on **Read a data source**) takes a number or date field that grows whenever a row is added or changed, such as **Modified** (`ModifiedUtc`). Each successful run keeps the largest value it read; the next run reads only larger values. The first run reads every row.
- Kept values are saved only when a run succeeds; a failed or cancelled run re-reads the same rows, so destinations should update by key rather than append.
- A step that reads only new rows reads all of them, so it can't also set a maximum number of rows. Changing the data source, data set or field starts over. Previews read every row and never change kept values.
- **Start over** (pipeline **Actions** menu) forgets kept values so the next run reads everything; unavailable while a run is queued or running.
- The field must reflect every change: a row changed without changing it, or one that gets a value below the largest already read (such as a long transaction), isn't read. When every change must be delivered, read a change queue with a custom data source (see `orchardcore-data-pipeline-steps`, building an incremental export).

### Scheduling runs

Use a workflow: a **Timer Event** (cron) followed by **Run Data Pipeline Task**. Enable **Skip when a run is already queued or running** when the timer may fire more often than a run lasts, so runs don't pile up (the workflow continues with **Skipped**). The workflow can also notify on failure or pass run parameters. See `orchardcore-workflows`.

### Run history

**Tools > Data Pipelines > Run History** lists runs, most recent first: status (queued, running, succeeded, failed, cancelled), version, queued time, duration, trigger and user; search by pipeline name and filter by status. **Run history** in a pipeline's **Actions** menu lists that pipeline's runs. A run page shows queued/started/ended times and the error, each step with status, rows and files read and written and warnings, the deliveries, and the log (last 200 messages). `RunDataPipelines` can cancel a queued run (at once) or a running one (at its next batch). Runs are deleted `RunRetentionDays` (30) after they end.

## Workflows

With Workflows enabled, two activities appear in the **Data** category.

**Run Data Pipeline Task** queues a run of the published version (runs with the publisher's access):

| Setting | Description |
|---------|-------------|
| Data pipeline | The pipeline to run. |
| Parameters | One `name=value` per line, as Liquid, such as `Since={{ Workflow.Input.Since }}`; read by steps with `{Parameter:name}`. |
| Skip when a run is already queued or running | Off by default. When on and a run is active, continues with **Skipped** instead of queuing. |
| Wait for the run to end | On by default: halts until the run ends, then **Succeeded**, or **Failed** when the run failed or was cancelled; the run is the last result and the `DataPipelineRun` output. Off: continues at once with **Queued** and the run id as the last result. |

The task takes **Failed** at once when the pipeline doesn't exist, was never published, or is disabled.

**Data Pipeline Run Completed Event** starts or resumes a workflow when a run ends: **Data pipeline** (one or any) and **Runs** (every run, succeeded, or failed/cancelled). It is correlated by pipeline id.

The run, the last result of both activities, has `RunId`, `PipelineId`, `PipelineName`, `Status` (`Succeeded`, `Failed`, `Cancelled`), `Error`, `CompletedUtc`, `Deliveries` (`Description`, `Url`) and `Steps` (`StepId`, `Title`, `Status`, `RowsIn`, `RowsOut`).

## Security

### Permissions

| Permission | Allows |
|------------|--------|
| `ManageDataPipelines` | Create, design, preview, publish, delete. Security critical (published pipelines run as their publisher). Implies the two below. |
| `RunDataPipelines` | Run published pipelines and cancel runs. Implies `ViewDataPipelines`. |
| `ViewDataPipelines` | List pipelines, view them and their versions in a read-only designer, view runs. |
| `ManageSharedLinks` | Manage **Tools > Shared Links** (Data Pipelines Core). |

The pipeline permissions are on `OrchardCore.DataPipelines.DataPipelinePermissions`, `ManageSharedLinks` on `OrchardCore.DataPipelines.SharedLinks.SharedLinkPermissions`. The Administrator role has them by default.

### Secrets

Passwords, private keys, passphrases, tokens and API keys of steps are encrypted with the tenant's Data Protection keys. They are never shown again: the editor shows an empty field (left empty keeps the secret) and a checkbox to remove it. Run messages never contain secrets. A secret encrypted on another site or with other keys (such as after copying a database) can't be read; the step reports an error until it is entered again.

### Shared links

**Share a download link** (and other modules, such as reports) share resources through links:

- each link holds a random 256-bit token; only its SHA-256 hash is stored. The link is emailed to its recipients and never recorded in a run or shown in the admin;
- a link works only for its recipients (site users, signed in) with its token; the site doesn't list what was shared with a user;
- a link works until it expires, reaches its maximum uses (counted with one atomic database update per use), or is revoked;
- a wrong token is answered as if the link didn't exist.

**Tools > Shared Links** lists the links of every module (search by name or by what shared them) with kind, recipients, expiry and use count; links can be revoked or deleted with their resource. A link is deleted with its resource a day after it stops working. For **Share a download link**, a copy of each file is kept in the tenant's `App_Data` folder (outside the media library), and the run warns about recipients who couldn't be emailed (for example, without an email address).

### SFTP host keys

Set the **host key fingerprint** of **Upload to an SFTP server** to refuse servers that present another key. It is the SHA-256 fingerprint `ssh` shows, such as `SHA256:ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og` (prefix and Base64 padding optional). When empty, the step warns that the host isn't verified and logs the presented fingerprint so it can be copied into the settings.

## Saving files to the media library

**Save to the media library** only saves extensions the media library accepts. By default it accepts `.xlsx` but not `.csv`, `.json`, `.jsonl` or `.zip`; a run that saves one fails, and the editor warns. Add them to `OrchardCore:Media:AllowedFileExtensions`. The list **replaces** the defaults, so include every extension to keep:

```json
{
  "OrchardCore": {
    "Media": {
      "AllowedFileExtensions": [
        ".jpg", ".jpeg", ".png", ".gif", ".ico", ".webp",
        ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".pps", ".ppsx", ".odt", ".xls", ".xlsx", ".psd",
        ".mp3", ".m4a", ".ogg", ".wav", ".mp4", ".m4v", ".mov", ".wmv", ".avi", ".mpg", ".ogv", ".3gp", ".webm",
        ".csv", ".json", ".jsonl", ".zip"
      ]
    }
  }
}
```

Media library files are served to anyone with the URL unless Secure Media restricts them (see `orchardcore-media`). Use **Share a download link** for data that must reach only some users.

## Recipes and deployment

The **Data Pipelines** deployment step (**Data** category) exports pipelines with their published definition, or their draft when never published: **Include all the data pipelines** (default, includes pipelines created later) or the checked pipelines. Secrets (settings whose names start with `Protected`), runs, run history and shared links are not exported.

The `DataPipelines` recipe step imports pipelines. Step settings are in `Properties` under the settings type name, as an export shows them:

```json
{
  "steps": [
    {
      "name": "DataPipelines",
      "Pipelines": [
        {
          "PipelineId": "4fa5t9rfmxagnt3ab966tzrk5r",
          "Name": "Blog posts export",
          "Description": "Exports the blog posts every night.",
          "IsEnabled": true,
          "Definition": {
            "Steps": [
              {
                "StepId": "source",
                "Type": "DataSource",
                "Title": "Blog posts",
                "X": 180,
                "Y": 320,
                "Properties": {
                  "DataSourceStepSettings": {
                    "Source": "Contents",
                    "DataSet": "BlogPost",
                    "Fields": [ "ContentItemId", "DisplayText", "ModifiedUtc" ],
                    "IncrementalField": "ModifiedUtc"
                  }
                }
              },
              {
                "StepId": "file",
                "Type": "CreateFile",
                "X": 480,
                "Y": 320,
                "Properties": {
                  "CreateFileStepSettings": {
                    "Format": "xlsx",
                    "FileName": "posts-{Date:yyyyMMdd}",
                    "UseDisplayNames": true
                  }
                }
              }
            ],
            "Connections": [
              { "SourceStepId": "source", "SourcePort": "Output", "TargetStepId": "file", "TargetPort": "Input" }
            ]
          }
        }
      ]
    }
  ]
}
```

- Pipelines match by `PipelineId` (at most 26 characters). An existing pipeline gets the imported name, description and definition **as its draft** and keeps its published version; otherwise a pipeline is created (with a new id when none is given).
- An imported definition is a draft: it runs only once a user publishes it. Re-enter step secrets before publishing.
- Prefer exporting a pipeline designed in the admin and editing the payload. See [references/data-pipelines-examples.md](references/data-pipelines-examples.md) for more recipes.

## Configuration

`DataPipelineOptions` binds to `OrchardCore:DataPipelines`, for all tenants or per tenant:

```json
{
  "OrchardCore": {
    "DataPipelines": {
      "StepMemoryLimitMegabytes": 32,
      "MaxConcurrentRuns": 4,
      "RunRetentionDays": 30,
      "PreviewRowLimit": 100
    }
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `StepMemoryLimitMegabytes` | `32` | Memory of a step that sees many rows at once (sort rows, join right input, groups, distinct keys); the rest spills to run files. |
| `MaxConcurrentRuns` | `4` | Runs of the tenant executing at once. |
| `RunRetentionDays` | `30` | Days runs are kept after they end; `0` keeps them. |
| `PreviewRowLimit` | `100` | Rows each source reads in a preview. |

```csharp
services.Configure<DataPipelineOptions>(options =>
{
    options.StepMemoryLimitMegabytes = 128;
    options.MaxConcurrentRuns = 2;
});
```

## Extending data pipelines

| To add | Implement / register | Skill |
|--------|----------------------|-------|
| A data source | `RecordDataSource` or `IDataSource`, `services.AddDataSource<T>()` | `orchardcore-data-sources` |
| A file format | `IDataFileFormat` / `IDataWorkbookFormat`, `services.AddDataFileFormat<T>()` | `orchardcore-data-sources` |
| A formula function | `ExpressionFunction.CreateScalar` / `CreateAggregate`, `services.AddExpressionFunction(...)` | `orchardcore-data-formulas` |
| A step type, its editor and icon | `DataPipelineStepType<TStep, TSettings>`, `DataPipelineStepDisplayDriver<TSettings, TViewModel>`, `services.AddDataPipelineStep<TStep, TDriver>()`, `DataPipelineStepIcon-{StepName}.cshtml` | `orchardcore-data-pipeline-steps` |
| State between runs, work confirmed on success | `context.GetState` / `SetState`, `context.Run.OnSucceeded` | `orchardcore-data-pipeline-steps` |
| Reactions to pipeline changes or run ends | `IDataPipelineHandler`, `IDataPipelineRunHandler` | `orchardcore-data-pipeline-steps` |
| Shared links to a module's resource | `ISharedLinkHandler`, `ISharedLinkManager` | `orchardcore-data-pipeline-steps` |

`IDataTimeZoneResolver` (Data Pipelines Core) resolves the same time zones as **Convert time zone** (site, user, or by id) in a request and in a background run.

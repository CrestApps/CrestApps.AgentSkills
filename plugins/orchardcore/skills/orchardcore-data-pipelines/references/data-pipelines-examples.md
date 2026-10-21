# Data Pipelines Examples

The settings type names below are the ones an export shows (`{StepType}StepSettings` under `Properties`). Enumerations are written by name. Prefer exporting a pipeline designed in the admin and editing the payload; imported pipelines are drafts until a user publishes them.

## Import a CSV from the media library as content items

Reads `imports/products.csv`, updates the `Product` item whose display text matches the `Name` column (or creates it), and writes the rows that can't be saved to a CSV file shared with an administrator.

```json
{
  "steps": [
    {
      "name": "DataPipelines",
      "Pipelines": [
        {
          "PipelineId": "productsimport000000000001",
          "Name": "Products import",
          "IsEnabled": true,
          "Definition": {
            "Steps": [
              {
                "StepId": "read",
                "Type": "ReadMediaFile",
                "X": 180,
                "Y": 320,
                "Properties": {
                  "ReadMediaFileStepSettings": {
                    "Path": "imports/products.csv",
                    "Delimiter": ",",
                    "HasHeaderRow": true
                  }
                }
              },
              {
                "StepId": "save",
                "Type": "SaveContentItems",
                "X": 480,
                "Y": 320,
                "Properties": {
                  "SaveContentItemsStepSettings": {
                    "ContentType": "Product",
                    "Mode": "CreateOrUpdate",
                    "MatchBy": "DisplayText",
                    "KeyField": "Name",
                    "Mappings": [
                      { "Target": "Product.Price", "Source": "Price" },
                      { "Target": "Product.Sku", "Source": "Sku" }
                    ],
                    "Publish": true,
                    "BatchSize": 100
                  }
                }
              },
              {
                "StepId": "rejected",
                "Type": "CreateFile",
                "X": 780,
                "Y": 320,
                "Properties": {
                  "CreateFileStepSettings": { "Format": "csv", "FileName": "rejected-products-{Date:yyyyMMdd}" }
                }
              },
              {
                "StepId": "share",
                "Type": "ShareDownloadLink",
                "X": 1080,
                "Y": 320,
                "Properties": {
                  "ShareDownloadLinkStepSettings": {
                    "Recipients": [ "admin" ],
                    "ExpiresAfterDays": 7,
                    "Subject": "{PipelineName}: rows that couldn't be saved"
                  }
                }
              }
            ],
            "Connections": [
              { "SourceStepId": "read", "SourcePort": "Output", "TargetStepId": "save", "TargetPort": "Input" },
              { "SourceStepId": "save", "SourcePort": "Rejected", "TargetStepId": "rejected", "TargetPort": "Input" },
              { "SourceStepId": "rejected", "SourcePort": "Output", "TargetStepId": "share", "TargetPort": "Input" }
            ]
          }
        }
      ]
    }
  ]
}
```

- Every CSV column is read as text; the step converts each value to the type of the part or field value it writes (a value it can't convert is written empty). Add a **Select fields** step with types to catch bad values earlier, and rely on **Rejected** for items that fail validation.
- `Product.Price` and `Product.Sku` must be values the `Contents` data source reads for the content type (see `orchardcore-data-sources`).

## Filtered nightly export to an SFTP server

Reads the orders modified in the last 7 days whose status isn't `Cancelled`, keeps three renamed fields, and uploads a CSV. The password or private key is a secret: enter it in the designer after the import (settings starting with `Protected` aren't exported or imported).

```json
{
  "steps": [
    {
      "name": "DataPipelines",
      "Pipelines": [
        {
          "PipelineId": "ordersexport0000000000001",
          "Name": "Orders export",
          "IsEnabled": true,
          "Definition": {
            "Steps": [
              {
                "StepId": "orders",
                "Type": "DataSource",
                "Title": "Recent orders",
                "X": 180,
                "Y": 320,
                "Properties": {
                  "DataSourceStepSettings": {
                    "Source": "Contents",
                    "DataSet": "Order",
                    "Fields": [ "ContentItemId", "DisplayText", "Order.Status", "Order.Total", "ModifiedUtc" ],
                    "Filters": [
                      { "Field": "ModifiedUtc", "Operator": "InLastDays", "Values": [ "7" ] },
                      { "Field": "Order.Status", "Operator": "NotIn", "Values": [ "Cancelled" ] }
                    ]
                  }
                }
              },
              {
                "StepId": "select",
                "Type": "SelectFields",
                "X": 480,
                "Y": 320,
                "Properties": {
                  "SelectFieldsStepSettings": {
                    "Fields": [
                      { "Field": "DisplayText", "Name": "OrderNumber" },
                      { "Field": "Order.Total", "Name": "Total", "Type": "Decimal" },
                      { "Field": "ModifiedUtc", "Name": "Modified" }
                    ]
                  }
                }
              },
              {
                "StepId": "file",
                "Type": "CreateFile",
                "X": 780,
                "Y": 320,
                "Properties": {
                  "CreateFileStepSettings": { "Format": "csv", "FileName": "orders-{Date:yyyy-MM-dd}" }
                }
              },
              {
                "StepId": "sftp",
                "Type": "UploadToSftp",
                "X": 1080,
                "Y": 320,
                "Properties": {
                  "UploadToSftpStepSettings": {
                    "Host": "sftp.example.com",
                    "Port": 22,
                    "Username": "exports",
                    "HostKeyFingerprint": "SHA256:ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og",
                    "RemoteFolder": "/incoming/{Date:yyyy}",
                    "Overwrite": true
                  }
                }
              }
            ],
            "Connections": [
              { "SourceStepId": "orders", "SourcePort": "Output", "TargetStepId": "select", "TargetPort": "Input" },
              { "SourceStepId": "select", "SourcePort": "Output", "TargetStepId": "file", "TargetPort": "Input" },
              { "SourceStepId": "file", "SourcePort": "Output", "TargetStepId": "sftp", "TargetPort": "Input" }
            ]
          }
        }
      ]
    }
  ]
}
```

Schedule it with a workflow: **Timer Event** (for example `0 2 * * *`) followed by **Run Data Pipeline Task** on this pipeline with **Skip when a run is already queued or running** enabled. The `UploadToSftp` step requires the `OrchardCore.DataPipelines.Sftp` module.

## Several data sets in one Excel workbook

Two sources connected to the same **Create a file** step write two worksheets, in connection order. `SheetName` holds one worksheet name per line (`\n`); an empty line keeps the title of the source step.

```json
{
  "steps": [
    {
      "name": "DataPipelines",
      "Pipelines": [
        {
          "PipelineId": "siteworkbook0000000000001",
          "Name": "Site workbook",
          "IsEnabled": true,
          "Definition": {
            "Steps": [
              {
                "StepId": "users",
                "Type": "DataSource",
                "Title": "Users",
                "X": 180,
                "Y": 220,
                "Properties": {
                  "DataSourceStepSettings": { "Source": "Users", "DataSet": "Users", "Fields": [ "UserName", "Email", "Roles" ] }
                }
              },
              {
                "StepId": "posts",
                "Type": "DataSource",
                "Title": "Blog posts",
                "X": 180,
                "Y": 420,
                "Properties": {
                  "DataSourceStepSettings": { "Source": "Contents", "DataSet": "BlogPost", "Fields": [ "DisplayText", "Owner", "PublishedUtc" ] }
                }
              },
              {
                "StepId": "workbook",
                "Type": "CreateFile",
                "X": 480,
                "Y": 320,
                "Properties": {
                  "CreateFileStepSettings": {
                    "Format": "xlsx",
                    "FileName": "site-{Date}",
                    "UseDisplayNames": true,
                    "SheetName": "Accounts\nPosts"
                  }
                }
              },
              {
                "StepId": "media",
                "Type": "SaveToMedia",
                "X": 780,
                "Y": 320,
                "Properties": {
                  "SaveToMediaStepSettings": { "Folder": "reports", "Overwrite": true }
                }
              }
            ],
            "Connections": [
              { "SourceStepId": "users", "SourcePort": "Output", "TargetStepId": "workbook", "TargetPort": "Input" },
              { "SourceStepId": "posts", "SourcePort": "Output", "TargetStepId": "workbook", "TargetPort": "Input" },
              { "SourceStepId": "workbook", "SourcePort": "Output", "TargetStepId": "media", "TargetPort": "Input" }
            ]
          }
        }
      ]
    }
  ]
}
```

`.xlsx` is accepted by the media library by default; `.csv`, `.json`, `.jsonl` and `.zip` must be added to `OrchardCore:Media:AllowedFileExtensions` first.

## Troubleshooting

| Symptom | Cause and fix |
|---------|---------------|
| Run fails with "The media library doesn't accept files with the extension" | Add the extension to `OrchardCore:Media:AllowedFileExtensions` (the list replaces the defaults), or write `.xlsx`. |
| Runs fail after the publisher left | The publishing user was deleted or disabled. Publish the pipeline again with an active user. |
| A data set is "missing" in a run but visible in the designer | Runs read as the publisher, previews as the designer. Give the publisher the permission, or publish as a user who has it. |
| A step reports a secret can't be read | The database was copied from another site or keys changed. Enter the secret again. |
| Incremental runs miss rows | The incremental field didn't change with the row, or a long transaction wrote a smaller value. Read a change queue instead (see `orchardcore-data-pipeline-steps`). |
| Runs pile up in the queue | Enable **Skip when a run is already queued or running** on the Run Data Pipeline Task. |
| Workbook run fails past about a million rows | A worksheet holds at most 1,048,576 rows. Use CSV or split into several worksheets. |

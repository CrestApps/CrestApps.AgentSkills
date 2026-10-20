# Orchard Core 4.0 Configuration Section Names

Orchard Core 4.0 no longer prefixes configuration sections with `OrchardCore_`. Sections are grouped by service, with a nested section per provider, and all of them stay nested under the root `OrchardCore` section (for example `OrchardCore:Media:Azure`).

## Rules

- The previous names are **deprecated but still read**, in addition to the new names, so existing configurations keep working.
- When a setting is defined under both names, the value under the **new** name wins.
- An **array** defined under the new name **replaces** the array defined under the previous name (arrays are not merged).
- The previous names will be removed in a future major version, so rename them in `appsettings.json`, `App_Data/appsettings.json`, tenant `appsettings.json` files, environment variables, Azure App Settings, and Key Vault secrets.
- The older dot-notation (`OrchardCore.Media.Azure`) is still resolved as a fallback for the underscore names.
- New in 4.0, no previous name: `OrchardCore:Antivirus:ClamAV`, `OrchardCore:Liquid:PermittedSiteProperties`, `OrchardCore:TempDirectory`.

## Rename table

| Previous section name | New section name |
| --- | --- |
| `OrchardCore_Admin` | `Admin` |
| `OrchardCore_Apis_GraphQL` | `Apis:GraphQL` |
| `OrchardCore_AutoSetup` | `AutoSetup` |
| `OrchardCore_AzureAISearch` | `Search:AzureAISearch` |
| `OrchardCore_BackgroundService` | `BackgroundService` |
| `OrchardCore_ContentLocalization_CulturePickerOptions` | `ContentLocalization:CulturePickerOptions` |
| `OrchardCore_ContentTypes` | `ContentTypes` |
| `OrchardCore_Data_Sqlite` | `Data:Sqlite` |
| `OrchardCore_Data_TableOptions` | `Data:TableOptions` |
| `OrchardCore_DataProtection_Azure` | `DataProtection:Azure` |
| `OrchardCore_Documents` | `Documents` |
| `OrchardCore_DynamicCache` | `DynamicCache` |
| `OrchardCore_Elasticsearch` | `Search:Elasticsearch` |
| `OrchardCore_Email_Azure`, `OrchardCore_Email_AzureCommunicationServices` | `Email:Azure` |
| `OrchardCore_Email_Smtp`, `OrchardCore_Email` | `Email:Smtp` |
| `OrchardCore_Facebook` | `Facebook` |
| `OrchardCore_GitHub` | `Authentication:GitHub` |
| `OrchardCore_Google` | `Authentication:Google` |
| `OrchardCore_HealthChecks` | `HealthChecks` |
| `OrchardCore_KeyVault_Azure` | `KeyVault:Azure` |
| `OrchardCore_Liquid` | `Liquid` |
| `OrchardCore_Localization_CultureOptions` | `Localization:CultureOptions` |
| `OrchardCore_Markdown` | `Markdown` |
| `OrchardCore_Media` | `Media` |
| `OrchardCore_Media_AmazonS3` | `Media:AmazonS3` |
| `OrchardCore_Media_AmazonS3_ImageSharp_Cache` | `Media:AmazonS3:ImageCache` |
| `OrchardCore_Media_Azure` | `Media:Azure` |
| `OrchardCore_Media_Azure_ImageSharp_Cache` | `Media:Azure:ImageCache` |
| `OrchardCore_Media_Slugify` | `Media:Slugify` |
| `OrchardCore_Microsoft_Authentication_AzureAD` | `Authentication:AzureAD` |
| `OrchardCore_Microsoft_Authentication_MicrosoftAccount` | `Authentication:MicrosoftAccount` |
| `OrchardCore_Navigation` | `Navigation` |
| `OrchardCore_Notifications` | `Notifications` |
| `OrchardCore_ReCaptcha` | `ReCaptcha` |
| `OrchardCore_Redis` | `Redis` |
| `OrchardCore_Resources` | `Resources` |
| `OrchardCore_ReverseProxy` | `ReverseProxy` |
| `OrchardCore_Roles` | `Roles` |
| `OrchardCore_Security` | `Security` |
| `OrchardCore_Setup` | `Setup` |
| `OrchardCore_Shells_Azure` | `Shells:Azure` |
| `OrchardCore_Shells_Database` | `Shells:Database` |
| `OrchardCore_Sms_AzureCommunicationServices` | `Sms:Azure` |
| `OrchardCore_Tenants` | `Tenants` |
| `OrchardCore_Users` | `Users` |
| `OrchardCore_Workflows` | `Workflows` |
| `OrchardCore_X`, `OrchardCore_Twitter` | `X` |
| `OrchardCore_YesSql` | `YesSql` |

The Azure and Amazon S3 image cache sections are renamed from their 3.0 names (`..._ImageSharp_Cache`), which are still supported even though ImageSharp itself was removed in 4.0.

## Before and after

### `appsettings.json`

Before (3.x):

```json
{
  "OrchardCore": {
    "OrchardCore_Email_Smtp": {
      "DefaultSender": "site@example.com"
    },
    "OrchardCore_Media_Azure": {
      "ConnectionString": "...",
      "ContainerName": "media"
    },
    "OrchardCore_Media_Azure_ImageSharp_Cache": {
      "ContainerName": "imagecache"
    },
    "OrchardCore_Elasticsearch": {
      "Url": "http://localhost"
    }
  }
}
```

After (4.0):

```json
{
  "OrchardCore": {
    "Email": {
      "Smtp": {
        "DefaultSender": "site@example.com"
      }
    },
    "Media": {
      "Azure": {
        "ConnectionString": "...",
        "ContainerName": "media",
        "ImageCache": {
          "ContainerName": "imagecache"
        }
      }
    },
    "Search": {
      "Elasticsearch": {
        "Url": "http://localhost"
      }
    }
  }
}
```

### Environment variables and Azure App Settings

| Before (3.x) | After (4.0) |
| --- | --- |
| `OrchardCore__OrchardCore_Media_Azure__ConnectionString` | `OrchardCore__Media__Azure__ConnectionString` |
| `OrchardCore__OrchardCore_Email_Smtp__DefaultSender` | `OrchardCore__Email__Smtp__DefaultSender` |
| `OrchardCore__OrchardCore_Shells_Database__ConnectionString` | `OrchardCore__Shells__Database__ConnectionString` |
| `OrchardCore__Default__OrchardCore_Media__MaxFileSize` | `OrchardCore__Default__Media__MaxFileSize` |

### Azure Key Vault secret names

The Key Vault provider maps `--` to `:` and `---` to `_`, so the new names are shorter and no longer need the triple dash:

| Before (3.x) | After (4.0) |
| --- | --- |
| `OrchardCore--OrchardCore---Media---Azure--ConnectionString` | `OrchardCore--Media--Azure--ConnectionString` |
| `OrchardCore--OrchardCore---Shells---Database--ConnectionString` | `OrchardCore--Shells--Database--ConnectionString` |

### Tenant `App_Data/Sites/{tenant}/appsettings.json`

```json
{
  "Media": {
    "MaxFileSize": 52428800
  }
}
```

(previously `"OrchardCore_Media": { ... }`).

---
name: orchardcore-amazon-s3-media
description: Skill for configuring Amazon S3 media storage in Orchard Core. Covers the OrchardCore.Media.AmazonS3 and OrchardCore.Media.AmazonS3.ImageCache features, the OrchardCore Media AmazonS3 and Media AmazonS3 ImageCache configuration sections, environment variable names, bucket setup, AWS credentials and their loading order, bucket policies, tenant templating with ShellSettings, local S3 emulators with ServiceURL and ForcePathStyle, the Media Cache, resized image caching in S3, TUS resumable upload staging with S3 multipart uploads, and CDN use. Use this skill for Amazon S3 Media Storage, AwsStorageOptions, AwsMediaImageCacheOptions, BucketName, BasePath, CreateBucket, RemoveBucket, AWSS3ResizedImageCache, S3TusTempStore, upgrading from OrchardCore_Media_AmazonS3 or OrchardCore.Media.AmazonS3.ImageSharpImageCache, or related implementation, setup, extension, and troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Amazon S3 Media Storage

## Module Overview

The `OrchardCore.Media.AmazonS3` module stores media in S3 buckets instead of
`App_Data`:

- **`OrchardCore.Media.AmazonS3`** — replaces the media store with S3. Depends on (and enables) `OrchardCore.Media.Cache`.
- **`OrchardCore.Media.AmazonS3.ImageCache`** — stores resized images in S3 instead of `wwwroot/{tenant}/media-cache`.

Media is still served by the Orchard Core site: the Media Cache fetches objects
on demand, which keeps on-the-fly resizing working. Only one storage provider
is active at a time. Invalid configuration logs an error and falls back to local
file storage.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Media.AmazonS3",
        "OrchardCore.Media.AmazonS3.ImageCache"
      ],
      "disable": []
    }
  ]
}
```

## Amazon S3 Media Storage (`OrchardCore:Media:AmazonS3`)

| Property | Description | Default |
|---|---|---|
| `BucketName` | Bucket name (required). Templatable. | |
| `Region` | Region endpoint, e.g. `eu-central-1`. `Region` or `ServiceURL` is required. | |
| `ServiceURL` | Custom endpoint (emulators, S3-compatible stores). Use instead of `Region`. | |
| `ForcePathStyle` | Path-style addressing (`http://host/bucket`), required by emulators. | `false` |
| `Profile`, `ProfilesLocation` | AWS CLI profile and custom profiles file. | |
| `Credentials:AccessKey`, `Credentials:SecretKey` | Explicit keys when hosted outside AWS. | |
| `BasePath` | Sub-path inside the bucket, e.g. `/media`. Templatable. | `""` |
| `CreateBucket` | Create the bucket on startup (without ACLs). | `false` |
| `RemoveBucket` | Delete the bucket when the tenant is removed. | `false` |

Hosted in AWS (EC2, ECS, EKS), `BucketName` is enough; credentials come from the
IAM role:

```json
{
  "OrchardCore": {
    "Media": {
      "AmazonS3": {
        "BucketName": "my-orchard-media",
        "Region": "eu-central-1",
        "BasePath": "/media"
      }
    }
  }
}
```

Hosted outside AWS, prefer a profile or environment variables over keys in
`appsettings.json`:

```json
{
  "OrchardCore": {
    "Media": {
      "AmazonS3": {
        "BucketName": "my-orchard-media",
        "Region": "eu-central-1",
        "Profile": "default",
        "BasePath": "/media",
        "CreateBucket": true
      }
    }
  }
}
```

Environment variables: `OrchardCore__Media__AmazonS3__BucketName`,
`OrchardCore__Media__AmazonS3__Credentials__AccessKey`,
`OrchardCore__Media__AmazonS3__Credentials__SecretKey`.

## AWS Credentials Loading Order

`OrchardCore:Media:AmazonS3` is a subset of `AWSOptions` and resolves credentials
in this order:

1. The `Credentials` section.
2. Shared credentials file at `ProfilesLocation` (profile and location set).
3. SDK Store (Windows only) when only the profile is set.
4. Shared credentials file in the default location when only the profile is set.
5. Web Identity Federation (OIDC token file from environment variables).
6. `CredentialsProfileStoreChain` (SDK Store, then default shared file).
7. Environment variables `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`.
8. ECS task or EC2 instance credentials (IAM roles).

## Bucket Access

- Buckets created with `CreateBucket: true` have ACLs disabled.
- **Private bucket (recommended)**: media is served through Orchard Core, so only the application identity needs access.
- **Public bucket**: only when clients or a CDN read S3 directly. Add an explicit read policy and make sure Block Public Access does not cancel it.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AddPerm",
      "Effect": "Allow",
      "Principal": "*",
      "Action": "s3:GetObject",
      "Resource": "arn:aws:s3:::YOUR-BUCKET-NAME/YOUR-BASE-PATH/*"
    }
  ]
}
```

## Tenant Templating

`BucketName` and `BasePath` accept Liquid with `ShellSettings` in scope.
`{{ ShellSettings.Name }}` is lowercased; the full name must follow S3 bucket
naming rules. Only default Liquid filters and tags are available.

```json
{
  "OrchardCore": {
    "Media": {
      "AmazonS3": {
        "BucketName": "{{ ShellSettings.Name }}-media",
        "Region": "eu-central-1",
        "BasePath": "/media"
      }
    }
  }
}
```

Single bucket with a folder per tenant:

```json
{
  "OrchardCore": {
    "Media": {
      "AmazonS3": {
        "BucketName": "shared-media",
        "Region": "eu-central-1",
        "BasePath": "{{ ShellSettings.Name }}/Media"
      }
    }
  }
}
```

## Local S3 Emulator

Use `ServiceURL` (no `Region`), `ForcePathStyle: true`, and dummy credentials:

```json
{
  "OrchardCore": {
    "Media": {
      "AmazonS3": {
        "ServiceURL": "http://localhost:9444/",
        "Profile": "default",
        "Credentials": {
          "SecretKey": "dummy",
          "AccessKey": "dummy"
        },
        "BasePath": "/media",
        "CreateBucket": true,
        "RemoveBucket": true,
        "BucketName": "media",
        "ForcePathStyle": true
      }
    }
  }
}
```

```bash
# S3Mock (Adobe)
docker run -p 9444:9090 -t adobe/s3mock:latest

# LocalS3 (Robothy)
docker run -d -e MODE=IN_MEMORY -p 9444:80 luofuxiang/local-s3:latest
```

## Media Cache

- Enabled automatically with S3 storage; local remote-media files live in `wwwroot/{tenant}/ms-cache`.
- **Settings → Media → Cache** (permission `MediaPermissions.ManageAssetCache`) offers a **Purge** action.
- With a CDN, purge only after it has fetched most assets. Each PoP caches independently and CDNs evict on their own schedule, so the bucket must always stay reachable for re-fetches.

## Amazon S3 Image Cache (`OrchardCore:Media:AmazonS3:ImageCache`)

Stores resized images in S3 (`AWSS3ResizedImageCache`, options
`AwsMediaImageCacheOptions`). Depends on `OrchardCore.Media.AmazonS3`.
`BucketName` and `BasePath` can be templated like the storage section.

```json
{
  "OrchardCore": {
    "Media": {
      "AmazonS3": {
        "ImageCache": {
          "BucketName": "media-image-cache",
          "BasePath": "/cache",
          "CreateBucket": true,
          "RemoveBucket": false
        }
      }
    }
  }
}
```

- Environment variable: `OrchardCore__Media__AmazonS3__ImageCache__BucketName`.
- `BucketName`, `BasePath`, `CreateBucket`, `RemoveBucket` and `Credentials` come from the `ImageCache` section, but the SDK settings (`Region`, `ServiceURL`, `Profile`, `ForcePathStyle`) are bound from `Media:AmazonS3`, so keep the cache bucket in the same region/endpoint as the media bucket.
- Useful with ephemeral containers and slow local disks.
- Cache files are only removed with the tenant when each tenant has its own bucket.

## Resumable Uploads

When `OrchardCore.Media.Tus` is also enabled, TUS partial uploads are buffered
with S3 multipart uploads (`S3TusTempStore`) under a `_tus-uploads` prefix, so
any instance can continue an upload. See `orchardcore-media-gallery` for TUS,
SignalR and multi-instance guidance.

## Upgrading from 3.x

- Rename `OrchardCore_Media_AmazonS3` to `Media:AmazonS3` and `OrchardCore_Media_AmazonS3_ImageSharp_Cache` to `Media:AmazonS3:ImageCache`, including env vars (`OrchardCore__OrchardCore_Media_AmazonS3__BucketName` → `OrchardCore__Media__AmazonS3__BucketName`). The old names are deprecated but still read; the new name wins when both exist.
- Use the feature ID `OrchardCore.Media.AmazonS3.ImageCache` in recipes. The obsolete `OrchardCore.Media.AmazonS3.ImageSharpImageCache` ID only enables the new feature.
- `AwsImageSharpImageCacheOptions` is now `AwsMediaImageCacheOptions`, and resizing uses the built-in NetVips pipeline (no ImageSharp.Web).

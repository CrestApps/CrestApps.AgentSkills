---
name: orchardcore-amazon-s3-media
description: Skill for configuring Amazon S3 media storage in Orchard Core. Covers S3 bucket configuration, AWS credentials management and loading order, S3 security policies, multi-tenant bucket templating, local S3 emulator setup with Docker, ImageSharp cache integration, and CDN caching strategy. Use this skill when requests mention Orchard Core Amazon S3 Media Storage, Amazon S3 Media Storage Configuration, Configuration Properties, Basic Configuration (Hosted in AWS), Configuration with Explicit Credentials (Hosted Outside, Configuration with AWS Profile, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Media.AmazonS3, ShellSettings, App_Data, BucketName, appsettings.json. It also helps with Configuration with Explicit Credentials (Hosted Outside, Configuration with AWS Profile, AWS Credentials Loading Order, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Amazon S3 Media Storage - Prompt Templates

## Module Overview

The `OrchardCore.Media.AmazonS3` module enables storing media assets in Amazon S3 Buckets instead of the default `App_Data` file-based store. It provides two features:

- **OrchardCore.Media.AmazonS3** — Replaces the default media store with Amazon S3 storage.
- **OrchardCore.Media.AmazonS3.ImageSharpImageCache** — Stores ImageSharp resized image cache in Amazon S3 instead of the local file system.

Media is still served by the Orchard Core web site. The Media Cache module fetches assets on the fly from S3, enabling image resizing through ImageSharp.Web integration.

## Amazon S3 Media Storage Configuration

### Guidelines

- Enable the `OrchardCore.Media.AmazonS3` feature.
- Only one storage provider can be active at a time (File Storage, Azure Blob Storage, or Amazon S3 Storage).
- When hosting inside AWS (EC2, EKS, etc.), you only need `BucketName`; credentials are resolved via IAM roles.
- When hosting outside AWS, provide credentials via the `Credentials` section, AWS CLI profiles, or environment variables.
- Prefer AWS profiles or environment variables over embedding credentials in `appsettings.json` to avoid accidental source control exposure.
- Set `CreateBucket` to `true` to auto-create the bucket. New buckets are created without ACLs for security.

### Configuration Properties

| Property | Description | Default |
|---|---|---|
| BucketName | AWS S3 bucket name (required). | `""` |
| Region | AWS region endpoint (e.g., `eu-central-1`). | `""` |
| Profile | AWS CLI profile name (e.g., `default`). | `""` |
| ProfilesLocation | Custom location for AWS profiles file. | `""` |
| Credentials.SecretKey | AWS secret key (for hosting outside AWS). | `""` |
| Credentials.AccessKey | AWS access key (for hosting outside AWS). | `""` |
| BasePath | Subdirectory path inside the bucket. | `"/media"` |
| CreateBucket | Auto-create the bucket on startup. | `false` |

### Basic Configuration (Hosted in AWS)

```json
{
  "OrchardCore": {
    "OrchardCore_Media_AmazonS3": {
      "BucketName": "my-orchard-media",
      "BasePath": "/media"
    }
  }
}
```

### Configuration with Explicit Credentials (Hosted Outside AWS)

```json
{
  "OrchardCore": {
    "OrchardCore_Media_AmazonS3": {
      "BucketName": "my-orchard-media",
      "Region": "eu-central-1",
      "Credentials": {
        "SecretKey": "your-secret-key",
        "AccessKey": "your-access-key"
      },
      "BasePath": "/media",
      "CreateBucket": true
    }
  }
}
```

### Configuration with AWS Profile

```json
{
  "OrchardCore": {
    "OrchardCore_Media_AmazonS3": {
      "BucketName": "my-orchard-media",
      "Region": "eu-central-1",
      "Profile": "default",
      "ProfilesLocation": "",
      "BasePath": "/media"
    }
  }
}
```

## AWS Credentials Loading Order

The `OrchardCore_Media_AmazonS3` configuration follows the standard `AWSOptions` loading order:

1. `Credentials` property of `AWSOptions`.
2. Shared Credentials File (custom location) — when both profile and profile location are specified.
3. SDK Store (Windows only) — when only the profile is set.
4. Shared Credentials File (default location) — when only the profile is set.
5. AWS Web Identity Federation Credentials — when an OIDC token file exists in environment variables.
6. `CredentialsProfileStoreChain` — SDK Store (Windows) then Shared Credentials File (default).
7. Environment variables — when `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` are set.
8. ECS Task Credentials or EC2 Instance Credentials — when using IAM roles.

**Best practice**: Use profiles or environment variables instead of embedding credentials directly in configuration files.

## S3 Bucket Security Policies

### Guidelines

- Buckets created with `CreateBucket: true` are created without ACLs for security.
- If creating a bucket manually, enable ACLs and configure public access settings.
- To make media files publicly accessible, add an S3 bucket policy.
- For manually created buckets, block all public access and use a bucket policy for read access.

### Public Read Bucket Policy

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

### Manual Bucket ACL Setup

1. Open your bucket in the AWS Console.
2. Go to the **Permissions** tab.
3. Edit **Block public access** and tick "Block all public access".
4. Add the bucket policy above to grant read access to media files.

## Multi-Tenant Bucket Templating

### Guidelines

- Use Liquid templating in `BucketName` and `BasePath` for multi-tenant setups.
- The `ShellSettings` object is available in the Liquid template context.
- `{{ ShellSettings.Name }}` is automatically lowercased; ensure the full `BucketName` conforms to [S3 naming rules](https://docs.aws.amazon.com/AmazonS3/latest/userguide/bucketnamingrules.html).
- Only default Liquid filters and tags are available.

### Bucket per Tenant

```json
{
  "OrchardCore": {
    "OrchardCore_Media_AmazonS3": {
      "BucketName": "{{ ShellSettings.Name }}-media",
      "Region": "eu-central-1",
      "Credentials": {
        "SecretKey": "",
        "AccessKey": ""
      },
      "BasePath": "/media",
      "Profile": "",
      "ProfilesLocation": ""
    }
  }
}
```

### Single Bucket with Base Folder per Tenant

```json
{
  "OrchardCore": {
    "OrchardCore_Media_AmazonS3": {
      "BucketName": "shared-media",
      "Region": "eu-central-1",
      "Credentials": {
        "SecretKey": "",
        "AccessKey": ""
      },
      "BasePath": "{{ ShellSettings.Name }}/Media",
      "Profile": "",
      "ProfilesLocation": ""
    }
  }
}
```

## Local S3 Emulator Setup (Docker)

### Guidelines

- Use a local emulator for development to avoid shared online storage conflicts.
- Set `ServiceURL` instead of `Region` when using an emulator.
- Enable `ForcePathStyle: true` for all emulators (uses `http://localhost/mybucket` instead of `http://mybucket.localhost`).
- Credentials are required but not validated by emulators; use dummy values.

### Emulator Configuration

```json
{
  "OrchardCore": {
    "OrchardCore_Media_AmazonS3": {
      "ServiceURL": "http://localhost:9444/",
      "Profile": "default",
      "ProfilesLocation": "",
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
```

### Docker Commands for S3 Emulators

**S3Mock** (Adobe):

```bash
docker run -p 9444:9090 -t adobe/s3mock:latest
```

**LocalS3** (Robothy):

```bash
docker run -d -e MODE=IN_MEMORY -p 9444:80 luofuxiang/local-s3:latest
```

## Amazon S3 ImageSharp Image Cache

### Guidelines

- Enable the `OrchardCore.Media.AmazonS3.ImageSharpImageCache` feature.
- Replaces the default `PhysicalFileSystemCache` with `AWSS3StorageCache` for resized images.
- Useful for ephemeral file systems (containers, clean deployments).
- Reduces pressure on local disk IO.
- Cache files are only removed per tenant when using a separate bucket per tenant.
- Templating and emulator settings work the same as for the main S3 media storage.

### ImageSharp Cache Configuration

```json
{
  "OrchardCore": {
    "OrchardCore_Media_AmazonS3_ImageSharp_Cache": {
      "Region": "eu-central-1",
      "Profile": "default",
      "ProfilesLocation": "",
      "Credentials": {
        "SecretKey": "",
        "AccessKey": ""
      },
      "BasePath": "/cache",
      "CreateBucket": true,
      "RemoveBucket": false,
      "BucketName": "imagesharp-cache"
    }
  }
}
```

## CDN Caching Strategy

- When fronting media with a CDN, allow sufficient time for CDN PoPs to cache assets before purging the local Media Cache.
- Each CDN PoP maintains its own cache independently.
- The Media Cache will re-fetch assets from S3 on demand when a CDN PoP requests an uncached item.
- CDN providers clear caches on their own schedules; the S3 source must always remain accessible.
- The Media Cache feature is automatically enabled with Amazon S3 storage and supports purging via the admin dashboard.

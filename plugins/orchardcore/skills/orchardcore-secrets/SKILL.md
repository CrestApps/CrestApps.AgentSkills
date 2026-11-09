---
name: orchardcore-secrets
description: Skill for the Orchard Core 4.0 Secrets module. Covers the OrchardCore.Secrets and OrchardCore.Secrets.AzureKeyVault features, ISecretManager, ISecretStore and ISecretStoreOperations, the TextSecret, RsaKeySecret and X509Secret types, the Database and AzureKeyVault stores, the secret-input tag helper and GetSecretValueAsync, the SecretName properties of built-in settings, the Migrate Secrets and Secret Stores screens, the Secrets recipe and deployment steps with encrypted exports, the LegacySecretConfiguration warning, and upgrading credentials of existing tenants. Use this skill when requests mention Orchard Core secrets, storing passwords or API keys securely, moving credentials to secrets, a secret store, Azure Key Vault secrets, or a module that keeps a credential in its settings. Strong matches include OrchardCore.Secrets, OrchardCore.Secrets.Abstractions, ISecretManager, TextSecret, X509Secret, RsaKeySecret, SecretInputViewModel, SecretMigration, and ManageSecrets.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Secrets

The Secrets module (new in 4.0) keeps passwords, API keys, connection strings,
RSA keys, and certificates in one place, encrypted with ASP.NET Core Data
Protection or stored in Azure Key Vault. Built-in features can reference a
secret by name instead of keeping their own protected copy of a credential.

## Features and Packages

| Feature ID | Name | Notes |
|---|---|---|
| `OrchardCore.Secrets` | Secrets | Admin screens, the `Database` store, the three secret types, recipe and deployment steps. Category **Security**. |
| `OrchardCore.Secrets.AzureKeyVault` | Secrets - Azure Key Vault | Adds the `AzureKeyVault` store. Depends on `OrchardCore.Secrets`. |
| `OrchardCore.OpenId.Secrets` | OpenID Connect Secrets | In the `OrchardCore.OpenId` module. Loads the OpenID server signing and encryption keys from secrets. Depends on `OrchardCore.OpenId.Server` and `OrchardCore.Secrets`. |

Both Secrets modules ship in `OrchardCore.Application.Cms.Targets`. A module
that only consumes or edits credentials references `OrchardCore.Secrets.Abstractions`
and does not declare a dependency on the `OrchardCore.Secrets` feature.

```xml
<PackageReference Include="OrchardCore.Secrets.Abstractions" Version="4.*" />
```

Admin screens are under **Tools → Security**: **Secrets**, **Migrate Secrets**,
and **Secret Stores**.

## Guidelines

- Name secrets with dotted logical names such as `Smtp.Password` or
  `Payment.ApiKey`. Names can't be changed after creation; settings reference
  them by name.
- Never put a secret value into a `*SecretName` property, a recipe, or a
  deployment plan. Supply recipe values from configuration
  (`OrchardCore:Secrets:{Name}`) or use an encrypted export.
- Persist the Data Protection key ring for every instance (see
  `orchardcore-azure-data-protection`). The `Database` store can't decrypt
  secrets without the original keys.
- Expiration dates are informational. Expired secrets keep working; admins with
  `ViewSecrets` see a dashboard warning for secrets expired or expiring within 30 days.
- Store services (`ISecretManager`, `ISecretStore`, `ISecretTypeProvider`,
  `ISecretStoreOperations`) are tenant singletons. Custom stores must be
  thread-safe and must not depend on scoped services.
- Use a distributed lock provider on multi-instance sites; secret mutations
  and store moves share a tenant lock.

## Reading and Writing Secrets

```csharp
using OrchardCore.Secrets;

namespace MyModule.Services;

public sealed class PaymentGatewayClient
{
    private readonly ISecretManager _secretManager;

    public PaymentGatewayClient(ISecretManager secretManager)
    {
        _secretManager = secretManager;
    }

    public async Task<string> GetApiKeyAsync()
    {
        var secret = await _secretManager.GetSecretAsync<TextSecret>("Payment.ApiKey");

        return secret?.Text;
    }

    public Task SaveApiKeyAsync(string apiKey)
        => _secretManager.SaveSecretAsync(
            "Payment.ApiKey",
            new TextSecret { Text = apiKey },
            "Database",
            new SecretSaveOptions { Description = "Payment gateway API key" });
}
```

`ISecretManager` members: `GetSecretAsync<T>(name)` and `GetSecretAsync<T>(name, storeName)`,
`SaveSecretAsync<T>(name, secret, options)` and `SaveSecretAsync<T>(name, secret, storeName, options)`,
`RemoveSecretAsync(name)` and `RemoveSecretAsync(name, storeName)`,
`GetSecretInfosAsync()` (metadata only, as `SecretInfo`), and `GetStores()`.
`SecretSaveOptions` carries `Description` and `ExpiresUtc`.

## Secret Types and Stores

| Type | Members | Use |
|---|---|---|
| `TextSecret` | `Text` | Passwords, API keys, connection strings, client secrets. The only type that settings and the **Migrate Secrets** screen use. |
| `RsaKeySecret` | `PublicKey`, `PrivateKey` (Base64), `IncludesPrivateKey`, `KeySize` (2048) | Portable signing keys, encrypted deployment exports. Can be generated in the admin. |
| `X509Secret` | `Source` (`X509SecretSource.Store` or `Pem`), `StoreLocation`, `StoreName`, `Thumbprint`, `CertificatePem`, `PrivateKeyPem`, `FromPem()`, `GetCertificate()` | A reference to the OS certificate store, or a PEM certificate plus an unencrypted PKCS#8, RSA, or EC private key (no certificate store needed, e.g. Linux or shared hosting). |

| Store name | Feature | Notes |
|---|---|---|
| `Database` | `OrchardCore.Secrets` | Default. Values encrypted with Data Protection in the tenant database. Not separately disableable. |
| `AzureKeyVault` | `OrchardCore.Secrets.AzureKeyVault` | Uses a host-registered keyed `SecretClient`; names are namespaced by prefix, tenant, and a hash. Deletion is a soft delete, never a purge. |

Add a store with `services.AddSecretStore<TStore>()` (implement `ISecretStore`:
`Name`, `IsReadOnly`, `GetSecretAsync<T>`, `SaveSecretAsync<T>`,
`RemoveSecretAsync`, `GetSecretInfosAsync`). Add a type with
`services.AddSecretType<TProvider>()`, deriving from `SecretTypeProvider<TSecret>`,
and register the same provider on every instance that imports it. See
[references/secrets-examples.md](references/secrets-examples.md) for the Key
Vault configuration, a custom store, and a custom type.

## Keeping a Module's Credential in a Secret

The pattern that the built-in integrations use, without depending on the
Secrets feature:

1. Keep the Data Protection-protected property and add a `{Property}SecretName`
   property next to it. The secret takes precedence when set.
2. Use `SecretInputViewModel` in the editor model, build it with
   `SecretInputViewModel.Create(protectedValue, secretName)`, and apply posted
   values with `await model.ApiKey.UpdateAsync(new SecretInputUpdateContext(...))`;
   store `ProtectedValue` and `SecretName` only when the result `Succeeded`.
3. Add `@addTagHelper *, OrchardCore.Secrets.Abstractions` and render
   `<secret-input asp-for="ApiKey" secret-name="MyService.ApiKey" />`.
   Without the Secrets feature it is a password box; with it, the user picks
   **User-specified value** or **Use a secret** (with **New secret** and a reload button).
4. Read the clear value with
   `await serviceProvider.GetSecretValueAsync(secretName, protectedValue, protector, logger)`.
   It reads the secret when the feature is enabled, otherwise decrypts the
   protected value, and returns `null` (with a log entry, never the value) when neither works.
5. Optionally list the credential on **Migrate Secrets** with a
   `DisplayDriver<SecretMigration>` registered in a
   `[RequireFeatures("OrchardCore.Secrets")]` startup, calling
   `migration.MoveToSecretAsync(...)`.

Keep the Data Protection purpose (protector name) unchanged forever. A full
driver, view, runtime read, and migration driver are in
[references/secrets-examples.md](references/secrets-examples.md).

When a setting must always reference a secret of a given type, render the
`SelectSecret` view component (`selectedSecret`, `htmlId`, `htmlName`,
`secretTypes`, `required`, `cssClass`); the feature that renders it must depend
on `OrchardCore.Secrets`.

## Built-in Credentials That Can Reference Secrets

| Integration | Settings class | Credential | Secret reference | Suggested secret name |
|---|---|---|---|---|
| SMTP email | `SmtpSettings` | `Password` | `PasswordSecretName` | `Smtp.Password` |
| Azure email | `AzureEmailSettings` | `ConnectionString` | `ConnectionStringSecretName` | `AzureEmail.ConnectionString` |
| Twilio SMS | `TwilioSettings` | `AuthToken` | `AuthTokenSecretName` | `Twilio.AuthToken` |
| Azure SMS | `AzureSmsSettings` | `ConnectionString` | `ConnectionStringSecretName` | `AzureSms.ConnectionString` |
| Facebook | `FacebookSettings` | `AppSecret` | `AppSecretSecretName` | `Facebook.AppSecret` |
| GitHub authentication | `GitHubAuthenticationSettings` | `ClientSecret` | `ClientSecretSecretName` | `GitHub.ClientSecret` |
| Google authentication | `GoogleAuthenticationSettings` | `ClientSecret` | `ClientSecretSecretName` | `Google.ClientSecret` |
| Microsoft Account | `MicrosoftAccountSettings` | `AppSecret` | `AppSecretSecretName` | `MicrosoftAccount.AppSecret` |
| X (Twitter) | `TwitterSettings` | `ConsumerSecret`, `AccessTokenSecret` | `ConsumerSecretSecretName`, `AccessTokenSecretSecretName` | `Twitter.ConsumerSecret`, `Twitter.AccessTokenSecret` |
| OpenID Connect client | `OpenIdClientSettings` | `ClientSecret` | `ClientSecretSecretName` | `OpenIdClient.ClientSecret` |
| Azure AI Search | `AzureAISearchDefaultSettings` | `ApiKey` | `ApiKeySecretName` | `AzureAISearch.ApiKey` |
| OpenID server keys | `OpenIdSecretSettings` | signing and encryption keys | `SigningKeySecretName`, `EncryptionKeySecretName` | `OpenId.SigningCertificate`, `OpenId.EncryptionCertificate` |

Only `TextSecret` values are selectable for these credentials, except the
OpenID server keys, which accept an `RsaKeySecret` (tried first) or an `X509Secret`.

## Recipes

The `Secrets` step creates `TextSecret` entries. With an array, each entry
takes `Name`, an optional `Store`, and an optional `Value`; without `Value`,
the value is read from configuration key `OrchardCore:Secrets:{Name}`
(environment variable `OrchardCore__Secrets__Smtp.Password`; the dot in the
name is kept). Entries without a value are skipped with a warning.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [ "OrchardCore.Secrets", "OrchardCore.Email.Smtp" ]
    },
    {
      "name": "Secrets",
      "Secrets": [
        { "Name": "Smtp.Password", "Store": "Database" }
      ]
    },
    {
      "name": "Settings",
      "SmtpSettings": {
        "IsEnabled": true,
        "DefaultSender": "noreply@example.com",
        "Host": "smtp.example.com",
        "Port": 587,
        "EncryptionMethod": "StartTls",
        "RequireCredentials": true,
        "UserName": "smtp-user",
        "PasswordSecretName": "Smtp.Password",
        "DeliveryMethod": "Network"
      }
    }
  ]
}
```

The `Settings` step replaces the whole settings object, so list every property
you need. Don't put a value into `Value` in a committed recipe.

## Deployment

The **Secrets** deployment step exports the metadata of every secret
(`Store`, `Type`, `Description`, `ExpiresUtc`) under an object keyed by name,
without values; on import, values come from `OrchardCore:Secrets:{Name}`.
Selecting an **Encryption Key** (`EncryptionKeyName`, an `RsaKeySecret` or
RSA `X509Secret`) exports values encrypted with RSA-OAEP-SHA256 plus AES-GCM,
authenticated with their metadata. The source holds only the destination's
public key under the same logical name; the destination holds the private key
before import. The key secret itself is never exported. See
[references/secrets-examples.md](references/secrets-examples.md) for both
formats and the key setup.

## Moving Credentials and Stores

- **Migrate Secrets** lists credentials kept in the settings of enabled
  features that don't reference a secret yet. **Move to Secrets** decrypts each
  with its original Data Protection purpose, saves a `TextSecret` in the chosen
  store, sets the `*SecretName` reference, and clears the old property only
  after the secret is persisted. Existing secrets are never overwritten;
  failures are reported and leave settings unchanged. Users must manage both
  the secrets and that feature's settings.
- **Secret Stores** (or **Move** beside a secret) moves secrets between stores
  on the server. Names, types, descriptions, and expirations are kept, so
  references don't change. Each move rejects a destination collision, commits
  and verifies the destination, then deletes the source. Bulk moves are not
  transactional; review each result. X.509 moves copy only store references
  or PEM material held in the secret, never OS certificates.
- In code, use `ISecretStoreOperations.MoveSecretAsync(name, source, destination)`
  and `MoveAllSecretsAsync(source, destination)`; results are
  `SecretStoreOperationResult` (`Succeeded`, `DestinationSaved`, `Failure`).
- Before disabling an optional store, make its active-secret count zero on
  every tenant, restart, and verify consumers. Moving doesn't change the
  default writable store, and deleting is not forensic erasure (Key Vault soft
  delete, database backups).

## Credentials Supplied Through Configuration

Configuration credentials (`appsettings.json`, environment variables) keep
working in 4.0 but log a warning with event `8100`, name
`LegacySecretConfiguration`, category `OrchardCore.Secrets.Configuration`. It
names the tenant, integration, and key, never the value, and also fires when
a configuration credential remains next to a selected secret. Keys checked:
`Authentication:GitHub:ClientSecret`, `Facebook:AppSecret`,
`Authentication:Google:ClientSecret`, `Authentication:MicrosoftAccount:AppSecret`,
`X:ConsumerSecret`, `X:AccessTokenSecret`, `Email:Smtp:Password`,
`Email:Azure:ConnectionString`, `Sms:Azure:ConnectionString`, and
`Search:AzureAISearch:Credential:Key` (all under `OrchardCore`, plus their
legacy section aliases).

To retire them: create the secret, select it in the integration's settings
screen (the configuration-only default SMTP, Azure email, and Azure SMS
providers have no secret reference, so switch to their tenant provider),
verify the service, remove the configuration
value from every source, restart, activate each tenant, exercise each
integration, and search the logs for `LegacySecretConfiguration` again.
Options initialize lazily, so also audit configuration sources directly.
Custom integrations can emit the same warning with
`section.WarnIfLegacySecretConfigured(logger, tenantName, integration, keys)`.

## Permissions

| Permission | Grants |
|---|---|
| `SecretsPermissions.ManageSecrets` | Create, edit, move, and delete secrets. Administrator stereotype. |
| `SecretsPermissions.ViewSecrets` | List secret metadata, never values. Implied by `ManageSecrets`. |

Secret values are never sent back to the browser; leave the value empty when
editing to keep it.

## Upgrading from 3.x

- **Back up first**: the tenant database, application data, the Data
  Protection key ring, the application identity, and certificate stores.
  A database backup alone can't decrypt credentials.
- Saved credentials keep working; nothing is migrated automatically. Moving
  them is optional, from **Migrate Secrets** or each settings screen.
- Enabling `OrchardCore.OpenId.Secrets` creates `X509Secret` references named
  `OpenId.SigningCertificate` and `OpenId.EncryptionCertificate` for configured
  server certificates; it doesn't copy certificates or private keys, and
  auto-generated development certificates are not converted.
- Code reading a settings credential directly must also honor its
  `*SecretName` (use `GetSecretValueAsync()` or the integration's runtime
  options); a moved credential's protected property is empty.
- Rolling back binaries does not restore cleared credential fields. Restore the
  pre-upgrade database and key-ring backups when going back to 3.x.
- A decryption failure on **Migrate Secrets** means the original key ring is
  missing. Restore it or re-enter the value; never delete the key ring or clear
  fields manually.

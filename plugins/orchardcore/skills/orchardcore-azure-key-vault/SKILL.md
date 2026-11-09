---
name: orchardcore-azure-key-vault
description: Skill for loading Orchard Core app configuration and secrets from Azure Key Vault. Covers the AddOrchardCoreAzureKeyVault configuration extension for IHostBuilder, IWebHostBuilder and ConfigurationManager, DefaultAzureCredential and custom TokenCredential authentication, the OrchardCore KeyVault Azure configuration section, KeyVaultName, VaultURI and ReloadInterval, AzureKeyVaultSecretManager secret name translation, and Orchard Core 4 secret names for the renamed configuration sections such as Media Azure, DataProtection Azure, Redis and Email Smtp, and how it differs from the OrchardCore.Secrets.AzureKeyVault secret store. Use this skill when requests mention Orchard Core Azure Key Vault, secret management, connection string secrets, Managed Identity configuration, OrchardCore.Configuration.KeyVault, upgrading from the OrchardCore_KeyVault_Azure section, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Azure Key Vault - Prompt Templates

## Load Configuration From Azure Key Vault

You are an Orchard Core expert. Wire Azure Key Vault as a configuration source so secrets such as connection strings and API keys are pulled from Key Vault instead of appsettings.

### Guidelines

- Azure Key Vault support ships in the `OrchardCore.Configuration.KeyVault` library. It is a host configuration provider, not a tenant feature, so you enable it in your startup code, not through a recipe or the admin Features screen.
- Add a package reference to `OrchardCore.Configuration.KeyVault` in the web (startup) project.
- Register the provider by calling `AddOrchardCoreAzureKeyVault` on the builder. Overloads exist for `IHostBuilder`, `IWebHostBuilder`, and `ConfigurationManager`.
- Authentication defaults to `DefaultAzureCredential` (Azure Identity). In Azure, prefer a Managed Identity; locally, sign in through Visual Studio, VS Code, or the Azure CLI. You may pass a custom `TokenCredential` to override this.
- Configure the vault with the `OrchardCore:KeyVault:Azure` section (in appsettings or environment variables, since it is read before Key Vault is added): provide `KeyVaultName` or `VaultURI`, and optionally `ReloadInterval` (in seconds) to poll for changes. Leave `ReloadInterval` blank to disable reloading.
- Secret-name translation: Key Vault forbids `:` and `_`, so Orchard Core's `AzureKeyVaultSecretManager` translates `---` to `_` and `--` to `:` when mapping a secret name back to a configuration key.
- All C# classes must use the `sealed` modifier, except View Models.

### Package Reference (web project)

```xml
<ItemGroup>
  <PackageReference Include="OrchardCore.Configuration.KeyVault" Version="3.*" />
</ItemGroup>
```

### Registering Key Vault in Program.cs

Using the minimal hosting model with `ConfigurationManager`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddOrchardCoreAzureKeyVault();

builder.Services
    .AddOrchardCms();

var app = builder.Build();

app.UseOrchardCore();

app.Run();
```

Using the generic host builder:

```csharp
Host.CreateDefaultBuilder(args)
    .AddOrchardCoreAzureKeyVault()
    .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
```

Passing a custom credential (for example a specific user-assigned managed identity):

```csharp
var credential = new ManagedIdentityCredential("<client-id>");

builder.Configuration.AddOrchardCoreAzureKeyVault(credential);
```

### Configuration Section

```json
{
  "OrchardCore": {
    "KeyVault": {
      "Azure": {
        "KeyVaultName": "my-vault",
        "VaultURI": "",
        "ReloadInterval": "60"
      }
    }
  }
}
```

Environment variable form: `OrchardCore__KeyVault__Azure__KeyVaultName=my-vault`.

- Provide either `KeyVaultName` (short name) or `VaultURI` (full vault host URI). `VaultURI` takes precedence when set. A missing or invalid value throws at startup with a message naming the `OrchardCore:KeyVault:Azure:*` key.
- `ReloadInterval` is a number of seconds; when present the provider re-polls the vault on that interval.

### Secret Name Translation

Key Vault secret names cannot contain `:` or `_`. Orchard Core maps them back to configuration keys:

| Key Vault secret name | Resolved configuration key |
|---|---|
| `OrchardCore--Shells--Database--ConnectionString` | `OrchardCore:Shells:Database:ConnectionString` |
| `OrchardCore--Media--Azure--ConnectionString` | `OrchardCore:Media:Azure:ConnectionString` |
| `OrchardCore--DataProtection--Azure--ConnectionString` | `OrchardCore:DataProtection:Azure:ConnectionString` |
| `OrchardCore--Redis--Configuration` | `OrchardCore:Redis:Configuration` |
| `OrchardCore--Email--Smtp--Password` | `OrchardCore:Email:Smtp:Password` |
| `OrchardCore--SignalR--Azure--ConnectionString` | `OrchardCore:SignalR:Azure:ConnectionString` |
| `ConnectionStrings--Default` | `ConnectionStrings:Default` |

Orchard Core 4.0 configuration sections are grouped by service without the
`OrchardCore_` prefix, so most secret names no longer need the triple dash. Use
`---` only for keys that really contain `_`.

Rules applied by `AzureKeyVaultSecretManager`:

- `--` (double dash) becomes `:` (section separator).
- `---` (triple dash) becomes `_` (underscore inside a key segment).

### Common Uses

- Store the shells/database connection string, SMTP credentials, OpenID signing secrets, and external provider API keys in Key Vault, then reference them through normal configuration keys elsewhere in the app.
- Combine with `OrchardCore.DataProtection.Azure` (data-protection key storage) for a fully externalized secret and key management setup.

### Configuration Provider vs. the Secrets Module Store

Orchard Core 4.0 also ships `OrchardCore.Secrets.AzureKeyVault`, a tenant
feature that makes Key Vault a store of the Secrets module (store name
`AzureKeyVault`, configured under `OrchardCore:Secrets:AzureKeyVault` with a
host-registered keyed `SecretClient`). The two are independent:

| | `OrchardCore.Configuration.KeyVault` | `OrchardCore.Secrets.AzureKeyVault` |
|---|---|---|
| Kind | Host configuration provider | Tenant feature (store of `OrchardCore.Secrets`) |
| Consumers | Any configuration key | Settings that reference a secret by `*SecretName`, and `ISecretManager` |
| Entry names | `--`/`---` encoded configuration keys | Prefix, tenant, and name with a hash, plus ownership tags |
| Managed in | Azure only | Orchard admin (**Tools → Security → Secrets**) |

Credentials supplied through configuration (including this provider), such as
`OrchardCore--Email--Smtp--Password`, still work in 4.0 but log a
`LegacySecretConfiguration` warning (event `8100`) for the integrations that
support secrets. To retire them, create the secret in the Secrets module (in the
`AzureKeyVault` or `Database` store), select it in the integration's settings,
then delete the configuration entry. See [orchardcore-secrets](../orchardcore-secrets/SKILL.md).

### Notes

- Because Key Vault is a host-level configuration provider, its values are available to every tenant through the shared host configuration.
- If a secret does not resolve, verify the double/triple-dash encoding of the secret name and that the authenticating identity has `get`/`list` secret permissions on the vault.

### Upgrading from 3.x

- Rename the `OrchardCore:OrchardCore_KeyVault_Azure` section to `OrchardCore:KeyVault:Azure` (env var `OrchardCore__KeyVault__Azure__KeyVaultName`). The old name is deprecated but still read.
- Rename secrets that target renamed sections, for example `OrchardCore--OrchardCore---Media---Azure--ConnectionString` to `OrchardCore--Media--Azure--ConnectionString` and `OrchardCore--OrchardCore---Redis--Configuration` to `OrchardCore--Redis--Configuration`. Old section names still bind, and the new name wins when both are present.
- Configuration credentials of integrations that support the Secrets module (SMTP, Azure email and SMS, Azure AI Search, Facebook, GitHub, Google, Microsoft Account, X) now log `LegacySecretConfiguration`; plan to move them to secrets.

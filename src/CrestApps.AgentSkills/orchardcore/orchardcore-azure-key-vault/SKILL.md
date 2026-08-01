---
name: orchardcore-azure-key-vault
description: Skill for loading Orchard Core app configuration and secrets from Azure Key Vault. Covers the AddOrchardCoreAzureKeyVault configuration extension, DefaultAzureCredential authentication, secret name translation rules, and reload intervals. Use this skill when requests mention Orchard Core Azure Key Vault, secret management, connection string secrets, DefaultAzureCredential, Managed Identity configuration, KeyVaultName or VaultURI settings, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Configuration.KeyVault, AddOrchardCoreAzureKeyVault, AzureKeyVaultSecretManager, AzureKeyVaultConfigurationOptions, TokenCredential, and the OrchardCore_KeyVault_Azure configuration section. It also helps with host startup wiring, secret naming conventions, and the configuration patterns captured in this skill.
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
- Configure the vault with the `OrchardCore_KeyVault_Azure` section: provide `KeyVaultName` or `VaultURI`, and optionally `ReloadInterval` (in seconds) to poll for changes. Leave `ReloadInterval` blank to disable reloading.
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
    "OrchardCore_KeyVault_Azure": {
      "KeyVaultName": "my-vault",
      "VaultURI": "",
      "ReloadInterval": "60"
    }
  }
}
```

- Provide either `KeyVaultName` (short name) or `VaultURI` (full vault host URI). `VaultURI` takes precedence when set.
- `ReloadInterval` is a number of seconds; when present the provider re-polls the vault on that interval.

### Secret Name Translation

Key Vault secret names cannot contain `:` or `_`. Orchard Core maps them back to configuration keys:

| Key Vault secret name | Resolved configuration key |
|---|---|
| `OrchardCore--OrchardCore---Shells---Database--ConnectionString` | `OrchardCore:OrchardCore_Shells_Database:ConnectionString` |

Rules applied by `AzureKeyVaultSecretManager`:

- `--` (double dash) becomes `:` (section separator).
- `---` (triple dash) becomes `_` (underscore inside a key segment).

### Common Uses

- Store the shells/database connection string, SMTP credentials, OpenID signing secrets, and external provider API keys in Key Vault, then reference them through normal configuration keys elsewhere in the app.
- Combine with `OrchardCore.DataProtection.Azure` (data-protection key storage) for a fully externalized secret and key management setup.

### Notes

- Because Key Vault is a host-level configuration provider, its values are available to every tenant through the shared host configuration.
- If a secret does not resolve, verify the double/triple-dash encoding of the secret name and that the authenticating identity has `get`/`list` secret permissions on the vault.

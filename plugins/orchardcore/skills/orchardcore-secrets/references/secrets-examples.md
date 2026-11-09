# Orchard Core Secrets Examples

## Example 1: A Settings Credential That Can Live in a Secret

A module keeps an API key in its site settings. Without the Secrets feature
the key is protected with Data Protection in the settings; with it, the user
can select a `TextSecret` instead. The module references only
`OrchardCore.Secrets.Abstractions`.

```xml
<PackageReference Include="OrchardCore.Secrets.Abstractions" Version="4.*" />
```

### Settings, constants, and view model

```csharp
namespace MyModule;

public sealed class WeatherSettings
{
    public string Endpoint { get; set; }

    // Protected with Data Protection.
    public string ApiKey { get; set; }

    // The name of a TextSecret. Takes precedence over ApiKey when set.
    public string ApiKeySecretName { get; set; }
}

public static class WeatherConstants
{
    // Never change the purpose, or stored values can't be decrypted or migrated.
    public const string ProtectorName = "MyModule.Weather";

    public const string ApiKeySecretName = "Weather.ApiKey";

    public const string GroupId = "weather";
}
```

```csharp
using OrchardCore.Secrets;

namespace MyModule.ViewModels;

public class WeatherSettingsViewModel
{
    public string Endpoint { get; set; }

    public SecretInputViewModel ApiKey { get; set; } = new();
}
```

### Display driver

```csharp
using Microsoft.AspNetCore.DataProtection;
using MyModule.ViewModels;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Secrets;
using OrchardCore.Settings;

namespace MyModule.Drivers;

public sealed class WeatherSettingsDisplayDriver : SiteDisplayDriver<WeatherSettings>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IServiceProvider _serviceProvider;

    public WeatherSettingsDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IServiceProvider serviceProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _serviceProvider = serviceProvider;
    }

    protected override string SettingsGroupId => WeatherConstants.GroupId;

    public override IDisplayResult Edit(ISite site, WeatherSettings settings, BuildEditorContext context)
        => Initialize<WeatherSettingsViewModel>("WeatherSettings_Edit", model =>
        {
            model.Endpoint = settings.Endpoint;
            model.ApiKey = SecretInputViewModel.Create(settings.ApiKey, settings.ApiKeySecretName);
        }).Location("Content:5")
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, WeatherSettings settings, UpdateEditorContext context)
    {
        var model = new WeatherSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var apiKey = await model.ApiKey.UpdateAsync(new SecretInputUpdateContext(
            _serviceProvider,
            _dataProtectionProvider.CreateProtector(WeatherConstants.ProtectorName),
            context.Updater.ModelState,
            $"{Prefix}.{nameof(model.ApiKey)}")
        {
            ProtectedValue = settings.ApiKey,
            SecretName = settings.ApiKeySecretName,
        });

        if (apiKey.Succeeded)
        {
            settings.ApiKey = apiKey.ProtectedValue;
            settings.ApiKeySecretName = apiKey.SecretName;
        }

        settings.Endpoint = model.Endpoint;

        return await EditAsync(site, settings, context);
    }
}
```

The last `SecretInputUpdateContext` argument is the full name of the edited
property, so validation errors show next to it. Also authorize the settings
permission in `EditAsync` and `UpdateAsync` (see `orchardcore-site-settings`).

### View

`Views/_ViewImports.cshtml`:

```cshtml
@addTagHelper *, OrchardCore.Secrets.Abstractions
```

`Views/WeatherSettings.Edit.cshtml`:

```cshtml
@model MyModule.ViewModels.WeatherSettingsViewModel

<div class="ocat-wrapper" asp-validation-class-for="ApiKey">
    <label asp-for="ApiKey" class="ocat-label">@T["API key"]</label>
    <div class="ocat-end">
        <secret-input asp-for="ApiKey" secret-name="@MyModule.WeatherConstants.ApiKeySecretName" />
        <span class="hint">@T["The API key of the weather service."]</span>
    </div>
</div>
```

`secret-name` is the name suggested when the user creates a secret;
`placeholder` is optional. The first control gets the property id, so
`<label asp-for>` targets it.

### Reading the value at runtime

```csharp
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Secrets;
using OrchardCore.Settings;

namespace MyModule.Services;

public sealed class WeatherClient
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public WeatherClient(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IServiceProvider serviceProvider,
        ILogger<WeatherClient> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<string> GetApiKeyAsync()
    {
        var settings = await _siteService.GetSettingsAsync<WeatherSettings>();

        return await _serviceProvider.GetSecretValueAsync(
            settings.ApiKeySecretName,
            settings.ApiKey,
            _dataProtectionProvider.CreateProtector(WeatherConstants.ProtectorName),
            _logger);
    }
}
```

When the value feeds cached options (an `IConfigureOptions<T>`), release the
tenant or signal the options change when the settings are saved.

### Listing the credential on Migrate Secrets

```csharp
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Secrets;
using OrchardCore.Settings;

namespace MyModule.Drivers;

public sealed class WeatherSecretMigrationDisplayDriver : DisplayDriver<SecretMigration>
{
    private readonly ISiteService _siteService;
    private readonly ISecretManager _secretManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public WeatherSecretMigrationDisplayDriver(
        ISiteService siteService,
        ISecretManager secretManager,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<WeatherSecretMigrationDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _secretManager = secretManager;
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    // Several drivers edit the same SecretMigration, so each binds its own fields.
    protected override void BuildPrefix(SecretMigration model, string htmlFieldPrefix)
    {
        base.BuildPrefix(model, htmlFieldPrefix);
        Prefix += ".Weather";
    }

    public override async Task<IDisplayResult> EditAsync(SecretMigration migration, BuildEditorContext context)
    {
        var settings = await _siteService.GetSettingsAsync<WeatherSettings>();

        if (!CanMigrate(settings))
        {
            return null;
        }

        return Initialize<SecretMigrationItemViewModel>("SecretMigrationItem_Edit", model =>
        {
            model.Group = S["Weather"];
            model.DisplayName = S["API key"];
            model.SecretName = WeatherConstants.ApiKeySecretName;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(SecretMigration migration, UpdateEditorContext context)
    {
        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<WeatherSettings>();

        if (!CanMigrate(settings))
        {
            return null;
        }

        var model = new SecretMigrationItemViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Migrate)
        {
            await migration.MoveToSecretAsync(
                _secretManager,
                _dataProtectionProvider.CreateProtector(WeatherConstants.ProtectorName),
                settings.ApiKey,
                model.SecretName,
                S["Weather: API key"],
                async secretName =>
                {
                    settings.ApiKeySecretName = secretName;
                    settings.ApiKey = null;

                    site.Put(settings);
                    await _siteService.UpdateSiteSettingsAsync(site);
                });
        }

        return await EditAsync(migration, context);
    }

    private static bool CanMigrate(WeatherSettings settings)
        => !string.IsNullOrWhiteSpace(settings.ApiKey) && string.IsNullOrWhiteSpace(settings.ApiKeySecretName);
}

[RequireFeatures("OrchardCore.Secrets")]
public sealed class SecretsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDisplayDriver<SecretMigration, WeatherSecretMigrationDisplayDriver>();
}
```

`MoveToSecretAsync()` saves the secret immediately but runs the callback only
after every driver has saved its secrets (`SecretMigration.UpdateSettingsAsync()`),
so don't change the settings anywhere else in the driver. It never overwrites
an existing secret and records failures (`MissingName`, `AlreadyExists`,
`DecryptionFailed`, ...) in `SecretMigration.Results`. Check the permission of
your settings before listing or moving the credential.

The same pattern works for a credential in a content part or custom document:
keep the protected value and the secret name side by side, edit them with a
`SecretInputViewModel`, and read them with `GetSecretValueAsync()`. Give each
item its own suggested secret name, such as `Webhooks.{ContentItemId}.SigningKey`.

## Example 2: Setup Recipe With Secrets From the Environment

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Secrets",
        "OrchardCore.Secrets.AzureKeyVault",
        "OrchardCore.Sms",
        "OrchardCore.Sms.Azure"
      ]
    },
    {
      "name": "Secrets",
      "Secrets": [
        { "Name": "AzureSms.ConnectionString", "Store": "AzureKeyVault" },
        { "Name": "Payment.ApiKey" }
      ]
    }
  ]
}
```

```bash
export 'OrchardCore__Secrets__AzureSms.ConnectionString=endpoint=https://...;accesskey=...'
export 'OrchardCore__Secrets__Payment.ApiKey=sk_live_...'
```

Names match exactly; the dot stays in the variable name. An entry without
`Store` goes to the default writable store. Then reference the secret from the
feature's settings screen or a `Settings` step.

## Example 3: Deployment Exports

Metadata-only export (values come from `OrchardCore:Secrets:{Name}` on import):

```json
{
  "steps": [
    {
      "name": "Secrets",
      "Secrets": {
        "Smtp.Password": {
          "SecretInfo": {
            "Store": "Database",
            "Type": "TextSecret",
            "Description": null,
            "ExpiresUtc": null
          }
        }
      }
    }
  ]
}
```

Encrypted export, produced when the deployment step selects an encryption key:

```json
{
  "steps": [
    {
      "name": "Secrets",
      "EncryptionKeyName": "Deployment.EncryptionKey",
      "Secrets": {
        "Smtp.Password": {
          "SecretInfo": {
            "Store": "Database",
            "Type": "TextSecret",
            "Description": null,
            "ExpiresUtc": null
          },
          "Version": 1,
          "EncryptedKey": "BASE64_RSA_ENCRYPTED_AES_KEY",
          "EncryptedData": "BASE64_AES_GCM_CIPHERTEXT",
          "IV": "BASE64_GCM_NONCE",
          "Tag": "BASE64_GCM_TAG"
        }
      }
    }
  ]
}
```

Don't edit names or `SecretInfo` in an encrypted recipe: they are authenticated
data. Import fails (recipe error, no plaintext fallback) when the key is
missing, the envelope is tampered with, or the target store doesn't exist.

### Provisioning the encryption key

On the destination, generate a key pair once and keep the private key there:

```csharp
using System.Security.Cryptography;
using OrchardCore.Secrets;

using var rsa = RSA.Create(2048);

await secretManager.SaveSecretAsync("Deployment.EncryptionKey", new RsaKeySecret
{
    PublicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey()),
    PrivateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
    IncludesPrivateKey = true,
});
```

On the source, save only the destination's public key under the same name:

```csharp
await secretManager.SaveSecretAsync("Deployment.EncryptionKey", new RsaKeySecret
{
    PublicKey = destinationPublicKey,
    IncludesPrivateKey = false,
});
```

An `X509Secret` works too: the source holds the certificate only (PEM or store
reference), the destination holds it with its private key. Custom code can
encrypt one entry with `ISecretEncryptionService.EncryptAsync(secret, keyName, secretInfo)`
from `OrchardCore.Secrets.Services`.

## Example 4: Azure Key Vault Store

Register a keyed `SecretClient` in the host, then point the tenant at it:

```csharp
using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);

#pragma warning disable SCME0002 // Azure SDK configuration support is experimental.
builder.AddKeyedAzureClient<SecretClient, SecretClientSettings>("SecretClient", "AzureClients:SecretClient");
#pragma warning restore SCME0002

builder.Services.AddOrchardCms();
```

```json
{
  "AzureClients": {
    "SecretClient": {
      "VaultUri": "https://my-vault.vault.azure.net/",
      "Credential": {
        "CredentialSource": "ManagedIdentityCredential"
      }
    }
  },
  "OrchardCore": {
    "Secrets": {
      "AzureKeyVault": {
        "AzureClient": "SecretClient",
        "NamePrefix": "myapp"
      }
    }
  }
}
```

- `AzureClient` must equal the host service key; JSON alone doesn't register a
  client, and the reserved key `OrchardCore.Secrets.AzureKeyVault` is rejected.
- `NamePrefix` defaults to `oc`; use a distinct prefix per application sharing
  a vault. Changing it, or renaming a tenant, changes the namespace.
- Vault entry names look like `myapp-default-payment-apikey-<hash>` and carry
  ownership tags; entries created outside Orchard are not adopted.
- Other tenant services can reuse the selected client with
  `[FromKeyedServices(AzureKeyVaultSecretStore.SecretClientServiceKey)] SecretClient client`.
- Prefer a deterministic credential such as `ManagedIdentityCredential` or
  `WorkloadIdentityCredential`; `DefaultAzureCredential` is not a supported
  `CredentialSource`. Never enable diagnostics content logging.
- Enabling this store doesn't move existing `Database` secrets. Use
  **Secret Stores** to move them.

## Example 5: Custom Store and Custom Secret Type

```csharp
using OrchardCore.Secrets;

namespace MyModule.Secrets;

public sealed class VaultSecretStore : ISecretStore
{
    public string Name => "MyVault";

    public bool IsReadOnly => false;

    public Task<T> GetSecretAsync<T>(string name) where T : class, ISecret
        => throw new NotImplementedException();

    // Complete only after the value is persisted, not merely staged.
    public Task SaveSecretAsync<T>(string name, T secret, SecretSaveOptions options = null) where T : class, ISecret
        => throw new NotImplementedException();

    public Task RemoveSecretAsync(string name)
        => throw new NotImplementedException();

    public Task<IEnumerable<SecretInfo>> GetSecretInfosAsync()
        => throw new NotImplementedException();
}
```

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Secrets;

namespace MyModule.Secrets;

public sealed class OAuthClientSecret : ISecret
{
    public string ClientId { get; set; }

    public string ClientSecret { get; set; }
}

public sealed class OAuthClientSecretTypeProvider : SecretTypeProvider<OAuthClientSecret>
{
    private readonly IStringLocalizer S;

    public OAuthClientSecretTypeProvider(IStringLocalizer<OAuthClientSecretTypeProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override string DisplayName => S["OAuth Client"];

    public override string Description => S["Stores an OAuth client identifier and secret."];
}
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Secrets;

namespace MyModule;

[RequireFeatures("OrchardCore.Secrets")]
public sealed class SecretsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSecretStore<Secrets.VaultSecretStore>();
        services.AddSecretType<Secrets.OAuthClientSecretTypeProvider>();
        services.AddJsonDerivedTypeInfo<Secrets.OAuthClientSecret, ISecret>();
    }
}
```

Stores and type providers are tenant singletons: make them thread-safe and
don't inject scoped services. Add a `DisplayDriver<ISecret>` for the admin
editor of a custom type, and register the type provider on every instance that
imports it. Override `Serialize`/`Deserialize` for a custom wire format.

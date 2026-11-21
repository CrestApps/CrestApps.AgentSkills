# A Fulfillment Partner Provider

A complete provider for a fictional print-on-demand company, *Contoso Print*. `ContosoPrintClient` and `ContosoPrintPart` stand for your own HTTP client and the product part that maps a product (or variant) to the partner's catalog item.

## Manifest

```csharp
using CrestApps.OrchardCore.Fulfillment;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Contoso Print Fulfillment",
    Author = "Contoso",
    Version = "1.0.0"
)]

[assembly: Feature(
    Id = "Contoso.Fulfillment.Print",
    Name = "Contoso Print",
    Description = "Sends paid orders for Contoso Print products to Contoso Print.",
    Category = "Commerce",
    Dependencies =
    [
        FulfillmentConstants.Features.Fulfillment,
    ]
)]
```

## Settings

Credentials are protected site settings; the driver shows whether a key is kept, never the key.

```csharp
public sealed class ContosoPrintSettings
{
    public const string ProtectionPurpose = "Contoso.Fulfillment.Print";

    public string StoreId { get; set; }

    // Protected with ProtectionPurpose; never read it without unprotecting.
    public string ApiKey { get; set; }
}

public class ContosoPrintSettingsViewModel
{
    public string StoreId { get; set; }

    public string ApiKey { get; set; }

    public bool RemoveApiKey { get; set; }

    [BindNever]
    public bool HasApiKey { get; set; }
}

public sealed class ContosoPrintSettingsDisplayDriver : SiteDisplayDriver<ContosoPrintSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public ContosoPrintSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
    }

    // Shown on Settings > Commerce > Fulfillment partners.
    protected override string SettingsGroupId
        => FulfillmentConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, ContosoPrintSettings settings, BuildEditorContext context)
        => Initialize<ContosoPrintSettingsViewModel>("ContosoPrintSettings_Edit", model =>
        {
            model.StoreId = settings.StoreId;
            model.HasApiKey = ProtectedSettingExtensions.HasSecret(settings.ApiKey);
        }).Location("Content:5")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, FulfillmentPermissions.ManageFulfillmentPartners))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ContosoPrintSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, FulfillmentPermissions.ManageFulfillmentPartners))
        {
            return null;
        }

        var model = new ContosoPrintSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.StoreId = model.StoreId?.Trim();
        settings.ApiKey = _dataProtectionProvider.ApplySecret(ContosoPrintSettings.ProtectionPurpose, settings.ApiKey, model.ApiKey, model.RemoveApiKey);

        return Edit(site, settings, context);
    }
}
```

## The provider

```csharp
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Fulfillment;
using CrestApps.OrchardCore.Fulfillment.Models;
using CrestApps.OrchardCore.Fulfillment.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Settings;

public sealed class ContosoPrintFulfillmentProvider : IFulfillmentProvider
{
    public const string ProviderName = "ContosoPrint";

    private readonly ISiteService _siteService;
    private readonly IContentManager _contentManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ContosoPrintClient _client;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public ContosoPrintFulfillmentProvider(
        ISiteService siteService,
        IContentManager contentManager,
        IDataProtectionProvider dataProtectionProvider,
        ContosoPrintClient client,
        ILogger<ContosoPrintFulfillmentProvider> logger,
        IStringLocalizer<ContosoPrintFulfillmentProvider> stringLocalizer)
    {
        _siteService = siteService;
        _contentManager = contentManager;
        _dataProtectionProvider = dataProtectionProvider;
        _client = client;
        _logger = logger;
        S = stringLocalizer;
    }

    // Stored on every request: never change it.
    public string Name
        => ProviderName;

    public LocalizedString DisplayName
        => S["Contoso Print"];

    // Saved settings only: the settings page, the order card and the paid-order handler call this.
    public async Task<bool> IsConnectedAsync()
    {
        var settings = await _siteService.GetSettingsAsync<ContosoPrintSettings>();

        return !string.IsNullOrEmpty(settings.StoreId) && ProtectedSettingExtensions.HasSecret(settings.ApiKey);
    }

    public async Task<FulfillmentConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var (storeId, apiKey) = await GetCredentialsAsync();

        if (apiKey is null)
        {
            return FulfillmentConnectionResult.Failure(S["Enter the API key first."]);
        }

        var ok = await _client.PingAsync(storeId, apiKey, cancellationToken);

        return ok
            ? FulfillmentConnectionResult.Success(S["Connected to Contoso Print."])
            : FulfillmentConnectionResult.Failure(S["Contoso Print refused the key."]);
    }

    // Claims the lines whose published product maps to a Contoso Print item. Creates nothing at the partner.
    public async Task<IReadOnlyList<FulfillmentLineClaim>> ClaimLinesAsync(FulfillmentOrder order, CancellationToken cancellationToken = default)
    {
        var claims = new List<FulfillmentLineClaim>();

        foreach (var line in order.Lines)
        {
            if (string.IsNullOrEmpty(line.ContentItemId))
            {
                continue;
            }

            var product = await _contentManager.GetAsync(line.ContentItemId, VersionOptions.Published);

            if (product?.As<ContosoPrintPart>() is { } part && part.MapsVariant(line.VariantId))
            {
                claims.Add(new FulfillmentLineClaim(line.LineId, line.Quantity));
            }
        }

        return claims;
    }

    public async Task<FulfillmentSubmitResult> SubmitAsync(FulfillmentOrder order, CancellationToken cancellationToken = default)
    {
        var (storeId, apiKey) = await GetCredentialsAsync();

        if (apiKey is null)
        {
            // Certainly not created: the engine releases the reserved shipment.
            return FulfillmentSubmitResult.Refused(S["Contoso Print isn't connected."]);
        }

        var (firstName, lastName) = FulfillmentNames.Split(order.ShippingAddress?.Name);

        try
        {
            // The order id is the external reference, so a retry can't create the order twice.
            var response = await _client.CreateOrderAsync(storeId, apiKey, new ContosoPrintOrder
            {
                ExternalId = order.OrderId,
                FirstName = firstName,
                LastName = lastName ?? firstName,
                Address = order.ShippingAddress,
                Email = order.Email,
                Phone = order.Phone,
                Items = [.. order.Lines.Select(line => new ContosoPrintItem(line.Sku, line.VariantId, line.Quantity))],
            }, cancellationToken);

            return response.Status switch
            {
                ContosoPrintStatus.Created => FulfillmentSubmitResult.Sent(response.OrderId, response.Message),
                ContosoPrintStatus.Duplicate => FulfillmentSubmitResult.Sent(response.OrderId, response.Message),
                ContosoPrintStatus.RateLimited => FulfillmentSubmitResult.RetryLater(response.Message),
                ContosoPrintStatus.Rejected => FulfillmentSubmitResult.Refused(response.Message),
                _ => FulfillmentSubmitResult.NeedsAttention(response.Message),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Timed out: the partner may have the order. A person decides.
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Contoso Print could not be reached for order '{OrderNumber}'.", order.OrderNumber);

            // The request may have arrived: never guess.
            return FulfillmentSubmitResult.NeedsAttention(S["Contoso Print could not be reached; check whether it has the order."]);
        }
    }

    private async Task<(string StoreId, string ApiKey)> GetCredentialsAsync()
    {
        var settings = await _siteService.GetSettingsAsync<ContosoPrintSettings>();
        var apiKey = _dataProtectionProvider.UnprotectSettingOrDefault(ContosoPrintSettings.ProtectionPurpose, settings.ApiKey, _logger, nameof(ContosoPrintSettings.ApiKey));

        return (settings.StoreId, string.IsNullOrEmpty(apiKey) ? null : apiKey);
    }
}
```

## Startup

```csharp
using CrestApps.OrchardCore.Fulfillment;
using CrestApps.OrchardCore.Fulfillment.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient<ContosoPrintClient>();

        services.AddScoped<ContosoPrintFulfillmentProvider>();
        services.AddScoped<IFulfillmentProvider>(sp => sp.GetRequiredService<ContosoPrintFulfillmentProvider>());

        services.AddSiteDisplayDriver<ContosoPrintSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(FulfillmentConstants.SettingsGroupId, FulfillmentPermissions.ManageFulfillmentPartners);
    }
}
```

## Rules the engine relies on

- `IsConnectedAsync` never calls the partner.
- `ClaimLinesAsync` reads the **published** product and claims at most the quantity it is given; the engine caps larger claims and refuses conflicting claims between partners.
- `SubmitAsync` only sees this partner's lines. Return `RetryLater` (retried with a growing wait, at most 10 times) or `Refused` (shipment released) only when the partner certainly did not create the order. A timeout or exception becomes **Needs attention** with the shipment kept.
- `Sent` with the partner's order id keeps the shipment pending; the store types the carrier and tracking number on it when the parcel ships.
- Describe `ContosoPrintSettings` to CrestApps Recipes with a `SiteSettingsSchemaBase` (only the non-secret `StoreId`), so the `Settings` recipe step can set it.

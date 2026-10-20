# OrchardCore.Email Examples

## Provider-neutral HTML and Text Email

```csharp
using OrchardCore.Email;
using OrchardCore.Infrastructure;

namespace MyModule;

public sealed class ReceiptEmailService
{
    private readonly IEmailService _emailService;

    public ReceiptEmailService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public Task<Result> SendAsync(string recipient, string receiptNumber)
    {
        return _emailService.SendAsync(new MailMessage
        {
            To = recipient,
            Subject = $"Receipt {receiptNumber}",
            HtmlBody = $"<p>Your receipt <strong>{receiptNumber}</strong> is ready.</p>",
            TextBody = $"Your receipt {receiptNumber} is ready.",
        });
    }
}
```

## Sending an Attachment

```csharp
using OrchardCore.Email;
using OrchardCore.Infrastructure;

namespace MyModule;

public sealed class DocumentEmailService
{
    private readonly IEmailService _emailService;

    public DocumentEmailService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public Task<Result> SendAsync(string recipient, string documentName, Stream document)
    {
        var message = new MailMessage
        {
            To = recipient,
            Subject = $"Document {documentName}",
            TextBody = "Please find the requested document attached.",
        };

        message.Attachments.Add(new MailMessageAttachment
        {
            Filename = documentName,
            Stream = document,
        });

        return _emailService.SendAsync(message);
    }
}
```

## Tenant SMTP Provider Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Email",
        "OrchardCore.Email.Smtp"
      ]
    },
    {
      "name": "Settings",
      "SmtpSettings": {
        "IsEnabled": true,
        "DefaultSender": "noreply@mysite.example",
        "Host": "smtp.mysite.example",
        "Port": 587,
        "EncryptionMethod": "StartTls",
        "RequireCredentials": true,
        "UserName": "smtp-user",
        "DeliveryMethod": "Network"
      },
      "EmailSettings": {
        "DefaultProviderName": "SMTP"
      }
    }
  ]
}
```

Enter the SMTP password in the admin UI, which encrypts it with Data
Protection, or use the `DefaultSMTP` provider from `OrchardCore:Email:Smtp`.

## Azure Communication Services Provider Recipe

The connection string comes from `OrchardCore:Email:Azure` (for example the
`OrchardCore__Email__Azure__ConnectionString` environment variable), so the
recipe only selects the configuration-backed provider.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Email",
        "OrchardCore.Email.Azure"
      ]
    },
    {
      "name": "Settings",
      "EmailSettings": {
        "DefaultProviderName": "DefaultAzure"
      }
    }
  ]
}
```

## Delivery Event Handler

```csharp
using OrchardCore.Email;
using OrchardCore.Email.Services;

namespace MyModule;

public sealed class EmailAuditHandler : EmailServiceEventsBase
{
    public override Task SentAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override Task FailedAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
```

```csharp
services.AddScoped<IEmailServiceEvents, EmailAuditHandler>();
```

## Custom Settings-backed Provider

A provider whose settings live in site settings. `IOptionsMonitor<T>` keeps
the running tenant in sync with the admin UI without a shell release.

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Email;
using OrchardCore.Email.Services;
using OrchardCore.Infrastructure;
using OrchardCore.Settings;

namespace MyModule;

public sealed class WebhookEmailSettings
{
    public bool IsEnabled { get; set; }

    public string Endpoint { get; set; }
}

public sealed class WebhookEmailOptions
{
    public bool IsEnabled { get; set; }

    public string Endpoint { get; set; }
}

public sealed class WebhookEmailOptionsConfiguration : IConfigureOptions<WebhookEmailOptions>
{
    private readonly ISiteService _siteService;

    public WebhookEmailOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(WebhookEmailOptions options)
    {
        var settings = _siteService.GetSettings<WebhookEmailSettings>();

        options.IsEnabled = settings.IsEnabled && !string.IsNullOrEmpty(settings.Endpoint);
        options.Endpoint = settings.Endpoint;
    }
}

public sealed class WebhookEmailProvider : IEmailProvider
{
    public const string TechnicalName = "Webhook";

    private readonly IOptionsMonitor<WebhookEmailOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;

    internal readonly IStringLocalizer S;

    public WebhookEmailProvider(
        IOptionsMonitor<WebhookEmailOptions> options,
        IHttpClientFactory httpClientFactory,
        IStringLocalizer<WebhookEmailProvider> stringLocalizer)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        S = stringLocalizer;
    }

    public LocalizedString DisplayName => S["Webhook"];

    public async Task<Result> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;

        if (!options.IsEnabled)
        {
            return Result.Failed(S["The Webhook email provider is disabled."]);
        }

        var client = _httpClientFactory.CreateClient(TechnicalName);
        var response = await client.PostAsJsonAsync(
            options.Endpoint,
            new { message.To, message.Subject, message.HtmlBody, message.TextBody },
            cancellationToken);

        return response.IsSuccessStatusCode
            ? Result.Success()
            : Result.Failed(S["The webhook rejected the email."]);
    }
}

public sealed class WebhookEmailProviderOptionsConfiguration : IConfigureOptions<EmailProviderOptions>
{
    private readonly IOptionsMonitor<WebhookEmailOptions> _options;

    public WebhookEmailProviderOptionsConfiguration(IOptionsMonitor<WebhookEmailOptions> options)
    {
        _options = options;
    }

    public void Configure(EmailProviderOptions options)
    {
        options.TryAddProvider(WebhookEmailProvider.TechnicalName, new EmailProviderTypeOptions(typeof(WebhookEmailProvider))
        {
            IsEnabled = _options.CurrentValue.IsEnabled,
        });
    }
}
```

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(WebhookEmailProvider.TechnicalName);

        services
            .AddSignalOptionsChangeTokenSource<WebhookEmailOptions>()
            .AddTransient<IConfigureOptions<WebhookEmailOptions>, WebhookEmailOptionsConfiguration>()
            .AddEmailProviderOptionsConfiguration<WebhookEmailProviderOptionsConfiguration>();
    }
}
```

In the `SiteDisplayDriver<WebhookEmailSettings>` that edits the settings, inject
`IOptionsUpdateNotifier` and, when the model is valid and a value changed, call
`RequestUpdate<WebhookEmailOptions>().RequestUpdate<EmailProviderOptions>().RequestUpdate<EmailOptions>()`.

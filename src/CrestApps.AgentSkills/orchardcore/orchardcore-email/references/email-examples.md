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
        "EncryptionMethod": "STARTTLS",
        "RequireCredentials": true,
        "UserName": "smtp-user",
        "Password": "supply-from-a-secret-store",
        "DeliveryMethod": "Network"
      },
      "EmailSettings": {
        "DefaultProviderName": "SMTP"
      }
    }
  ]
}
```

## Azure Communication Services Provider Recipe

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
      "AzureEmailSettings": {
        "IsEnabled": true,
        "DefaultSender": "DoNotReply@notify.mysite.example",
        "ConnectionString": "supply-from-a-secret-store"
      },
      "EmailSettings": {
        "DefaultProviderName": "Azure"
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

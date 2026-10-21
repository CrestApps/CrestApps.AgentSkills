# Adding a Messaging Channel

A channel is its own feature that depends on
`CrestApps.OrchardCore.Omnichannel.Messaging` (category **Contact Center**). It
answers only what differs between channels; routing, ownership, SLA, templates,
broadcasts, outbox retries, AI hand-off, permissions and the UI are shared. Never
add `if (channel == "X")` to the workspace; extend `IMessagingChannel` or
`MessagingChannelCapabilities` instead. SMS (`SmsMessagingChannel` in
`CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Core`) is the reference.

## Contract

`IMessagingChannel` (namespace `CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels`):

| Member | Purpose |
|---|---|
| `Name`, `DisplayName`, `IconCssClass`, `Order` | Identity. `Name` is stored on every conversation, message and address, so it never changes. Reuse `OmnichannelConstants.Channels` when one exists. |
| `Capabilities` | `MessagingChannelCapabilities`: `SupportsSubject`, `Attachments`, `SupportsDeliveryReceipts`, `SupportsBroadcast` (default `true`), `ObservesQuietHours`, `MaxBodyLength`, `AddressesArePhoneNumbers`. |
| `NormalizeAddress`, `FormatAddress`, `IsValidAddress`, `IsValidNewAddress` | The normalized address is the conversation key, so every spelling must normalize to one string. |
| `SendAsync(MessagingOutboundMessage, cancellationToken)` | Hand one message (`ServiceAddress`, `ContactAddress`, `Subject`, `Body`, `MediaUrls`) to the provider; return `MessageDispatchResult.Success(providerMessageId)` or `Failed(...)`. |
| `IsOptedOut`, `GetContactAddresses`, `FindContactIdsAsync`, `SearchContactIdsByAddressAsync` | How a CRM contact is reached, recognized and opted out. Read addresses through `IContactMethodsAccessor`. |

`MessagingAttachmentCapabilities` lists `Formats` (from `MessagingFileFormats`,
such as `Images` or `Documents`), `MaxCount`, `MaxTotalBytes` and
`ShrinkImagesToFit`. The composer and server enforce exactly those formats.

## Skeleton

```csharp
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace MyCompany.OrchardCore.Messaging.Email;

public sealed class EmailMessagingChannel : IMessagingChannel
{
    private static readonly MessagingChannelCapabilities _capabilities = new()
    {
        SupportsSubject = true,
        SupportsDeliveryReceipts = false,
        SupportsBroadcast = true,
        ObservesQuietHours = false,
    };

    private readonly IContactMethodsAccessor _contactMethods;
    private readonly IStringLocalizer S;

    public EmailMessagingChannel(
        IContactMethodsAccessor contactMethods,
        IStringLocalizer<EmailMessagingChannel> stringLocalizer)
    {
        _contactMethods = contactMethods;
        S = stringLocalizer;
    }

    public string Name => OmnichannelConstants.Channels.Email;

    public LocalizedString DisplayName => S["Email"];

    public string IconCssClass => "fa-solid fa-envelope";

    public int Order => 10;

    public MessagingChannelCapabilities Capabilities => _capabilities;

    public string NormalizeAddress(string address)
        => string.IsNullOrWhiteSpace(address) ? null : address.Trim().ToLowerInvariant();

    public string FormatAddress(string address)
        => address;

    public bool IsValidAddress(string address)
        => !string.IsNullOrWhiteSpace(address) && address.Contains('@');

    public Task<MessageDispatchResult> SendAsync(MessagingOutboundMessage message, CancellationToken cancellationToken = default)
    {
        // Send through the provider that serves message.ServiceAddress.
        return Task.FromResult(MessageDispatchResult.Success());
    }

    public bool IsOptedOut(ContentItem contact)
        => contact is not null &&
            contact.TryGet<OmnichannelContactPart>(out var part) &&
            part.DoNotEmail;

    public IReadOnlyList<string> GetContactAddresses(ContentItem contact)
        => contact is null
            ? []
            : _contactMethods.Get(contact).EmailAddresses
                .Select(email => NormalizeAddress(email.Email))
                .Where(email => email is not null)
                .ToArray();

    public Task<IReadOnlyList<string>> FindContactIdsAsync(string address, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> SearchContactIdsByAddressAsync(
        string term,
        IReadOnlyCollection<string> contactTypes,
        int take,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);
}
```

Replace the two lookup stubs with real queries (the SMS channel uses
`IOmnichannelContactPhoneLookup`).

## Registration

```csharp
public override void ConfigureServices(IServiceCollection services)
{
    services.AddContactMethods();
    services.AddMessagingChannel<EmailMessagingChannel>();

    services.AddOmnichannelAddressCapability(OmnichannelAddressTypes.EmailAddress, OmnichannelConstants.Channels.Email, capability =>
    {
        capability.DisplayName = S["Email"];
        capability.Description = S["Mail sent and received on this address in the messaging workspace."];
    });

    services.AddEntryPointChannel(OmnichannelConstants.Channels.Email, channel =>
    {
        channel.DisplayName = S["Email"];
        channel.Description = S["Routes the conversations of its addresses to a queue or an agent."];
    });
}
```

The workspace stores and validates the channel's addresses through the channel
(`MessagingChannelEndpointAddressPolicy`) and adds the inbound routing editor.
Add a `DisplayDriver<OmnichannelChannelEndpoint>` for per-address provider
settings, with the shape root marked `data-address-capability="Email"`.

## Inbound and Receipts

1. Verify the provider signature in your webhook and commit the delivery to the
   Contact Center provider webhook inbox (keyed on the provider message id) so
   retries are de-duplicated.
2. Build an `OmnichannelMessage` with `Channel`, `ServiceAddress`,
   `CustomerAddress`, `Content`, `IsInbound = true`, `ProviderMessageId`,
   `CreatedUtc` and any `MediaReferences`, and call
   `IMessagingInboundProcessor.ProcessAsync`. The workspace finds or creates the
   conversation, routes it, starts the first-response clock, stores and
   notifies.
3. Put channel-only rules (keywords, consent windows) in an
   `IMessagingInboundHandler` (`Order`, `ReceivingAsync`, `ReceivedAsync`); set
   `MessagingInboundContext.SuppressAutomatedReplies` to silence auto-replies.
4. Report receipts with `IMessagingConversationService.ApplyDeliveryReceiptAsync`
   (`MessageDeliveryReceipt`: `Channel`, `ServiceAddress`, `ContactAddress`,
   `ProviderMessageId`, `Status`, `ErrorCode`).
5. A provider that serves media only to its own account registers an
   `IMessagingMediaRequestAuthenticator`. For SMS providers, picture sending is
   `ISmsMediaDispatchProvider` on the provider, or an `ISmsMediaSender`
   registered under the provider name.

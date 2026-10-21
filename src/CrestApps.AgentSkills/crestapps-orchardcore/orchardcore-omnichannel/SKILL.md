---
name: orchardcore-omnichannel
description: Skill for the CrestApps Orchard Core Omnichannel base module and the map of the Omnichannel module family. Covers the CrestApps.OrchardCore.Omnichannel feature (shared OmnichannelMessage store in the Omnichannel YesSql collection, IOmnichannelProcessor and IOmnichannelEventHandler contracts, OmnichannelEvent and SmsReceived, ISmsProviderRouter), the Omnichannel - Azure Communication Services feature CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices, the inbound webhook map (api/azure/webhook/eventgrid, api/twilio/webhook/sms, api/telnyx/webhook/sms), and which sibling feature to enable. Use this skill when requests mention Orchard Core Omnichannel, Azure Communication Services for Omnichannel, omnichannel processors or event handlers, or choosing Omnichannel features. Strong matches include CrestApps.OrchardCore.Omnichannel.Core, OmnichannelConstants, OmnichannelMessage, OmnichannelEvent, IOmnichannelProcessor, and IOmnichannelEventHandler.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Omnichannel (Base Module)

## Choose and Enable Omnichannel Features

You are an Orchard Core expert. Use this skill to pick the right Omnichannel
feature and to build on the shared contracts that every Omnichannel channel
uses. The base feature is plumbing: it stores messages and preferences and
defines contracts. It sends nothing, places no calls and has no admin screens.

### Guidelines

- Install the CrestApps Omnichannel packages in the web/startup project.
- The base feature `CrestApps.OrchardCore.Omnichannel` is enabled by every
  other Omnichannel feature through dependencies; you rarely enable it alone.
- Omnichannel domain data (messages, activities, batches, AI session links) is
  stored in the dedicated `Omnichannel` YesSql collection
  (`OmnichannelConstants.CollectionName`).
- The base feature defines no HTTP endpoint. Inbound traffic arrives through the
  channel or provider feature that owns the webhook.
- Message bodies and customer and service addresses are stored as plaintext in
  the tenant database. Protect them with database or disk encryption and treat
  them as personal data.
- Keep provider credentials in user secrets, environment variables or Key Vault,
  never in recipes or source.
- C# samples use `sealed` classes and file-scoped namespaces.

## Feature Map

| Need | Feature ID | Skill |
|---|---|---|
| Shared message store, preferences, contracts | `CrestApps.OrchardCore.Omnichannel` | this skill |
| Azure Communication Services email and SMS providers | `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` | this skill |
| Headless contacts, subjects, campaigns, activities | `CrestApps.OrchardCore.Omnichannel.Activities` | `orchardcore-omnichannel-management` |
| Omnichannel addresses (channel endpoints) admin | `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints` (dependency only) | `orchardcore-omnichannel-management` |
| CRM admin, loads, subject flows, cadences, reports | `CrestApps.OrchardCore.Omnichannel.Managements` | `orchardcore-omnichannel-management` |
| Leads, accounts, opportunities | `CrestApps.OrchardCore.Omnichannel.Crm` | `orchardcore-omnichannel-management` |
| AI-driven SMS conversations | `CrestApps.OrchardCore.Omnichannel.Sms` | `orchardcore-omnichannel-sms` |
| AI-driven voice calls (provider neutral) | `CrestApps.OrchardCore.Omnichannel.Voice` | `orchardcore-omnichannel-voice` |
| Human messaging inbox | `CrestApps.OrchardCore.Omnichannel.Messaging` | `orchardcore-omnichannel-messaging` |
| SMS channel of the inbox | `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` | `orchardcore-omnichannel-messaging` |
| Push routing for the inbox | `CrestApps.OrchardCore.Omnichannel.Messaging.RoutedDistribution` | `orchardcore-omnichannel-messaging` |
| Azure Event Grid inbound webhook | `CrestApps.OrchardCore.Omnichannel.EventGrid` | `orchardcore-omnichannel-event-grid` |

Related skills owned elsewhere: `orchardcore-contact-center` (queues, entry
points, agents, dialer, business hours), `orchardcore-telephony`,
`orchardcore-telephony-telnyx` (Telnyx SMS and the Telnyx AI Voice Agent),
`orchardcore-telephony-asterisk`, `orchardcore-dnc-registry`,
`orchardcore-phone-numbers`, `orchardcore-signalr`, and the framework skills
`orchardcore-sms` and `orchardcore-sms-azure`.

## Enable via Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Omnichannel",
        "CrestApps.OrchardCore.Omnichannel.Managements"
      ],
      "disable": []
    }
  ]
}
```

## Azure Communication Services

`CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` (**Omnichannel -
Azure Communication Services**) depends on `CrestApps.OrchardCore.Omnichannel`,
`OrchardCore.Email.Azure` and `OrchardCore.Sms.Azure`. It adds no settings,
connection-string model or processor of its own: it switches on Orchard Core's
ACS email and SMS providers so Omnichannel components can send through the
standard `ISmsService` and email services.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices"
      ],
      "disable": []
    }
  ]
}
```

After enabling it:

1. Configure the Azure email provider at **Settings > Communication > Email**.
2. Configure the Azure SMS provider at **Settings > Communication > SMS**.
   Enabling the feature does not change a tenant's selected default provider.
3. Enable a consumer: **SMS Omnichannel Automation** for AI SMS, or the **SMS
   Messaging Channel**, whose SMS addresses can pin the Azure provider per number.
4. For inbound ACS texts, enable **Omnichannel - Azure Event Grid** and subscribe
   it to the ACS resource's SMS events. `Microsoft.Communication.SMSReceived` is
   routed as an `SMS` inbound text; `Microsoft.Communication.SMSDeliveryReportReceived`
   is stored but not routed (the sent message's status is not updated). The ACS
   number must exist as an SMS omnichannel address so the event matches it.

## Inbound Webhook Map

| Provider path | Endpoint | Owning feature |
|---|---|---|
| Azure Event Grid (including ACS SMS) | `POST ~/api/azure/webhook/eventgrid` | `CrestApps.OrchardCore.Omnichannel.EventGrid` |
| Twilio inbound SMS | `POST ~/api/twilio/webhook/sms` | `CrestApps.OrchardCore.Omnichannel.Sms` or `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` (mapped once when both are on); refused while the Twilio provider is disabled in the SMS settings |
| Telnyx inbound SMS | `POST ~/api/telnyx/webhook/sms` | Telnyx SMS (`orchardcore-telephony-telnyx`) |

Every SMS receiver raises the same `OmnichannelConstants.Events.SmsReceived`
event on the `SMS` channel, so SMS Automation and the SMS Messaging Channel act
on Twilio, Telnyx and ACS texts identically.

## Shared Contracts

All types are in `CrestApps.OrchardCore.Omnichannel.Core` (models in
`CrestApps.OrchardCore.Omnichannel.Core.Models`, services in
`CrestApps.OrchardCore.Omnichannel.Core.Services`).

| Type | Purpose |
|---|---|
| `IOmnichannelProcessor` | Starts an automated activity on one channel (`Channel`, `StartAsync(activity, cancellationToken)`). SMS Automation registers `SMS`; Telnyx AI Voice registers `Phone`. |
| `IOmnichannelEventHandler` | Receives every inbound `OmnichannelEvent` (`HandleAsync(event, cancellationToken)`). |
| `OmnichannelEvent` | `Id`, `EventType`, `Subject`, `Message`, `Data` (`BinaryData`) and `GetDataFromJson<T>()`. |
| `OmnichannelMessage` | Stored message: `Channel`, `CustomerAddress`, `ServiceAddress`, `Content`, `IsInbound`, `ConversationId`, `ProviderMessageId`, `DeliveryStatus`, `MediaReferences`, `ErrorCode`. |
| `ISmsProviderRouter` | Resolves the SMS provider that owns a sending number (`ResolveProviderNameAsync`, `GetProviderAsync`, `SendAsync`), falling back to the tenant default. |
| `OmnichannelConstants.Channels` | `Phone`, `SMS`, `Email`. |
| `OmnichannelConstants.SmsErrorCodes` | Provider-neutral refusals such as `recipient_opted_out` and `media_not_supported`. |

### Handle an Inbound Event

Every handler is called for every event, so filter by event type, channel and
direction before acting, and keep side effects idempotent because providers
retry.

```csharp
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace MyCompany.OrchardCore.Communications;

public sealed class ProviderReceiptHandler : IOmnichannelEventHandler
{
    public Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(omnichannelEvent.EventType, "Contoso.MessageDelivered", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        // Read the provider payload with omnichannelEvent.GetDataFromJson<T>().
        return Task.CompletedTask;
    }
}
```

Register it in the owning feature's startup with
`services.AddScoped<IOmnichannelEventHandler, ProviderReceiptHandler>();`.

### Add an Automated Channel Processor

```csharp
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace MyCompany.OrchardCore.Communications;

public sealed class WhatsAppOmnichannelProcessor : IOmnichannelProcessor
{
    public string Channel => "WhatsApp";

    public Task StartAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        // Send the opening message for the automated activity.
        return Task.CompletedTask;
    }
}
```

Register with
`services.TryAddEnumerable(ServiceDescriptor.Scoped<IOmnichannelProcessor, WhatsAppOmnichannelProcessor>());`
and also register the channel in `ActivityChannelOptions` (see
`orchardcore-omnichannel-management`) so loads and filters offer it. The
management feature's `AutomatedActivitiesProcessorBackgroundTask` dispatches
due automated activities to the processor whose `Channel` matches.

## Upgrading from 2.x

- Webhooks follow `api/{provider}/webhook/{action}`: point Event Grid at
  `api/azure/webhook/eventgrid` and Twilio at `api/twilio/webhook/sms`. The old
  `Omnichannel/webhook/AzureEventGrid`, `Omnichannel/webhook/Twilio` and
  `Omnichannel/webhook/TwilioEventGrid` routes are gone.
- Contacts no longer carry `DoNotChat`; the preferences are `DoNotCall`,
  `DoNotSms` and `DoNotEmail`.
- Read contact phone numbers and emails through `IContactMethodsAccessor`
  (see `orchardcore-omnichannel-management`), not by walking the
  `ContactMethods` bag.
- The Dialpad provider is removed; use Telnyx or Asterisk.
- CrestApps SignalR is deprecated in favor of `OrchardCore.SignalR`; the
  messaging hub is mapped with `SignalRHubRoutes.GetHubPath<T>()` (see
  `orchardcore-signalr`).

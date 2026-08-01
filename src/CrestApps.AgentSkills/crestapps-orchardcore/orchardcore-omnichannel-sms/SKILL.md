---
name: orchardcore-omnichannel-sms
description: Skill for implementing AI-assisted Omnichannel SMS automation in Orchard Core using CrestApps modules. Covers outbound activity dispatch, inbound Twilio webhooks, signature validation, AI chat sessions, channel endpoints, and conclusion processing. Use this skill when requests mention Orchard Core Omnichannel SMS, Twilio webhooks, automated SMS activities, AI SMS conversations, SMS channel endpoints, or inbound message handlers. Strong matches include work with CrestApps.OrchardCore.Omnichannel.Sms, SmsOmnichannelProcessor, SmsOmnichannelEventHandler, TwilioWebhookEndpoint, TwilioEventGridEndpoint, IOmnichannelProcessor, and TwillioRequestValidator.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Omnichannel SMS Automation

## Configure AI-Assisted SMS

You are an Orchard Core expert. Configure the SMS Omnichannel feature as an
automated activity channel backed by Orchard Core SMS and the CrestApps AI Chat
services. Secure inbound Twilio traffic before it enters the Omnichannel event
pipeline.

### Guidelines

- Install `CrestApps.OrchardCore.Omnichannel.Sms` in the web/startup project.
- Enable the exact feature ID `CrestApps.OrchardCore.Omnichannel.Sms`; its
  manifest depends on the AI base feature, dependency-only AI Chat Core,
  Omnichannel Management, and `OrchardCore.Sms`.
- The SMS feature supplies `SmsOmnichannelProcessor` as an
  `IOmnichannelProcessor` for the `SMS` channel and registers
  `SmsOmnichannelEventHandler` as an `IOmnichannelEventHandler`.
- Configure a working Orchard Core SMS provider before scheduling outbound
  automated activities. This module calls `ISmsService`; it does not replace an
  SMS provider.
- Configure a channel endpoint for the SMS service address and choose that
  endpoint on an automated SMS subject flow.
- The direct Twilio webhook and the Twilio Event Grid endpoint are anonymous
  and disable antiforgery, but each validates `X-Twilio-Signature` using the
  configured Twilio auth token.
- Keep the Twilio auth token encrypted in Orchard Core SMS settings and inject
  provider credentials through secrets or environment configuration. Never put
  tokens in source code or recipes.
- Keep custom code `sealed` with file-scoped namespaces. View Models remain
  unsealed only when model binding requires them.

## Feature and Package

| Item | Value |
|---|---|
| NuGet package | `CrestApps.OrchardCore.Omnichannel.Sms` |
| SMS feature | `CrestApps.OrchardCore.Omnichannel.Sms` |
| Required AI feature | `CrestApps.OrchardCore.AI` |
| Required chat dependency | `CrestApps.OrchardCore.AI.Chat.Core` activated by the SMS feature |
| Required management feature | `CrestApps.OrchardCore.Omnichannel.Managements` |
| Required platform feature | `OrchardCore.Sms` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.Omnichannel",
        "CrestApps.OrchardCore.Omnichannel.Managements",
        "OrchardCore.Sms",
        "CrestApps.OrchardCore.Omnichannel.Sms"
      ],
      "disable": []
    }
  ]
}
```

## Configure the Flow Before Dispatch

Configure the management layer first:

1. Add `OmnichannelContactPart` to the contact content type and create contacts.
2. Create an `OmnichannelSubject` content type.
3. Create the campaign, channel endpoint, dispositions, and subject flow.
4. Set the subject flow's interaction type to `Automated` and its channel to
   `SMS`.
5. Select the SMS channel endpoint and configure the flow's AI settings.
6. Ensure the selected chat profile has a resolvable chat deployment before
   loading automated activities.
7. Create activities through the Interaction Center or an activity batch.

The management background task invokes `SmsOmnichannelProcessor` for scheduled
automated SMS activities. It does not send messages for an unconfigured
channel, an absent processor, or an activity outside the processor's channel.

## Outbound SMS Behavior

`SmsOmnichannelProcessor.StartAsync`:

1. Finds or creates an `AIChatSession` for the activity.
2. Resolves the configured chat profile and creates a session when one is not
   already linked to the activity.
3. Renders that profile's required initial-prompt pattern with `Activity`,
   `Contact`, `FlowSettings`, `Profile`, `Session`, and, when available,
   `Campaign`.
4. Sends the rendered text through `ISmsService`.
5. Uses the matching `OmnichannelChannelEndpoint` value as the SMS `From`
   address when one is selected.
6. Stores the assistant prompt, saves the session, and sets the activity to
   `AwaitingCustomerAnswer` after a successful send.

The initial pattern must render nonempty content. Treat a render failure or SMS
provider failure as an operational error rather than marking the activity as
successfully started.

## Inbound Webhook Endpoints

| Route | Payload path | Signature behavior |
|---|---|---|
| `POST /Omnichannel/webhook/Twilio` | Direct Twilio form webhook | Uses `TwillioRequestValidator` with the full URL, form values, and `X-Twilio-Signature` |
| `POST /Omnichannel/webhook/TwilioEventGrid` | Twilio form event delivered to this route | Builds the sorted form signature input and validates `X-Twilio-Signature` |

Both endpoints read `From`, `To`, `Body`, and `MessageSid`, save an inbound
`OmnichannelMessage` in the `Omnichannel` collection, create an
`OmnichannelEvent` with event type `OmnichannelConstants.Events.SmsReceived`,
then invoke all `IOmnichannelEventHandler` implementations.

Configure Twilio's public webhook URL with the actual HTTPS host and route:

```text
https://<host>/Omnichannel/webhook/Twilio
```

Do not add application authentication to this callback without preserving
Twilio signature validation. The endpoint has to be reachable by Twilio, while
the signature protects it from forged requests.

## Inbound SMS and AI Response

`SmsOmnichannelEventHandler` processes only inbound messages whose event type
is `SmsReceived` and whose channel is `SMS`.

1. It resolves the channel endpoint from the service address.
2. It finds the automated SMS activity by endpoint and customer address.
3. It sets the activity to `AwaitingAgentResponse`, then checks configured
   opt-out keywords before invoking AI.
4. It appends the customer message as an AI chat user prompt.
5. It resolves the selected chat profile and deployment, then requests a completion.
6. It sends the generated assistant text through `ISmsService`.
7. It appends the assistant prompt and changes the activity status to
   `AwaitingCustomerAnswer`.

If no channel endpoint, matching activity, campaign, AI session, or deployment
exists, the handler logs the condition and does not send a reply. Configure
these records before allowing customers to reply.

## Conversation Conclusion

After a successful AI SMS reply, the handler schedules deferred conclusion
analysis using the `sms-conclusion-analysis` template. The analysis can:

- select a disposition and mark the activity `Completed`
- execute the matching `ISubjectActionExecutor` actions
- update the subject when the campaign allows it
- update the contact when the campaign allows it

This deferred step is intentionally separate from the webhook response. Avoid
blocking Twilio delivery on long-running AI conclusion analysis.

## Implement a Supplementary Handler

Add a separate event handler only for behavior that is not covered by
`SmsOmnichannelEventHandler`, such as recording a provider delivery event.

```csharp
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace MyCompany.OrchardCore.Sms;

public sealed class DeliveryStatusHandler : IOmnichannelEventHandler
{
    public Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(omnichannelEvent.EventType, "Contoso.SmsDelivered", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        // Persist or report the provider delivery status.
        return Task.CompletedTask;
    }
}
```

Because the dispatcher calls every registered handler, always filter the event
type, channel, and direction before applying side effects.

## Security and Operations Checklist

- Configure `OrchardCore.Sms` and protect the Twilio auth token at rest.
- Use HTTPS and the external URL Twilio signs when validating webhooks.
- Test a valid signature, an invalid signature, a missing token, and a normal
  inbound reply.
- Normalize and configure the SMS channel endpoint so it matches the inbound
  service address.
- Make custom side effects idempotent because providers can retry webhooks.
- Monitor failed `ISmsService` sends and AI completion failures.
- Configure opt-out handling through Omnichannel contact preferences and
  disposition actions before automating outreach.

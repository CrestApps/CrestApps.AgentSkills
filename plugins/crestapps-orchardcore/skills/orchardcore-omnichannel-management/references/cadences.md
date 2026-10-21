# Re-engagement Cadences

Cadences are part of the **Omnichannel Management** feature
(`CrestApps.OrchardCore.Omnichannel.Managements`). A cadence is a reusable,
named series of follow-up messages that re-engages a contact who went quiet in
an **automated** conversation. Re-engagement is off by default: a load with no
cadence never follows up.

| Item | Value |
|---|---|
| Admin | **Interaction Center > Management > Cadences** |
| Permission | `ManageCadences` |
| Model | `Cadence` (`DisplayText`, `Description`, `Enabled`, `Steps`) with `CadenceStep` (`DelayMinutes`, `IsAiGenerated`, `Message`) |
| Recipe step | `OmnichannelCadence` |
| Deployment step | **Omnichannel Cadences** |
| Sender | `SmsReEngagementBackgroundTask` (**Automated SMS Re-Engagement**, `*/5 * * * *`) from `CrestApps.OrchardCore.Omnichannel.Sms` |

## How Steps Work

- **After (minutes)** (`DelayMinutes`) counts silence from the automation's
  previous message, so `60` then `1440` sends one follow-up after an hour and
  the next a day later.
- A step is either a **Defined message** (sent verbatim) or **AI-generated**
  (`IsAiGenerated: true`; `Message` is optional guidance for the AI).
- The number of steps caps the follow-ups. After the last step the
  conversation falls back to the subject's no-response handling.
- A follow-up is sent only when the cadence is enabled, steps remain, the last
  message was the automation's, the silence exceeds the step delay, and the
  load's business-hours calendar is open in the contact's time zone. Each send
  restarts the no-response window.
- Live replies to an actively messaging contact are never gated by business
  hours; only proactive follow-ups are.

## Use a Cadence

1. Create the cadence under **Management > Cadences**.
2. On an **Automatic** activity load, pick it in **Re-engagement** and,
   optionally, a **Business hours** calendar (shown when one exists; calendars
   come from Contact Center Business Hours, see `orchardcore-contact-center`).
3. The cadence is snapshotted onto each activity at load time, so later edits
   or deletion do not affect conversations in flight.

## Recipe

Import cadences before the campaigns and loads that reference them. A step that
is not AI-generated must carry a message; an invalid entry is reported and
skipped.

```json
{
  "steps": [
    {
      "name": "OmnichannelCadence",
      "Cadences": [
        {
          "ItemId": "7rq2m9x4k1c6v3b8n5t0h2y7z",
          "DisplayText": "Gentle reminder",
          "Description": "Two follow-ups for a contact who stops replying.",
          "Enabled": true,
          "Steps": [
            { "DelayMinutes": 60, "IsAiGenerated": false, "Message": "Just checking in. Do you still need help?" },
            { "DelayMinutes": 1440, "IsAiGenerated": true, "Message": "Offer to call them back at a time that suits them." }
          ]
        }
      ]
    }
  ]
}
```

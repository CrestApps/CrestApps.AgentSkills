# Meters by area

Every name below matches the `CrestApps.OrchardCore.*` wildcard. The full instrument, span and log-id contract is in the CrestApps docs page `observability.md`.

| Area | Meter / activity source | Main instruments and spans |
| --- | --- | --- |
| Platform helpers | `CrestApps.OrchardCore.Platform` | `crestapps.background_task.*`, `crestapps.deferred_task.duration`, `crestapps.retry.attempts`, `crestapps.background.pacer.*`; span `crestapps.deferred.<name>` |
| AI modules | `CrestApps.OrchardCore.AI` | `crestapps.ai.datasource.sync.duration`, `crestapps.ai.datasource.documents`, `crestapps.ai.tool.timeouts`, `crestapps.ai.eval.duration`, `crestapps.ai.alerts.raised` / `.resolved`, `crestapps.ai.webhook.delivery.duration`, `crestapps.recipes.ai_imports`. Chat turns, tools, retrieval, ingestion, memory, MCP, A2A and realtime voice are measured once by CrestApps.Core (`crestapps.core.*`) |
| Reports and Report Builder | `CrestApps.OrchardCore.Reports` | `crestapps.report.run.duration`, `crestapps.report.query.duration`, `crestapps.report.run.failures` (`crestapps.report.level` = `run` or `query`), export, view refresh and subscription instruments; tags `crestapps.report.source`, `crestapps.report.data_source`, `outcome`. Report, query and data set ids are span tags only |
| Data connectors | `CrestApps.OrchardCore.DataConnectors` | `crestapps.dataconnector.refresh.duration` / `.rows` / `.active`, `crestapps.dataconnector.read.*`, `crestapps.dataconnector.http.retries`, `crestapps.dataconnector.network.blocked`; spans `crestapps.dataconnector.refresh`, `crestapps.dataconnector.read` |
| Content transfer | `CrestApps.OrchardCore.ContentTransfer` | `crestapps.contenttransfer.import.*`, export instruments |
| Do-not-call registry | `CrestApps.OrchardCore.DncRegistry` | `crestapps.dnc.screening.duration` / `.size` / `.listed`, `crestapps.dnc.import.duration` |
| Omnichannel bulk work | `CrestApps.OrchardCore.Omnichannel` | Bulk loads and bulk actions |
| Recipes | `CrestApps.OrchardCore.Recipes` | Recipe and deployment imports |
| Inventory | `CrestApps.OrchardCore.Inventory` | Holds and stock movements (approximate) |
| Contact center | `CrestApps.OrchardCore.ContactCenter` | Call funnel, offers, queues, inbound routing, `crestapps.contactcenter.outbox.*`, provider commands, dialer, agents, deadlines, gauges; spans `crestapps.contactcenter.call.ingest`, `.offer`, `.inbound.route`, `.outbox.dispatch`, `.provider_command.dispatch`, `.dialer.attempt` |
| Webhooks and provider inbox | `CrestApps.OrchardCore.Webhooks` | `crestapps.webhook.deliveries`, `crestapps.webhook.inbox.accepted` / `.dispatches` / `.handler.duration` / `.lag` / `.dead_lettered` / `.pending.age`; span `crestapps.webhook.inbox.dispatch` |
| AI voice | `CrestApps.OrchardCore.Voice` | AI voice sessions |
| Telephony (every provider) | `CrestApps.OrchardCore.Telephony` | Telephony commands, `crestapps.telephony.voice_event.discards` |
| Telnyx | `CrestApps.OrchardCore.Telnyx` | Telnyx calls and webhooks |
| Asterisk | `CrestApps.OrchardCore.Asterisk` | `crestapps.asterisk.realtime.connections`, `.connected` (up-down), `.reconnect_attempts`, `.ingestion.saturated` |
| WebSockets | `CrestApps.OrchardCore.WebSockets` | `crestapps.websockets.rendezvous`, `crestapps.websockets.rendezvous.wait.duration` |
| Phone verification | `CrestApps.OrchardCore.PhoneNumbers` | Verification sends and checks |
| Messaging and SMS | `CrestApps.OrchardCore.Messaging` | `crestapps.messaging.*` sends, inbound, opt-outs, outbox retries, AI turns |
| Azure Event Grid | `CrestApps.OrchardCore.EventGrid` | `crestapps.eventgrid.*` |
| Commerce | `CrestApps.OrchardCore.Checkout`, `.Stripe`, `.Subscriptions`, `.Orders`, `.Taxation`, `.Marketplace`, `.Products`, `.BusinessDocuments` | Checkout, payments, subscription transitions, orders, tax, payouts, product feeds |
| Scheduling and calendars | `CrestApps.OrchardCore.Scheduling`, `CrestApps.OrchardCore.Calendars` | `crestapps.scheduling.observer.failures`, `crestapps.calendars.sync.duration` / `.events` |
| Workforce, dispatch, work orders, geo, solver | `CrestApps.OrchardCore.Workforce`, `.Dispatch`, `.WorkOrders`, `.Geo`, `.Solver` | Notifications, adherence alerts, payroll exports, visit events, SLA events, geocoding, OR-Tools runs |
| Tenant hierarchy | `CrestApps.OrchardCore.TenantHierarchy` | `crestapps.tenanthierarchy.child.operation.duration`; spans `crestapps.tenanthierarchy.child.<operation>` (never a tenant name) |
| Learning | `CrestApps.OrchardCore.Learning` | Enrollments, completions, attempts, upkeep |
| NLWeb | `CrestApps.OrchardCore.NLWeb` | `crestapps.nlweb.ask.duration`, `crestapps.nlweb.ask.results` |

## Log event id ranges (`CrestAppsEventIds`)

| Range | Area |
| --- | --- |
| 10000s | Shared platform helpers |
| 11000s | AI modules (AI.Core 11000-11099, AI module 11100-11199) |
| 12000s | Commerce |
| 13000s | Scheduling, calendars, workforce, skills, work orders, dispatch, geo |
| 14000s | Reports |
| 15000s | Data connectors |
| 16000s | Content transfer, imports, exports, recipes, do-not-call registry |
| 17000s | Contact center (17900s: webhooks; 17950 an inbox delivery dead-lettered) |
| 18000s | Messaging and SMS (18500s: AI voice) |
| 19000s | Telephony and its providers |
| 20000s | Startup and tenant management |

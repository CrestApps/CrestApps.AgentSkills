---
name: orchardcore-agentic-commerce
description: Skill for the CrestApps Orchard Core Agentic Commerce module (CrestApps.OrchardCore.AgenticCommerce), which lets AI agents such as Gemini and ChatGPT buy from the store with the Universal Commerce Protocol (UCP), the Agentic Commerce Protocol (ACP) and the AP2 mandates extension (preview). Covers every feature (UCP, account linking, catalog and cart, MCP and A2A bindings, ACP, Agent Product Feeds, AP2), the /.well-known/ucp and /.well-known/acp.json discovery documents, checkout session endpoints, agent platforms and their sign-in, AgenticCommerceSettings, store signing keys in Secrets, idempotency, payment handlers (Stripe shared payment tokens, Google Pay, test credentials), delegated 3-D Secure, embedded checkout, signed order updates, the AgentPlatforms recipe step, and extending with IAgentPaymentHandler, IUcpProfileContributor, IUcpCheckoutExtension and IAgentOrderPublisher. Use it when requests mention agentic commerce, selling through AI agents, UCP, ACP, AP2, agent checkout or agent product feeds.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Agentic Commerce

You are an Orchard Core expert. Generate accurate setup, integration, extension and troubleshooting guidance for the CrestApps **Agentic Commerce** module. An AI agent finds the store, builds a checkout and pays with a one-purchase credential its platform got from the buyer. The store keeps its own prices, tax, shipping, discounts and stock: every agent checkout is an ordinary store checkout priced and paid through the checkout engine (`orchardcore-checkout`), and every paid agent checkout is an order on the `agent` sales channel (`orchardcore-orders`).

## Guidelines

- Install `CrestApps.OrchardCore.AgenticCommerce` in the web/startup project. Enable `CrestApps.OrchardCore.AgenticCommerce` plus the protocol features of the platforms you sell through.
- Fill in **Settings > Commerce > Store** first, including **Website and policies** (terms of service and privacy policy are required by Google and OpenAI before agents may check out); see `orchardcore-commerce`.
- Connect Stripe (`orchardcore-payments`) for real agent payments; on a test site use the Checkout Test payments feature and a platform in **Test mode**.
- Never price, hold stock or charge in protocol code. Every binding maps its JSON to `IAgentCheckoutService`, and money moves only through `ICheckoutEngine.BeginPaymentAsync`.
- Delegated credentials and 3-D Secure results are server-only provider data; never accept them from a browser.
- Fetch platform URLs only through the egress-guarded clients (profiles through `UcpProfileClient`, webhooks through the Webhooks outbox).
- Protocol money is integers in the currency's minor units (`CurrencyScale`); never multiply by 100.
- Credentials never travel in recipes or deployments: give imported platforms new API keys and signing secrets.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

## Features

| Feature | ID | Depends on | Adds |
| --- | --- | --- | --- |
| Agentic Commerce | `CrestApps.OrchardCore.AgenticCommerce` | Commerce, Store Profile, Checkout, Orders, Products, Webhooks, `OrchardCore.Secrets` | Agent platforms, settings, store signing keys, the protocol-neutral agent checkout, the hand-off link, order updates, payment handlers |
| Universal Commerce Protocol | `CrestApps.OrchardCore.AgenticCommerce.Ucp` | Agentic Commerce | `/.well-known/ucp`, `ucp/v1` checkout sessions and orders, fulfillment, discount and buyer consent extensions, embedded checkout, signed order webhooks |
| UCP Account Linking | `CrestApps.OrchardCore.AgenticCommerce.Ucp.IdentityLinking` | UCP, `OrchardCore.OpenId.Server` | `dev.ucp.common.identity_linking`: buyers link their store account through the OpenID server |
| UCP Catalog and Cart | `CrestApps.OrchardCore.AgenticCommerce.Ucp.Catalog` | UCP, Product Listing | Catalog search, lookup and product, the UCP cart |
| UCP over MCP | `CrestApps.OrchardCore.AgenticCommerce.Ucp.Mcp` | UCP | UCP checkout as MCP tools at `ucp/mcp` |
| UCP over A2A | `CrestApps.OrchardCore.AgenticCommerce.Ucp.A2a` | UCP | UCP checkout and orders through an A2A agent at `ucp/a2a` (card at `ucp/a2a/.well-known/agent-card.json`) |
| Agentic Commerce Protocol | `CrestApps.OrchardCore.AgenticCommerce.Acp` | Agentic Commerce | `/.well-known/acp.json`, `acp/checkout_sessions`, optional request signatures, signed order webhooks |
| ACP over MCP | `CrestApps.OrchardCore.AgenticCommerce.Acp.Mcp` | ACP | ACP checkout sessions as MCP tools at `acp/mcp` |
| Agent Product Feeds | `CrestApps.OrchardCore.AgenticCommerce.Feeds` | Agentic Commerce, Product Feeds | OpenAI product feed, daily SFTP push, ACP feed API client |
| Agent Payments Protocol (Preview) | `CrestApps.OrchardCore.AgenticCommerce.Ap2` | UCP | AP2 merchant authorizations and checkout mandates for UCP |

Projects: `CrestApps.OrchardCore.AgenticCommerce.Abstractions` (`AgenticCommerceConstants`, the checkout models such as `AgentCheckoutInput`, `AgentCheckoutView`, `AgentPayment`, and `IAgentPaymentHandler`), `CrestApps.OrchardCore.AgenticCommerce.Core` (`AgentPlatform`, `AgentCheckout`, `AgenticCommerceSettings`, `IAgentCheckoutService`, `IAgentPlatformStore`, `IAgentCheckoutStore`, `IMerchantSigningKeyService`, the AP2 verifier and signature helpers) and the module (endpoints, mappers, `IUcpProfileContributor`, `IUcpCheckoutExtension`, `IAcpDiscoveryContributor`, `IAgentOrderPublisher`, admin, recipes, AI tools).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Commerce.StoreProfile",
        "CrestApps.OrchardCore.Stripe",
        "CrestApps.OrchardCore.AgenticCommerce",
        "CrestApps.OrchardCore.AgenticCommerce.Ucp",
        "CrestApps.OrchardCore.AgenticCommerce.Ucp.Catalog",
        "CrestApps.OrchardCore.AgenticCommerce.Acp",
        "CrestApps.OrchardCore.AgenticCommerce.Feeds"
      ],
      "disable": []
    }
  ]
}
```

## Admin locations

| Menu | Route | Permission |
| --- | --- | --- |
| **Commerce > AI agents** | `AgentPlatforms/Index` (area `CrestApps.OrchardCore.AgenticCommerce`) | `ManageAgentPlatforms` |
| **Settings > Commerce > Agentic commerce** | settings group `agentic-commerce` | `ManageAgenticCommerceSettings` |
| **Settings > Commerce > Agent feeds** (Agent Product Feeds) | settings group `agent-feeds` | `ManageAgentFeeds` |

Agent orders appear under **Commerce > Orders** with the *AI agents* channel filter.

## How a checkout works

One protocol-neutral service, `IAgentCheckoutService` (`CreateAsync`, `GetAsync`, `UpdateAsync`, `CompleteAsync`, `CancelAsync`, `PreviewAsync`, `GetPaymentHandlersAsync`, `ExpireAsync`), does the work for every binding:

1. **Create.** Items arrive by product feed item id (`ProductFeedItemIds`, resolved through `IAgentItemResolver`), with buyer, address and discount codes, kept on an `AgentCheckout` record in the `AgenticCommerce` collection.
2. **Price.** Items are priced as a private cart (`ICartService`), so every storefront rule applies; an item the store doesn't sell stops the checkout.
3. **Order and store checkout.** `IOrderService.CreateFromCartAsync` makes a draft order on `OrderChannels.Agent` (stock is held), and `ICheckoutEngine.StartAsync` starts its store checkout. Changing items releases the old checkout and replaces the order.
4. **Steps.** Contact and billing through `ICheckoutContactStep` (Customers), address and shipping through `ICheckoutFulfillmentStep` (Shipping); without a choice the cheapest shipping is picked. Codes go through the coupon service.
5. **Pay.** The agent names a payment handler; the handler turns the credential into provider data (`CheckoutPaymentDataKeys.DelegatedCredential`), `BeginPaymentAsync` charges it through a provider with `AcceptsDelegatedCredentials`, and `TryCompleteAsync` pays the order.

Totals always add up: subtotal - discount + shipping + tax + fees = total. Requests for one checkout are serialized under a distributed lock (`agent-checkout:{id}`, 10 s); a busy checkout gets `409` with `Retry-After: 1`. A platform only sees its own checkouts.

| `AgentCheckoutStatus` | UCP | ACP |
| --- | --- | --- |
| `Incomplete` | `incomplete` | `not_ready_for_payment` |
| `RequiresEscalation` | `requires_escalation` | `requires_escalation` |
| `ReadyForComplete` | `ready_for_complete` | `ready_for_payment` |
| `CompleteInProgress` | `complete_in_progress` | `complete_in_progress` |
| `Completed` | `completed` | `completed` |
| `Canceled` | `canceled` | `canceled` |
| `Expired` | `canceled` | `expired` |
| `AuthenticationRequired` | (not used) | `authentication_required` |

When the buyer must act (3-D Secure, a store checkout field the agent can't fill), the checkout is `requires_escalation` with a `requires_buyer_input` message and `continue_url`, the signed hand-off link `agentic-commerce/continue/{token}` (two days). Opening it gives a guest's checkout to that browser (a linked buyer's stays theirs) and goes to the next step. The agent's request runs as `agent-platform:{platformId}`, so the store checkout belongs to the linked buyer or to nobody until the hand-off.

Endpoints, discovery, sign-in, idempotency, 3-D Secure, embedded checkout, account linking, catalog, MCP and A2A are detailed in [references/protocols-and-endpoints.md](references/protocols-and-endpoints.md).

## Agent platforms

Each business whose agents may buy is an `AgentPlatform` (`IAgentPlatformStore`, edited under **Commerce > AI agents**): `Name`, `Description`, `Enabled` (**Its agents may buy**), `TestMode`, `Protocols` (`ucp`, `acp`), `OAuthClientId`, `ProfileUrl` (its UCP profile), `WebhookUrl` (**Order updates address**), `AllowedPaymentHandlers`, `AllowedIpRanges` (addresses or CIDR; empty allows any), `EmbeddedCheckoutOrigins`, `AcpOrderCreatedEvent` (`order_create`), `AcpOrderUpdatedEvent` (`order_update`), `AcpRequireSignedRequests`, `AcpSignatureAlgorithm` (`ES256` only), `AcpSignaturePublicKey` (EC P-256 JWK or PEM public key), `AcpSignatureClockSkewSeconds` (1 to 3600, 300 when empty). Credentials are kept separately: **New API key** (`agk_...`, shown once, only its SHA-256 hash and last four characters kept) and **New secret** (`whsec_...`, shown once, protected with the tenant's data protection keys) for ACP order webhooks.

A request signs in with an API key (`Authorization: Bearer agk_...` or `X-API-Key`), an OAuth token from the tenant's OpenID server whose `client_id`/`azp` is the platform's **OAuth client ID**, or (UCP) an RFC 9421 HTTP message signature with a key from the platform's UCP profile. The platform must be enabled, use the request's protocol and call from an allowed IP. `AgenticCommerceSettings.AllowUnregisteredUcpPlatforms` lets signed requests from unknown UCP profiles in as unregistered platforms (no test mode, no IP or handler limits).

## Settings

`AgenticCommerceSettings` (**Settings > Commerce > Agentic commerce**):

| Property | Default | Meaning |
| --- | --- | --- |
| `CheckoutLifetimeHours` | 6 | 1 to 72; the **Agent checkout expiry** task (every 5 minutes) expires checkouts and releases stock |
| `MaxOpenCheckoutsPerPlatform` | 1000 | One more gets `429` |
| `AllowUnregisteredUcpPlatforms` | false | Accept signed requests from UCP profiles no platform has |
| `AllowEmbeddedCheckout` | false | UCP embedded checkout, with the platform's **Embedding origins** |
| `StripeBusinessProfileId` | | Stripe business profile (`profile_...`) agents issue shared payment tokens to; required for UCP Stripe tokens, sent as ACP `merchant_id` |
| `EnableGooglePay`, `GooglePayMerchantId`, `GooglePayMerchantName`, `GooglePayTestEnvironment` (true) | | Offer Google Pay through Stripe to UCP agents |
| `EnableDelegatedAuthentication`, `AcquirerBin`, `AcquirerCountry`, `AcquirerMerchantId`, `AuthenticationMerchantName`, `ThreeDSecureRequestorId` | | Let ACP agents run 3-D Secure themselves |

## Payment handlers

| Handler | Name | Offered when |
| --- | --- | --- |
| `StripeSharedPaymentTokenHandler` | UCP `com.stripe.payments`; ACP `dev.acp.tokenized.card` (id `stripe_card`, PSP `stripe`) | Stripe is on and set up; UCP also needs `StripeBusinessProfileId` |
| `GooglePayPaymentHandler` | UCP `com.google.pay` | Stripe has a publishable key and `EnableGooglePay` is on |
| `TestAgentPaymentHandler` | `dev.crestapps.test` (id `test`) | Test payments feature on; only for platforms in **Test mode**. `tok_test_success` pays, `tok_test_decline` declines, `tok_test_3ds` asks for 3-D Secure |

A platform in test mode sees only the test handler; a non-empty **Payment handlers** list limits the rest by name. Stripe shared payment tokens use Stripe's preview API `2026-09-30.preview`.

## Order updates and signing keys

`AgentOrderEventHandler` (and `AgentShipmentEventHandler` with Shipping) call each `IAgentOrderPublisher` of the checkout's protocol when the order is paid (new order), canceled, refunded or returned, or a shipment is shipped, delivered or returned (changed order). Updates are queued as Webhooks **direct deliveries** (`IWebhookOutbox`, see `orchardcore-webhooks`), signed at every attempt and retried after 1 min, 5 min, 30 min, 2 h, 6 h, 12 h and 24 h. UCP posts the UCP order to the profile's order `webhook_url` (else **Order updates address**) signed with RFC 9421 and the store's ES256 key; ACP posts `{"type", "data"}` to **Order updates address** with `Merchant-Signature: t=...,v1=...` (HMAC-SHA256 with the platform's secret) and sends nothing without both.

`IMerchantSigningKeyService` gives the store's ES256 (P-256) keys from the shared signing keys (key set `AgenticCommerce.Merchant`), each a `TextSecret` in Orchard Core Secrets named `SigningKeys.AgenticCommerce.Merchant.{kid}`. The **Signing key rotation** task (daily) makes a new key at 90 days; retired keys stay published in the profile's `keys` for 7 days and their secrets are removed after 28 days. Keys from the old `MerchantSigningKeysDocument` are imported with their ids. The keys sign UCP order webhooks, AP2 merchant authorizations and AP2 receipts.

## AI tools

With `CrestApps.OrchardCore.AI`, Product Listing and Product Feeds on, five tools in the **Commerce** category (`searchStoreProducts`, `getStoreProduct`, `startStoreCheckout`, `addToStoreCart`, `getStoreCart`) let the site's own AI profiles, MCP and WebMCP search, start a checkout (platform id `site-assistant`, protocol `assistant`; it never pays, it returns a `checkout_url`) and use the caller's own cart. All are marked anonymous-safe. See `orchardcore-ai-tools`.

## Agent product feeds

**Agent Product Feeds** publishes `feeds/openai/products.jsonl.gz` and `feeds/openai/products.csv.gz` (or uncompressed `.jsonl`/`.csv`, with `?currency=` and the product feed `?key=`), pushes the full feed daily over SFTP (02:30, shards of 500,000 items) and keeps ACP feed APIs up to date. Products need **Show to AI agents** and **Let AI agents check out** (Product Feed part, else the feed defaults). See [references/agent-feeds.md](references/agent-feeds.md).

## Recipes and deployment

The **AI agent platforms** deployment step and the `AgentPlatforms` recipe step move platforms (matched as catalog entries). API key hashes, hints and signing secrets are never exported or read; the ACP signature settings, the public key included, do travel.

```json
{
  "steps": [
    {
      "name": "AgentPlatforms",
      "Platforms": [
        {
          "Name": "Contoso Agents",
          "Description": "Contoso's shopping agent.",
          "Enabled": true,
          "TestMode": true,
          "Protocols": [ "ucp", "acp" ],
          "OAuthClientId": "contoso-agents",
          "ProfileUrl": "https://agents.contoso.com/.well-known/ucp",
          "WebhookUrl": "https://agents.contoso.com/webhooks/orders",
          "AllowedPaymentHandlers": [ "com.stripe.payments", "dev.acp.tokenized.card" ],
          "AllowedIpRanges": [ "203.0.113.0/24" ],
          "EmbeddedCheckoutOrigins": [ "https://agents.contoso.com" ],
          "AcpOrderCreatedEvent": "order_create",
          "AcpOrderUpdatedEvent": "order_update",
          "AcpRequireSignedRequests": false
        }
      ]
    },
    {
      "name": "Settings",
      "AgenticCommerceSettings": {
        "CheckoutLifetimeHours": 6,
        "MaxOpenCheckoutsPerPlatform": 1000,
        "AllowUnregisteredUcpPlatforms": false,
        "AllowEmbeddedCheckout": false,
        "StripeBusinessProfileId": "profile_123",
        "EnableGooglePay": true,
        "GooglePayMerchantId": "BCR2DN4T000000",
        "GooglePayMerchantName": "Contoso",
        "GooglePayTestEnvironment": true
      }
    }
  ]
}
```

Two recipes ship with the module (run them from **Tools > Recipes**):

| Recipe | Does |
| --- | --- |
| `AgenticCommerceSample` (Agentic commerce: sample) | Enables Test payments, Agentic Commerce, UCP, UCP Catalog and Cart, UCP over MCP, UCP over A2A, ACP, ACP over MCP and Agent Product Feeds, and adds a *Test agent platform* in test mode for UCP and ACP. Then give it an API key under **Commerce > AI agents** |
| `AgenticCommerceAccountLinking` (Agentic commerce: UCP account linking) | Enables `OrchardCore.OpenId.Server`, `OrchardCore.OpenId.Validation` and UCP Account Linking, and adds the `dev.ucp.shopping.checkout:manage`, `dev.ucp.shopping.order:read` and `dev.ucp.shopping.order:manage` OpenID scopes. Then add an OpenID application per platform (authorization code + PKCE) and put its client id on the platform |

The module has no `appsettings.json` section.

## Extension points

| Contract | Use it to |
| --- | --- |
| `IAgentPaymentHandler` (Abstractions) | Add a way agents pay |
| `IAgentCheckoutService` (Core) | Drive agent checkouts from another binding |
| `IUcpProfileContributor`, `IUcpCheckoutExtension` | Add a UCP capability, extension or transport, and its checkout members |
| `IAcpDiscoveryContributor` | Add to `/.well-known/acp.json` |
| `IAgentOrderPublisher` with `IWebhookOutbox` and `IWebhookDeliveryFormatter` | Send a protocol's order updates |
| `ICheckoutContactStep`, `ICheckoutFulfillmentStep` (Checkout.Abstractions) | Fill the Contact and Shipping steps from code |
| `IdempotencyPolicy`, `IdempotentOperation` (`CrestApps.OrchardCore.Api`) | Keep a protocol's own idempotency rules |

Samples for each are in [references/extending.md](references/extending.md).

## Permissions

| Permission | Shown as | Default roles |
| --- | --- | --- |
| `ManageAgentPlatforms` | Manage AI agent platforms | Administrator, Store manager |
| `ManageAgenticCommerceSettings` | Manage agentic commerce settings | Administrator, Store manager |
| `ManageAgentFeeds` (Agent Product Feeds) | Manage agent product feeds | Administrator, Store manager |

Agents need no permission: they sign in as their platform and run as `agent-platform:{platformId}`, so idempotency keys are kept per platform.

## Limitations

- ACP request signatures are ES256 only (no Ed25519 in .NET); the store does not host ACP's delegate payment endpoint.
- Delegated 3-D Secure is ACP only; UCP checkouts go to `requires_escalation`.
- Embedded checkout accepts no delegation; the buyer does every step in the store's checkout.
- AP2 is a preview for UCP only; Stripe shared payment tokens are a Stripe preview API.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| **Commerce > AI agents** missing | Enable Agentic Commerce; the user needs `ManageAgentPlatforms` |
| `/.well-known/ucp` or `/.well-known/acp.json` answers 404 | Enable the UCP or ACP feature |
| `401` with `WWW-Authenticate: Bearer` | No credentials, or an old API key; give the platform a new key with **New API key** |
| `403` | Platform disabled, protocol not ticked, IP not in **Allowed IP addresses**, or a `UCP-Agent` header naming another profile |
| UCP `422 version_unsupported`, `424 profile_unreachable`, `422 profile_malformed` | The platform's UCP version or profile; profiles are fetched through the egress guard (no private hosts, no redirects, 256 KB, 10 s) |
| ACP `400 missing_api_version` / `unsupported_api_version` | Send `API-Version` `2026-04-17` or `2026-01-30` |
| ACP `400 idempotency_key_required` | Every ACP POST needs `Idempotency-Key` |
| ACP `401 invalid_signature` | Wrong **Platform's public key**, a clock outside the skew, or a body not signed as RFC 8785 canonical JSON |
| No payment handler offered | Stripe not set up, `StripeBusinessProfileId` empty (UCP), platform in test mode without Test payments, or **Payment handlers** excludes it |
| `429` | **Open checkouts per platform** reached; old checkouts expire after **Checkout lifetime** |
| No order updates | ACP: set **Order updates address** and a signing secret. UCP: the profile's order `webhook_url` or **Order updates address**. Check **Settings > Webhooks** deliveries |
| Product not offered to agents | **Show to AI agents** / **Let AI agents check out** on the Product Feed part or feed defaults; the item must have a price and be purchasable |
| Embedded checkout not shown | **Allow embedded checkout**, exact `https://host[:port]` **Embedding origins**, HTTPS site, and `ec_version` matching the checkout's version |
| Account linking metadata 404 | Enable the OpenID server (run `AgenticCommerceAccountLinking`); PKCE must be S256 |

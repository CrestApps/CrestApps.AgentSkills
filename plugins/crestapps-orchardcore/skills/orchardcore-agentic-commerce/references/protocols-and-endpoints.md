# Agentic Commerce Protocols and Endpoints

UCP serves versions `2026-08-25` and `2026-04-08`; ACP serves `2026-04-17` and `2026-01-30`. Paths come after the tenant's base address.

## Endpoints

| Method and path | Feature | Does |
| --- | --- | --- |
| `GET /.well-known/ucp` | UCP | The UCP profile for `2026-08-25` with the older version's address. Public, cached 5 minutes, `ETag` |
| `GET /.well-known/ucp/{version}` | UCP | The profile for one version |
| `POST ucp/v1/checkout-sessions` | UCP | Creates a checkout (`201`); with `cart_id` and no `line_items`, from a UCP cart |
| `GET` / `PUT ucp/v1/checkout-sessions/{id}` | UCP | Reads / changes a checkout |
| `POST ucp/v1/checkout-sessions/{id}/complete` | UCP | Pays and completes |
| `POST ucp/v1/checkout-sessions/{id}/cancel` | UCP | Cancels |
| `GET ucp/v1/orders/{id}` | UCP | An order the platform's agents placed, or (linked account, `dev.ucp.shopping.order:read`) one of the buyer's orders |
| `POST ucp/v1/catalog/search`, `catalog/lookup` (up to 50 ids), `catalog/product` | UCP Catalog and Cart | Catalog |
| `POST ucp/v1/carts`, `GET`/`PUT ucp/v1/carts/{id}`, `POST ucp/v1/carts/{id}/cancel` | UCP Catalog and Cart | The UCP cart |
| `POST ucp/mcp` | UCP over MCP | JSON-RPC MCP binding |
| `GET ucp/a2a/.well-known/agent-card.json`, `POST ucp/a2a` | UCP over A2A | A2A agent card (public, 5 minutes) and JSON-RPC `message/send` |
| `GET /.well-known/oauth-authorization-server` | UCP Account Linking | RFC 8414 metadata of the tenant's OpenID server with UCP scopes and S256-only PKCE; `404` when the OpenID server is off |
| `GET /.well-known/acp.json` | ACP | ACP discovery document. Public, cached 1 hour |
| `POST acp/checkout_sessions` | ACP | Creates a checkout (`201`) |
| `GET` / `POST acp/checkout_sessions/{id}` | ACP | Reads / changes |
| `POST acp/checkout_sessions/{id}/complete` | ACP | Pays and completes |
| `POST acp/checkout_sessions/{id}/cancel` | ACP | Cancels; `405` when it can't be canceled |
| `POST acp/mcp` | ACP over MCP | JSON-RPC MCP binding |
| `GET agentic-commerce/continue/{token}` | Agentic Commerce | The hand-off link; with `ec_version`, the embedded checkout page |
| `GET agentic-commerce/embedded/{token}/checkout`, `POST agentic-commerce/embedded/{token}/auth` | UCP | The embedded page reads its checkout / checks the platform's credential |

Bodies are at most 1 MB (`413`), read once exactly as sent so signatures and digests can be checked. UCP answers business outcomes (such as changing a completed checkout) with `200` and the checkout with messages; only not found (`404`), busy (`409` + `Retry-After`) and too many (`429`) are HTTP errors. ACP answers errors with a `4xx` and an ACP error object (`type`, `code`, `message`, `param`).

## UCP profile and negotiation

`UcpProfileBuilder` builds the profile from every `IUcpProfileContributor`: services and transports (`rest`, `mcp`, `a2a`, `embedded`), capabilities and extensions, the payment handlers, and the store's public signing keys under `keys`.

| Capability | Offered by |
| --- | --- |
| `dev.ucp.shopping.checkout` with `dev.ucp.shopping.fulfillment`, `dev.ucp.shopping.discount`, `dev.ucp.shopping.buyer_consent` | UCP |
| `dev.ucp.shopping.order` | UCP |
| `dev.ucp.shopping.catalog.search`, `dev.ucp.shopping.catalog.lookup`, `dev.ucp.shopping.cart` | UCP Catalog and Cart |
| `dev.ucp.common.identity_linking` | UCP Account Linking |
| `dev.ucp.common.payment.ap2_mandate` (`dev.ucp.shopping.ap2_mandate` in `2026-04-08`) | AP2 |

An agent names its platform profile in the `UCP-Agent` header (`profile="https://..."`). `UcpProfileClient` fetches it through the egress-guarded client `AgenticCommerce.Profiles` (no private hosts, no redirects, 256 KB, 10 s, cached for its `max-age` between 1 minute and 1 hour). Errors: `400 invalid_profile_url`, `424 profile_unreachable`, `422 profile_malformed`. Without the header the platform's saved **UCP profile** is used. `UcpNegotiator` requires a served `ucp.version` (`422 version_unsupported`) and keeps a capability only when both profiles offer it in the same version, an extension only while its parent is kept. A platform without a profile gets the newest version and everything.

## ACP discovery and versions

`/.well-known/acp.json` names the protocol, versions, `api_base_url` (`{base}/acp`), transports (`rest`, and `mcp` with ACP over MCP), the `checkout` and `orders` services and the `discount` extension; features add to it with `IAcpDiscoveryContributor`. Every ACP request sends `API-Version`; missing or unknown gets `400 missing_api_version` or `unsupported_api_version` with `supported_versions`. `Request-Id` is echoed.

## Sign-in

| Credential | Sent as | Protocols |
| --- | --- | --- |
| API key | `Authorization: Bearer agk_...` or `X-API-Key: agk_...` | UCP, ACP |
| OAuth bearer token | From the tenant's OpenID server, validated with Orchard Core's `Api` scheme; `client_id` (or `azp`) must be the platform's **OAuth client ID**. A token whose `sub` is not the client is a linked buyer's token | UCP, ACP |
| RFC 9421 HTTP message signature | `Signature-Input`, `Signature`, `Content-Digest` with a body; `keyid` must be in the platform profile's `keys` | UCP |

A signed request is always verified even when a key or token also signed it in: `400 digest_mismatch`, `400 algorithm_unsupported`, `401` for a bad signature. No credentials gets `401` with `WWW-Authenticate: Bearer`. A platform with a **UCP profile** may only act for that profile (`403` otherwise).

### ACP request signatures

Per platform (**ACP request signatures** card): `AcpRequireSignedRequests`, `AcpSignatureAlgorithm` (`ES256`), `AcpSignaturePublicKey` (EC P-256 JWK or PEM `PUBLIC KEY`; a private key is refused on save; it is not a secret and travels in recipes), `AcpSignatureClockSkewSeconds`. The `Signature` header is a base64url detached ES256 signature (JOSE `r || s`, DER accepted) over the RFC 8785 canonical JSON of the body (nothing for no body); `Timestamp` (RFC 3339) is checked against the skew but not signed. Checked after sign-in and before any checkout work, including idempotency replay. A failure is `401` `invalid_request`/`invalid_signature`. A platform that does not require signatures but has a key still has signed requests verified.

```csharp
// How an agent signs a request for this store.
var input = AcpRequestSignature.GetSigningInput(JsonNode.Parse(body));
request.Headers.Add("Signature", AcpRequestSignature.Sign(input, privateKey));
request.Headers.Add("Timestamp", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
```

## Idempotency

Mutating checkout and cart endpoints use `WithIdempotency(IdempotencyPolicy)` from `CrestApps.OrchardCore.Api` (`CrestApps.OrchardCore.Core`); keys are kept per platform.

| | UCP | ACP |
| --- | --- | --- |
| Key required | No | On every POST (`400 idempotency_key_required`) |
| Kept for | 48 hours | 24 hours |
| Key length | At most 255 | 1 to 255 |
| Same key, same body | First answer again | First answer again, `Idempotent-Replayed: true`, key echoed |
| Same body means | Same bytes (SHA-256 of the raw body) | Equal RFC 8785 canonical JSON |
| Same key, other body | `409 idempotency_conflict` | `422 idempotency_conflict` |
| First still running | `409 idempotency_in_flight` | `409 idempotency_in_flight`, `Retry-After: 1` |

ACP over MCP takes the key in `meta.idempotency_key` (`AcpMcpIdempotency`, per platform, tool and checkout; replay has `_meta.idempotent_replayed: true`). UCP over A2A runs a mutating action once per A2A `messageId` for 48 hours (`IdempotentOperation`).

## Delegated 3-D Secure (ACP)

Asked for only when **Let agents run 3-D Secure** is on with the acquirer settings, the agent's create request has `capabilities.interventions.supported` containing `3ds`, the provider sets `AcceptsExternalAuthentication` (Stripe, test), and the card brand has a directory server (`visa`, `mastercard`, `american_express`). Flow: complete → `authentication_required` with a `requires_3ds` message and `authentication_metadata` (`acquirer_details`, `directory_server`), `continue_url` kept → the agent runs 3-D Secure and completes again with a new idempotency key, the same handler and `authentication_result` (`outcome`, and `outcome_details` with `three_ds_cryptogram`, `electronic_commerce_indicator`, `transaction_id`, `version`). Missing result: `400 requires_3ds` (`param: "$.authentication_result"`); malformed: `400 invalid`. Any outcome other than authenticated, `attempt_acknowledged` or `informational` cancels the store checkout with `payment_declined`. The second complete resumes the same payment attempt, so money can't move twice. UCP's payment authentication extension is not offered; UCP checkouts escalate.

## Embedded checkout (UCP)

Needs `AllowEmbeddedCheckout` and the platform's **Embedding origins** (`https://host[:port]`, normalized by `EmbeddingPolicy.NormalizeOrigin`; `http` only on loopback). The profile then lists the `embedded` transport and every checkout whose `continue_url` is the hand-off link carries the embedded binding `{ "transport": "embedded", "config": { "delegate": [] } }`. Opening `continue_url` with `ec_version` returns the embedded page with `Content-Security-Policy: frame-ancestors <origins>` (no `X-Frame-Options`), a signed frame cookie, and over HTTPS `SameSite=None; Secure; Partitioned` cookies. `ec_color_scheme=light|dark` sets `data-bs-theme`; `ec_delegate` is read but no delegation is accepted. `ec_auth` is an OAuth token issued to the platform's **OAuth client ID** (needs OpenID validation). The `agentic-commerce-embedded-checkout` script speaks JSON-RPC 2.0 over `postMessage` (or an injected `EmbeddedCheckoutProtocolConsumer`): `ec.ready`, `ec.auth` (requests), `ec.start`, `ec.line_items.change`, `ec.buyer.change`, `ec.fulfillment.change`, `ec.payment.change`, `ec.totals.change`, `ec.messages.change`, `ec.complete`, `ec.error` (notifications).

## Account linking (UCP)

Scopes `dev.ucp.shopping.checkout:manage` (check out as the buyer, with saved addresses; the checkout and order belong to the buyer, `AgentCheckoutContext.UserId`) and `dev.ucp.shopping.order:read` (`GET ucp/v1/orders/{id}` also returns the buyer's orders). `UcpAuthorizationServerMetadataHandler` adds the scopes and `code_challenge_methods_supported: ["S256"]` to the OpenID server's metadata (also `/.well-known/openid-configuration`). While UCP Account Linking is on, **the OpenID server accepts only S256 PKCE** (`plain` is removed). Set up with the `AgenticCommerceAccountLinking` recipe plus an OpenID application per platform.

## Catalog and cart (UCP)

`UcpCatalog` reads the same items as the product feeds (same ids, prices, photos, availability), only items with **Show to AI agents**. `catalog/search` takes `query`, `pagination.limit` (max 50, default 20), `pagination.cursor`, `context.currency` and returns `products`, `pagination.total_count`, `next_cursor`. A UCP cart keeps items without payment or store checkout.

## UCP over MCP

`POST ucp/mcp`: JSON-RPC 2.0 as plain JSON, MCP protocol `2025-06-18`, `initialize`, `ping`, `tools/list`, `tools/call` (notifications get `202`). Tools `create_checkout` (`checkout`), `get_checkout` (`id`), `update_checkout` (`id`, `checkout`), `complete_checkout` (`id`, `checkout` with payment), `cancel_checkout` (`id`); each returns the checkout in `structuredContent`.

## ACP over MCP

`POST acp/mcp`: MCP Streamable HTTP answered as plain JSON, protocol `2025-06-18`; `/.well-known/acp.json` lists `mcp`. Sign-in is on the HTTP request (never a tool argument); failures are HTTP `401`/`403` with JSON-RPC `-32000` carrying the ACP error. Every tool takes `{ meta, id, payload }`: `meta` holds what REST sends as headers (`api_version` required, `idempotency_key`, `request_id`, `signature`, `timestamp`), `id` the checkout id (all but create), `payload` the REST body. Tools `create_checkout_session`, `get_checkout_session`, `update_checkout_session`, `complete_checkout_session`, `cancel_checkout_session` run `AcpCheckoutOperations`, the same code as REST. ACP errors are `-32000` with the ACP error in `error.data`; malformed arguments are `-32602`.

## UCP over A2A

A2A `0.3.0` card at `ucp/a2a/.well-known/agent-card.json` (url `ucp/a2a`), listing UCP's extension per served version with checkout and order capabilities (no catalog or cart). Send `X-A2A-Extensions` to activate it. `message/send` only (`message/stream` gets `-32004`, task methods `-32001`, others `-32601`). Put the action and payload in data parts (merged): `create_checkout` (`a2a.ucp.checkout`), `get_checkout` (`id`), `update_checkout`, `complete_checkout` (`a2a.ucp.checkout.payment`, `signals`, and with AP2 `ap2.checkout_mandate`), `cancel_checkout`, `add_to_checkout` (`product_id`, `quantity`), `get_order` (`id`). The agent remembers each `contextId`'s last checkout for 72 hours. Answers are agent messages with the checkout under `a2a.ucp.checkout` or the order under `a2a.ucp.order`.

## AP2 mandates (preview)

With AP2 agreed (profile `vp_formats_supported` `dc+sd-jwt`), every checkout answer carries `ap2.merchant_authorization` (detached ES256 JWS over the RFC 8785 form of the checkout without `ap2`), completing requires `ap2.checkout_mandate` (`mandate_required`), and `Ap2MandateVerifier` checks the SD-JWT delegation chain (root signed by a key in the platform profile's `keys`, 60 s skew; `agent_missing_key` without keys). The mandate must match the checkout id, items, quantities and total in minor units (`mandate_scope_mismatch`). On completion the mandate and a store-signed receipt are kept on the agent checkout and later answers carry `ap2.checkout_receipt`. ACP checkouts don't use AP2.

# Agent Product Feeds

`CrestApps.OrchardCore.AgenticCommerce.Feeds` (depends on Agentic Commerce and `CrestApps.OrchardCore.Products.Feeds`) publishes the store's products for AI agents from the same items as the product feed (see `orchardcore-products`).

**Settings > Commerce > Agent feeds** (settings group `agent-feeds`, permission `ManageAgentFeeds`) shows the feed addresses, how many items are in the feed and why others were left out, the SFTP push and feed API targets, the last pushes' results, and **Push now**.

## The OpenAI feed

- `feeds/openai/products.jsonl.gz`, `feeds/openai/products.csv.gz`, or uncompressed `.jsonl` / `.csv`, with `?currency=` and the product feed's `?key=` when one is set.
- `OpenAIProductFeedService` builds `OpenAIProductFeedRecord`s with `OpenAIProductFeedWriter.ToRecord`, cached as long as the product feed (a stock change rebuilds both).

| Field | From |
| --- | --- |
| `item_id`, `group_id`, `listing_has_variations`, `variant_dict` | Item id (`ProductFeedItemIds`), the product id for a variant, its options |
| `title`, `description`, `url`, `image_url`, `additional_image_urls` | As in the product feed |
| `brand`, `gtin`, `mpn`, `condition`, `product_category`, `color`, `size` | As in the product feed |
| `availability` | `in_stock`, `out_of_stock`, `pre_order`, `backorder`, `unknown` |
| `price`, `sale_price`, `shipping_price` | `79.99 USD`, at the currency's precision |
| `weight`, `item_weight_unit`, `length`, `width`, `height`, `dimensions_unit` | Shipping contributor |
| `accepts_returns`, `return_deadline_in_days`, `return_policy`, `accepts_exchanges` | Returns contributor and the store profile's return policy link |
| `seller_name`, `seller_url`, `seller_privacy_policy`, `seller_tos` | Store profile |
| `is_digital` | Product type Digital |
| `is_eligible_search`, `is_eligible_checkout` | **Show to AI agents**, **Let AI agents check out**; an item agents may not show stays with `is_eligible_search: false` so ChatGPT drops it sooner |
| `store_country`, `target_countries` | Store profile country; `AgentFeedSettings.TargetCountries` (store country when empty) |

`OpenAIProductFeedValidator` leaves out and lists items that break OpenAI's rules: missing required fields (`item_id`, `title`, `description`, `url`, `brand`, `seller_name`, `image_url`, `availability`, `price`), unknown availability, badly written money, a sale price not below the price, a GTIN without a valid check digit, a variant without a distinct `group_id` or `variant_dict`, a weight or size without its unit, and a checkout-eligible item without `seller_privacy_policy` and `seller_tos`.

## The SFTP push

With **Push the full feed over SFTP every day** on, the **Agent product feeds push** task (`30 2 * * *`) has `AgentFeedSftpPusher` write the whole catalog in shards of at most 500,000 items ordered by item id with stable names (`products-00001.jsonl.gz`, ...), overwriting each push and deleting shards no longer needed. It signs in with a password, a private key (OpenSSH or PEM, with passphrase) or both, all protected with the tenant's data protection keys, and checks the server's SHA-256 host key fingerprint when one is set. Results are kept in `AgentFeedStatusDocument`.

## ACP feed APIs

Each `AcpFeedTarget` in `AgentFeedSettings.FeedApiTargets` (`Id`, `Name`, https `BaseUrl`, protected `BearerToken`, `FeedId`, `TargetCountry`, `Currency`, `Enabled`) is kept up to date by `AcpFeedPusher`: `POST /feeds` creates the feed when the target has no id, and `PATCH /feeds/{id}/products` upserts products in batches of 100, everything on the daily push and **Push now**, and changed products after publishing or (with Inventory) after available units change, once per unit of work. Calls send `Authorization: Bearer`, `API-Version: 2026-04-17` and a deterministic `Idempotency-Key`, through the egress guard. Only items agents may show are sent; an unpublished product stays on the platform until changed there. `AcpFeedProductMapper` makes one ACP `Product` per store product with each item as a `Variant` (minor-unit `price` and `list_price`, `availability`, `categories`, `variant_options`, `seller` links).

## Recipe

`AgentFeedSettings` travels with the `Settings` step. Secrets (SFTP password, private key, passphrase, bearer tokens) are stored protected: enter them again on another site.

```json
{
  "steps": [
    {
      "name": "Settings",
      "AgentFeedSettings": {
        "TargetCountries": [ "US", "CA" ],
        "Sftp": {
          "Enabled": true,
          "Host": "sftp.example.com",
          "Port": 22,
          "Username": "contoso",
          "RemoteFolder": "/",
          "Format": "jsonl.gz"
        },
        "FeedApiTargets": [
          {
            "Id": "01kfeedtarget0000000000000",
            "Name": "Example agent",
            "BaseUrl": "https://api.example.com/acp",
            "TargetCountry": "US",
            "Enabled": true
          }
        ]
      }
    }
  ]
}
```

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Item missing from the OpenAI feed | The settings page lists why: a missing required field, no seller links for a checkout-eligible item, or the product feed already left it out |
| `is_eligible_search` is false | **Show to AI agents** is off on the Product Feed part or `ProductFeedSettings.ShowToAgentsByDefault` is off |
| SFTP push fails | Host key fingerprint mismatch, wrong credentials, or the egress guard refusing a private host |
| Feed API push refused | Bearer token, `BaseUrl` (must be https), or the target's `FeedId` |

# Store credit, loyalty points and referrals

Each is a feature of the Customers module that depends on Customers and Orders. Store credit and points are `ICheckoutTenderProvider`s: they pay after tax, for the session owner's own customer only, never on a checkout that settles an existing balance or saves a card. Every change takes `CheckoutTenderLocks.GetKey(tenderKey, accountId)`, the lock the checkout's tender ledger uses. The services live in the module (`CrestApps.OrchardCore.Customers.Services`), so a module that calls them references `CrestApps.OrchardCore.Customers`.

## Store credit (`CrestApps.OrchardCore.Customers.StoreCredit`)

- `StoreCreditAccount` (collection `Customers`, `StoreCreditAccountIndex`): one customer, one `Currency`, `Balance`, `Activity` (`StoreCreditActivity`: `CreatedUtc`, `Amount`, `Balance`, `Reason`, `SessionId`, `Actor`). `IStoreCreditStore`: `FindAsync(customerId, currency)`, `ListByCustomerAsync`.
- `StoreCreditService.ChangeAsync(customerId, currency, amount, reason, actor, sessionId)` creates the account on a first credit; returns `null` when the lock is busy or there is nothing to take. Positive amounts give, negative take; the balance never goes below zero.
- Checkout: `StoreCreditTenderProvider` (key `store-credit`, `StoreCreditService.TenderKey`) and `StoreCreditCheckoutFlowDisplayDriver` (`CheckoutStoreCredit` at `Aside:7`, button `apply-tender:store-credit`, remove `remove-tender:store-credit:{accountId}`).
- Refunds as credit: the provider is the Orders feature's `IOrderStoreCreditRefunder`.
- Admin: the customer's page shows balances and the last 20 changes; `POST Admin/customers/{customerId}/store-credit` (`amount`, `currency`, required `reason`) with `ManageCustomers`. Guests cannot be given credit.
- Storefront: `account/store-credit` (`AccountStoreCredit`).

```csharp
using CrestApps.OrchardCore.Customers.Services;

public sealed class GoodwillCredit
{
    private readonly StoreCreditService _credit;

    public GoodwillCredit(StoreCreditService credit)
    {
        _credit = credit;
    }

    public async Task<bool> GiveAsync(string customerId)
        => await _credit.ChangeAsync(customerId, "USD", 15m, "Late delivery", actor: "support") is not null;
}
```

## Loyalty points (`CrestApps.OrchardCore.Customers.Loyalty`)

- `LoyaltyAccount` (`LoyaltyAccountIndex`): whole `Points`, `Activity` (`LoyaltyActivity` with `LoyaltyActivityKind` `Earned`, `Reversed`, `Spent`, `Returned`, `Adjusted`). `GetEarnedFor(orderId)`.
- Earning: `LoyaltyOrderEventHandler.PaidAsync` gives the signed-in buyer `floor(PointsPerUnit x (Subtotal - non-shipping discounts - what points paid))` once per order; shipping and tax earn nothing. Cancel reverses everything earned; a refund reverses its share.
- Spending: `LoyaltyTenderProvider` (key `loyalty-points`) offers nothing below `MinimumPointsToRedeem`; value is points x `PointValue` rounded down to the cent. Driver `CheckoutLoyalty` at `Aside:8`.
- Manual: `LoyaltyService.ChangeAsync(customerId, points, LoyaltyActivityKind.Adjusted, reason, actor, orderId)`; admin form `POST Admin/customers/{customerId}/loyalty`.
- Storefront: `account/rewards` (`AccountRewards`).

```json
{
  "steps": [
    {
      "name": "Settings",
      "LoyaltySettings": {
        "ProgramName": "Rewards",
        "PointsPerUnit": 1,
        "PointValue": 0.01,
        "MinimumPointsToRedeem": 100
      }
    }
  ]
}
```

## Referrals (`CrestApps.OrchardCore.Customers.Referrals`)

- `ReferralProfile` (`ReferralProfileIndex` on customer and code): an 8-character `Code` and the `Referrals` rewarded (`ReferralRecord`).
- `GET refer/{code}` stores the code in the `orchard-referral` cookie (30 days). When `FriendCouponId` names a redeemable, non-automatic coupon, it generates one single-use `REF-` code (`ICouponStore.GenerateCodesAsync(coupon, 1, "REF-", 1)`) into the discount link cookie; generation is rate limited per address (scope `referral-link`).
- `ReferralCheckoutHandler.ActivatingAsync` records the code on each new checkout (`CheckoutReferral`), except a balance settlement.
- `ReferralOrderEventHandler.PaidAsync` rewards the referrer once per friend: the order has an email, the friend is not the referrer, has no other paid order, and spent at least `MinimumOrderAmount`. The reward is store credit (`RewardAmount`) or points (`RewardPoints`) when that feature is on. A canceled order takes the reward back; a refund does not.
- Storefront: `account/refer` (`AccountReferrals`).

```json
{
  "steps": [
    {
      "name": "Settings",
      "ReferralSettings": {
        "FriendCouponId": null,
        "RewardKind": 1,
        "RewardAmount": 10,
        "RewardPoints": 500,
        "MinimumOrderAmount": 25
      }
    }
  ]
}
```

`ReferralRewardKind`: `0` none, `1` store credit, `2` points. `FriendCouponId` is the coupon's item id (see the coupon catalog in `orchardcore-checkout`).

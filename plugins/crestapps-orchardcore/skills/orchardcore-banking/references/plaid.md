# Plaid Bank Account Linking

The Plaid module (`CrestApps.OrchardCore.Plaid`, feature **Plaid Bank Account Linking**) is Banking's first `IBankAccountLinkProvider` (name `Plaid`). Banking owns the pages and never names a vendor. Projects: `CrestApps.OrchardCore.Plaid.Core` (HTTP client, webhook verifier, link provider, item store, observer, webhook handler; `AddPlaidCore()`) and `CrestApps.OrchardCore.Plaid` (settings, the `BankAccountLink_Plaid` shape, the webhook endpoint, migration). Plaid has no official .NET library; the module calls its JSON API with the named `HttpClient` `Plaid`.

## Set it up

1. In the Plaid dashboard copy the client id and secret of the environment. Ask Plaid for Auth in Production.
2. Under **Tools > Security > Secrets**, add a text secret such as `Plaid.Secret` (`PlaidConstants.DefaultSecretName`). Its value comes from configuration (`OrchardCore:Secrets:Plaid.Secret`) or the secret editor, never from a recipe.
3. Under **Settings > Plaid** (`ManagePlaidSettings`), choose the environment (`Sandbox` or `Production`), enter the client id, pick the secret and save. **Test the connection** asks Plaid for a link token.
4. With a Content Security Policy, allow `https://cdn.plaid.com` for scripts and `https://*.plaid.com` for frames: the `plaid-link` resource is CDN-only.

```json
{
  "steps": [
    { "name": "Feature", "enable": [ "OrchardCore.Secrets", "CrestApps.OrchardCore.Plaid" ], "disable": [] },
    { "name": "Secrets", "Secrets": [ { "Name": "Plaid.Secret", "Store": "Database" } ] },
    {
      "name": "Settings",
      "PlaidSettings": { "Environment": "Sandbox", "ClientId": "your-client-id", "SecretName": "Plaid.Secret" }
    }
  ]
}
```

Changing the environment needs that environment's secret.

## Where people link

| Page | Route | Permission |
|---|---|---|
| **Banking > My Bank Accounts > Add**: **Link your bank with Plaid** (new account) | `banking/my-accounts/link/{provider}` | `ManageOwnBankAccounts` |
| **My Bank Accounts > Edit**: **Link your bank with Plaid instead** (replaces the numbers) | `banking/my-accounts/link/{provider}/{id}` | `ManageOwnBankAccounts`, own accounts |
| Admin account page action bar (secondary): **Link with Plaid** | `banking/accounts/link/{id}/{provider}` | `ManageBankAccounts` |

## The flow

1. `/link/token/create` with products `auth`, country `US`, depository checking and savings, the tenant's webhook URL, and `client_user_id` = SHA-256 of the tenant name and user id (Plaid never sees a user id, name or email). The token is never stored.
2. Plaid Link runs in the `BankAccountLink_Plaid` shape; `onSuccess` posts `{ publicToken, accountId, institutionName }` as Banking's `clientResult`.
3. `/item/public_token/exchange` (never retried), then `/auth/get` for the account's ACH numbers, subtype and mask.
4. Banking's `IBankAccountService.ApplyLink` saves the numbers and sets `Verified` with `VerificationMethod` = `Plaid`.
5. `/item/remove` at once, unless the account number is tokenized.

`PRODUCT_NOT_READY`, a missing number or any Plaid error refuses the link with Plaid's `display_message` and removes the item. Micro-deposit flows are not used: the person types the numbers or uses a prenote instead.

## Tokenized numbers and kept items

Banks that give Plaid a tokenized account number (such as Chase, PNC, US Bank) keep the item as a `PlaidItem` in the `Plaid` collection (`PlaidItemIndex`: `ItemId`, `BankAccountId`) with the access token protected by `ISensitiveValueProtector` (purpose `CrestApps.OrchardCore.Plaid.AccessToken`). `PlaidBankAccountObserver` removes it when the account is deleted, closed or given typed numbers. Kept items are never in a recipe or deployment.

## Webhook

`POST /plaid/webhook` (route `CrestApps.Plaid.Webhook`, anonymous) is sent to Plaid with each link token, so nothing is entered in the dashboard.

1. The `Plaid-Verification` JWT is checked (`ES256`, key by `kid` from `/webhook_verification_key/get` cached a day, `iat` within five minutes, `request_body_sha256` equal to the body hash). Otherwise 400. Bodies over 64 KB are refused.
2. The body is stored in the provider webhook inbox (`IProviderWebhookInbox`, provider `Plaid`, handler `plaid-webhook`); delivery id is the body's SHA-256, so resends are stored once. See `orchardcore-webhooks`.
3. `PlaidWebhookInboxHandler` handles `ITEM: USER_PERMISSION_REVOKED` and `ITEM: USER_ACCOUNT_REVOKED` by moving a Plaid-verified account back to `Unverified` and removing the item. Other webhooks are ignored.

## HTTP

Credentials go in the `PLAID-CLIENT-ID` and `PLAID-SECRET` headers, with `Plaid-Version: 2020-09-14`. The standard resilience handler retries only the calls safe to repeat (`/link/token/create`, `/auth/get`, `/item/remove`, `/webhook_verification_key/get`). Errors become `PlaidApiException` (`error_type`, `error_code`, `request_id`); tokens, numbers and bodies are never logged.

## Another host

`AddPlaidCore()` registers everything except `IPlaidCredentialsProvider` (reads `PlaidSettings` and the secret) and `IPlaidLinkSite` (builds the link token request from the current request), which the module provides and another host can replace.

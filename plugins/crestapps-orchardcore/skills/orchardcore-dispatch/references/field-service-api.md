# Field Service API and Technician Mobile Endpoints

The **Field Service API** feature (`CrestApps.OrchardCore.Dispatch.Api`) maps minimal API groups that require the Orchard Core `Api` authentication scheme (`OrchardCoreConstants.AuthenticationSchemes.Api`, such as an OpenID access token), skip antiforgery, and check the same permission as the matching admin page. Requests and responses are JSON with camelCase names and enum names as strings; errors are RFC 9457 problem details. With OpenAPI on, endpoints are tagged **Work Orders**, **Dispatch**, **Geo** and **Assets**.

## Work Orders at `api/work-orders/v1`

| Endpoint | Permission | Body or query |
|---|---|---|
| `GET types` | `ViewWorkOrders` | |
| `GET checklists` | `ViewWorkOrders` | |
| `GET work-orders` | `ViewWorkOrders` | `?status=&open=&q=&skip=&take=` (newest first, with the count; `take` up to 200) |
| `GET work-orders/{id}` | `ViewWorkOrders` | |
| `POST work-orders` | `ManageWorkOrders` | `typeId`, `summary`, `description`, `priority`, `customerId`, `accountId`, `contactId`, `contact`, `serviceAddress`, `accessNotes`, `assetIds`, `earliestUtc`, `latestUtc`; optional `Idempotency-Key` header |
| `POST work-orders/{id}/status` | `ManageWorkOrders` | `{ "status", "subStatusId", "note" }` |
| `POST work-orders/{id}/notes` | `ManageWorkOrders` | `{ "text" }` |
| `POST work-orders/{id}/answers` | `ManageWorkOrders` | `{ "checklistIndex", "questionId", "value", "answeredUtc" }` |
| `POST work-orders/{id}/parts` | `ManageWorkOrders` | `{ "lineId", "quantity", "locationId" }` |
| `POST work-orders/{id}/signatures` | `ManageWorkOrders` | `{ "name", "imagePath" }` |
| `POST work-orders/{id}/attachments` | `ManageWorkOrders` | `{ "path" }` (a file already in the media library) |
| `POST work-orders/{id}/complete` | `ManageWorkOrders` | Completes, or answers with what is missing |
| `GET agreements` | `ManageServiceAgreements` | `?status=&q=&skip=&take=` (Service Agreements feature on) |
| `GET agreements/{id}` | `ManageServiceAgreements` | |

Create a work order once per key (sending it again returns the first work order):

```http
POST /api/work-orders/v1/work-orders
Authorization: Bearer {access-token}
Idempotency-Key: call-2027-03-14-0042
Content-Type: application/json

{
  "typeId": "4f4cr6a68f529ayad8d5he25xq",
  "summary": "No heat in the flat",
  "priority": "High",
  "contact": { "name": "Dana Reyes", "phone": "+16175550100", "email": "dana@example.com" },
  "serviceAddress": { "addressLine1": "12 Elm Street", "city": "Boston", "region": "MA", "postalCode": "02118", "country": "US" },
  "accessNotes": "Ring flat 3."
}
```

## Visits at `api/dispatch/v1`

| Endpoint | Permission | Body or query |
|---|---|---|
| `GET board` | `ViewDispatch` | `?day=&teamId=` (today in the dispatch time zone by default; a team includes its sub-teams) |
| `GET visits` | `ViewDispatch` | `?from=&to=&workerId=` (UTC, at most 31 days apart) or `?unscheduled=true` |
| `GET visits/{id}` | `ViewDispatch` | With history and notices |
| `GET visits/{id}/candidates` | `ViewDispatch` | `?from=&to=` |
| `POST visits/{id}/assign` | `ManageDispatch` | `{ "workerIds", "startUtc", "durationMinutes", "pinned" }` (same locks as the board) |
| `POST visits/{id}/unassign` | `ManageDispatch` | |
| `POST visits/{id}/status` | `ManageDispatch` | `{ "status", "note" }` |
| `POST optimize` | `ManageDispatch` | `{ "day": "yyyy-MM-dd", "workerIds", "includeUnscheduled" }`; no workers means everyone |
| `POST optimize/apply` | `ManageDispatch` | `{ "moves" }` from a proposal |
| `GET positions` | `ViewDispatch` | Latest positions from the last 30 minutes (Technician Mobile) |

```http
POST /api/dispatch/v1/visits/{visitId}/assign
Authorization: Bearer {access-token}
Content-Type: application/json

{ "workerIds": [ "{leadWorkerId}" ], "startUtc": "2027-03-15T14:00:00Z", "pinned": false }
```

## Geo at `api/geo/v1`

| Endpoint | Permission | Query |
|---|---|---|
| `GET geocode` | `ViewDispatch` | `?address=` |
| `GET territories/resolve` | `ViewDispatch` | `?latitude=&longitude=&postalCode=` |

## Assets at `api/assets/v1`

Mapped only when `CrestApps.OrchardCore.Assets` is on.

| Endpoint | Permission | Body or query |
|---|---|---|
| `GET assets` | `ViewAssets` | `?q=&typeId=&ownerId=&skip=&take=` |
| `GET assets/{id}` | `ViewAssets` | |
| `GET assets/{id}/readings` | `ViewAssets` | `?take=` |
| `POST assets/{id}/readings` | `ManageAssets` | `{ "meter", "value", "utc" }`; a cumulative meter can't go back |

## Technician Mobile at `api/dispatch/v1/mobile`

The Field page (`~/Admin/my-work/field`) calls the same handlers at `my-work/field/api` with its cookie and an antiforgery header from `my-work/field/api/token`; other apps use `api/dispatch/v1/mobile` with the `Api` scheme. The caller needs **Use the My Work portal** and a worker profile.

| Endpoint | Body | Answer |
|---|---|---|
| `GET pack` | | The caller's visits from today through `OfflineDays`, with work orders, next statuses, stock locations and location settings |
| `POST sync` | `{ "commands": [...] }`, oldest first, at most `MaxCommandsPerSync` | `{ "results": [ { "key", "visitId", "outcome", "code", "reason" } ] }` |
| `POST consent` | `{ "given": true }` | The consent record |
| `POST pings` | `{ "pings": [ { "utc", "latitude", "longitude", "accuracyMeters", "speed", "heading" } ] }` | How many were kept and why the rest weren't |

A `FieldCommand` has a device-made `key`, `kind` (`Status`, `Answer`, `Note`, `Part`, `Signature`, `Photo`, `FollowUp`), `visitId`, `utc`, and the fields for its kind: `status` and `text` (status), `checklistIndex`, `questionId`, `value` (answer), `text` (note, follow-up summary), `lineId`, `quantity`, `locationId` (part), `name` and `data` as a data URL (signature, photo; a signature with only `name` is a typed name).

```json
{
  "commands": [
    { "key": "d7f1c2a0-01", "kind": "Status", "visitId": "{visitId}", "utc": "2027-03-15T14:02:00Z", "status": "EnRoute" },
    { "key": "d7f1c2a0-02", "kind": "Answer", "visitId": "{visitId}", "utc": "2027-03-15T14:40:00Z", "checklistIndex": 0, "questionId": "flue-co", "value": "12" },
    { "key": "d7f1c2a0-03", "kind": "Part", "visitId": "{visitId}", "utc": "2027-03-15T14:45:00Z", "lineId": "filter", "quantity": 1, "locationId": "{vanStockLocationId}" }
  ]
}
```

Outcomes are `Applied`, `Duplicate` (a key seen before, with the first result from its `FieldCommandReceipt`) or `Rejected` with a code: `moved` (the visit is no longer the caller's), `canceled`, `missing`, `stale` (a later answer already exists) or `invalid`. Photos and signatures are saved through `IFieldFileStore` (the media library under `work-orders/{number}/`, named by the command key, at most `MaxFileKilobytes`).

Positions are kept only after consent (`WorkerLocationConsent`, versioned by `ConsentNoticeVersion`), only while a visit is under way or still to come today, and never off shift. The **Dispatch: location retention** task (hourly) removes positions after `LocationRetentionDays` and receipts after `ReceiptRetentionDays`.

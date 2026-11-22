# Open Badges 3.0 Credentials in Detail

Supporting detail for `orchardcore-learning-certifications`. Feature `CrestApps.OrchardCore.Learning.Credentials` implements Open Badges 3.0 (issuer) on the W3C Verifiable Credentials Data Model 2.0, with the JWT proof format and a W3C Bitstring Status List. It makes no certification or conformance claim (1EdTech's issuer certification needs a membership account and was not run).

## The Credential

Each `CredentialAward` carries an `OpenBadgeCredential`:

- `@context`: `https://www.w3.org/ns/credentials/v2`, then `https://purl.imsglobal.org/spec/ob/v3p0/context-3.0.3.json`; `type`: `VerifiableCredential`, `OpenBadgeCredential`.
- `id`: `https://<site>/learning/credentials/awards/<code>` (128 random bits); `issuer`: a `Profile` with id `https://<site>/learning/credentials/issuer`.
- `validFrom`, `awardedDate`, `validUntil` (when the definition has `ValidityMonths`), `name`.
- `credentialSubject`: an `AchievementSubject` whose `identifier` is a hashed `emailAddress` `IdentityObject` (`sha256$` of the lowercased address plus the salt) and the `achievement` (`https://<site>/learning/credentials/achievements/<definition id>`). No subject `id`, so the token has no `sub`.
- `credentialSchema`: the Open Badges achievement credential schema, type `1EdTechJsonSchemaValidator2019`.
- `credentialStatus`: a `BitstringStatusListEntry` with `statusPurpose` `revocation`, the award's `statusListIndex` and `statusListCredential` `https://<site>/learning/credentials/status/<n>`.

The proof is a compact JWS (`alg` `RS256`, `typ` `JWT`, `kid` = `https://<site>/learning/credentials/keys/<kid>`) whose payload adds `iss`, `jti`, `nbf` and `exp`. Status lists hold 131,072 entries, slots are random and a list fills to half. Data Integrity proofs are not built.

## Public Endpoints

All anonymous, with `X-Content-Type-Options: nosniff`, `X-Robots-Tag: noindex, nofollow`, `Access-Control-Allow-Origin: *`, and rate limited (group `LearningCredentials`, 120 per minute per address) when Rate Limits is on.

| Address | Answers |
|---|---|
| `GET /learning/credentials/awards/{code}` | The credential as JSON-LD (`application/ld+json`) |
| `GET /learning/credentials/awards/{code}/jwt` | The signed credential (`application/jwt`) |
| `GET /learning/credentials/achievements/{definitionId}` | The published achievement |
| `GET /learning/credentials/issuer` | The issuer profile |
| `GET /learning/credentials/jwks` | Public keys of the key set |
| `GET /learning/credentials/keys/{kid}` | One public key |
| `GET /learning/credentials/status/{n}` | The `BitstringStatusListCredential`, computed per request (`no-store`); signed when the request accepts `application/vc+jwt` or `application/jwt`, or at `/status/{n}/jwt` |
| `GET /learning/credentials/verify/{code}` | The verification page |

The base address is the site's **Base URL**; the request host is never used, because it is the client's choice.

## Verification Page

`/learning/credentials/verify/{code}` checks the token signature against the key its `kid` names, `jti` and `iss` against the issued credential, expiry and the revocation bit. It shows valid, expired, revoked, not valid or not found, the achievement, the issuer, dates in the site time zone and the recipient's name only when they chose **Show my name**. It never shows the revocation reason.

## Learner and Admin Pages

- **My credentials** (`/learning/credentials`, Access learning): download JSON-LD and JWT (`/learning/credentials/mine/{id}/{format}`), share link, **Show my name** / **Hide my name**.
- **Learning > Credential Awards**: search by recipient user name, filter by credential, status and issue dates; award page with the check; **Award a credential** and **Revoke**, each with a reason.

## Personal Data

Export lists awards with the credential and token. Erasure revokes each award (reason "Personal data erased"), keeps its slot revoked, and removes the recipient's user id, identity hash, credential, token and award reason. Copies already downloaded stay signed and show as revoked to verifiers that check the status list.

## Not Built

Data Integrity proofs, the 1EdTech Revocation List, Open Badges host or displayer APIs, CLR 2.0, `EndorsementCredential`, DID or `sourcedId` recipients, wallet hand-off protocols and QR codes, JSON-LD expansion, and Audit Trail events.

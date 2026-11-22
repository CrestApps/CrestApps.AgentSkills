# Invitations, Account Team and E-mail Domains

## Invite Someone from Code

`IAccountInvitationService` is registered by the Account Members feature (`CrestApps.OrchardCore.Accounts.Members`). The account must have `AccountMembersPart`. The token in the result is returned once; only its hash is stored, so build the link right away.

```csharp
public sealed class CourseSeatInviter
{
    private readonly IAccountInvitationService _invitations;
    private readonly LinkGenerator _linkGenerator;

    public CourseSeatInviter(IAccountInvitationService invitations, LinkGenerator linkGenerator)
    {
        _invitations = invitations;
        _linkGenerator = linkGenerator;
    }

    public async Task<string> InviteAsync(HttpContext httpContext, string accountId, string email, string seatId)
    {
        var result = await _invitations.InviteAsync(new AccountInvitationRequest
        {
            AccountId = accountId,
            Email = email,
            Role = AccountMemberRole.Member,
            InvitedBy = httpContext.User.Identity?.Name,
            Source = "CourseSeat",
            SourceId = seatId,
            Lifetime = TimeSpan.FromDays(7),
        }, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return null;
        }

        return _linkGenerator.GetUriByRouteValues(httpContext, "AccountInvitation", new { token = result.Token });
    }
}
```

- A new invitation revokes an earlier pending one for the same account, e-mail and `Source`/`SourceId`, and announces each with `RevokedAsync`.
- An e-mail that is already a member is refused (`AccountInvitationResult.Error`).
- `AcceptAsync(token, userId)` needs a signed-in user whose confirmed e-mail is the invited one and who belongs to no other account. When the e-mail is not confirmed, the result has `NeedsEmailConfirmation`.

## React to Invitations

Observers are called in the request that made the change. Every method has a default no-op body, so implement only what you need.

```csharp
public sealed class SeatInvitationObserver : IAccountInvitationObserver
{
    private readonly ILogger _logger;

    public SeatInvitationObserver(ILogger<SeatInvitationObserver> logger)
    {
        _logger = logger;
    }

    public Task AcceptedAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
    {
        if (invitation.Source == "CourseSeat")
        {
            _logger.LogInformation("Seat {SeatId} taken by {UserId}.", invitation.SourceId, invitation.AcceptedUserId);
        }

        return Task.CompletedTask;
    }

    public Task ExpiredAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// Startup (feature depending on CrestApps.OrchardCore.Accounts.Members):
// services.AddAccountInvitationObserver<SeatInvitationObserver>();
```

`ExpiredAsync` runs from `AccountInvitationExpiryBackgroundTask` (schedule `25 * * * *`), which calls `ExpireDueAsync`.

## Workflow Activities

Registered with `OrchardCore.Workflows` on, category **Accounts**:

| Activity | Kind | Outcomes | Notes |
|---|---|---|---|
| `AccountInvitationSentEvent` (**Account Invitation Sent**) | Event | `Done` | Input: `InvitationId`, `AccountId`, `Email`, `Role`, `UserId`, `Source`, `SourceId`; correlation id is the invitation id. |
| `AccountInvitationAcceptedEvent` (**Account Invitation Accepted**) | Event | `Done` | Same input; `UserId` is the user who accepted. |
| `InviteToAccountTask` (**Invite to Account**) | Task | `Done`, `Failed` | Properties `AccountId` and `Email` (workflow expressions) and `Role`. `InvitedBy` is `Workflow`. `LastResult` is the invitation id, or the error. |

The events are triggered after the request ends. With CrestApps Recipes on, the three activities have workflow activity schemas.

## Storefront Pages

| Route | Name | Access |
|---|---|---|
| `GET account/invitations/{token}` | `AccountInvitation` | Anyone with the link; sends `Referrer-Policy: no-referrer`. |
| `POST account/invitations/{token}` | | Signed-in user; accepts. |
| `GET account/members` | `AccountMembers` | A member of an account; 404 for anyone else. |
| `POST account/members/invite` | | An account `Admin` member (form fields `email`, `role`, `message`). |
| `POST account/members/invitations/{id}/revoke` | | An account `Admin` member of the invitation's account. |

The invitation e-mail (Email feature on) carries the absolute link on the site's base URL, or on the requesting host when no base URL is set, and the expiry in the site's time zone.

## Account with Team, Domains and a Billing Location

```json
{
  "steps": [
    {
      "name": "Accounts",
      "Accounts": [
        {
          "ContentItemId": "4xf4a7cq978mj04mg6nfygm3dd",
          "ContentType": "Account",
          "DisplayText": "Contoso",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Contoso" },
          "AccountPart": {},
          "AccountTeamPart": {
            "OwnerStaffId": "4s7f2k9q1m3n5p7r9t1v3x5z7b",
            "TeamStaffIds": [ "4s8g3l0r2n4p6q8s0u2w4y6a8c" ]
          },
          "AccountEmailDomainsPart": {
            "Domains": [ "contoso.com", "contoso.co.uk" ]
          },
          "AccountLocationsPart": {
            "Locations": [
              {
                "LocationId": "hq",
                "Name": "Head office",
                "Address": { "Country": "US", "AddressLine1": "1 Main Street", "City": "Reno", "Region": "NV", "PostalCode": "89501" },
                "IsBilling": true
              }
            ]
          }
        }
      ]
    }
  ]
}
```

- `OwnerStaffId` and `TeamStaffIds` are staff member ids from the Staff module (see `orchardcore-staff`); they must exist on the target site.
- `AccountEmailDomainsPartHandler` refuses an import that lists a public mail domain or a domain another account already lists.

## Find the Account of an E-mail Address

```csharp
public sealed class InboundRequestAccountResolver
{
    private readonly IAccountEmailDomains _emailDomains;

    public InboundRequestAccountResolver(IAccountEmailDomains emailDomains)
    {
        _emailDomains = emailDomains;
    }

    // Returns null when no published account, or more than one, lists the domain.
    public Task<string> ResolveAsync(string fromAddress, CancellationToken cancellationToken)
        => _emailDomains.FindAccountIdAsync(fromAddress, cancellationToken);
}
```

`IAccountEmailDomains` is registered by `CrestApps.OrchardCore.Accounts.EmailDomains`; make your feature depend on it. Add tenant-specific public domains in configuration:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Accounts": {
        "EmailDomains": {
          "PublicDomains": [ "example-isp.net" ]
        }
      }
    }
  }
}
```

# Auto Setup for Tests

Auto Setup provisions tenants from configuration on their first request, so an agent never has to drive the setup wizard. It is the recommended way to get a test site.

## Register the feature

`OrchardCore.AutoSetup` ships in `OrchardCore.Application.Cms.Targets` (and the Core targets), but `dotnet new occms` does not register it. Add it in the web project's `Program.cs`:

```csharp
builder.Services
    .AddOrchardCms()
    .AddSetupFeatures("OrchardCore.AutoSetup");
```

The Orchard Core repository's `OrchardCore.Cms.Web` already does this. Keep the Auto Setup configuration out of production, or register the feature only in development.

## How it works

- The feature reads the `OrchardCore:AutoSetup` section.
- Its middleware runs setup when the requested tenant is uninitialized (for example a fresh `App_Data`).
- With an empty `AutoSetupPath` the first request to the tenant triggers it; otherwise the request must hit that path (`/autosetup`, `/mytenant/autosetup`).
- Success is logged as `The AutoSetup successfully provisioned the site '{SiteName}'.`

## Environment variables

Keys are `OrchardCore__AutoSetup__Tenants__{index}__{Option}`; `__` separates configuration levels. The deprecated `OrchardCore__OrchardCore_AutoSetup__` prefix still works.

### bash

Values are bare; with `KEY="value"` inline, the quotes become part of the value and corrupt the password or email.

```bash
WEB_PROJECT=src/MySite.Web
PORT=$(cat .orchardcore-port)

OrchardCore__AutoSetup__AutoSetupPath= \
OrchardCore__AutoSetup__Tenants__0__ShellName=Default \
OrchardCore__AutoSetup__Tenants__0__SiteName=TestSite \
OrchardCore__AutoSetup__Tenants__0__SiteTimeZone=America/Los_Angeles \
OrchardCore__AutoSetup__Tenants__0__AdminUsername=admin \
OrchardCore__AutoSetup__Tenants__0__AdminEmail=admin@test.com \
OrchardCore__AutoSetup__Tenants__0__AdminPassword=Password1! \
OrchardCore__AutoSetup__Tenants__0__DatabaseProvider=Sqlite \
OrchardCore__AutoSetup__Tenants__0__RecipeName=Blog \
dotnet run --project "$WEB_PROJECT" --no-build --urls "http://localhost:$PORT" > autosetup-console.log 2>&1 &

curl -s -o /dev/null "http://localhost:$PORT/"
grep -m1 "successfully provisioned" autosetup-console.log
```

### PowerShell

Assigning a quoted string to `$env:` stores the value without quotes, so quoting is fine here.

```powershell
$port = Get-Content .orchardcore-port
$env:OrchardCore__AutoSetup__AutoSetupPath = ""
$env:OrchardCore__AutoSetup__Tenants__0__ShellName = "Default"
$env:OrchardCore__AutoSetup__Tenants__0__SiteName = "TestSite"
$env:OrchardCore__AutoSetup__Tenants__0__SiteTimeZone = "America/Los_Angeles"
$env:OrchardCore__AutoSetup__Tenants__0__AdminUsername = "admin"
$env:OrchardCore__AutoSetup__Tenants__0__AdminEmail = "admin@test.com"
$env:OrchardCore__AutoSetup__Tenants__0__AdminPassword = "Password1!"
$env:OrchardCore__AutoSetup__Tenants__0__DatabaseProvider = "Sqlite"
$env:OrchardCore__AutoSetup__Tenants__0__RecipeName = "Blog"
dotnet run --project $webProject --no-build --urls "http://localhost:$port"
```

The same keys work in user secrets (`dotnet user-secrets set "OrchardCore:AutoSetup:Tenants:0:ShellName" "Default"`) or in the `environmentVariables` of a `launchSettings.json` profile started with `dotnet run --launch-profile <name>`.

## Tenant options

| Option | Required | Notes |
|--------|----------|-------|
| `ShellName` | yes | `Default` for the root tenant; the list must contain it. |
| `SiteName` | yes | Display name. |
| `SiteTimeZone` | yes | For example `America/Los_Angeles`. |
| `AdminUsername`, `AdminEmail`, `AdminPassword` | yes | The password must satisfy the password policy. |
| `DatabaseProvider` | yes | `Sqlite` needs no connection string; others are `SqlConnection`, `Postgres`, `MySql`. |
| `RecipeName` | yes | A setup recipe, for example `Blog`, `Headless`, `Agency`, `SaaS`, `Blank`, or your own. |
| `DatabaseConnectionString` | non-SQLite | |
| `DatabaseTablePrefix`, `DatabaseSchema` | no | For tenants sharing a database. |
| `RequestUrlHost`, `RequestUrlPrefix` | no | For additional tenants. |
| `FeatureProfile` | no | Requires `OrchardCore.Tenants.FeatureProfiles`. |

To test your own setup recipe, point `RecipeName` at its `name`.

## Confirming success

- The log contains the success line.
- `App_Data/Sites/Default/` exists in the web project.
- `/` returns the home page (or a redirect to it) and `/Login` returns `200`.

## Troubleshooting

| Symptom | Cause and fix |
|---------|---------------|
| `The SiteTimeZone field is required.` | Add `...__Tenants__0__SiteTimeZone`. |
| `The single 'Default' tenant should be provided.` | The first tenant's `ShellName` must be `Default`. |
| Setup never triggers | `App_Data` already holds an initialized tenant, or `AddSetupFeatures("OrchardCore.AutoSetup")` is missing. Stop the site and delete `App_Data`. |
| Settings ignored | The section was not bound; check the `OrchardCore__AutoSetup__` prefix and the `Tenants__0__` index. |
| Login fails with the configured password | A value was quoted in the bash inline form. |
| `DatabaseProvider` errors | Use `Sqlite`, `SqlConnection`, `Postgres` or `MySql`, with a connection string for the server providers. |

# Orchard Core Tenants Examples

## Example 1: Multi-Tenant Blog Setup

Setting up a SaaS application with a Blog tenant:

```bash
# Create the project
dotnet new occms -n MyMultiSite
cd MyMultiSite
dotnet run

# Set up the Default tenant with the SaaS recipe
# Navigate to https://localhost:5001 and complete setup:
# - Recipe: SaaS
# - Database: Sqlite
# - Admin: admin / Password123!
```

After setup, create a Blog tenant:
1. Go to **Multi-Tenancy > Tenants**.
2. Click **Add Tenant**.
3. Fill in:
   - **Name**: Blog
   - **URL Prefix**: blog
   - **Database Provider**: Sqlite
4. Click **Create**, then **Setup** on the new tenant.
5. During tenant setup:
   - **Recipe**: Blog
   - **Site Name**: Company Blog
   - **Admin**: admin / Password123!
6. Access the blog at `https://localhost:5001/blog`.

## Example 2: Creating Multiple Tenants via the Tenants API

There is no recipe step that creates tenants. Use the Tenants API (or the **Create Tenant Task** workflow activity) with a token that has `ManageTenants`:

```bash
for tenant in Marketing Engineering Sales; do
  prefix=$(echo "$tenant" | tr '[:upper:]' '[:lower:]')
  curl -X POST https://localhost:5001/api/tenants/create \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer {{token}}" \
    -d "{ \"Name\": \"$tenant\", \"RequestUrlPrefix\": \"$prefix\", \"Description\": \"$tenant department site\", \"RecipeName\": \"Blog\", \"DatabaseProvider\": \"Sqlite\" }"
done
```

Each request is validated with `ITenantValidator`; a `400 Bad Request` lists the errors (invalid name, duplicate prefix and host, unknown feature profile).

## Example 3: Tenant-Aware Service

```csharp
using OrchardCore.Environment.Shell;

public sealed class TenantBrandingService
{
    private readonly ShellSettings _shellSettings;

    public TenantBrandingService(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public string GetTenantLogo()
    {
        return _shellSettings.Name switch
        {
            "Marketing" => "/media/logos/marketing.png",
            "Engineering" => "/media/logos/engineering.png",
            "Sales" => "/media/logos/sales.png",
            _ => "/media/logos/default.png"
        };
    }
}
```

## Example 4: Generated Tenant Database Isolation

Use tenant-aware Fluid patterns to generate a prefix or schema when tenants
share a database:

```json
{
  "OrchardCore": {
    "Tenants": {
      "RequireTablePrefix": true,
      "TablePrefixPattern": "tenant_{{ ShellSettings.Name }}",
      "SchemaPattern": "{{ ShellSettings.Name }}"
    }
  }
}
```

The generated and manually entered values must be valid SQL identifiers.
When `DatabaseProvider` is configured, the provider cannot be changed in the
Setup or Tenants UI. If both `DatabaseProvider` and `ConnectionString` are
configured, the connection string is also fixed. Setup asks for a connection
string only when the selected provider requires one and no saved value exists.

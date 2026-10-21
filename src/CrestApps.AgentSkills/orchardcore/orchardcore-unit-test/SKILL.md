---
name: orchardcore-unit-test
description: Skill for writing automated tests for your own Orchard Core modules and themes outside the Orchard Core repository. Covers test project setup with xUnit and Moq, unit testing content part handlers, display drivers with a stub IUpdateModel, data migrations with a mocked IContentDefinitionManager, permission providers and services, building content items and definitions in tests, and in-process integration tests that host the real web project with WebApplicationFactory, provision a tenant through Auto Setup into a temporary App_Data, and run code in the tenant with IShellHost and ShellScope. Use this skill when requests mention Orchard Core unit tests, testing a module, mocking Orchard Core services, testing a driver, handler or migration, or integration tests with a tenant. Strong matches include xUnit, Moq, WebApplicationFactory, Microsoft.AspNetCore.Mvc.Testing, OrchardCore.AutoSetup, IShellHost, ShellScope, ContentPartHandler, ContentPartDisplayDriver, IUpdateModel, DataMigration.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Module Testing

Orchard Core does not ship a testing package. Its own harness (`SiteContext`, `OrchardTestFixture`, `BlogContext` in `test/OrchardCore.Tests`) is not published to NuGet, so do not reference or recreate those names. Everything below uses shipped Orchard Core packages plus standard .NET test packages.

| Testing | Approach |
|---------|----------|
| Logic in a service, handler, driver, migration or permission provider | Unit test with xUnit and Moq; construct the class directly. |
| Behavior that needs the real pipeline (content storage, YesSql indexes, recipes, routes, APIs) | Integration test hosting the web project with `WebApplicationFactory` and an auto-provisioned tenant. |
| Pages in a browser | `orchardcore-tester` (playwright-cli against a running site). |

## Test Project Setup

```bash
dotnet new xunit -n MyModule.Tests
cd MyModule.Tests
dotnet add package Moq
dotnet add reference ../MyModule/MyModule.csproj

# Integration tests only: reference the web project too.
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add reference ../MySite.Web/MySite.Web.csproj
```

- Use the latest stable package versions; keep the Orchard Core packages at the version the site uses (they come through the module and web project references).
- Name classes `{Subject}Tests` and methods `{Action}_{Condition}_{ExpectedResult}`, and follow Arrange, Act, Assert.
- Use `[Fact]` for one case and `[Theory]` with `[InlineData]` or `[MemberData]` for parameter sets.

## Unit Testing a Content Part Handler

Content part handlers are plain classes. Build the content type definition with the metadata builders, mock what the handler injects, and call the handler method directly.

```csharp
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;

public sealed class ProductPartHandlerTests
{
    [Fact]
    public async Task PublishingAsync_EmptySku_GeneratesSku()
    {
        // Arrange
        var typeDefinition = new ContentTypeDefinitionBuilder()
            .WithName("Product")
            .WithPart(nameof(ProductPart), part => part.WithSettings(new ProductPartSettings { SkuPrefix = "P-" }))
            .Build();

        var definitionManager = new Mock<IContentDefinitionManager>();
        definitionManager
            .Setup(manager => manager.GetTypeDefinitionAsync("Product"))
            .ReturnsAsync(typeDefinition);

        var handler = new ProductPartHandler(definitionManager.Object, Mock.Of<IStringLocalizer<ProductPartHandler>>());

        var contentItem = new ContentItem { ContentItemId = "item-1", ContentType = "Product" };
        var part = contentItem.GetOrCreate<ProductPart>();
        part.ContentItem = contentItem;

        // Act
        await handler.PublishingAsync(new PublishContentContext(contentItem, null), part);

        // Assert
        Assert.StartsWith("P-", part.Sku);
    }
}
```

Other handler contexts follow the same shape, for example `new UpdateContentContext(contentItem)` for `UpdatedAsync`.

## Unit Testing a Display Driver

Call `UpdateAsync` with an `UpdatePartEditorContext` whose updater is a stub `IUpdateModel` that fills the view model, then assert on the part and on `ModelState`. No shape factory is needed because the returned shape result is not rendered.

```csharp
using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;

public sealed class ProductPartDisplayDriverTests
{
    [Fact]
    public async Task UpdateAsync_MissingSku_AddsModelError()
    {
        var driver = new ProductPartDisplayDriver();
        var part = new ProductPart { Sku = "P-1" };

        var context = CreateUpdateContext(new StubUpdater(sku: ""));

        await driver.UpdateAsync(part, context);

        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Equal("P-1", part.Sku);
    }

    private static UpdatePartEditorContext CreateUpdateContext(IUpdateModel updater)
    {
        var typePartDefinition = new ContentTypePartDefinition(
            nameof(ProductPart),
            new ContentPartDefinition(nameof(ProductPart)),
            new JsonObject());

        var editorContext = new UpdateEditorContext(null, string.Empty, false, string.Empty, null, null, updater);

        return new UpdatePartEditorContext(typePartDefinition, editorContext);
    }

    private sealed class StubUpdater : IUpdateModel
    {
        private readonly string _sku;

        public StubUpdater(string sku)
        {
            _sku = sku;
        }

        public ModelStateDictionary ModelState { get; } = new();

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model) where TModel : class
            => BindAsync(model);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix) where TModel : class
            => BindAsync(model);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params Expression<Func<TModel, object>>[] includeExpressions) where TModel : class
            => BindAsync(model);

        public bool TryValidateModel(object model) => true;

        public bool TryValidateModel(object model, string prefix) => true;

        private Task<bool> BindAsync<TModel>(TModel model) where TModel : class
        {
            if (model is ProductPartViewModel viewModel)
            {
                viewModel.Sku = _sku;
            }

            return Task.FromResult(true);
        }
    }
}
```

When the driver injects `IStringLocalizer<T>` or `IHtmlLocalizer<T>`, pass a small stub that returns the key as the value (see `references/testing-examples.md`) so `S["..."]` produces readable messages.

## Unit Testing a Data Migration

Migration steps are public methods. Mock `IContentDefinitionManager`, run the step, and assert on the returned version and the stored definitions. `AlterTypeDefinitionAsync` and `AlterPartDefinitionAsync` are extension methods that load the definition (`LoadTypeDefinitionAsync`, `null` for a new type) and save it with `StoreTypeDefinitionAsync`/`StorePartDefinitionAsync`, so verify those interface calls; Moq cannot verify extension methods.

```csharp
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

public sealed class ProductMigrationsTests
{
    [Fact]
    public async Task CreateAsync_NewTenant_ReturnsLatestVersion()
    {
        var definitionManager = new Mock<IContentDefinitionManager>();
        var migrations = new ProductMigrations(definitionManager.Object);

        var version = await migrations.CreateAsync();

        Assert.Equal(2, version);
        definitionManager.Verify(
            manager => manager.StoreTypeDefinitionAsync(It.Is<ContentTypeDefinition>(definition =>
                definition.Name == "Product" &&
                definition.Parts.Any(part => part.Name == "ProductPart"))),
            Times.Once);
    }
}
```

For an `UpdateFromXAsync` that edits existing definitions, set up `LoadTypeDefinitionsAsync()` or `LoadTypeDefinitionAsync(name)` to return definitions built with `new ContentTypeDefinitionBuilder()...Build()`. Migrations that use `SchemaBuilder` (index tables) are best covered by an integration test, which runs every migration on a fresh tenant.

## Unit Testing a Permission Provider

```csharp
using OrchardCore.Security.Permissions;

public sealed class PermissionsTests
{
    [Fact]
    public async Task GetPermissionsAsync_Always_ReturnsManageProducts()
    {
        var provider = new ProductPermissionProvider();

        var permissions = await provider.GetPermissionsAsync();

        Assert.Contains(permissions, permission => permission.Name == ProductPermissions.ManageProducts.Name);
        Assert.Contains(provider.GetDefaultStereotypes(), stereotype => stereotype.Name == "Administrator");
    }
}
```

## Integration Tests with a Real Tenant

Host the web project in memory with `WebApplicationFactory<Program>`, point `App_Data` at a temporary folder, and let Auto Setup provision the Default tenant on the first request.

1. Make `Program` visible to the test project by adding `public partial class Program;` at the end of the web project's `Program.cs`.
2. Register Auto Setup for the test environment (the module ships in `OrchardCore.Application.Cms.Targets`):

```csharp
var orchard = builder.Services.AddOrchardCms();

if (builder.Environment.IsEnvironment("Testing"))
{
    orchard.AddSetupFeatures("OrchardCore.AutoSetup");
}
```

3. Write the factory and tests.

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

public sealed class SiteFactory : WebApplicationFactory<Program>
{
    private readonly string _appDataPath = Path.Combine(Path.GetTempPath(), "orchard-tests", Guid.NewGuid().ToString("n"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string>
        {
            ["OrchardCore:AutoSetup:AutoSetupPath"] = "",
            ["OrchardCore:AutoSetup:Tenants:0:ShellName"] = ShellSettings.DefaultShellName,
            ["OrchardCore:AutoSetup:Tenants:0:SiteName"] = "Test Site",
            ["OrchardCore:AutoSetup:Tenants:0:SiteTimeZone"] = "UTC",
            ["OrchardCore:AutoSetup:Tenants:0:AdminUsername"] = "admin",
            ["OrchardCore:AutoSetup:Tenants:0:AdminEmail"] = "admin@test.com",
            ["OrchardCore:AutoSetup:Tenants:0:AdminPassword"] = "Password1!",
            ["OrchardCore:AutoSetup:Tenants:0:DatabaseProvider"] = "Sqlite",
            ["OrchardCore:AutoSetup:Tenants:0:RecipeName"] = "MySite.Tests",
        }));

        // Keep each factory's tenants and SQLite files out of the web project.
        builder.ConfigureServices(services => services.PostConfigure<ShellOptions>(options =>
            options.ShellsApplicationDataPath = _appDataPath));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_appDataPath))
        {
            try
            {
                Directory.Delete(_appDataPath, recursive: true);
            }
            catch (IOException)
            {
                // SQLite files can stay locked briefly; the temp folder is disposable.
            }
        }
    }
}
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

public sealed class ProductIntegrationTests : IClassFixture<SiteFactory>
{
    private readonly SiteFactory _factory;

    public ProductIntegrationTests(SiteFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAsync_PublishedProduct_GetsSku()
    {
        // The first request provisions the tenant.
        var client = _factory.CreateClient();
        (await client.GetAsync("/")).EnsureSuccessStatusCode();

        var shellHost = _factory.Services.GetRequiredService<IShellHost>();
        var scope = await shellHost.GetScopeAsync(ShellSettings.DefaultShellName);

        await scope.UsingAsync(async shellScope =>
        {
            var contentManager = shellScope.ServiceProvider.GetRequiredService<IContentManager>();

            var product = await contentManager.NewAsync("Product");
            await contentManager.CreateAsync(product, VersionOptions.Published);

            Assert.False(string.IsNullOrEmpty(product.As<ProductPart>().Sku));
        });
    }
}
```

- Resolve tenant services (`IContentManager`, `ISession`, your module services) only inside `UsingAsync`; `_factory.Services` is the host container and holds host services such as `IShellHost`.
- Use a dedicated setup recipe for tests (here `MySite.Tests`, with `"issetuprecipe": true`) that enables your module's features and defines the content it needs (see `orchardcore-recipes`).
- Each `SiteFactory` instance gets its own `App_Data`, so test classes can run in parallel. Tests sharing one fixture share the tenant; do not assume an empty database.
- Call HTTP endpoints with `client`; admin pages need an authenticated cookie and antiforgery tokens, so prefer testing services through the scope and APIs through `api/...` with a bearer token.

## Running Tests

```bash
dotnet test
dotnet test --filter "FullyQualifiedName~ProductPart"
```

## Gotchas

- `ContentItem.GetOrCreate<TPart>()` returns a detached part; set `part.ContentItem` before passing it to code that reads `part.ContentItem`, and call `contentItem.Apply(part)` when the item JSON must reflect the change.
- `Mock.Of<IStringLocalizer<T>>()` returns `null` strings; use a stub localizer when assertions read messages.
- Auto Setup needs `SiteTimeZone` and a `Default` tenant; a misconfiguration shows up as a setup page (status 200) instead of the site, so assert on content, not only on the status code.
- Keep Auto Setup registered only for the `Testing` environment so production sites never self-provision.

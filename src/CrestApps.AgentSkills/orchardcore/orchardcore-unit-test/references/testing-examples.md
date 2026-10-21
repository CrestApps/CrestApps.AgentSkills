# Orchard Core Module Testing Examples

## Stub Localizers

`Mock.Of<IStringLocalizer<T>>()` returns `null` for every string. These stubs return the key, so validation messages and display texts can be asserted.

```csharp
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;

public sealed class StubStringLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}

public sealed class StubHtmlLocalizer<T> : IHtmlLocalizer<T>
{
    public LocalizedHtmlString this[string name] => new(name, name);

    public LocalizedHtmlString this[string name, params object[] arguments] => new(name, name, false, arguments);

    public LocalizedString GetString(string name) => new(name, name);

    public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
```

## Moq Cheat Sheet

| Need | Code |
|------|------|
| Stub a property | `Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 1, 1))` |
| Set up an async method | `mock.Setup(x => x.GetTypeDefinitionAsync("Product")).ReturnsAsync(definition)` |
| Match any argument | `It.IsAny<string>()`, or `It.Is<T>(value => ...)` for a condition |
| Verify a call | `mock.Verify(x => x.StoreTypeDefinitionAsync(It.IsAny<ContentTypeDefinition>()), Times.Once)` |
| Verify nothing else ran | `mock.VerifyNoOtherCalls()` |
| Verify a log entry | `logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once)` |

Only interface and virtual members can be mocked or verified; extension methods (for example `AlterTypeDefinitionAsync`) cannot.

## Building Definitions and Items

```csharp
using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;

// A type with a configured part.
var typeDefinition = new ContentTypeDefinitionBuilder()
    .WithName("Product")
    .WithPart("ProductPart", part => part.WithSettings(new ProductPartSettings { SkuPrefix = "P-" }))
    .Build();

// A part definition with a field.
var partDefinition = new ContentPartDefinitionBuilder()
    .Named("ProductPart")
    .WithField<TextField>("Description", field => field.WithDisplayName("Description"))
    .Build();

// A type-part definition for driver contexts.
var typePartDefinition = new ContentTypePartDefinition("ProductPart", new ContentPartDefinition("ProductPart"), new JsonObject());

// A content item with a part.
var contentItem = new ContentItem { ContentItemId = "item-1", ContentType = "Product" };
var part = contentItem.GetOrCreate<ProductPart>();
part.Sku = "P-1";
contentItem.Apply(part);
```

## Services That Need a Container

When the class under test needs real Orchard Core infrastructure that is registered by an extension method, build a small `ServiceCollection`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Scripting;
using OrchardCore.Scripting.JavaScript;

var services = new ServiceCollection()
    .AddMemoryCache()
    .AddScripting()
    .AddJavaScriptEngine()
    .BuildServiceProvider();

var scriptingManager = services.GetRequiredService<IScriptingManager>();
```

Prefer this only for leaf services; anything that needs a tenant (`ISession`, `IContentManager`, site settings) belongs in an integration test.

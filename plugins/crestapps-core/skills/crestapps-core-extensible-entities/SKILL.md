---
name: crestapps-core-extensible-entities
description: Skill for storing typed extension data on CrestApps.Core extensible entities and configuring JSON serialization.
---

# CrestApps.Core Extensible Entities - Prompt Templates

## Store Extensible Metadata

You are a CrestApps.Core expert. Generate code and guidance for typed metadata stored in `ExtensibleEntity.Properties`.

### Guidelines

- Derive a model from `ExtensibleEntity` when it needs schema-free metadata in addition to typed properties.
- `Properties` is an ordinal-ignore-case dictionary and serializes as a nested `Properties` JSON object through `JsonExtensionDataConverter`.
- Use `Put<T>`, `TryGet<T>`, `GetOrCreate<T>`, `Alter<T>`, `Has<T>`, and `Remove<T>` rather than casting dictionary values directly.
- `GetOrCreate<T>()` returns a new object when absent but does not store it by itself. Use `Alter<T>()` or call `Put(...)` after mutation.
- Use the type name as the default key only for one metadata object per type. Use `Put(name, value)` and `Get<T>(name)` when the key must be explicit.

### Typed Metadata

```csharp
using CrestApps.Core;

public sealed class InvoiceMetadata
{
    public string InvoiceNumber { get; set; }

    public decimal Amount { get; set; }
}

entity.Put(new InvoiceMetadata
{
    InvoiceNumber = "INV-2026-001",
    Amount = 149.99m,
});

if (entity.TryGet<InvoiceMetadata>(out var invoice))
{
    Console.WriteLine(invoice.InvoiceNumber);
}

entity.Alter<InvoiceMetadata>(metadata =>
{
    metadata.Amount = 199.99m;
});
```

### Configure Serialization

When CrestApps.Core is registered, configure the shared serializer through the options pattern during startup:

```csharp
builder.Services.Configure<ExtensibleEntityJsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new MyMetadataJsonConverter());
});
```

`ExtensibleEntityJsonOptions.CreateDefaultSerializerOptions()` enables case-insensitive property names, trailing commas, enum strings, and number reading from strings. The framework startup initializer assigns configured options to `ExtensibleEntityExtensions.JsonSerializerOptions`.

For a host that does not use the CrestApps.Core DI setup, assign the static options before the first entity serialization:

```csharp
var options = ExtensibleEntityJsonOptions.CreateDefaultSerializerOptions();
options.Converters.Add(new MyMetadataJsonConverter());

ExtensibleEntityExtensions.JsonSerializerOptions = options;
```

`JsonExtensionDataConverter` preserves JSON objects as `JsonNode` values in the property bag and writes the bag as a normal nested object. Do not replace it with `[JsonExtensionData]`, which flattens values at the entity root.


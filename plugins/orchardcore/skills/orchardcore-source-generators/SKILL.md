---
name: orchardcore-source-generators
description: Skill for using Orchard Core source generators. Covers the GenerateArguments attribute for compile-time named argument types, anonymous type interceptors for .NET 9+, PropertyBasedNamedEnumerable architecture, Pager and NavigationBuilder examples, migration from anonymous to named types, and performance optimization. Use this skill when requests mention Orchard Core Source Generators, Use Source Generators for Performance Optimization, Quick Start with [GenerateArguments], What Gets Generated, Benefits of [GenerateArguments], Real-World Examples, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.DisplayManagement, NavigationBuilder, INamedEnumerable, MyShape, IReadOnlyList, [GenerateArguments]. It also helps with Benefits of [GenerateArguments], Real-World Examples, Anonymous Type Interceptors (.NET 9+), plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Source Generators - Prompt Templates

## Use Source Generators for Performance Optimization

You are an Orchard Core expert. Generate code using source generators, the `[GenerateArguments]` attribute, anonymous type interceptors, and named argument types for shapes and display management.

### Guidelines

- Use `[GenerateArguments]` on `partial` classes to generate optimized `INamedEnumerable<object>` implementations at compile time.
- Generated classes extend `PropertyBasedNamedEnumerable` with zero-reflection property access via switch expressions.
- Classes marked with `[GenerateArguments]` must be `partial` and have at least one public instance property with a getter.
- For .NET 9+ projects, anonymous types passed to `Arguments.From()` are automatically optimized by interceptors.
- On older .NET versions, `Arguments.From()` falls back to cached reflection (acceptable for infrequent usage).
- Prefer named types with `[GenerateArguments]` for production code over anonymous types.
- Source-generated argument classes support nested types, records, and inheritance.
- All C# classes must use the `sealed` modifier where applicable.

### Quick Start with [GenerateArguments]

```csharp
using OrchardCore.DisplayManagement;

[GenerateArguments]
public sealed partial class MyShapeArguments
{
    public string Title { get; set; }
    public int Count { get; set; }
    public bool IsActive { get; set; }
}

// Usage - direct property access, no reflection
var args = new MyShapeArguments
{
    Title = "Hello",
    Count = 42,
    IsActive = true,
};

var shape = await shapeFactory.CreateAsync("MyShape", args);
```

### What Gets Generated

The source generator produces an optimized implementation extending `PropertyBasedNamedEnumerable`:

```csharp
// Your code:
[GenerateArguments]
public sealed partial class MyData
{
    public string Name { get; set; }
    public int Value { get; set; }
}

// Auto-generated at compile time:
public sealed partial class MyData : PropertyBasedNamedEnumerable
{
    private static readonly string[] s_propertyNames = ["Name", "Value"];

    protected override int PropertyCount => 2;

    protected override IReadOnlyList<string> PropertyNames => s_propertyNames;

    protected override object? GetPropertyValue(int index) => index switch
    {
        0 => this.Name,
        1 => this.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
```

### Benefits of [GenerateArguments]

- **No reflection** — Direct property access via switch expressions.
- **Compile-time code generation** — Errors caught at build time.
- **Lazy evaluation** — Properties accessed on-demand.
- **Type safety** — Strongly typed arguments with IntelliSense.
- **Minimal allocation** — No intermediate dictionaries until accessed.

### Real-World Examples

#### Pager Shape Arguments

```csharp
[GenerateArguments]
public partial class PagerSlim
{
    public int PageSize { get; set; }
    public string Before { get; set; }
    public string After { get; set; }
}
```

#### Content Zone Arguments

```csharp
[GenerateArguments]
internal sealed partial class ContentZoneArguments
{
    public string Identifier { get; set; }
}
```

#### Navigation Arguments

```csharp
[GenerateArguments]
internal sealed partial class NavigationArguments
{
    public string MenuName { get; set; }
    public string RouteUrl { get; set; }
}
```

### Anonymous Type Interceptors (.NET 9+)

For .NET 9+ projects, anonymous types are automatically optimized using C# interceptors with no code changes:

```csharp
// Automatically optimized at compile time
var shape = await factory.CreateAsync("MyShape", new
{
    Title = "Hello",
    Count = 42,
});
```

**Requirements:**
- .NET 9.0 or later
- C# 13 or later

**How it works:** The interceptor detects `Arguments.From(anonymousType)` calls and generates optimized code using type inference to cast to the actual anonymous type, eliminating reflection and dynamic overhead.

### Fallback: Reflection with Caching

On older .NET versions, `Arguments.From()` uses cached reflection:

```csharp
// Works on any .NET version - uses reflection + caching
var args = Arguments.From(new { Title = "Test", Count = 5 });
```

### Migration from Anonymous Types to Named Types

```csharp
// Before (anonymous type):
var shape = await factory.CreateAsync("MyShape", new
{
    Title = "Hello",
    Count = 5,
});

// After (recommended named type):
[GenerateArguments]
public sealed partial class MyShapeArguments
{
    public string Title { get; set; }
    public int Count { get; set; }
}

var args = new MyShapeArguments { Title = "Hello", Count = 5 };
var shape = await factory.CreateAsync("MyShape", args);
```

### Migration Benefits

- **Better IntelliSense** — Strongly typed with autocomplete.
- **Compile-time safety** — Typos caught at build time.
- **Reusability** — Named types shared across multiple shapes.
- **Testability** — Easier to unit test.
- **Zero reflection** — Direct property access.

### Advanced: Nested Types

```csharp
public sealed partial class OuterClass
{
    [GenerateArguments]
    public sealed partial class InnerArguments
    {
        public string Value { get; set; }
    }
}
```

### Advanced: Records

```csharp
[GenerateArguments]
public sealed partial record MyRecordArgs(string Title, int Count);
```

### Decision Guide

```
Production code with reusable types?
├─ Yes → Use [GenerateArguments] ✅ RECOMMENDED
└─ No
   └─ Using .NET 9+ with anonymous types?
      ├─ Yes → Interceptors (automatic) ✅
      └─ No → Reflection with caching (acceptable for rare usage)
```

### Troubleshooting

**"Type must be partial"** — Add the `partial` keyword:

```csharp
// Correct
[GenerateArguments]
public sealed partial class MyData { }

// Wrong - will not compile
[GenerateArguments]
public sealed class MyData { }
```

**Generated code not found:**
1. Rebuild the project
2. Check `obj/Debug/net10.0/generated/` for generated files
3. Restart Visual Studio if needed

**Do not manually cast to `INamedEnumerable`:**

```csharp
// Correct - pass arguments directly
var args = new MyShapeArguments { Title = "Test" };
var shape = await factory.CreateAsync("MyShape", args);

// Wrong - unnecessary manual cast
var enumerable = (INamedEnumerable<object>)args;
```

### Best Practices

1. Use `[GenerateArguments]` for all production shape arguments.
2. Use descriptive class names (e.g., `UserProfileShapeArgs` over `Args`).
3. Group related properties into focused models.
4. Keep property counts reasonable (2-20 properties).
5. Add XML comments to document the model's purpose.
6. Avoid complex logic in property getters.

---
name: crestapps-core-data-storage
description: Skill for choosing Entity Framework Core or YesSql stores and registering per-feature persistence in CrestApps.Core.
---

# CrestApps.Core Data Storage - Prompt Templates

## Add Durable Storage

You are a CrestApps.Core expert. Generate code and guidance for storage registration in CrestApps.Core.

### Guidelines

- Pick Entity Framework Core or YesSql based on the host architecture.
- Register the data store first, then add per-feature stores.
- Keep storage registration close to the feature that needs it so required stores stay explicit.
- Do not assume one global storage registration covers every feature automatically.

### Entity Framework Core Example

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddEntityCoreStores()
        .AddOpenAI()
        .AddChatInteractions(ci => ci.AddEntityCoreStores())
        .AddDocumentProcessing(dp => dp
            .AddEntityCoreStores()
            .AddOpenXml()
            .AddPdf()
        )
        .AddAIMemory(memory => memory.AddEntityCoreStores())
    )
    .AddIndexingServices(indexing => indexing.AddEntityCoreStores())
    .AddEntityCoreSqliteDataStore("Data Source=App_Data\\crestapps.db")
);
```

### YesSql Example

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddYesSqlStores()
        .AddOpenAI()
        .AddChatInteractions(ci => ci.AddYesSqlStores())
        .AddDocumentProcessing(dp => dp
            .AddYesSqlStores()
            .AddOpenXml()
            .AddPdf()
        )
        .AddAIMemory(memory => memory.AddYesSqlStores())
    )
);
```

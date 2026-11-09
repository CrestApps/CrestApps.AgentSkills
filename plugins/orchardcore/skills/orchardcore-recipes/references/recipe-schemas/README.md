# Orchard Core recipe schema references

These files are generated from `IRecipeStep.GetSchemaAsync()` implementations in `CrestApps.OrchardCore`.

Use them as follows:

1. Read `recipe.schema.json` first to shape the root recipe object.
2. Read `index.json` to find the exact per-step schema file for each step name.
3. Read the matching `<step>.schema.json` file before emitting that step.
4. Do not invent alternate property names or step casing if the schema specifies them.

Important note for `ContentDefinition`:

- `ContentDefinition.schema.json` is authoritative for the recipe-step structure and built-in part-setting fragments exported by the runtime schema.
- The current runtime schema does not enumerate every field editor, display mode, and field-specific settings variant.
- For those option catalogs, also consult the `orchardcore-content-fields` and `orchardcore-content-parts` skills, then ensure the final step still matches `ContentDefinition.schema.json`.

Orchard Core 4.0 notes:

- The Html and Markdown part and field settings no longer include `RenderLiquid`; use `LiquidPart` or `LiquidField` instead.
- Credentials can reference a secret from the `Secrets` module through the `*SecretName` settings properties. Only the generic `Settings` step accepts them; the dedicated `GitHubAuthenticationSettings`, `FacebookCoreSettings`, `FacebookLoginSettings`, `MicrosoftAccountSettings`, `TwitterSettings`, `AzureADSettings` and `OpenIdClientSettings` steps store a credential as given, and a clear value does not work at runtime.
- `WorkflowType` carries the modernized workflow model: `Variables`, `IsActivity`, `IsSingletonPerCorrelation`, `BranchingMode`, `FaultOnScriptErrors`, `RecordActivityData`, a `Syntax` on expressions, `OutputBindings` on activities and an `ActivityRetryPolicy` on tasks.
- `ReloadTenant` takes no properties.

Maintenance note: the schemas are generated from the C# definitions in `CrestApps.OrchardCore` (Recipes.Core and the module-owned `IRecipeStep` and schema definitions). Regenerate with `utilities/CrestApps.OrchardCore.RecipeSchemaExporter`; set `ORCHARDCORE_SOURCE_DIR` to an Orchard Core checkout so feature IDs are scanned from its manifests. No hand edits are needed after regeneration.

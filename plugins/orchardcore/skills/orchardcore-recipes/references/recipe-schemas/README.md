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

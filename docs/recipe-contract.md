# Size-specific recipe contract

T3.4 / issue #43 validates recipe lines in Domain without UI, API, or EF Core dependencies.

- A recipe is the set of `RecipeIngredient` lines with the same `SizeVariantId`, matching the existing schema. No separate recipe entity or table is needed.
- Creating a line requires a resolved `SizeVariant` and active `Ingredient`, not unchecked foreign-key IDs. Callers must load current definitions; a missing lookup must not be substituted with a fabricated entity. An inactive size can still have its recipe configured before reactivation.
- Quantities must be positive, have at most three decimal places, and fit the existing `numeric(12,3)` recipe column (`0.001` through `999999999.999`).
- Units are trimmed and lowercased. Compatibility and conversion use `IngredientUnit`: g/kg, ml/l, and pcs. Unknown units and cross-family conversions are rejected.
- The converted base-unit quantity must fit `numeric(14,3)` exactly. No silent rounding, truncation, or overflow is allowed. Lines retain their entered quantity and normalized unit for display; creating a line never changes stock.
- Ingredient or size deactivation preserves existing lines. Current ingredients must be resolved and revalidated before later recipe writes or order consumption; construction is not a guarantee of future ingredient availability.

Application commands/queries (T3.5), atomic persistence and complete recipe replacement (T3.6), screens (T3.7), and order consumption (T6.6) remain separate tasks. The existing unique `(size_variant_id, ingredient_id)` index guards duplicate persisted lines. No schema change or migration is introduced here.

## Verification

```powershell
dotnet test tests/CoffeeShop.Domain.Tests --configuration Release --filter FullyQualifiedName~RecipeIngredientTests
dotnet build CoffeeShop.slnx --configuration Release
dotnet format CoffeeShop.slnx --verify-no-changes --no-restore
dotnet test CoffeeShop.slnx --no-build --configuration Release
```

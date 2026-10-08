# Catalog notebook (T3.7)

The **Menu and Recipes** navigation entry opens the catalog notebook. It uses the
existing paper/pencil theme, embedded handwriting fonts, 48px actions, a hard
offset detail-card shadow, and a stacked layout on narrow windows. Inventory
navigation remains available.

## Run locally

Use .NET 10 on Windows with the Windows App SDK runtime. Supply a migrated local
PostgreSQL database and an appropriately restricted account through
`ConnectionStrings__CoffeeShop` (the existing composition-root setting).
The application does not migrate or seed a database on startup. Do not commit
credentials. See `docs/catalog-persistence.md` for persistence verification.

```powershell
dotnet run --project src/CoffeeShop.WinUI -r win-x64
```

## Operator check

1. Open **Categories**, create `Coffee`, edit its name, and leave it active.
2. Open **Drinks and recipes** and create `Latte`, a description, that category,
   initial size `Small`, and price `25.50` (use your Windows decimal separator).
3. Select the drink. Edit its name/description; add a `Large` size priced `35.75`.
   Edit either size's name/price and verify the refreshed values.
4. Open **Add-ons**, create `Cream` priced `5`, and edit its price/name. Return to
   the drink and **Choose compatible add-ons**. Select the topping and save.
   Saving with all unchecked removes compatibility. Each topping is once per drink.
5. With active ingredients available from Inventory, use **View / edit recipe**
   on each size. Add ingredient lines, set quantities and compatible units, remove
   a line, and save. Reopen both recipes and verify their independent quantities.
6. Try zero quantity, a duplicate ingredient, an incompatible unit, a blank name,
   or an invalid price. The editor stays open, retains inputs, and shows the
   Application error details. Correct them and save. Invalid recipes must not
   replace the stored recipe. An unavailable historical ingredient is labelled
   for replacement; ingredient activation is revalidated by Application on save.
7. Search by drink name/description. Try a nonmatching search and verify the
   visible empty-state guidance. Include inactive drinks to inspect saved history.
8. Deactivate a size, topping, category, and drink using the confirmation dialogs.
   Verify unavailable labels and retained recipes/references, not physical deletion.
9. Stop the local database and reload. Verify actionable error guidance, then
   restart it and use **Reload catalog**. Controls are disabled during operations;
   an in-flight save cannot be submitted or dismissed twice. If a write succeeds
   but refresh fails, the editor closes and explicitly warns not to resubmit.
10. Resize the window below and above 1008px; the detail editor moves below/beside
    the list. Use keyboard focus and scrolling to reach all actions.

Reactivation is not added: the existing catalog Application commands expose
deactivation only. Categories and drink searches show at most 200 matches with
visible truncation guidance; narrow the drink search when necessary. Add-ons and
active ingredient options are not capped.

## Automated scripted verification

```powershell
dotnet test tests/CoffeeShop.WinUI.Tests --configuration Release
# Set COFFEESHOP_TEST_POSTGRES to a disposable PostgreSQL admin connection first.
dotnet test tests/CoffeeShop.Infrastructure.Tests --configuration Release --filter FullyQualifiedName~WinUICatalogWorkspace
dotnet build CoffeeShop.slnx --configuration Release
dotnet test CoffeeShop.slnx --configuration Release
dotnet format CoffeeShop.slnx --verify-no-changes --no-restore
```

The WinUI tests compile the exact XAML-independent presentation sources used by
the page. They execute the operator workflow through real Application handlers,
with fake persistence, and verify validation, not-found/conflict, empty/loading,
duplicate-action protection, refresh failure, and culture-aware decimal parsing.
The PostgreSQL case executes that same presentation workflow with actual scoped
repositories, migrations, and fresh database contexts, including invalid-recipe
preservation and inactive history. It skips explicitly without the test connection.
The existing catalog-postgres CI job includes this case.

These are headless scripted checks, not pointer-driven UI automation or visual
layout certification. XAML compilation verifies the controls and event wiring;
the operator check remains the reproducible manual layout/accessibility check.

## Boundaries

The page and presentation model call only Application command/query handlers;
they never reference EF Core, Npgsql, or infrastructure types. The App composition
root registers those handlers and the clock. Each read/write creates and disposes
its own service scope, avoiding root-scoped tracked entities and overlapping
DbContext operations. The only persistence additions are read-only add-on and
active-ingredient projections needed by the editors. No database schema, Domain
rules, API endpoints, stock changes, or authentication workflows are added.

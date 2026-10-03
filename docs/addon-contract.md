# Catalog add-ons for sales

`CatalogAddons.GetAllowedAddons` reads resolved current menu-item/category definitions,
add-on definitions, and `MenuItemAddon` compatibility links. It returns immutable
`CatalogAddonSnapshot` values containing menu-item ID, add-on ID, name, unit price,
and `MaximumQuantityPerDrink` (always 1). Only active compatible add-ons are returned;
an unavailable item or category returns a conflict. An empty result is valid.

Compatibility belongs to a menu item, not a size. One of each add-on is allowed per
drink, so a line of two drinks charges each selected add-on twice. Duplicate add-on
identifiers are rejected rather than combined or silently removed. Add-ons remain
optional; there is no limit on the number of distinct compatible add-ons.

When creating a draft, supply current resolved add-ons and compatibility links in
`OrderLineSelection`. `OrderLine` revalidates compatibility and availability and
passes the same catalog snapshot contract to `OrderLineAddon`. Missing links fail
closed. Sales stores names and prices as snapshots, so subsequent catalog changes
do not alter an existing line's total. A UI read snapshot is not evidence of current
availability: callers must reload definitions/links before ordering, and the later
confirmation workflow must re-resolve them at its transaction boundary.

Persistence loading/saving, API endpoints, screens, and confirmation transactions
remain separate OpenSpec tasks. This rule needs no add-on quantity column or schema
migration.

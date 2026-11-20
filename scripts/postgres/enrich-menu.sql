-- Additive enrichment for the Corner Cafe Windows demo catalog (prices in VND).
-- Existing prices, activation states, order history and stock balances are retained.
-- Newly introduced ingredients receive audited opening demo stock exactly once.
\set ON_ERROR_STOP on
BEGIN;
SELECT pg_advisory_xact_lock(hashtext('corner-cafe-menu-enrichment-v1'));

CREATE FUNCTION pg_temp.enrichment_id(key text) RETURNS uuid
LANGUAGE sql IMMUTABLE AS $$ SELECT md5('corner-cafe-menu-enrichment-v1:' || key)::uuid $$;

DO $$
BEGIN
    IF (SELECT count(*) FROM catalog.categories WHERE name IN
        ('Espresso Bar', 'Cold Coffee', 'Tea & Matcha', 'Bakery', 'Seasonal')) <> 5 THEN
        RAISE EXCEPTION 'Load the Corner Cafe Windows demo catalog before enriching its menu.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM identity.users u JOIN identity.user_roles ur ON ur.user_id = u.id
        JOIN identity.roles r ON r.id = ur.role_id WHERE r.code = 'Manager' AND u.is_active) THEN
        RAISE EXCEPTION 'A manager account is required to attribute opening demo stock.';
    END IF;
END $$;

CREATE TEMP TABLE enrichment_descriptions (name text PRIMARY KEY, description text) ON COMMIT DROP;
INSERT INTO enrichment_descriptions VALUES
    ('House Espresso', 'A concentrated double shot of our house Arabica blend, with dark chocolate notes and a lingering roasted finish.'),
    ('Americano', 'A double espresso lengthened with hot water for a clean, full-bodied cup and a gentle cocoa finish.'),
    ('Cappuccino', 'House espresso beneath steamed whole milk and a generous cap of velvety foam. Rich, balanced, and comforting.'),
    ('Café Latte', 'A double espresso softened with silky whole milk and a fine layer of microfoam. Smooth from the first sip.'),
    ('Cafe Latte', 'A double espresso softened with silky whole milk and a fine layer of microfoam. Smooth from the first sip.'),
    ('Vanilla Latte', 'House espresso and steamed milk rounded out with fragrant vanilla syrup for a mellow, lightly sweet cup.'),
    ('Mocha', 'Dark chocolate and double espresso folded into steamed milk, bringing together a cocoa-rich finish and a gentle coffee bite.'),
    ('Cold Brew', 'Slow-steeped Arabica poured over ice. Smooth, refreshing, and naturally mellow, with no milk or added syrup.'),
    ('Coconut Cold Brew', 'Our slow-steeped cold brew lifted with creamy coconut milk. A smooth iced coffee with a tropical finish.'),
    ('Vietnamese Iced Coffee', 'Strong house coffee layered with sweetened condensed milk and poured over ice. Bold, creamy, and distinctly Vietnamese.'),
    ('Matcha Latte', 'Ceremonial matcha whisked until smooth, then paired with chilled whole milk for a vivid green, gently earthy iced latte.'),
    ('Peach Green Tea', 'Fragrant jasmine green tea with peach purée, served over ice for a bright, fruit-forward afternoon refresher.'),
    ('Jasmine Milk Tea', 'Aromatic jasmine black tea softened with whole milk. Floral on the nose, smooth and mellow on the palate.'),
    ('Honey Lemon Green Tea', 'Jasmine green tea brightened with fresh lemon juice and forest honey. A fragrant balance of citrus and gentle sweetness.'),
    ('Butter Croissant', 'A golden, all-butter croissant with delicate crisp layers and a soft center. Delivered fresh each morning.'),
    ('Almond Croissant', 'A flaky butter croissant filled with almond cream, finished with toasted almond slices for a crisp, nutty bite.'),
    ('Blueberry Muffin', 'A tender blueberry muffin with juicy berries and a golden crumb topping. A comforting match for a morning coffee.'),
    ('Honey Oat Latte', 'Double espresso with barista oat milk and forest honey. Creamy, gently sweet, and finished with a soft oat note.'),
    ('Salted Caramel Latte', 'House espresso and silky milk swirled with salted caramel. Buttery sweetness meets a delicate salty finish.'),
    ('Cocoa Oat Latte', 'Double espresso, dark chocolate, and barista oat milk come together in a smooth, cocoa-rich latte.'),
    ('Pumpkin Spice Latte', 'An autumn favorite of espresso, steamed milk, and warming spiced syrup. Available when the seasonal menu returns.');

UPDATE catalog.menu_items m SET description = d.description, updated_at = now()
FROM enrichment_descriptions d
WHERE m.name = d.name AND m.description IS DISTINCT FROM d.description;

CREATE TEMP TABLE enrichment_items (
    key text PRIMARY KEY, category text, name text, description text,
    regular_price numeric, large_extra numeric
) ON COMMIT DROP;
INSERT INTO enrichment_items VALUES
    ('flat-white', 'Espresso Bar', 'Flat White', 'A double espresso with a short pour of finely textured milk. More coffee-forward than a latte, with a silky finish.', 54000, 10000),
    ('cortado', 'Espresso Bar', 'Cortado', 'A double espresso balanced with an equal pour of warm milk. Small, smooth, and made for a slow coffee break.', 48000, NULL),
    ('espresso-tonic', 'Cold Coffee', 'Espresso Tonic', 'House espresso floated over chilled tonic water and a touch of lemon. Sparkling, crisp, and refreshingly bittersweet.', 62000, 12000),
    ('strawberry-matcha', 'Tea & Matcha', 'Strawberry Matcha Latte', 'Whisked matcha, chilled milk, and strawberry purée in vibrant layers. Earthy green tea meets a fresh berry finish.', 72000, 12000),
    ('cream-cold-brew', 'Cold Coffee', 'Salted Cream Cold Brew', 'Slow-steeped Arabica over ice, topped with salted vanilla cream. A bold coffee base beneath a smooth, lightly sweet cap.', 70000, 12000),
    ('mango-passion-tea', 'Tea & Matcha', 'Mango Passion Tea', 'Jasmine green tea shaken with mango-passionfruit purée and a splash of lemon. Bright, tropical, and served over ice.', 64000, 10000),
    ('chocolate-croissant', 'Bakery', 'Chocolate Croissant', 'A flaky butter croissant with a rich chocolate center and a light cocoa finish. Served as one generous pastry.', 48000, NULL),
    ('basque-cheesecake', 'Bakery', 'Basque Cheesecake', 'A creamy cheesecake slice with a deeply caramelized top and a soft center. Rich enough to share, lovely with espresso.', 65000, NULL);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM enrichment_items e JOIN catalog.menu_items m ON m.name = e.name
        WHERE m.id <> pg_temp.enrichment_id('item-' || e.key)) THEN
        RAISE EXCEPTION 'An enrichment item name already belongs to another item; resolve it before rerunning.';
    END IF;
END $$;

INSERT INTO catalog.menu_items (id, category_id, name, description, is_active, created_at, updated_at)
SELECT pg_temp.enrichment_id('item-' || e.key), c.id, e.name, e.description, true, now(), now()
FROM enrichment_items e JOIN catalog.categories c ON c.name = e.category
WHERE true
ON CONFLICT (id) DO NOTHING;

INSERT INTO catalog.size_variants (id, menu_item_id, size_name, price, is_active)
SELECT pg_temp.enrichment_id('size-' || e.key || '-' || size.key), pg_temp.enrichment_id('item-' || e.key),
       CASE WHEN e.category = 'Bakery' THEN 'Single' ELSE size.name END,
       e.regular_price + CASE WHEN size.key = 'large' THEN e.large_extra ELSE 0 END, true
FROM enrichment_items e CROSS JOIN (VALUES ('regular','Regular'),('large','Large')) AS size(key,name)
WHERE size.key = 'regular' OR e.large_extra IS NOT NULL
ON CONFLICT (menu_item_id, size_name) DO NOTHING;

CREATE TEMP TABLE enrichment_stock (
    key text PRIMARY KEY, name text, unit text, opening numeric, threshold numeric
) ON COMMIT DROP;
INSERT INTO enrichment_stock VALUES
    ('beans', 'House blend Arabica beans', 'g', 5000, 2500),
    ('milk', 'Whole milk', 'ml', 10000, 8000),
    ('matcha', 'Ceremonial matcha powder', 'g', 500, 150),
    ('green', 'Jasmine green tea', 'g', 1000, 500),
    ('lemon', 'Fresh lemon juice', 'ml', 2000, 750),
    ('tonic', 'Tonic water', 'ml', 12000, 1800),
    ('strawberry', 'Strawberry purée', 'ml', 3000, 600),
    ('cream', 'Salted vanilla cream', 'ml', 3000, 600),
    ('mango', 'Mango passionfruit purée', 'ml', 3000, 600),
    ('chocolate-croissant', 'Chocolate croissant', 'pcs', 24, 6),
    ('cheesecake', 'Basque cheesecake slice', 'pcs', 20, 5);

WITH inserted AS (
    INSERT INTO inventory.ingredients (id, name, base_unit, available_quantity, reorder_threshold,
        is_active, created_at, updated_at)
    SELECT pg_temp.enrichment_id('ingredient-' || key), name, unit, opening, threshold, true, now(), now()
    FROM enrichment_stock WHERE true ON CONFLICT (name) DO NOTHING
    RETURNING id, available_quantity
)
INSERT INTO inventory.stock_movements (id, ingredient_id, type, quantity_delta, actor_id, reason, occurred_at)
SELECT pg_temp.enrichment_id('opening-' || i.id), i.id, 1, i.available_quantity,
       (SELECT u.id FROM identity.users u JOIN identity.user_roles ur ON ur.user_id = u.id
        JOIN identity.roles r ON r.id = ur.role_id WHERE r.code = 'Manager' AND u.is_active
        ORDER BY u.created_at, u.id LIMIT 1),
       'Opening stock for enriched demo menu', now()
FROM inserted i;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM enrichment_stock e JOIN inventory.ingredients i ON i.name = e.name
        WHERE i.base_unit <> e.unit OR NOT i.is_active) THEN
        RAISE EXCEPTION 'An enrichment ingredient is inactive or has an incompatible unit.';
    END IF;
END $$;

INSERT INTO catalog.recipe_ingredients (id, size_variant_id, ingredient_id, quantity, unit)
SELECT pg_temp.enrichment_id('recipe-' || recipe.item || '-' || size.key || '-' || stock.key),
       pg_temp.enrichment_id('size-' || recipe.item || '-' || size.key), ingredient.id,
       recipe.quantity * CASE WHEN size.key = 'large' THEN 1.3 ELSE 1 END, stock.unit
FROM (VALUES
    ('flat-white','beans',18), ('flat-white','milk',150),
    ('cortado','beans',18), ('cortado','milk',36),
    ('espresso-tonic','beans',18), ('espresso-tonic','tonic',150), ('espresso-tonic','lemon',5),
    ('strawberry-matcha','matcha',4), ('strawberry-matcha','milk',180), ('strawberry-matcha','strawberry',35),
    ('cream-cold-brew','beans',25), ('cream-cold-brew','cream',45),
    ('mango-passion-tea','green',6), ('mango-passion-tea','mango',45), ('mango-passion-tea','lemon',10),
    ('chocolate-croissant','chocolate-croissant',1), ('basque-cheesecake','cheesecake',1)
) AS recipe(item,stock,quantity)
JOIN enrichment_items e ON e.key = recipe.item
JOIN enrichment_stock stock ON stock.key = recipe.stock
JOIN inventory.ingredients ingredient ON ingredient.name = stock.name
CROSS JOIN (VALUES ('regular'),('large')) AS size(key)
WHERE size.key = 'regular' OR e.large_extra IS NOT NULL
ON CONFLICT (size_variant_id, ingredient_id) DO NOTHING;

INSERT INTO catalog.menu_item_addons (menu_item_id, addon_id)
SELECT pg_temp.enrichment_id('item-' || e.key), a.id
FROM enrichment_items e CROSS JOIN catalog.addons a
WHERE a.is_active AND (
    (e.key IN ('flat-white','cortado','cream-cold-brew','espresso-tonic') AND a.name = 'Extra espresso shot') OR
    (e.key IN ('flat-white','cortado','strawberry-matcha') AND a.name = 'Oat milk swap') OR
    (e.key IN ('flat-white','cream-cold-brew') AND a.name = 'Vanilla syrup'))
ON CONFLICT DO NOTHING;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM catalog.size_variants v JOIN enrichment_items e
        ON v.menu_item_id = pg_temp.enrichment_id('item-' || e.key)
        WHERE NOT EXISTS (SELECT 1 FROM catalog.recipe_ingredients r WHERE r.size_variant_id = v.id)) THEN
        RAISE EXCEPTION 'Every enriched size must have a recipe.';
    END IF;
END $$;

SELECT 'Menu enrichment ready' AS result,
       (SELECT count(*) FROM enrichment_items) AS enriched_menu_items,
       (SELECT count(*) FROM catalog.size_variants WHERE menu_item_id IN
           (SELECT pg_temp.enrichment_id('item-' || key) FROM enrichment_items)) AS enriched_sizes;
COMMIT;

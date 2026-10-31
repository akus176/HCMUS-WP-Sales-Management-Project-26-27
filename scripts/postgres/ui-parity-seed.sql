-- Optional #119 verification fixture. Run only in a disposable database after
-- midterm-seed.sql. Adds active/inactive add-ons, a Confirmed queue item,
-- and an unpaid Ready item for native row/action/form coverage.
BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM catalog.menu_items WHERE name LIKE 'Midterm %') THEN
        RAISE EXCEPTION 'Run midterm-seed.sql before ui-parity-seed.sql';
    END IF;
END $$;

INSERT INTO catalog.addons (id, name, price, is_active)
VALUES
    (md5('coffeeshop-ui119:extra-shot')::uuid, 'UI119 Extra shot', 12.00, true),
    (md5('coffeeshop-ui119:oat-milk')::uuid, 'UI119 Oat milk', 8.00, true),
    (md5('coffeeshop-ui119:retired-syrup')::uuid, 'UI119 Retired syrup', 5.00, false)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, price = EXCLUDED.price, is_active = EXCLUDED.is_active;

INSERT INTO catalog.menu_item_addons (menu_item_id, addon_id)
SELECT item.id, md5('coffeeshop-ui119:extra-shot')::uuid
FROM catalog.menu_items item
WHERE item.name = 'Midterm House Espresso'
ON CONFLICT DO NOTHING;

-- MIDTERM-023 is takeaway, with no loyalty or delivery record. Represent it
-- before preparation instead of after completion. MIDTERM-016 remains Ready,
-- with payment outstanding so the payment/recovery form can be exercised.
DELETE FROM sales.payments
WHERE order_id IN (
    md5('coffeeshop-midterm-v1:order-16')::uuid,
    md5('coffeeshop-midterm-v1:order-23')::uuid
);
UPDATE sales.orders SET paid_amount = 0
WHERE id IN (
    md5('coffeeshop-midterm-v1:order-16')::uuid,
    md5('coffeeshop-midterm-v1:order-23')::uuid
);
UPDATE sales.orders SET status = 'confirmed', completed_at = NULL
WHERE id = md5('coffeeshop-midterm-v1:order-23')::uuid;
UPDATE sales.order_status_history SET to_status = 'confirmed'
WHERE order_id = md5('coffeeshop-midterm-v1:order-23')::uuid;

COMMIT;

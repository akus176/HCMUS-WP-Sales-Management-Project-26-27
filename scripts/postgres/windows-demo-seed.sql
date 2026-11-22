-- Corner Cafe Windows walkthrough data. Apply after EF migrations and staff-role seeding.
-- Requires an empty demo database. Use -v demo_database=NAME to explicitly select a different empty database.
-- All people and contact details are fictional.
\set ON_ERROR_STOP on
\if :{?demo_database}
\else
\set demo_database corner_cafe_windows_demo
\endif
BEGIN;

SELECT set_config('coffeeshop.demo_seed_database', :'demo_database', true);

DO $$
BEGIN
    IF current_database() <> current_setting('coffeeshop.demo_seed_database') THEN
        RAISE EXCEPTION 'This seed must run against the explicitly selected demo database.';
    END IF;
    IF EXISTS (SELECT 1 FROM catalog.categories) OR EXISTS (SELECT 1 FROM catalog.menu_items)
        OR EXISTS (SELECT 1 FROM catalog.addons) OR EXISTS (SELECT 1 FROM sales.tables)
        OR EXISTS (SELECT 1 FROM sales.orders) OR EXISTS (SELECT 1 FROM customers.customers)
        OR EXISTS (SELECT 1 FROM customers.loyalty_settings) OR EXISTS (SELECT 1 FROM inventory.ingredients)
        OR EXISTS (SELECT 1 FROM inventory.suppliers) OR EXISTS (SELECT 1 FROM inventory.purchase_receipts) THEN
        RAISE EXCEPTION 'The demo database already contains data.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM identity.roles WHERE code = 'DeliveryStaff') THEN
        RAISE EXCEPTION 'Run the API with --seed-staff-roles before loading the demo.';
    END IF;
END $$;

CREATE FUNCTION pg_temp.demo_id(key text) RETURNS uuid
LANGUAGE sql IMMUTABLE AS $$ SELECT md5('corner-cafe-windows-demo-v1:' || key)::uuid $$;

CREATE TEMP TABLE demo_clock ON COMMIT DROP AS
SELECT now() AS clock,
       (now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS today,
       ((now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date - 43)::timestamp
            AT TIME ZONE 'Asia/Ho_Chi_Minh' AS opened;

INSERT INTO identity.users (id, username, email, password_hash, full_name, is_active, created_at, updated_at)
SELECT pg_temp.demo_id('staff-' || role), 'history_' || role, role || '@staff.example.test',
       'historical-fixture-only', full_name, true, opened, opened
FROM demo_clock CROSS JOIN (VALUES
    ('manager', 'Mai Pham'), ('barista', 'Bao Le'), ('courier', 'Khoa Tran')
) AS staff(role, full_name);

INSERT INTO identity.user_roles (user_id, role_id, assigned_at, assigned_by)
SELECT pg_temp.demo_id('staff-courier'), id, opened, pg_temp.demo_id('staff-manager')
FROM identity.roles CROSS JOIN demo_clock WHERE code = 'DeliveryStaff';

INSERT INTO catalog.categories (id, name, display_order, is_active, created_at, updated_at)
SELECT pg_temp.demo_id('category-' || key), name, sort_order, true, opened, opened
FROM demo_clock CROSS JOIN (VALUES
    ('espresso', 'Espresso Bar', 1), ('cold', 'Cold Coffee', 2), ('tea', 'Tea & Matcha', 3),
    ('bakery', 'Bakery', 4), ('seasonal', 'Seasonal', 5)
) AS categories(key, name, sort_order);

CREATE TEMP TABLE demo_menu (
    number int PRIMARY KEY, category text, name text, description text, price numeric,
    large_extra numeric, active bool
) ON COMMIT DROP;
INSERT INTO demo_menu VALUES
    (1, 'espresso', 'House Espresso', 'Double shot with a dark chocolate finish.', 35000, NULL, true),
    (2, 'espresso', 'Americano', 'House espresso, topped with hot water.', 42000, 8000, true),
    (3, 'espresso', 'Cappuccino', 'Double espresso with steamed milk and foam.', 52000, 10000, true),
    (4, 'espresso', 'Café Latte', 'House espresso with silky whole milk.', 55000, 11000, true),
    (5, 'espresso', 'Vanilla Latte', 'Espresso, whole milk, and vanilla syrup.', 61000, 11000, true),
    (6, 'espresso', 'Mocha', 'Espresso and dark chocolate with steamed milk.', 65000, 11000, true),
    (7, 'cold', 'Cold Brew', 'Slow-steeped Arabica served over ice.', 58000, 10000, true),
    (8, 'cold', 'Coconut Cold Brew', 'Cold brew with a splash of coconut milk.', 66000, 11000, true),
    (9, 'tea', 'Matcha Latte', 'Whisked matcha and chilled whole milk.', 65000, 11000, true),
    (10, 'tea', 'Peach Green Tea', 'Brewed green tea with peach purée.', 56000, 10000, true),
    (11, 'tea', 'Jasmine Milk Tea', 'Jasmine tea finished with whole milk.', 57000, 10000, true),
    (12, 'tea', 'Honey Lemon Green Tea', 'Green tea, fresh lemon juice, and honey.', 55000, 10000, true),
    (13, 'bakery', 'Butter Croissant', 'Flaky butter pastry delivered fresh each morning.', 38000, NULL, true),
    (14, 'bakery', 'Almond Croissant', 'Butter croissant with almond filling.', 48000, NULL, true),
    (15, 'bakery', 'Blueberry Muffin', 'Blueberry muffin with a crumb topping.', 39000, NULL, true),
    (16, 'seasonal', 'Honey Oat Latte', 'Oat milk, house espresso, and honey.', 68000, 11000, true),
    (17, 'seasonal', 'Salted Caramel Latte', 'Espresso, milk, and salted caramel syrup.', 68000, 11000, true),
    (18, 'seasonal', 'Cocoa Oat Latte', 'Espresso and dark cocoa with oat milk.', 69000, 11000, true),
    (19, 'seasonal', 'Pumpkin Spice Latte', 'Autumn espresso with spiced syrup.', 67000, NULL, false),
    (20, 'cold', 'Vietnamese Iced Coffee', 'Strong house coffee with sweetened condensed milk.', 49000, 9000, true);

INSERT INTO catalog.menu_items (id, category_id, name, description, is_active, created_at, updated_at)
SELECT pg_temp.demo_id('item-' || number), pg_temp.demo_id('category-' || category),
       name, description, active, opened, opened FROM demo_menu CROSS JOIN demo_clock;

INSERT INTO catalog.size_variants (id, menu_item_id, size_name, price, is_active)
SELECT pg_temp.demo_id('size-' || number || '-regular'), pg_temp.demo_id('item-' || number),
       CASE WHEN category = 'bakery' THEN 'Single' ELSE 'Regular' END, price, active FROM demo_menu
UNION ALL
SELECT pg_temp.demo_id('size-' || number || '-large'), pg_temp.demo_id('item-' || number),
       'Large', price + large_extra, active FROM demo_menu WHERE large_extra IS NOT NULL;

CREATE TEMP TABLE demo_stock (
    key text PRIMARY KEY, name text, unit text, threshold numeric, closing numeric, cost numeric, supplier text
) ON COMMIT DROP;
INSERT INTO demo_stock VALUES
    ('beans', 'House blend Arabica beans', 'g', 2500, 2100, 310, 'roastery'),
    ('milk', 'Whole milk', 'ml', 8000, 16400, 31, 'dairy'),
    ('oat', 'Barista oat milk', 'ml', 3500, 2850, 58, 'dairy'),
    ('coconut', 'Coconut milk', 'ml', 1200, 2450, 42, 'market'),
    ('condensed', 'Sweetened condensed milk', 'ml', 1000, 3400, 46, 'dairy'),
    ('matcha', 'Ceremonial matcha powder', 'g', 150, 490, 4200, 'tea'),
    ('green', 'Jasmine green tea', 'g', 500, 1760, 560, 'tea'),
    ('black', 'Jasmine black tea', 'g', 350, 1720, 540, 'tea'),
    ('peach', 'Peach purée', 'ml', 900, 1980, 75, 'market'),
    ('lemon', 'Fresh lemon juice', 'ml', 750, 1530, 32, 'market'),
    ('honey', 'Forest honey', 'ml', 500, 1825, 110, 'market'),
    ('vanilla', 'Vanilla syrup', 'ml', 700, 460, 95, 'market'),
    ('caramel', 'Salted caramel syrup', 'ml', 550, 1425, 100, 'market'),
    ('cocoa', 'Dark chocolate sauce', 'ml', 800, 2100, 130, 'market'),
    ('butter', 'Butter croissant', 'pcs', 9, 26, 18000, 'bakery'),
    ('almond', 'Almond croissant', 'pcs', 8, 17, 23000, 'bakery'),
    ('muffin', 'Blueberry muffin', 'pcs', 8, 22, 16500, 'bakery');

INSERT INTO inventory.ingredients (id, name, base_unit, available_quantity, reorder_threshold, is_active, created_at, updated_at)
SELECT pg_temp.demo_id('ingredient-' || key), name, unit, 0, threshold, true, opened, opened
FROM demo_stock CROSS JOIN demo_clock;

INSERT INTO catalog.recipe_ingredients (id, size_variant_id, ingredient_id, quantity, unit)
SELECT pg_temp.demo_id('recipe-' || recipe.item || '-' || sizes.key || '-' || stock.key),
       pg_temp.demo_id('size-' || recipe.item || '-' || sizes.key), pg_temp.demo_id('ingredient-' || stock.key),
       recipe.quantity * sizes.multiplier, stock.unit
FROM (VALUES
    (1, 'beans', 18), (2, 'beans', 18), (3, 'beans', 18), (3, 'milk', 135),
    (4, 'beans', 18), (4, 'milk', 200), (5, 'beans', 18), (5, 'milk', 190), (5, 'vanilla', 18),
    (6, 'beans', 18), (6, 'milk', 180), (6, 'cocoa', 24), (7, 'beans', 25),
    (8, 'beans', 25), (8, 'coconut', 48), (9, 'matcha', 4), (9, 'milk', 200),
    (10, 'green', 6), (10, 'peach', 40), (11, 'black', 6), (11, 'milk', 105),
    (12, 'green', 5), (12, 'honey', 18), (12, 'lemon', 25),
    (13, 'butter', 1), (14, 'almond', 1), (15, 'muffin', 1),
    (16, 'beans', 18), (16, 'oat', 195), (16, 'honey', 18),
    (17, 'beans', 18), (17, 'milk', 190), (17, 'caramel', 20),
    (18, 'beans', 18), (18, 'oat', 185), (18, 'cocoa', 22),
    (19, 'beans', 18), (19, 'milk', 190), (20, 'beans', 24), (20, 'condensed', 32)
) AS recipe(item, ingredient, quantity)
JOIN demo_stock stock ON stock.key = recipe.ingredient
JOIN demo_menu menu ON menu.number = recipe.item
CROSS JOIN (VALUES ('regular', 1.00), ('large', 1.30)) AS sizes(key, multiplier)
WHERE sizes.key = 'regular' OR menu.large_extra IS NOT NULL;

INSERT INTO catalog.addons (id, name, price, is_active)
SELECT pg_temp.demo_id('addon-' || key), name, price, active
FROM (VALUES ('shot', 'Extra espresso shot', 12000, true), ('oat', 'Oat milk swap', 10000, true),
             ('vanilla', 'Vanilla syrup', 8000, true), ('seasonal', 'Pumpkin spice syrup', 8000, false))
    AS addons(key, name, price, active);

INSERT INTO catalog.menu_item_addons (menu_item_id, addon_id)
SELECT pg_temp.demo_id('item-' || item), pg_temp.demo_id('addon-' || key)
FROM (VALUES (1,'shot'), (2,'shot'), (3,'shot'), (4,'shot'), (5,'shot'), (6,'shot'), (7,'shot'),
             (16,'shot'), (17,'shot'), (20,'shot'), (3,'oat'), (4,'oat'), (5,'oat'), (6,'oat'), (9,'oat'),
             (4,'vanilla'), (7,'vanilla'), (19,'seasonal')) AS compatibility(item, key);

INSERT INTO inventory.suppliers (id, name, phone, email, address, is_active, created_at, updated_at)
SELECT pg_temp.demo_id('supplier-' || key), name, '000-000-10' || number,
       key || '@suppliers.example.test', address, true, opened, opened
FROM demo_clock CROSS JOIN (VALUES
    (1,'roastery','Hearthline Roastery','Da Lat demo warehouse'),
    (2,'dairy','Morning Cart Dairy','Thu Duc demo cold storage'),
    (3,'tea','Green Terrace Tea','District 5 demo tea wholesale'),
    (4,'market','Lantern Pantry Supply','District 3 demo wholesale market'),
    (5,'bakery','Small Batch Bakehouse','Binh Thanh demo bakery')
) AS suppliers(number, key, name, address);

INSERT INTO customers.customers (id, full_name, phone, email, is_active, loyalty_points_balance, created_at, updated_at)
SELECT pg_temp.demo_id('customer-' || number), name, '+8400000' || lpad(number::text, 4, '0'),
       'guest-' || lpad(number::text, 2, '0') || '@example.test', number <> 36, 0, opened, opened
FROM demo_clock CROSS JOIN (VALUES
    (1,'An Nguyễn'), (2,'Bình Trần'), (3,'Chi Lê'), (4,'Dũng Phạm'), (5,'Giang Võ'), (6,'Hạnh Đỗ'),
    (7,'Khánh Bùi'), (8,'Lan Phan'), (9,'Minh Đặng'), (10,'Nga Huỳnh'), (11,'Oanh Trương'), (12,'Phúc Lý'),
    (13,'Quỳnh Vũ'), (14,'Sơn Hoàng'), (15,'Tâm Đinh'), (16,'Thảo Trịnh'), (17,'Uyên Hồ'), (18,'Vân Cao'),
    (19,'Yến Lương'), (20,'Anh Nguyễn'), (21,'Bảo Trần'), (22,'Châu Lê'), (23,'Diệp Phạm'), (24,'Đức Võ'),
    (25,'Hà Đỗ'), (26,'Hải Bùi'), (27,'Kiên Phan'), (28,'Linh Đặng'), (29,'My Huỳnh'), (30,'Nam Trương'),
    (31,'Nhi Lý'), (32,'Nhung Vũ'), (33,'Tuấn Hoàng'), (34,'Tú Đinh'), (35,'Vi Trịnh'), (36,'Vy Hồ')
) AS people(number, name);

INSERT INTO customers.customer_addresses (id, customer_id, label, address_line, is_default, is_active)
SELECT pg_temp.demo_id('address-' || n || '-home'), pg_temp.demo_id('customer-' || n), 'Home',
       'Unit ' || (100 + n) || ', Demo Courtyard, ' ||
            (ARRAY['District 3', 'Binh Thanh', 'Phu Nhuan', 'District 1'])[1 + n % 4] || ', Ho Chi Minh City',
       true, true FROM generate_series(1, 35) AS n
UNION ALL
SELECT pg_temp.demo_id('address-' || n || '-work'), pg_temp.demo_id('customer-' || n), 'Office',
       'Suite ' || (220 + n) || ', Demo Exchange, District 1, Ho Chi Minh City', false, true
FROM generate_series(1, 12) AS n;

INSERT INTO customers.loyalty_settings (id, earn_points_per_currency_unit, redeem_value_per_point,
                                         max_redeemable_percentage, updated_at, updated_by)
SELECT pg_temp.demo_id('loyalty'), 0.001, 1000, 20, opened, pg_temp.demo_id('staff-manager') FROM demo_clock;

INSERT INTO sales.tables (id, table_number, seats, is_occupied, active_order_id)
SELECT pg_temp.demo_id('table-' || n), 'T' || lpad(n::text, 2, '0'), CASE WHEN n <= 4 THEN 2 ELSE 4 END, false, NULL
FROM generate_series(1, 8) AS n;

CREATE TEMP TABLE demo_tickets ON COMMIT DROP AS
WITH history AS (
    SELECT day, n, (day * 37 + n * 19) AS pattern,
           today - 43 + day AS business_date
    FROM demo_clock CROSS JOIN generate_series(1, 42) AS day
    CROSS JOIN LATERAL generate_series(1, 12 + day / 4 + (day * 13) % 9
        + CASE WHEN extract(isodow FROM today - 43 + day) >= 6 THEN 8 ELSE 0 END) AS n
)
SELECT day, n, pattern, business_date,
       CASE WHEN pattern % 7 < 3 THEN 'dine_in' WHEN pattern % 7 < 6 THEN 'takeaway' ELSE 'delivery' END AS channel,
       CASE WHEN pattern % 29 = 0 THEN 'cancelled' ELSE 'completed' END AS status,
       CASE WHEN pattern % 7 = 6 OR pattern % 5 < 2
            THEN CASE WHEN pattern % 8 < 4 THEN 1 + pattern % 12 ELSE 13 + pattern % 23 END END AS customer,
       CASE WHEN pattern % 7 < 3 THEN 1 + pattern % 8 END AS table_number,
       (business_date + time '07:00' + make_interval(mins => (n - 1) * 24 + pattern % 17))
            AT TIME ZONE 'Asia/Ho_Chi_Minh' AS created,
       pattern % 11 = 0 AND pattern % 29 <> 0 AS promotional,
       pattern % 17 = 0 AS paid
FROM history
UNION ALL
SELECT 43, live.n, live.n * 19, today, live.channel, live.status, live.customer, live.table_number,
       clock - make_interval(mins => live.minutes_ago), false, live.paid
FROM demo_clock CROSS JOIN (VALUES
    (1, 'dine_in', 'confirmed', 1, 1, 22, false),
    (2, 'takeaway', 'confirmed', NULL, NULL, 19, false),
    (3, 'dine_in', 'preparing', 2, 2, 31, false),
    (4, 'delivery', 'preparing', 8, NULL, 44, false),
    (5, 'dine_in', 'ready', 3, 3, 51, false),
    (6, 'takeaway', 'ready', NULL, NULL, 58, true),
    (7, 'delivery', 'ready', 12, NULL, 62, true),
    (8, 'dine_in', 'draft', 5, 4, 11, false)
) AS live(n, channel, status, customer, table_number, minutes_ago, paid);

ALTER TABLE demo_tickets ADD COLUMN id uuid;
UPDATE demo_tickets SET id = pg_temp.demo_id('order-' || day || '-' || n);

CREATE TEMP TABLE demo_lines ON COMMIT DROP AS
WITH selected AS (
    SELECT ticket.*, picked.line, menu.number, menu.name, menu.category, menu.large_extra,
           picked.line = 1 AND menu.large_extra IS NOT NULL AND ticket.pattern % 4 = 0 AS large
    FROM demo_tickets ticket
    CROSS JOIN LATERAL (
        VALUES
            (1, (ARRAY[4,7,9,4,20,3,5,9,2,11,4,7,10,16,1,20,6,8,17,12,18])[1 + ticket.pattern % 21]),
            (2, CASE WHEN ticket.pattern % 5 < 3 THEN (ARRAY[13,13,14,15])[1 + ticket.pattern % 4] END),
            (3, CASE WHEN ticket.channel = 'dine_in' AND ticket.pattern % 8 = 0 THEN 4 END)
    ) AS picked(line, menu_number)
    JOIN demo_menu menu ON menu.number = picked.menu_number
)
SELECT selected.id AS order_id, pg_temp.demo_id('line-' || day || '-' || n || '-' || line) AS id,
       pg_temp.demo_id('item-' || number) AS item_id, name AS item_name, line,
       variant.id AS variant_id, variant.size_name, variant.price,
       1 + CASE WHEN pattern % 13 = 0 AND line <= 2 THEN 1 ELSE 0 END AS quantity,
       CASE WHEN line = 1 AND number IN (1, 2, 3, 4, 5, 6, 7, 16, 17, 20) AND pattern % 9 = 0 THEN 'shot' END AS addon
FROM selected JOIN catalog.size_variants variant
  ON variant.id = pg_temp.demo_id('size-' || number || CASE WHEN large THEN '-large' ELSE '-regular' END);

CREATE TEMP TABLE demo_totals ON COMMIT DROP AS
WITH totals AS (
    SELECT order_id, sum(line.quantity * (line.price + coalesce(addon.price, 0))) AS subtotal
    FROM demo_lines line LEFT JOIN catalog.addons addon ON addon.id = pg_temp.demo_id('addon-' || line.addon)
    GROUP BY order_id
)
SELECT ticket.id, subtotal,
       CASE WHEN promotional THEN round(subtotal * 0.10, 2) ELSE 0 END AS discount,
       CASE WHEN status = 'completed' AND customer <= 12 AND day >= 20 AND pattern % 19 = 0
                 AND count(*) FILTER (WHERE status = 'completed') OVER (
                     PARTITION BY customer ORDER BY created, ticket.id ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) >= 3
            THEN 5 ELSE 0 END AS redeemed
FROM demo_tickets ticket JOIN totals ON totals.order_id = ticket.id;

INSERT INTO sales.orders (id, order_number, channel, status, customer_id, table_id, subtotal, discount_total,
    total, paid_amount, loyalty_points_redeemed, loyalty_discount_amount, created_by, created_at,
    confirmed_at, completed_at, cancelled_at, cancel_reason)
SELECT ticket.id, 'CC-' || to_char(business_date, 'YYMMDD') || '-' || lpad(n::text, 3, '0'),
       channel, status, pg_temp.demo_id('customer-' || customer), pg_temp.demo_id('table-' || table_number),
       subtotal, discount, subtotal - discount - redeemed * 1000,
       CASE WHEN status = 'completed' OR (day = 43 AND paid)
            THEN subtotal - discount - redeemed * 1000 ELSE 0 END,
       redeemed, redeemed * 1000, pg_temp.demo_id('staff-manager'), created,
       CASE WHEN status <> 'draft' THEN created + interval '1 minute' END,
       CASE WHEN status = 'completed' THEN created +
            CASE WHEN channel = 'delivery' THEN interval '37 minutes' ELSE interval '14 minutes' END END,
       CASE WHEN status = 'cancelled' THEN created + interval '5 minutes' END,
       CASE WHEN status = 'cancelled' THEN
            CASE WHEN pattern % 2 = 0 THEN 'Guest could not wait' ELSE 'Duplicate ticket corrected before preparation' END END
FROM demo_tickets ticket JOIN demo_totals amounts ON amounts.id = ticket.id;

UPDATE sales.tables seating
SET is_occupied = true, active_order_id = ticket.id
FROM demo_tickets ticket
WHERE ticket.day = 43 AND ticket.status IN ('confirmed', 'preparing', 'ready')
  AND seating.id = pg_temp.demo_id('table-' || ticket.table_number);

INSERT INTO sales.order_lines (id, order_id, menu_item_id, size_variant_id, item_name_snapshot,
    size_name_snapshot, unit_price_snapshot, quantity, line_total)
SELECT line.id, order_id, item_id, variant_id, item_name, size_name, line.price, quantity,
       quantity * (line.price + coalesce(addon.price, 0))
FROM demo_lines line LEFT JOIN catalog.addons addon ON addon.id = pg_temp.demo_id('addon-' || line.addon);

INSERT INTO sales.order_line_addons (id, order_line_id, addon_id, addon_name_snapshot, addon_price_snapshot)
SELECT pg_temp.demo_id('order-addon-' || line.id), line.id, addon.id, addon.name, addon.price
FROM demo_lines line JOIN catalog.addons addon ON addon.id = pg_temp.demo_id('addon-' || line.addon);

INSERT INTO sales.order_status_history (id, order_id, from_status, to_status, changed_by, changed_at)
SELECT pg_temp.demo_id('order-event-' || ticket.id || '-' || step.to_status),
       ticket.id, step.from_status, step.to_status, pg_temp.demo_id('staff-' || step.actor), step.occurred_at
FROM demo_tickets ticket JOIN sales.orders orders ON orders.id = ticket.id
CROSS JOIN LATERAL (VALUES
    (NULL, 'draft', 'manager', ticket.created),
    ('draft', 'confirmed', 'manager', orders.confirmed_at),
    ('confirmed', 'preparing', 'barista', CASE WHEN ticket.status IN ('preparing', 'ready', 'completed')
        THEN ticket.created + interval '3 minutes' END),
    ('preparing', 'ready', 'barista', CASE WHEN ticket.status IN ('ready', 'completed')
        THEN ticket.created + interval '8 minutes' END),
    ('ready', 'completed', 'manager', orders.completed_at),
    ('confirmed', 'cancelled', 'manager', orders.cancelled_at)
) AS step(from_status, to_status, actor, occurred_at)
WHERE step.occurred_at IS NOT NULL;

INSERT INTO sales.discounts (id, order_id, type, value, reason, status, requested_by, requested_at,
    approved_by, approved_at, computed_amount)
SELECT pg_temp.demo_id('discount-' || ticket.id), ticket.id, 'percentage', 10,
       CASE WHEN ticket.promotional THEN 'Neighborhood loyalty promotion' ELSE 'Staff request for teacher demo' END,
       CASE WHEN ticket.promotional THEN 'approved' ELSE 'pending' END,
       pg_temp.demo_id('staff-manager'), created,
       CASE WHEN ticket.promotional THEN pg_temp.demo_id('staff-manager') END,
       CASE WHEN ticket.promotional THEN created + interval '30 seconds' END,
       CASE WHEN ticket.promotional THEN amounts.discount END
FROM demo_tickets ticket JOIN demo_totals amounts ON amounts.id = ticket.id
WHERE ticket.promotional OR (day = 43 AND n = 8);

INSERT INTO sales.payments (id, order_id, method, amount, status, reference, paid_at, recorded_by)
SELECT pg_temp.demo_id('payment-' || orders.id || '-' || payment.part), orders.id, payment.method,
       payment.amount, 'succeeded',
       CASE WHEN payment.method <> 'cash' THEN orders.order_number || '-PAY-' || payment.part END,
       ticket.created + make_interval(mins => 9 + payment.part), pg_temp.demo_id('staff-manager')
FROM demo_tickets ticket JOIN sales.orders orders ON orders.id = ticket.id
CROSS JOIN LATERAL (SELECT ticket.day < 43 AND ticket.pattern % 23 = 0 AS split) option
CROSS JOIN LATERAL (VALUES
    (1, CASE WHEN option.split THEN 'cash' ELSE (ARRAY['cash', 'card', 'e_wallet'])[1 + ticket.pattern % 3] END,
        CASE WHEN option.split THEN floor(orders.total * 0.6) ELSE orders.total END),
    (2, 'e_wallet', CASE WHEN option.split THEN orders.total - floor(orders.total * 0.6) END)
) AS payment(part, method, amount)
WHERE orders.paid_amount > 0 AND payment.amount IS NOT NULL;

INSERT INTO sales.deliveries (id, order_id, recipient_name, contact_number, address_line, customer_address_id,
    status, assigned_staff_id, assigned_at, out_for_delivery_at, delivered_at)
SELECT pg_temp.demo_id('delivery-' || ticket.id), ticket.id, customer.full_name, customer.phone, address.address_line,
       address.id,
       CASE ticket.status WHEN 'completed' THEN 'delivered' WHEN 'ready' THEN 'assigned' ELSE 'unassigned' END,
       CASE WHEN ticket.status IN ('completed', 'ready') THEN pg_temp.demo_id('staff-courier') END,
       CASE WHEN ticket.status IN ('completed', 'ready') THEN created + interval '9 minutes' END,
       CASE WHEN ticket.status = 'completed' THEN created + interval '17 minutes' END,
       CASE WHEN ticket.status = 'completed' THEN created + interval '35 minutes' END
FROM demo_tickets ticket
JOIN customers.customers customer ON customer.id = pg_temp.demo_id('customer-' || ticket.customer)
JOIN customers.customer_addresses address ON address.id = pg_temp.demo_id('address-' || ticket.customer ||
    CASE WHEN ticket.customer <= 12 AND ticket.pattern % 4 = 0 THEN '-work' ELSE '-home' END)
WHERE ticket.channel = 'delivery';

INSERT INTO sales.delivery_status_history (id, delivery_id, from_status, to_status, actor_id, changed_at)
SELECT pg_temp.demo_id('delivery-event-' || delivery.id || '-' || step.to_status),
       delivery.id, step.from_status, step.to_status, pg_temp.demo_id('staff-' || step.actor), step.occurred_at
FROM sales.deliveries delivery
CROSS JOIN LATERAL (VALUES
    ('unassigned', 'assigned', 'manager', delivery.assigned_at),
    ('assigned', 'out_for_delivery', 'courier', delivery.out_for_delivery_at),
    ('out_for_delivery', 'delivered', 'courier', delivery.delivered_at)
) AS step(from_status, to_status, actor, occurred_at)
WHERE step.occurred_at IS NOT NULL;

INSERT INTO customers.loyalty_transactions
    (id, customer_id, order_id, type, points, balance_after, created_at, created_by)
WITH entries AS (
    SELECT id, customer_id, 'earn' AS type, floor(total / 1000)::int AS points, completed_at AS occurred_at
    FROM sales.orders WHERE status = 'completed' AND customer_id IS NOT NULL
    UNION ALL
    SELECT id, customer_id, 'redeem', -loyalty_points_redeemed, created_at
    FROM sales.orders WHERE loyalty_points_redeemed > 0
)
SELECT pg_temp.demo_id('loyalty-entry-' || id || '-' || type), customer_id, id, type, points,
       sum(points) OVER (PARTITION BY customer_id ORDER BY occurred_at, id, type ROWS UNBOUNDED PRECEDING),
       occurred_at, pg_temp.demo_id('staff-manager')
FROM entries;

UPDATE customers.customers customer SET loyalty_points_balance = total.points
FROM (SELECT customer_id, sum(points)::int AS points FROM customers.loyalty_transactions GROUP BY customer_id) total
WHERE customer.id = total.customer_id;

CREATE TEMP TABLE demo_usage ON COMMIT DROP AS
SELECT tickets.day, tickets.n, tickets.id AS order_id, tickets.status, orders.confirmed_at, orders.cancelled_at,
       recipe.ingredient_id, sum(line.quantity * recipe.quantity) AS quantity
FROM demo_lines line
JOIN demo_tickets tickets ON tickets.id = line.order_id
JOIN sales.orders orders ON orders.id = tickets.id
JOIN catalog.recipe_ingredients recipe ON recipe.size_variant_id = line.variant_id
WHERE tickets.status <> 'draft'
GROUP BY tickets.day, tickets.n, tickets.id, tickets.status, orders.confirmed_at, orders.cancelled_at, recipe.ingredient_id;

INSERT INTO inventory.stock_movements (id, ingredient_id, type, quantity_delta, actor_id, reason, occurred_at, order_id)
SELECT pg_temp.demo_id('stock-used-' || order_id || '-' || ingredient_id), ingredient_id, 2, -quantity,
       pg_temp.demo_id('staff-barista'), 'Ingredients used for confirmed ticket', confirmed_at, order_id
FROM demo_usage
UNION ALL
SELECT pg_temp.demo_id('stock-restored-' || order_id || '-' || ingredient_id), ingredient_id, 3, quantity,
       pg_temp.demo_id('staff-manager'), 'Restored after ticket cancellation', cancelled_at, order_id
FROM demo_usage WHERE status = 'cancelled';

INSERT INTO inventory.order_inventory_restorations (order_id, restored_at, restored_by)
SELECT id, cancelled_at, pg_temp.demo_id('staff-manager') FROM sales.orders WHERE status = 'cancelled';

CREATE TEMP TABLE demo_adjustments ON COMMIT DROP AS
SELECT key, quantity, reason,
       CASE WHEN days_ago = 0 THEN clock - interval '70 minutes'
            ELSE (today - days_ago + time '22:00') AT TIME ZONE 'Asia/Ho_Chi_Minh' END AS occurred_at
FROM demo_clock CROSS JOIN (VALUES
    ('vanilla', -250, 'Damaged syrup bottle removed after the evening count', 2),
    ('milk', -1500, 'End-of-shift dairy waste', 5),
    ('beans', -360, 'Grinder calibration and dial-in', 1),
    ('oat', -320, 'Open cartons discarded at shift handover', 0),
    ('butter', -4, 'Unsold pastries removed at closing', 3)
) AS adjustments(key, quantity, reason, days_ago);

INSERT INTO inventory.stock_movements (id, ingredient_id, type, quantity_delta, actor_id, reason, occurred_at)
SELECT pg_temp.demo_id('stock-adjustment-' || key), pg_temp.demo_id('ingredient-' || key), 1, quantity,
       pg_temp.demo_id('staff-manager'), reason, occurred_at
FROM demo_adjustments;

CREATE TEMP TABLE demo_incoming ON COMMIT DROP AS
WITH schedule AS (
    SELECT stock.*, pg_temp.demo_id('ingredient-' || stock.key) AS ingredient_id, slot,
           CASE WHEN stock.supplier = 'bakery' THEN slot ELSE 1 + 7 * (slot - 1) END AS first_day,
           CASE WHEN stock.supplier = 'bakery' THEN slot ELSE 7 * slot END AS last_day
    FROM demo_stock stock
    CROSS JOIN LATERAL generate_series(1, CASE WHEN stock.supplier = 'bakery' THEN 43 ELSE 7 END) AS slot
)
SELECT schedule.key, supplier, schedule.ingredient_id, first_day, cost,
       coalesce(sum(usage.quantity) FILTER (WHERE usage.status <> 'cancelled'), 0) +
       CASE WHEN slot = 1 THEN
           closing + coalesce((SELECT -sum(quantity) FROM demo_adjustments WHERE key = schedule.key), 0)
       ELSE 0 END AS quantity
FROM schedule
LEFT JOIN demo_usage usage ON usage.ingredient_id = schedule.ingredient_id AND usage.day BETWEEN first_day AND last_day
GROUP BY schedule.key, supplier, schedule.ingredient_id, first_day, cost, slot, closing;

INSERT INTO inventory.purchase_receipts (id, supplier_id, receipt_number, status, posted_at, created_at, updated_at)
SELECT DISTINCT pg_temp.demo_id('receipt-' || supplier || '-' || first_day), pg_temp.demo_id('supplier-' || supplier),
       'CC-RCV-' || upper(left(supplier, 3)) || '-' || to_char(today - 43 + first_day, 'YYMMDD'),
       1, arrival.occurred_at, arrival.occurred_at, arrival.occurred_at
FROM demo_incoming CROSS JOIN demo_clock
CROSS JOIN LATERAL (SELECT least(clock - interval '90 minutes',
    (today - 43 + first_day + time '05:45') AT TIME ZONE 'Asia/Ho_Chi_Minh') AS occurred_at) arrival
WHERE quantity > 0;

INSERT INTO inventory.purchase_receipt_lines (id, ingredient_id, quantity, unit_cost, purchase_receipt_id)
SELECT pg_temp.demo_id('receipt-line-' || key || '-' || first_day), ingredient_id, quantity, cost,
       pg_temp.demo_id('receipt-' || supplier || '-' || first_day)
FROM demo_incoming WHERE quantity > 0;

INSERT INTO inventory.stock_movements
    (id, ingredient_id, type, quantity_delta, actor_id, reason, occurred_at, purchase_receipt_id)
SELECT pg_temp.demo_id('received-' || line.id), line.ingredient_id, 0, line.quantity,
       pg_temp.demo_id('staff-manager'), 'Supplier delivery ' || receipt.receipt_number, receipt.posted_at, receipt.id
FROM inventory.purchase_receipt_lines line JOIN inventory.purchase_receipts receipt ON receipt.id = line.purchase_receipt_id;

INSERT INTO inventory.purchase_receipts (id, supplier_id, receipt_number, status, created_at, updated_at)
SELECT pg_temp.demo_id('draft-receipt'), pg_temp.demo_id('supplier-roastery'), 'CC-DRAFT-' || to_char(today, 'YYMMDD'),
       0, clock - interval '30 minutes', clock - interval '30 minutes'
FROM demo_clock;
INSERT INTO inventory.purchase_receipt_lines (id, ingredient_id, quantity, unit_cost, purchase_receipt_id)
VALUES (pg_temp.demo_id('draft-receipt-beans'), pg_temp.demo_id('ingredient-beans'), 6000, 310, pg_temp.demo_id('draft-receipt'));

UPDATE inventory.ingredients ingredient
SET available_quantity = total.quantity, updated_at = total.last_movement
FROM (SELECT ingredient_id, sum(quantity_delta) AS quantity, max(occurred_at) AS last_movement
      FROM inventory.stock_movements GROUP BY ingredient_id) total
WHERE ingredient.id = total.ingredient_id;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM demo_stock expected JOIN inventory.ingredients ingredient
        ON ingredient.id = pg_temp.demo_id('ingredient-' || expected.key)
        WHERE ingredient.available_quantity <> expected.closing
    ) THEN
        RAISE EXCEPTION 'Expected closing stock did not reconcile.';
    END IF;
END $$;

COMMIT;

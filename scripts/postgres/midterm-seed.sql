-- Deterministic midterm demonstration dataset. Run after applying EF migrations.
-- Safe to rerun: only rows owned by this script's stable IDs/names are replaced.
BEGIN;

CREATE OR REPLACE FUNCTION pg_temp.midterm_id(key text) RETURNS uuid
LANGUAGE sql IMMUTABLE AS $$ SELECT md5('coffeeshop-midterm-v1:' || key)::uuid $$;

DO $$
DECLARE
    stamp timestamptz := '2026-10-01 08:00:00+00';
    actor uuid := pg_temp.midterm_id('staff-manager');
    courier uuid := pg_temp.midterm_id('staff-delivery');
BEGIN
    -- Remove only this dataset, in dependency order. Stable IDs keep cleanup safe
    -- while the displayed data remains realistic for the midterm demonstration.
    UPDATE sales.tables SET active_order_id = NULL WHERE id IN
        (SELECT pg_temp.midterm_id('table-' || n) FROM generate_series(1,8) AS n);
    DELETE FROM customers.loyalty_transactions WHERE customer_id IN
        (SELECT pg_temp.midterm_id('customer-' || n) FROM generate_series(1,12) AS n);
    DELETE FROM sales.orders WHERE id IN
        (SELECT pg_temp.midterm_id('order-' || n) FROM generate_series(1,24) AS n);
    DELETE FROM sales.tables WHERE id IN
        (SELECT pg_temp.midterm_id('table-' || n) FROM generate_series(1,8) AS n);
    DELETE FROM inventory.stock_movements WHERE actor_id = actor;
    DELETE FROM inventory.purchase_receipts WHERE id IN
        (SELECT pg_temp.midterm_id('receipt-' || n) FROM generate_series(1,2) AS n);
    DELETE FROM catalog.recipe_ingredients WHERE size_variant_id IN
        (SELECT pg_temp.midterm_id('size-' || n || '-' || size_key)
         FROM generate_series(1,20) AS n
         CROSS JOIN (VALUES ('regular'),('large')) AS sizes(size_key));
    DELETE FROM catalog.size_variants WHERE menu_item_id IN
        (SELECT pg_temp.midterm_id('product-' || n) FROM generate_series(1,20) AS n);
    DELETE FROM catalog.menu_items WHERE id IN
        (SELECT pg_temp.midterm_id('product-' || n) FROM generate_series(1,20) AS n);
    DELETE FROM catalog.categories WHERE id IN
        (SELECT pg_temp.midterm_id('category-' || n) FROM generate_series(1,5) AS n);
    DELETE FROM inventory.suppliers WHERE id IN
        (SELECT pg_temp.midterm_id('supplier-' || n) FROM generate_series(1,3) AS n);
    DELETE FROM customers.customers WHERE id IN
        (SELECT pg_temp.midterm_id('customer-' || n) FROM generate_series(1,12) AS n);
    DELETE FROM customers.loyalty_settings WHERE id = pg_temp.midterm_id('loyalty-settings');
    DELETE FROM inventory.ingredients WHERE id IN
        (SELECT pg_temp.midterm_id('ingredient-' || n) FROM generate_series(1,9) AS n);
    DELETE FROM identity.user_roles WHERE user_id IN (actor, courier);
    DELETE FROM identity.users WHERE id IN (actor, courier);

    INSERT INTO identity.users (id, username, email, password_hash, full_name, phone, is_active, created_at, updated_at)
    VALUES (actor, 'demo_manager', 'manager@example.test', 'demo-only-not-for-authentication', 'Linh Tran', '+84900000000', true, stamp, stamp);

    INSERT INTO identity.users (id, username, email, password_hash, full_name, phone, is_active, created_at, updated_at)
    VALUES (courier, 'demo_courier', 'courier@example.test', 'demo-only-not-for-authentication', 'Huy Nguyen', '+84900000001', true, stamp, stamp);
    INSERT INTO identity.roles (id, code, name)
    VALUES (pg_temp.midterm_id('role-delivery'), 'DeliveryStaff', 'Delivery Staff')
    ON CONFLICT (code) DO NOTHING;
    INSERT INTO identity.user_roles (user_id, role_id, assigned_at, assigned_by)
    SELECT courier, id, stamp, actor FROM identity.roles WHERE code = 'DeliveryStaff';

    INSERT INTO catalog.categories (id, name, display_order, is_active, created_at, updated_at)
    SELECT pg_temp.midterm_id('category-' || n), name, n, true, stamp, stamp
    FROM (VALUES (1,'Coffee'),(2,'Tea'),(3,'Blended Drinks'),(4,'Bakery'),(5,'Seasonal Specials')) AS c(n,name);

    INSERT INTO catalog.menu_items (id, category_id, name, description, is_active, created_at, updated_at)
    SELECT pg_temp.midterm_id('product-' || p.n), pg_temp.midterm_id('category-' || CASE
               WHEN p.n BETWEEN 1 AND 9 THEN 1       -- Coffee
               WHEN p.n BETWEEN 10 AND 12 THEN 2     -- Tea
               WHEN p.n BETWEEN 13 AND 15 THEN 3     -- Blended
               WHEN p.n BETWEEN 16 AND 18 THEN 4     -- Bakery
               ELSE 5                                 -- Seasonal
           END),
           p.name, p.description, true, stamp, stamp
    FROM (VALUES
        (1,'House Espresso','Double shot, chocolate finish'),(2,'Americano','Espresso with hot water'),
        (3,'Cappuccino','Espresso, steamed milk, cocoa'),(4,'Cafe Latte','Balanced espresso and milk'),
        (5,'Vanilla Latte','House vanilla syrup and milk'),(6,'Caramel Macchiato','Vanilla, milk, caramel'),
        (7,'Mocha','Dark chocolate and espresso'),(8,'Cold Brew','Slow-steeped coffee'),
        (9,'Coconut Cold Brew','Cold brew with coconut milk'),(10,'Matcha Latte','Stone-ground matcha'),
        (11,'Peach Green Tea','Green tea and peach'),(12,'Jasmine Milk Tea','Jasmine tea and milk'),
        (13,'Strawberry Frappe','Strawberry and crushed ice'),(14,'Chocolate Frappe','Chocolate blended with ice'),
        (15,'Mango Smoothie','Mango and yogurt'),(16,'Butter Croissant','Flaky baked pastry'),
        (17,'Almond Croissant','Croissant with almond cream'),(18,'Blueberry Muffin','Baked with blueberries'),
        (19,'Pumpkin Spice Latte','Seasonal spice and espresso'),(20,'Honey Oat Latte','Oat milk and local honey')
    ) AS p(n,name,description);

    INSERT INTO catalog.size_variants (id, menu_item_id, size_name, price, is_active)
    SELECT pg_temp.midterm_id('size-' || p.n || '-' || s.size_key), pg_temp.midterm_id('product-' || p.n), s.size_name,
           (p.base_price + s.bump)::numeric(12,2), true
    FROM (VALUES
        (1,45.00),(2,42.00),(3,52.00),(4,55.00),(5,62.00),(6,65.00),(7,64.00),(8,58.00),(9,68.00),(10,60.00),
        (11,55.00),(12,59.00),(13,72.00),(14,70.00),(15,75.00),(16,48.00),(17,58.00),(18,45.00),(19,68.00),(20,63.00)
    ) AS p(n,base_price)
    CROSS JOIN (VALUES ('regular','Regular',0.00),('large','Large',12.00)) AS s(size_key,size_name,bump);

    INSERT INTO inventory.ingredients (id, name, base_unit, available_quantity, reorder_threshold, is_active, created_at, updated_at)
    VALUES
      (pg_temp.midterm_id('ingredient-1'),'Arabica beans','g',500,500,true,stamp,stamp),
      (pg_temp.midterm_id('ingredient-2'),'Whole milk','ml',3000,1000,true,stamp,stamp),
      (pg_temp.midterm_id('ingredient-3'),'Oat milk','ml',3500,500,true,stamp,stamp),
      (pg_temp.midterm_id('ingredient-4'),'Matcha powder','g',450,100,true,stamp,stamp),
      (pg_temp.midterm_id('ingredient-5'),'Black tea','g',700,150,true,stamp,stamp),
      (pg_temp.midterm_id('ingredient-6'),'Peach puree','ml',1800,300,true,stamp,stamp),
      (pg_temp.midterm_id('ingredient-7'),'Chocolate sauce','ml',1200,250,true,stamp,stamp),
      (pg_temp.midterm_id('ingredient-8'),'Vanilla syrup','ml',14,150,true,stamp,stamp),
      (pg_temp.midterm_id('ingredient-9'),'Pastry base','pcs',80,20,true,stamp,stamp);

    INSERT INTO catalog.recipe_ingredients (id, size_variant_id, ingredient_id, quantity, unit)
    SELECT pg_temp.midterm_id('recipe-' || p.n || '-' || s.size_key), pg_temp.midterm_id('size-' || p.n || '-' || s.size_key),
           pg_temp.midterm_id('ingredient-' || p.ingredient), (p.qty * s.multiplier)::numeric(12,3), p.unit
    FROM (VALUES
      (1,1,18.0,'g'),(2,1,18.0,'g'),(3,1,18.0,'g'),(4,1,18.0,'g'),(5,1,18.0,'g'),
      (6,1,18.0,'g'),(7,1,18.0,'g'),(8,1,22.0,'g'),(9,1,22.0,'g'),(10,4,4.0,'g'),
      (11,5,5.0,'g'),(12,5,6.0,'g'),(13,6,80.0,'ml'),(14,7,35.0,'ml'),(15,6,100.0,'ml'),
      (16,9,1.0,'pcs'),(17,9,1.0,'pcs'),(18,9,1.0,'pcs'),(19,1,18.0,'g'),(20,1,18.0,'g')
    ) AS p(n,ingredient,qty,unit)
    CROSS JOIN (VALUES ('regular',1.0),('large',1.35)) AS s(size_key,multiplier);

    INSERT INTO inventory.suppliers (id,name,phone,email,address,is_active,created_at,updated_at)
    VALUES
      (pg_temp.midterm_id('supplier-1'),'Highland Roasters','0281234567','beans@example.test','Da Lat',true,stamp,stamp),
      (pg_temp.midterm_id('supplier-2'),'Fresh Dairy Co.','0281234568','dairy@example.test','Thu Duc',true,stamp,stamp),
      (pg_temp.midterm_id('supplier-3'),'Tea and Pantry Co.','0281234569','tea@example.test','District 5',true,stamp,stamp);

    INSERT INTO customers.customers (id,full_name,phone,email,is_active,loyalty_points_balance,created_at,updated_at)
    SELECT pg_temp.midterm_id('customer-' || n), full_name, '+8491008' || lpad(n::text,4,'0'),
           'customer' || n || '@example.test', true, CASE WHEN n <= 4 THEN 120 ELSE 0 END, stamp, stamp
    FROM (VALUES (1,'An Nguyen'),(2,'Binh Tran'),(3,'Chi Le'),(4,'Duc Pham'),(5,'Em Hoang'),(6,'Giang Vo'),
          (7,'Hanh Do'),(8,'Khanh Bui'),(9,'Lan Phan'),(10,'Minh Dang'),(11,'Nga Huynh'),(12,'Oanh Truong')) AS c(n,full_name);

    INSERT INTO customers.customer_addresses (id,customer_id,label,address_line,is_default,is_active)
    SELECT pg_temp.midterm_id('address-' || n), pg_temp.midterm_id('customer-' || n), 'Home',
           n || ' Demo Street, Ho Chi Minh City', true, true FROM generate_series(1,12) AS n;

    INSERT INTO customers.loyalty_settings (id,earn_points_per_currency_unit,redeem_value_per_point,max_redeemable_percentage,updated_at,updated_by)
    VALUES (pg_temp.midterm_id('loyalty-settings'),0.01,0.5,20,stamp,actor);

    INSERT INTO sales.tables (id,table_number,seats,is_occupied,active_order_id)
    SELECT pg_temp.midterm_id('table-' || n), 'T' || lpad(n::text,2,'0'), CASE WHEN n % 2 = 0 THEN 4 ELSE 2 END, false, NULL
    FROM generate_series(1,8) AS n;

    INSERT INTO inventory.purchase_receipts (id,supplier_id,receipt_number,status,posted_at,created_at,updated_at)
    VALUES (pg_temp.midterm_id('receipt-1'),pg_temp.midterm_id('supplier-1'),'RC-2026-1001',1,stamp,stamp,stamp),
           (pg_temp.midterm_id('receipt-2'),pg_temp.midterm_id('supplier-2'),'RC-2026-1002',1,stamp + interval '1 day',stamp,stamp);

    INSERT INTO inventory.purchase_receipt_lines (id,ingredient_id,quantity,unit_cost,purchase_receipt_id)
    VALUES (pg_temp.midterm_id('receipt-line-1'),pg_temp.midterm_id('ingredient-1'),2000,0.08,pg_temp.midterm_id('receipt-1')),
           (pg_temp.midterm_id('receipt-line-2'),pg_temp.midterm_id('ingredient-2'),5000,0.03,pg_temp.midterm_id('receipt-2'));

    INSERT INTO inventory.stock_movements (id,ingredient_id,type,quantity_delta,actor_id,reason,occurred_at,purchase_receipt_id,order_id)
    VALUES (pg_temp.midterm_id('movement-receipt-1'),pg_temp.midterm_id('ingredient-1'),0,2000,actor,'Opening demo purchase receipt',stamp,pg_temp.midterm_id('receipt-1'),NULL),
           (pg_temp.midterm_id('movement-receipt-2'),pg_temp.midterm_id('ingredient-2'),0,5000,actor,'Opening demo purchase receipt',stamp,pg_temp.midterm_id('receipt-2'),NULL),
           (pg_temp.midterm_id('movement-adjustment-1'),pg_temp.midterm_id('ingredient-8'),1,-10,actor,'Demo waste adjustment',stamp + interval '2 days',NULL,NULL);
    UPDATE inventory.ingredients SET available_quantity = available_quantity - 10, updated_at = stamp + interval '2 days'
    WHERE id = pg_temp.midterm_id('ingredient-8');

    WITH source AS (
        SELECT n, (((n - 1) % 20) + 1) AS product_number,
               CASE WHEN n % 2 = 0 THEN 'large' ELSE 'regular' END AS size_key,
               1 + (n % 3) AS quantity, sv.price * (1 + (n % 3)) AS subtotal,
               CASE WHEN n % 5 = 0 THEN
                    CASE WHEN n % 2 = 0 THEN round(sv.price * (1 + (n % 3)) * 0.10, 2) ELSE 8::numeric END
                    ELSE 0::numeric END AS discount,
               CASE WHEN n = 2 THEN 10::numeric ELSE 0::numeric END AS loyalty_discount
        FROM generate_series(1,24) AS n
        JOIN catalog.size_variants sv ON sv.id = pg_temp.midterm_id('size-' || (((n - 1) % 20) + 1) ||
            CASE WHEN n % 2 = 0 THEN '-large' ELSE '-regular' END)
    )
    INSERT INTO sales.orders (id,order_number,channel,status,customer_id,table_id,subtotal,discount_total,total,paid_amount,
        loyalty_points_redeemed,loyalty_discount_amount,created_by,created_at,confirmed_at,completed_at,cancelled_at,cancel_reason)
    SELECT pg_temp.midterm_id('order-' || n), 'ORD-2026-' || lpad(n::text,3,'0'),
           CASE WHEN n % 3 = 1 THEN 'dine_in' WHEN n % 3 = 2 THEN 'takeaway' ELSE 'delivery' END,
           CASE WHEN n IN (5,14) THEN 'cancelled' WHEN n IN (7,16) THEN 'ready' WHEN n = 21 THEN 'preparing' ELSE 'completed' END,
           CASE WHEN n % 4 = 0 THEN NULL ELSE pg_temp.midterm_id('customer-' || (((n - 1) % 12) + 1)) END,
           CASE WHEN n % 3 = 1 THEN pg_temp.midterm_id('table-' || (((n - 1) % 8) + 1)) ELSE NULL END,
           subtotal, discount, subtotal - discount - loyalty_discount,
           CASE WHEN n IN (5,14) THEN 0 ELSE subtotal - discount - loyalty_discount END,
           CASE WHEN n = 2 THEN 20 ELSE 0 END, loyalty_discount,
           actor, stamp + (n::text || ' hours')::interval, stamp + (n::text || ' hours')::interval,
           CASE WHEN n IN (5,14,7,16,21) THEN NULL ELSE stamp + ((n + 1)::text || ' hours')::interval END,
           CASE WHEN n IN (5,14) THEN stamp + ((n + 1)::text || ' hours')::interval ELSE NULL END,
           CASE WHEN n IN (5,14) THEN 'Customer changed plans' ELSE NULL END
    FROM source;

    INSERT INTO sales.order_lines (id,order_id,menu_item_id,size_variant_id,item_name_snapshot,size_name_snapshot,unit_price_snapshot,quantity,line_total)
    SELECT pg_temp.midterm_id('order-line-' || n), pg_temp.midterm_id('order-' || n), mi.id, sv.id,
           mi.name, sv.size_name, sv.price, 1 + (n % 3), sv.price * (1 + (n % 3))
    FROM generate_series(1,24) AS n
    JOIN catalog.menu_items mi ON mi.id = pg_temp.midterm_id('product-' || (((n - 1) % 20) + 1))
    JOIN catalog.size_variants sv ON sv.menu_item_id = mi.id AND sv.id = pg_temp.midterm_id('size-' || (((n - 1) % 20) + 1) ||
        CASE WHEN n % 2 = 0 THEN '-large' ELSE '-regular' END);

    INSERT INTO sales.payments (id,order_id,method,amount,status,reference,paid_at,recorded_by)
    SELECT pg_temp.midterm_id('payment-' || n), pg_temp.midterm_id('order-' || n),
           CASE WHEN n % 3 = 0 THEN 'e_wallet' WHEN n % 3 = 1 THEN 'cash' ELSE 'card' END,
           o.total, 'succeeded', CASE WHEN n % 3 = 1 THEN NULL ELSE 'PAY-2026-' || n END, o.created_at + interval '10 minutes', actor
    FROM generate_series(1,24) AS n JOIN sales.orders o ON o.id = pg_temp.midterm_id('order-' || n)
    WHERE o.status <> 'cancelled';

    INSERT INTO sales.discounts (id,order_id,type,value,reason,status,requested_by,requested_at,approved_by,approved_at,computed_amount)
    SELECT pg_temp.midterm_id('discount-' || n), o.id,
           CASE WHEN n % 2 = 0 THEN 'percentage' ELSE 'fixed' END, CASE WHEN n % 2 = 0 THEN 10 ELSE 8 END,
           'October opening promotion', 'approved', actor, o.created_at,
           actor, o.created_at, o.discount_total
    FROM generate_series(10,20,5) AS n JOIN sales.orders o ON o.id = pg_temp.midterm_id('order-' || n);

    INSERT INTO sales.deliveries (id,order_id,recipient_name,contact_number,address_line,customer_address_id,status,assigned_staff_id,assigned_at,out_for_delivery_at,delivered_at,failed_at,failure_reason)
    SELECT pg_temp.midterm_id('delivery-' || n), o.id, COALESCE(c.full_name,'Walk-in Recipient'), COALESCE(c.phone,'0901234567'),
           COALESCE(a.address_line,'88 Demo Avenue, Ho Chi Minh City'), a.id,
           CASE WHEN n = 18 THEN 'failed' WHEN n IN (6,21) THEN 'assigned' WHEN n IN (9,15) THEN 'out_for_delivery'
                WHEN n IN (12,24) THEN 'delivered' ELSE 'unassigned' END,
           CASE WHEN n IN (6,9,12,15,18,21,24) THEN courier ELSE NULL END,
           CASE WHEN n IN (6,9,12,15,18,21,24) THEN o.created_at + interval '15 minutes' ELSE NULL END,
           CASE WHEN n IN (9,12,15,18,24) THEN o.created_at + interval '30 minutes' ELSE NULL END,
           CASE WHEN n IN (12,24) THEN o.created_at + interval '1 hour' ELSE NULL END,
           CASE WHEN n = 18 THEN o.created_at + interval '1 hour' ELSE NULL END,
           CASE WHEN n = 18 THEN 'Recipient unavailable' ELSE NULL END
    FROM generate_series(1,24) AS n JOIN sales.orders o ON o.id = pg_temp.midterm_id('order-' || n)
    LEFT JOIN customers.customers c ON c.id = o.customer_id
    LEFT JOIN customers.customer_addresses a ON a.customer_id = c.id AND a.is_default
    WHERE o.channel = 'delivery';

    INSERT INTO sales.order_status_history (id,order_id,from_status,to_status,changed_by,changed_at)
    SELECT pg_temp.midterm_id('history-' || n),o.id,NULL,o.status,actor,o.created_at
    FROM generate_series(1,24) AS n JOIN sales.orders o ON o.id = pg_temp.midterm_id('order-' || n);

    INSERT INTO customers.loyalty_transactions (id,customer_id,order_id,type,points,balance_after,reverses_transaction_id,created_at,created_by)
    SELECT pg_temp.midterm_id('loyalty-' || n),o.customer_id,o.id,'earn',CASE WHEN n = 2 THEN 140 ELSE 120 END,
           CASE WHEN n = 2 THEN 140 ELSE 120 END,NULL,o.created_at,actor
    FROM (VALUES (2),(8),(11),(17)) AS x(n) JOIN sales.orders o ON o.id=pg_temp.midterm_id('order-' || x.n)
    WHERE o.customer_id IS NOT NULL;

    INSERT INTO customers.loyalty_transactions (id,customer_id,order_id,type,points,balance_after,reverses_transaction_id,created_at,created_by)
    SELECT pg_temp.midterm_id('loyalty-redemption'),o.customer_id,o.id,'redeem',-20,120,NULL,o.created_at,actor
    FROM sales.orders o WHERE o.id=pg_temp.midterm_id('order-2');
END $$;

COMMIT;

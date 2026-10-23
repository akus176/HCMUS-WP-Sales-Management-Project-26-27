-- Run as the administrator against a dedicated, already migrated midterm database.
-- A name collision deliberately fails: never reuse an existing privileged role.
BEGIN;

CREATE ROLE "coffeeshop_midterm" LOGIN NOINHERIT NOSUPERUSER NOCREATEDB
    NOCREATEROLE NOREPLICATION NOBYPASSRLS;

-- PUBLIC privileges also apply to otherwise restricted roles.
DO $$
BEGIN
    EXECUTE format('REVOKE ALL ON DATABASE %I FROM PUBLIC', current_database());
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO "coffeeshop_midterm"', current_database());
END $$;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA public, catalog, customers, inventory, sales, identity FROM PUBLIC;

GRANT USAGE ON SCHEMA catalog, customers, inventory, sales, identity TO "coffeeshop_midterm";

GRANT SELECT, INSERT, UPDATE ON
    catalog.categories, catalog.menu_items, catalog.size_variants, catalog.addons,
    customers.customers, customers.customer_addresses, customers.loyalty_settings,
    inventory.ingredients, inventory.suppliers, inventory.purchase_receipts, inventory.purchase_receipt_lines,
    sales.orders, sales.order_lines, sales.order_line_addons, sales.tables, sales.discounts, sales.deliveries
    TO "coffeeshop_midterm";

-- Replacement workflows delete obsolete recipe and compatibility rows.
GRANT SELECT, INSERT, UPDATE, DELETE ON catalog.recipe_ingredients, catalog.menu_item_addons
    TO "coffeeshop_midterm";
GRANT DELETE ON inventory.purchase_receipt_lines TO "coffeeshop_midterm";

-- History and compensation records are append-only for the runtime account.
GRANT SELECT, INSERT ON
    inventory.stock_movements, inventory.order_inventory_restorations,
    customers.loyalty_transactions, sales.order_status_history, sales.delivery_status_history, sales.payments
    TO "coffeeshop_midterm";

-- Midterm actor selection and delivery eligibility need no credential/token access.
GRANT SELECT (id, full_name, is_active) ON identity.users TO "coffeeshop_midterm";
GRANT SELECT (id, code) ON identity.roles TO "coffeeshop_midterm";
GRANT SELECT (user_id, role_id) ON identity.user_roles TO "coffeeshop_midterm";

COMMIT;

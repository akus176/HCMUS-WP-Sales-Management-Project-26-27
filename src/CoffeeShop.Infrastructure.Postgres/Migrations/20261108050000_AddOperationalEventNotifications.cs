namespace CoffeeShop.Infrastructure.Postgres.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(CoffeeShopDbContext))]
[Migration("20261108050000_AddOperationalEventNotifications")]
public sealed class AddOperationalEventNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION identity.notify_operational_change() RETURNS trigger AS $$
        DECLARE row_data jsonb; event_id text;
        BEGIN
            row_data := to_jsonb(CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END);
            event_id := row_data ->> TG_ARGV[1];
            PERFORM pg_notify('coffeeshop_operational_events', json_build_object(
                'type', TG_ARGV[0], 'id', event_id, 'updatedAt', clock_timestamp())::text);
            RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
        END;
        $$ LANGUAGE plpgsql;
        CREATE TRIGGER operational_order AFTER INSERT OR UPDATE OR DELETE ON sales.orders
          FOR EACH ROW EXECUTE FUNCTION identity.notify_operational_change('order', 'id');
        CREATE TRIGGER operational_discount AFTER INSERT OR UPDATE OR DELETE ON sales.discounts
          FOR EACH ROW EXECUTE FUNCTION identity.notify_operational_change('discount', 'order_id');
        CREATE TRIGGER operational_payment AFTER INSERT OR UPDATE OR DELETE ON sales.payments
          FOR EACH ROW EXECUTE FUNCTION identity.notify_operational_change('payment', 'order_id');
        CREATE TRIGGER operational_inventory AFTER INSERT OR UPDATE OR DELETE ON inventory.ingredients
          FOR EACH ROW EXECUTE FUNCTION identity.notify_operational_change('inventory', 'id');
        CREATE TRIGGER operational_delivery AFTER INSERT OR UPDATE OR DELETE ON sales.deliveries
          FOR EACH ROW EXECUTE FUNCTION identity.notify_operational_change('delivery', 'order_id');
        CREATE TRIGGER operational_account AFTER INSERT OR UPDATE OR DELETE ON identity.users
          FOR EACH ROW EXECUTE FUNCTION identity.notify_operational_change('account', 'id');
        CREATE TRIGGER operational_account_role AFTER INSERT OR UPDATE OR DELETE ON identity.user_roles
          FOR EACH ROW EXECUTE FUNCTION identity.notify_operational_change('account', 'user_id');
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER operational_account_role ON identity.user_roles;
        DROP TRIGGER operational_account ON identity.users;
        DROP TRIGGER operational_delivery ON sales.deliveries;
        DROP TRIGGER operational_inventory ON inventory.ingredients;
        DROP TRIGGER operational_payment ON sales.payments;
        DROP TRIGGER operational_discount ON sales.discounts;
        DROP TRIGGER operational_order ON sales.orders;
        DROP FUNCTION identity.notify_operational_change();
        """);
}

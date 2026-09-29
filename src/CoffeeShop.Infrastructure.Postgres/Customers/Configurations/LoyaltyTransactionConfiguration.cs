namespace CoffeeShop.Infrastructure.Postgres.Customers.Configurations;

using CoffeeShop.Domain.Customers;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.ToTable("loyalty_transactions", "customers", table =>
        {
            table.HasCheckConstraint("CK_loyalty_transactions_type", "type IN ('earn', 'redeem', 'reverse_earn', 'reverse_redeem')");
            table.HasCheckConstraint("CK_loyalty_transactions_points", "points <> 0");
            table.HasCheckConstraint("CK_loyalty_transactions_balance_after", "balance_after >= 0");
        });

        builder.HasKey(lt => lt.Id);

        builder.Property(lt => lt.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(lt => lt.CustomerId)
            .HasConversion<EntityIdValueConverter>()
            .IsRequired()
            .HasColumnName("customer_id");

        builder.Property(lt => lt.OrderId)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("order_id");

        builder.Property(lt => lt.Type)
            .HasMaxLength(15)
            .IsRequired()
            .HasColumnName("type");

        builder.Property(lt => lt.Points)
            .IsRequired()
            .HasColumnName("points");

        builder.Property(lt => lt.BalanceAfter)
            .IsRequired()
            .HasColumnName("balance_after");

        builder.Property(lt => lt.ReversesTransactionId)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("reverses_transaction_id");

        builder.Property(lt => lt.CreatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(lt => lt.CreatedBy)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("created_by");

        // Partial unique index for idempotency: an order cannot double-earn/redeem the same transaction type
        builder.HasIndex(lt => new { lt.OrderId, lt.Type })
            .IsUnique()
            .HasFilter("order_id IS NOT NULL");

        builder.HasIndex(lt => new { lt.CustomerId, lt.CreatedAt });

        builder.HasOne(lt => lt.Customer)
            .WithMany(c => c.LoyaltyTransactions)
            .HasForeignKey(lt => lt.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(lt => lt.ReversesTransaction)
            .WithMany()
            .HasForeignKey(lt => lt.ReversesTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

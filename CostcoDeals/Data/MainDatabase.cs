using Microsoft.EntityFrameworkCore;
using CostcoDeals.Shared.Enums;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace CostcoDeals.Data
{
    /// <summary>
    /// EF Core DbContext for interacting with the Costco deals SQLite database.
    /// </summary>
    public class MainDatabase : DbContext
    {
        public MainDatabase(DbContextOptions<MainDatabase> options)
        : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<PriceHistory> PriceHistories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            // 1) Enforce unique products per warehouse
            mb.Entity<Product>()
              .HasIndex(p => new { p.CostcoId, p.WarehouseLocationId })
              .IsUnique();

            // 2) Default new products to the first enum value
            mb.Entity<Product>()
               .Property(p => p.WarehouseLocationId)
               .HasDefaultValue((int)Enum.GetValues<WarehouseLocation>().Cast<WarehouseLocation>().First());

            // 3) Optional: unique index on price history timestamp per product
            mb.Entity<PriceHistory>()
              .HasIndex(ph => new { ph.ProductId, ph.ScrapedAt });

            // 4) Configure the relationship (if not inferred automatically)
            mb.Entity<PriceHistory>()
              .HasOne(ph => ph.Product)
              .WithMany(p => p.PriceHistories)
              .HasForeignKey(ph => ph.ProductId);
        }
    }
}

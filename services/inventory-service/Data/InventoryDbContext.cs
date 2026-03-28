using Microsoft.EntityFrameworkCore;
using InventoryService.Models;

namespace InventoryService.Data
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

        public DbSet<Inventory> Inventories { get; set; } = null!;
        public DbSet<Reservation> Reservations { get; set; } = null!;
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Inventory configuration
            modelBuilder.Entity<Inventory>(entity =>
            {
                entity.HasIndex(e => e.ProductId).IsUnique();
                
                entity.Property(e => e.AvailableStock)
                    .HasDefaultValue(0);
                    
                entity.Property(e => e.ReservedStock)
                    .HasDefaultValue(0);
                    
                // Note: CHECK constraints removed - validation handled by [Range] attributes
                // PostgreSQL case-sensitivity causes issues with camelCase column names
            });
            
            // Reservation configuration
            modelBuilder.Entity<Reservation>(entity =>
            {
                entity.HasIndex(e => e.ReservationCode).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ExpiresAt);
                
                entity.Property(e => e.Status)
                    .HasDefaultValue(ReservationStatus.Active);
                    
                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("NOW()");
                    
                entity.HasOne(e => e.Inventory)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}

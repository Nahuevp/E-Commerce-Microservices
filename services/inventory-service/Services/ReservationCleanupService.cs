using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.Models;

namespace InventoryService.Services
{
    /// <summary>
    /// Background service that periodically cleans up expired reservations
    /// </summary>
    public class ReservationCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReservationCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
        
        public ReservationCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<ReservationCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Reservation cleanup service started");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredReservationsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during reservation cleanup");
                }
                
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            
            _logger.LogInformation("Reservation cleanup service stopped");
        }
        
        private async Task CleanupExpiredReservationsAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            
            var now = DateTime.UtcNow;
            
            // Find all active reservations that have expired
            var expiredReservations = await context.Reservations
                .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt < now)
                .Include(r => r.Inventory)
                .ToListAsync(stoppingToken);
            
            if (expiredReservations.Count == 0)
            {
                return;
            }
            
            _logger.LogInformation("Found {Count} expired reservations to clean up", expiredReservations.Count);
            
            foreach (var reservation in expiredReservations)
            {
                await using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);
                
                try
                {
                    // Return stock to available
                    if (reservation.Inventory != null)
                    {
                        reservation.Inventory.AvailableStock += reservation.Quantity;
                        reservation.Inventory.ReservedStock -= reservation.Quantity;
                    }
                    
                    // Mark as expired
                    reservation.Status = ReservationStatus.Expired;
                    
                    await context.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);
                    
                    _logger.LogInformation(
                        "Cleaned up expired reservation: Id={Id}, Code={Code}, ProductId={ProductId}, Quantity={Quantity}",
                        reservation.Id, reservation.ReservationCode, reservation.ProductId, reservation.Quantity);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(stoppingToken);
                    _logger.LogError(ex, "Failed to cleanup reservation {ReservationId}", reservation.Id);
                }
            }
        }
    }
}

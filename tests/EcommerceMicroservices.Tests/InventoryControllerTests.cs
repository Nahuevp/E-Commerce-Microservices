using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using InventoryService.Controllers;
using InventoryService.Data;
using InventoryService.Models;
using InventoryService.Models.DTOs;
using Xunit;

namespace EcommerceMicroservices.Tests;

public class InventoryControllerTests
{
    private InventoryDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new InventoryDbContext(options);
    }

    private InventoryController CreateController(InventoryDbContext context)
    {
        var loggerMock = new Mock<ILogger<InventoryController>>();
        return new InventoryController(context, loggerMock.Object);
    }

    [Fact]
    public async Task ReserveStock_WithAvailableStock_ReturnsSuccessfulReservation()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = CreateController(context);
        
        // Create inventory with stock
        var inventory = new Inventory
        {
            ProductId = 1,
            AvailableStock = 100,
            ReservedStock = 0,
            TotalStock = 100
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var request = new ReserveStockRequest
        {
            ProductId = 1,
            Quantity = 10
        };

        // Act
        var result = await controller.ReserveStock(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReservationResponse>(okResult.Value);
        
        Assert.Equal(1, response.ProductId);
        Assert.Equal(10, response.Quantity);
        Assert.Equal(ReservationStatus.Active, response.Status);
    }

    [Fact]
    public async Task ReserveStock_WithInsufficientStock_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = CreateController(context);
        
        // Create inventory with low stock
        var inventory = new Inventory
        {
            ProductId = 2,
            AvailableStock = 5,
            ReservedStock = 0,
            TotalStock = 5
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var request = new ReserveStockRequest
        {
            ProductId = 2,
            Quantity = 10 // More than available
        };

        // Act
        var result = await controller.ReserveStock(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<InsufficientStockResponse>(badRequestResult.Value);
        
        Assert.Equal(5, response.AvailableStock);
        Assert.Equal(10, response.RequestedQuantity);
    }

    [Fact]
    public async Task ReleaseReservation_NonExistentReservation_ReturnsNotFound()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = CreateController(context);

        // Act
        var result = await controller.ReleaseReservation(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ConfirmReservation_NonExistentReservation_ReturnsNotFound()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = CreateController(context);

        // Act
        var result = await controller.ConfirmReservation(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GenerateReservationCode_ReturnsCorrectFormat()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = CreateController(context);
        
        // Create inventory
        var inventory = new Inventory
        {
            ProductId = 6,
            AvailableStock = 100,
            ReservedStock = 0,
            TotalStock = 100
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var request = new ReserveStockRequest
        {
            ProductId = 6,
            Quantity = 1
        };

        // Act
        var result = await controller.ReserveStock(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReservationResponse>(okResult.Value);
        
        // Verify format is RES-XXXXXX (RES- followed by 6 alphanumeric characters)
        Assert.Matches(@"^RES-[A-Z0-9]{6}$", response.ReservationCode);
    }

    [Fact]
    public async Task CheckAvailability_NonExistentProduct_AutoInitializesInventory()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = CreateController(context);

        // Act
        var result = await controller.CheckAvailability(999);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AvailabilityResponse>(okResult.Value);
        
        Assert.Equal(999, response.ProductId);
        Assert.Equal(100, response.AvailableStock); // Default stock
    }

    [Fact]
    public async Task CheckAvailability_ExistingProduct_ReturnsAvailability()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = CreateController(context);
        
        // Create inventory
        var inventory = new Inventory
        {
            ProductId = 10,
            AvailableStock = 50,
            ReservedStock = 25,
            TotalStock = 75
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        // Act
        var result = await controller.CheckAvailability(10);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AvailabilityResponse>(okResult.Value);
        
        Assert.Equal(10, response.ProductId);
        Assert.Equal(50, response.AvailableStock);
        Assert.Equal(25, response.ReservedStock);
        Assert.Equal(75, response.TotalStock);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Controllers;
using PaymentService.Data;
using PaymentService.Models;
using Xunit;

namespace EcommerceMicroservices.Tests;

public class PaymentControllerTests
{
    private PaymentDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PaymentDbContext(options);
    }

    [Fact]
    public async Task ProcessPayment_ValidCard_ReturnsApproved()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = new PaymentController(context);
        var request = new PaymentRequest
        {
            OrderId = 1,
            Amount = 100.00m,
            CardNumber = "4111111111111111" // Does NOT start with 4000
        };

        // Act
        var result = await controller.ProcessPayment(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PaymentResponse>(okResult.Value);
        Assert.Equal("Approved", response.Status);
    }

    [Fact]
    public async Task ProcessPayment_CardStartingWith4000_ReturnsDeclined()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = new PaymentController(context);
        var request = new PaymentRequest
        {
            OrderId = 1,
            Amount = 100.00m,
            CardNumber = "4000123456789012" // Starts with 4000
        };

        // Act
        var result = await controller.ProcessPayment(request);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(402, objectResult.StatusCode);
        var response = Assert.IsType<PaymentResponse>(objectResult.Value);
        Assert.Equal("Declined", response.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task ProcessPayment_InvalidAmount_ReturnsBadRequest(decimal amount)
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = new PaymentController(context);
        var request = new PaymentRequest
        {
            OrderId = 1,
            Amount = amount,
            CardNumber = "4111111111111111"
        };

        // Act
        var result = await controller.ProcessPayment(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ProcessPayment_MasksCardNumber_ShowsOnlyLastFourDigits()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = new PaymentController(context);
        var request = new PaymentRequest
        {
            OrderId = 1,
            Amount = 100.00m,
            CardNumber = "4111111111111111"
        };

        // Act
        var result = await controller.ProcessPayment(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var paymentInDb = await context.Payments.FirstAsync();
        
        // The stored card number should be masked: ****1111
        Assert.Equal("****1111", paymentInDb.CardNumber);
    }

    [Fact]
    public async Task ProcessPayment_CreatesTransactionId_InCorrectFormat()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = new PaymentController(context);
        var request = new PaymentRequest
        {
            OrderId = 1,
            Amount = 100.00m,
            CardNumber = "4111111111111111"
        };

        // Act
        var result = await controller.ProcessPayment(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PaymentResponse>(okResult.Value);
        
        Assert.StartsWith("TXN-", response.TransactionId);
    }

    [Fact]
    public async Task ProcessPayment_SavesPaymentRecord_ToDatabase()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = new PaymentController(context);
        var request = new PaymentRequest
        {
            OrderId = 42,
            Amount = 250.50m,
            CardNumber = "4111111111111111"
        };

        // Act
        var result = await controller.ProcessPayment(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var savedPayment = await context.Payments.FirstAsync();
        
        Assert.Equal(42, savedPayment.OrderId);
        Assert.Equal(250.50m, savedPayment.Amount);
        Assert.Equal("Approved", savedPayment.Status);
    }
}

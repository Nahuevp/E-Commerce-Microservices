using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using CartService.Controllers;
using CartService.Data;
using CartService.Models;
using Xunit;

namespace EcommerceMicroservices.Tests;

public class CartControllerTests
{
    private CartDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CartDbContext(options);
    }

    private CartController CreateController(CartDbContext context)
    {
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<CartController>>();
        return new CartController(context, httpClientFactoryMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task GetCart_EmptyCart_ReturnsEmptyItems()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var cart = new Cart { UserId = 1, CreatedAt = DateTime.UtcNow };
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);

        // Act
        var result = await controller.GetCart(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var cartResponse = Assert.IsType<dynamic>(okResult.Value);
    }

    [Fact]
    public async Task GetCart_CartNotFound_ReturnsNotFound()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = CreateController(context);

        // Act
        var result = await controller.GetCart(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AddToCart_NewItem_CreatesCartItem()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var cart = new Cart { UserId = 1, CreatedAt = DateTime.UtcNow };
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        var request = new AddToCartRequest
        {
            UserId = 1,
            ProductId = 100,
            Quantity = 2,
            Price = 29.99m
        };

        // Act
        var result = await controller.AddToCart(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        
        // Verify item was added
        var items = await context.CartItems.ToListAsync();
        Assert.Single(items);
        Assert.Equal(100, items[0].ProductId);
        Assert.Equal(2, items[0].Quantity);
        Assert.Equal(29.99m, items[0].Price);
    }

    [Fact]
    public async Task AddToCart_ExistingProduct_IncreasesQuantity()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var cart = new Cart { UserId = 1, CreatedAt = DateTime.UtcNow };
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        
        var existingItem = new CartItem 
        { 
            CartId = cart.Id, 
            ProductId = 100, 
            Quantity = 1, 
            Price = 29.99m 
        };
        context.CartItems.Add(existingItem);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        var request = new AddToCartRequest
        {
            UserId = 1,
            ProductId = 100, // Same product
            Quantity = 3,
            Price = 29.99m
        };

        // Act
        var result = await controller.AddToCart(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        
        // Verify quantity was increased
        var items = await context.CartItems.ToListAsync();
        Assert.Single(items);
        Assert.Equal(4, items[0].Quantity); // 1 + 3
    }

    [Fact]
    public async Task UpdateCartItem_ValidRequest_UpdatesQuantity()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var cart = new Cart { UserId = 1, CreatedAt = DateTime.UtcNow };
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        
        var item = new CartItem 
        { 
            CartId = cart.Id, 
            ProductId = 100, 
            Quantity = 1, 
            Price = 29.99m 
        };
        context.CartItems.Add(item);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        var request = new UpdateCartItemRequest
        {
            Quantity = 5
        };

        // Act
        var result = await controller.UpdateCartItem(cart.Id, item.Id, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        
        var updatedItem = await context.CartItems.FindAsync(item.Id);
        Assert.Equal(5, updatedItem?.Quantity);
        Assert.Equal(24.99m, updatedItem?.Price);
    }

    [Fact]
    public async Task UpdateCartItem_ItemNotFound_ReturnsNotFound()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = CreateController(context);
        
        var request = new UpdateCartItemRequest { Quantity = 5 };

// Act
var result = await controller.UpdateCartItem(1, 999, request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RemoveFromCart_ValidItem_RemovesItem()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var cart = new Cart { UserId = 1, CreatedAt = DateTime.UtcNow };
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        
        var item = new CartItem 
        { 
            CartId = cart.Id, 
            ProductId = 100, 
            Quantity = 1, 
            Price = 29.99m 
        };
        context.CartItems.Add(item);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);

        // Act
        var result = await controller.RemoveFromCart(cart.Id, item.Id);

        // Assert
        Assert.IsType<OkResult>(result);
        
        var items = await context.CartItems.ToListAsync();
        Assert.Empty(items);
    }

    [Fact]
    public async Task RemoveFromCart_ItemNotFound_ReturnsNotFound()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var controller = CreateController(context);

        // Act
        var result = await controller.RemoveFromCart(1, 999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Checkout_EmptyCart_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var cart = new Cart { UserId = 1, CreatedAt = DateTime.UtcNow };
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        var request = new CheckoutRequest { CardNumber = "4111111111111111" };

        // Act
        var result = await controller.Checkout(cart.Id, request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<CheckoutFailureResponse>(badRequest.Value);
        Assert.Equal("Cart is empty", response.Error);
    }

    [Fact]
    public async Task Checkout_InvalidItems_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var cart = new Cart { UserId = 1, CreatedAt = DateTime.UtcNow };
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        
        // Add invalid items (ProductId = 0, Quantity = 0, Price = 0)
        var invalidItem = new CartItem 
        { 
            CartId = cart.Id, 
            ProductId = 0, 
            Quantity = 0, 
            Price = 0 
        };
        context.CartItems.Add(invalidItem);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        var request = new CheckoutRequest { CardNumber = "4111111111111111" };

        // Act
        var result = await controller.Checkout(cart.Id, request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<CheckoutFailureResponse>(badRequest.Value);
        Assert.Equal("Cart is empty or has invalid items", response.Error);
    }

    [Fact]
    public async Task RemoveProductFromAllCarts_RemovesAllOccurrences()
    {
        // Arrange
        var context = CreateInMemoryContext();
        
        var cart1 = new Cart { UserId = 1, CreatedAt = DateTime.UtcNow };
        var cart2 = new Cart { UserId = 2, CreatedAt = DateTime.UtcNow };
        context.Carts.AddRange(cart1, cart2);
        await context.SaveChangesAsync();
        
        // Add same product to both carts
        var item1 = new CartItem { CartId = cart1.Id, ProductId = 100, Quantity = 1, Price = 10 };
        var item2 = new CartItem { CartId = cart2.Id, ProductId = 100, Quantity = 2, Price = 10 };
        context.CartItems.AddRange(item1, item2);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);

        // Act
        var result = await controller.RemoveProductFromAllCarts(100);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        
        var items = await context.CartItems.Where(i => i.ProductId == 100).ToListAsync();
        Assert.Empty(items);
    }
}

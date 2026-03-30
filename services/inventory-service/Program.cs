using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.Extensions;
using InventoryService.Services;

var builder = WebApplication.CreateBuilder(args);

// Convert DATABASE_URL to Npgsql format if present
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var dbName = uri.AbsolutePath.Trim('/');
    var dbPort = uri.Port > 0 ? uri.Port : 5432;
    
    // Only use sslmode parameter, ignore others like channel_binding
    var connectionString = $"Host={uri.Host};Port={dbPort};Database={dbName};Username={userInfo[0]};Password={userInfo[1]};sslmode=require";
    
    Console.WriteLine($"Connecting to database: Host={uri.Host}, Database={dbName}");
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;
}

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        }));

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// Register background service for cleaning up expired reservations
builder.Services.AddHostedService<ReservationCleanupService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Create database and tables if they don't exist
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("Inventory database initialized successfully.");
    
    // Verify tables exist
    var count = db.Inventories.Count();
    Console.WriteLine($"Inventory table verified - {count} rows");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR initializing Inventory database: {ex.Message}");
    // Try to create tables manually
    try
    {
        using var scope2 = app.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<InventoryDbContext>();
        db2.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Inventories"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""ProductId"" INTEGER NOT NULL UNIQUE,
                ""TotalStock"" INTEGER NOT NULL DEFAULT 0,
                ""AvailableStock"" INTEGER NOT NULL DEFAULT 0,
                ""ReservedStock"" INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS ""InventoryReservations"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""ProductId"" INTEGER NOT NULL,
                ""Quantity"" INTEGER NOT NULL,
                ""ReservationCode"" TEXT,
                ""Status"" TEXT NOT NULL,
                ""CreatedAt"" TIMESTAMP NOT NULL,
                ""ExpiresAt"" TIMESTAMP NOT NULL
            );
        ");
        Console.WriteLine("Inventory tables created manually.");
    }
    catch (Exception ex2)
    {
        Console.WriteLine($"Could not create Inventory tables manually: {ex2.Message}");
    }
}

app.Run();

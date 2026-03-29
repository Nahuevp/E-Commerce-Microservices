using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Extensions;

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

builder.Services.AddDbContext<OrderDbContext>(options =>
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
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("Order database initialized successfully.");
    
    // Fix: Add Status and CreatedAt columns if they exist but without defaults
    // This handles the case where the table was created without default values
    try
    {
        db.Database.ExecuteSqlRaw(@"
            ALTER TABLE ""Orders"" ALTER COLUMN ""Status"" SET DEFAULT 'Pending';
            ALTER TABLE ""Orders"" ALTER COLUMN ""CreatedAt"" SET DEFAULT NOW();
            ALTER TABLE ""Orders"" ALTER COLUMN ""Status"" DROP NOT NULL;
            ALTER TABLE ""Orders"" ALTER COLUMN ""CreatedAt"" DROP NOT NULL;
        ");
        Console.WriteLine("Order table columns fixed with defaults.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Order table fix warning (can be ignored): {ex.Message}");
    }
    
    // Verify tables exist
    var count = db.Orders.Count();
    Console.WriteLine($"Order table verified - {count} rows");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR initializing Order database: {ex.Message}");
    // Try to create tables manually
    try
    {
        using var scope2 = app.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<OrderDbContext>();
        db2.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Orders"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""UserId"" INTEGER NOT NULL,
                ""ProductId"" INTEGER NOT NULL,
                ""Quantity"" INTEGER NOT NULL,
                ""TotalPrice"" DECIMAL(18,2) NOT NULL,
                ""Status"" TEXT DEFAULT 'Pending',
                ""CreatedAt"" TIMESTAMP DEFAULT NOW()
            );
        ");
        Console.WriteLine("Order tables created manually.");
    }
    catch (Exception ex2)
    {
        Console.WriteLine($"Could not create Order tables manually: {ex2.Message}");
    }
}

app.Run();

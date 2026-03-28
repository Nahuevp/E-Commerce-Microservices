using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Extensions;

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

builder.Services.AddDbContext<PaymentDbContext>(options =>
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
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("Payment database initialized successfully.");
    
    // Verify tables exist
    var count = db.Payments.Count();
    Console.WriteLine($"Payment table verified - {count} rows");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR initializing Payment database: {ex.Message}");
    // Try to create tables manually
    try
    {
        using var scope2 = app.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<PaymentDbContext>();
        db2.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Payments"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""OrderId"" INTEGER NOT NULL,
                ""Amount"" DECIMAL(18,2) NOT NULL,
                ""CardNumber"" TEXT,
                ""Status"" TEXT NOT NULL,
                ""TransactionId"" TEXT,
                ""Reason"" TEXT,
                ""CreatedAt"" TIMESTAMP NOT NULL
            );
        ");
        Console.WriteLine("Payment tables created manually.");
    }
    catch (Exception ex2)
    {
        Console.WriteLine($"Could not create Payment tables manually: {ex2.Message}");
    }
}

app.Run();

using AuthService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Convert DATABASE_URL to Npgsql format if present
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
Console.WriteLine($"AUTH SERVICE - DATABASE_URL: {(string.IsNullOrEmpty(databaseUrl) ? "NOT SET" : "SET")}");
if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var dbName = uri.AbsolutePath.Trim('/');
    var dbPort = uri.Port > 0 ? uri.Port : 5432;
    
    // Remove problematic query parameters like channel_binding
    var connectionString = $"Host={uri.Host};Port={dbPort};Database={dbName};Username={userInfo[0]};Password={userInfo[1]};sslmode=require";
    
    Console.WriteLine($"AUTH SERVICE - Connecting to: Host={uri.Host}, Database={dbName}, Port={dbPort}");
    Console.WriteLine($"AUTH SERVICE - Connection string (masked): Host={uri.Host};Port={dbPort};Database={dbName};Username={userInfo[0]};Password=***");
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;
}
else
{
    Console.WriteLine("AUTH SERVICE - Using default connection from config");
    Console.WriteLine($"AUTH SERVICE - DefaultConnection: {builder.Configuration["ConnectionStrings:DefaultConnection"]?.Substring(0, 20) ?? "NULL"}");
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

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        }));

var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_key_that_is_long_enough_for_hmac_sha256_which_needs_to_be_at_least_32_bytes";
Console.WriteLine($"AUTH SERVICE - JWT Key loaded: {(string.IsNullOrEmpty(builder.Configuration["Jwt:Key"]) ? "USING DEFAULT" : "FROM CONFIG")}");
var keyBytes = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

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
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("Auth database initialized successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Could not initialize database: {ex.Message}");
}

app.Run();

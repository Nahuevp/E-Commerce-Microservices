using Microsoft.EntityFrameworkCore;
using ProductService.Data;
using ProductService.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Railway: Accept PORT from environment variable
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});

// Convert DATABASE_URL to Npgsql format if present
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var dbName = uri.AbsolutePath.Trim('/');
    var dbPort = uri.Port > 0 ? uri.Port : 5432;
    var queryParams = uri.Query.TrimStart('?');
    var connectionString = $"Host={uri.Host};Port={dbPort};Database={dbName};Username={userInfo[0]};Password={userInfo[1]}";
    if (!string.IsNullOrEmpty(queryParams))
    {
        connectionString += ";" + queryParams.Replace("&", ";").Replace("%20", " ");
    }
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

builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    context.Database.EnsureCreated();
}

app.Run();

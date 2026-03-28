var builder = WebApplication.CreateBuilder(args);

// Railway/Render: Accept PORT from environment variable
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    // Only listen on the port Render assigns - don't expose any other ports
    options.ListenAnyIP(int.Parse(port));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Configure reverse proxy routes from appsettings or environment
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors("AllowAll");

// Serve static files from client (SPA)
app.UseDefaultFiles();
app.UseStaticFiles();

// Health check endpoint for Render - returns OK immediately without checking downstream services
// This is important to prevent Render from restarting due to health check timeouts
app.MapGet("/health", () => Results.Ok(new { status = "OK", timestamp = DateTime.UtcNow }));

// Also add a /health/all endpoint that checks all services
app.MapGet("/health/all", async (HttpContext context) =>
{
    var results = new Dictionary<string, string>();
    var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    
    var services = new[] {
        ("auth", "http://127.0.0.1:8001/auth/health"),
        ("product", "http://127.0.0.1:8002/products/health"),
        ("order", "http://127.0.0.1:8003/orders/health"),
        ("cart", "http://127.0.0.1:8004/carts/health"),
        ("payment", "http://127.0.0.1:8005/payments/health"),
        ("notification", "http://127.0.0.1:8006/notifications/health"),
        ("inventory", "http://127.0.0.1:8007/inventory/health")
    };
    
    foreach (var (name, url) in services)
    {
        try
        {
            var response = await httpClient.GetAsync(url);
            results[name] = response.IsSuccessStatusCode ? "healthy" : "unhealthy";
        }
        catch
        {
            results[name] = "down";
        }
    }
    
    return Results.Ok(results);
});

// SPA fallback - serve index.html for non-API routes
app.MapFallback(async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.MapReverseProxy();

app.Run();

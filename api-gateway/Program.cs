var builder = WebApplication.CreateBuilder(args);

// Railway/Render: Accept PORT from environment variable
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
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

// Health check endpoint for Render
app.MapGet("/health", () => Results.Ok("OK"));

// SPA fallback - serve index.html for non-API routes
app.MapFallback(async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.MapReverseProxy();

app.Run();

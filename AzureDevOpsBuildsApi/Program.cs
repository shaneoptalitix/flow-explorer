using AzureDevOpsReporter.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Flow Environments & Deployments Explorer API",
        Version = "v1",
        Description = "API for querying Azure DevOps environments, deployments, and builds"
    });
});

// Add Memory Cache with size limit
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024; // Total size units allowed in cache
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:8080",
                "https://localhost:7237",
                "https://localhost:8443",
                "http://localhost:8081",
                "null",
                "https://azuredevopsbuildsapi-cdadgzh8cwesccgx.uksouth-01.azurewebsites.net",
                "https://api.bitbucket.org"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Register HTTP client and services
builder.Services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>();
builder.Services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();

builder.Services.AddHttpClient<IBitbucketService, BitbucketService>();
builder.Services.AddScoped<IBitbucketService, BitbucketService>();

var app = builder.Build();

// Serve static files (HTML, CSS, JS) from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flow Environments & Deployments Explorer API v1");
    c.RoutePrefix = "swagger";
});

// Add health check endpoint
app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    version = "1.0.0",
    environment = app.Environment.EnvironmentName
}));

// Add cache clear endpoint (useful for development/testing)
app.MapPost("/api/cache/clear", (IAzureDevOpsService azureDevOpsService, IBitbucketService bitbucketService) =>
{
    return azureDevOpsService.ClearCache() && bitbucketService.ClearCache()
        ? Results.Ok(new { message = "Cache cleared successfully" }) 
        : Results.Problem("Unable to clear cache");    
});

app.UseCors("AllowWebApp");
app.UseAuthorization();
app.MapControllers();

app.Run();
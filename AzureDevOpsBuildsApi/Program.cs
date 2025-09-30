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
        Title = "Azure DevOps Environment Reporter API",
        Version = "v1",
        Description = "API for querying Azure DevOps environments, deployments, and builds"
    });
});

// Add CORS - Updated for Azure deployment
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
                "https://azuredevopsbuildsapi-cdadgzh8cwesccgx.uksouth-01.azurewebsites.net"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Register HTTP client and services
builder.Services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>();
builder.Services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();

var app = builder.Build();

// Serve static files (HTML, CSS, JS) from wwwroot
app.UseDefaultFiles(); // Serves index.html by default
app.UseStaticFiles();   // Serves all files from wwwroot folder

// Configure the HTTP request pipeline
// Swagger is now at /swagger instead of root
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure DevOps Reporter API v1");
    c.RoutePrefix = "swagger"; // Swagger UI at /swagger, not root
});

// Add health check endpoint
app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    version = "1.0.0",
    environment = app.Environment.EnvironmentName
}));

// Enable CORS
app.UseCors("AllowWebApp");

app.UseAuthorization();
app.MapControllers();

// Let Azure determine the port automatically
app.Run();
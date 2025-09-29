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

// Add CORS - Updated for HTTPS development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:8080", 
                "https://localhost:7237", 
                "https://localhost:8443",
                "http://localhost:8081",  // Add your nginx container port
                "null"  // Allow file:// protocol for development
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configure Kestrel for development HTTPS
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        // HTTP port
        options.ListenAnyIP(8080);
        
        // HTTPS port with certificate
        options.ListenAnyIP(8443, listenOptions =>
        {
            var certPath = builder.Configuration["ASPNETCORE_Kestrel__Certificates__Default__Path"];
            var certPassword = builder.Configuration["ASPNETCORE_Kestrel__Certificates__Default__Password"];
            
            if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
            {
                listenOptions.UseHttps(certPath, certPassword);
            }
            else
            {
                // Fallback to development certificate
                listenOptions.UseHttps();
            }
        });
    });
}

// Register HTTP client and services
builder.Services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>();
builder.Services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure DevOps Reporter API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowWebApp");

app.UseAuthorization();
app.MapControllers();

app.Run();
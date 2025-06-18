using MauiApp.Data;
using MauiApp.Core.Interfaces;
using MauiApp.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Configure JWT Authentication for API Gateway
var jwtKey = builder.Configuration["JwtSettings:SecretKey"] ?? "MyVeryLongSecretKeyThatShouldBeAtLeast32CharactersLong!@#$%";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "MauiApp.IdentityService";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "MauiApp.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAuth", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Global rate limiting
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // API-specific rate limiting
    options.AddPolicy("ApiPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(15)
            }));

    // Authentication endpoints - more permissive
    options.AddPolicy("AuthPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Add YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        // Add correlation ID to all requests
        builderContext.AddRequestTransform(transformContext =>
        {
            var correlationId = transformContext.HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                              ?? Guid.NewGuid().ToString();
            transformContext.ProxyRequest.Headers.Add("X-Correlation-ID", correlationId);
            transformContext.HttpContext.Response.Headers.Add("X-Correlation-ID", correlationId);
            return ValueTask.CompletedTask;
        });

        // Forward JWT token to downstream services
        builderContext.AddRequestTransform(transformContext =>
        {
            var authHeader = transformContext.HttpContext.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                transformContext.ProxyRequest.Headers.Add("Authorization", authHeader);
            }
            return ValueTask.CompletedTask;
        });
    });

// Add SQL Server database
builder.AddSqlServerDbContext<ApplicationDbContext>("mauiappdb");

// Add Azure Blob Storage
builder.AddAzureStorageBlobs("blobs");

// Add services
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserService, UserService>();

// Add API services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Enterprise Project Management API Gateway", 
        Version = "v1",
        Description = "API Gateway for the Enterprise Project Management Platform microservices"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// Add health checks for all microservices
builder.Services.AddHealthChecks()
    .AddDbContext<ApplicationDbContext>()
    .AddUrlGroup(new Uri("http://identityservice/health"), "Identity Service")
    .AddUrlGroup(new Uri("http://projectsservice/health"), "Projects Service")
    .AddUrlGroup(new Uri("http://tasksservice/health"), "Tasks Service")
    .AddUrlGroup(new Uri("http://collaborationservice/health"), "Collaboration Service")
    .AddUrlGroup(new Uri("http://filesservice/health"), "Files Service")
    .AddUrlGroup(new Uri("http://analyticsservice/health"), "Analytics Service")
    .AddUrlGroup(new Uri("http://syncservice/health"), "Sync Service")
    .AddUrlGroup(new Uri("http://notificationservice/health"), "Notification Service");

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7001", "http://localhost:5001", "maui://localhost") // MAUI client URLs
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Map Aspire service defaults
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Gateway v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseHttpsRedirection();

// Add correlation ID middleware
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                      ?? Guid.NewGuid().ToString();
    context.Request.Headers["X-Correlation-ID"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Processing request {Method} {Path} with correlation ID {CorrelationId}", 
            context.Request.Method, context.Request.Path, correlationId);
    }
    
    await next();
});

app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Map direct controllers (for gateway-specific endpoints)
app.MapControllers();

// Map reverse proxy routes
app.MapReverseProxy();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", options =>
{
    options.Predicate = _ => false; // Exclude all checks for liveness
});
app.MapHealthChecks("/health/ready"); // Include all checks for readiness

// API Gateway specific endpoints
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapGet("/api/gateway/status", (ILogger<Program> logger) =>
{
    logger.LogInformation("Gateway status check requested");
    return Results.Ok(new
    {
        Status = "Healthy",
        Service = "API Gateway",
        Version = "1.0.0",
        Timestamp = DateTime.UtcNow,
        Environment = app.Environment.EnvironmentName
    });
}).WithName("GetGatewayStatus").WithTags("Gateway");

app.MapGet("/api/gateway/services", () =>
{
    return Results.Ok(new
    {
        Services = new[]
        {
            new { Name = "Identity Service", Path = "/api/identity/**", Status = "Active" },
            new { Name = "Projects Service", Path = "/api/projects/**", Status = "Active" },
            new { Name = "Tasks Service", Path = "/api/tasks/**", Status = "Active" },
            new { Name = "Collaboration Service", Path = "/api/collaboration/**", Status = "Active" },
            new { Name = "Files Service", Path = "/api/files/**", Status = "Active" },
            new { Name = "Analytics Service", Path = "/api/analytics/**", Status = "Active" },
            new { Name = "Sync Service", Path = "/api/sync/**", Status = "Active" },
            new { Name = "Notification Service", Path = "/api/notifications/**", Status = "Active" }
        }
    });
}).WithName("GetServices").WithTags("Gateway").RequireRateLimiting("ApiPolicy");

app.Run();
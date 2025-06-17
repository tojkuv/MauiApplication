using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Azure.Storage.Blobs;
using MauiApp.FilesService.Data;
using MauiApp.FilesService.Services;
using MauiApp.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add database
builder.Services.AddDbContext<FilesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Azure Blob Storage
var storageConnectionString = builder.Configuration.GetConnectionString("AzureStorage") ?? 
    "UseDevelopmentStorage=true"; // Use storage emulator for development

builder.Services.AddSingleton(x => new BlobServiceClient(storageConnectionString));

// Configure JWT Authentication
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

// Add Authorization
builder.Services.AddAuthorization();

// Add services
builder.Services.AddScoped<IFilesService, FilesService>();

// Add controllers
builder.Services.AddControllers();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MauiApp Files Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
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
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Files" }));

// Map health checks
app.MapHealthChecks("/health/ready");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
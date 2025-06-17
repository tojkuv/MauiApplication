using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MauiApp.Core.Entities;
using MauiApp.Core.Interfaces;
using MauiApp.IdentityService.Data;
using MauiApp.IdentityService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add database
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Redis (comment out for now)
// builder.Services.AddStackExchangeRedisCache(options =>
//     options.Configuration = builder.Configuration.GetConnectionString("Redis"));

// Configure JWT settings
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);

// Use a default secret if not provided (for development)
if (string.IsNullOrEmpty(jwtSettings.SecretKey))
{
    jwtSettings.SecretKey = "MyVeryLongSecretKeyThatShouldBeAtLeast32CharactersLong!@#$%";
}
if (string.IsNullOrEmpty(jwtSettings.Issuer))
{
    jwtSettings.Issuer = "MauiApp.IdentityService";
}
if (string.IsNullOrEmpty(jwtSettings.Audience))
{
    jwtSettings.Audience = "MauiApp.Client";
}

builder.Services.Configure<JwtSettings>(options =>
{
    options.SecretKey = jwtSettings.SecretKey;
    options.Issuer = jwtSettings.Issuer;
    options.Audience = jwtSettings.Audience;
    options.AccessTokenExpirationMinutes = jwtSettings.AccessTokenExpirationMinutes;
    options.RefreshTokenExpirationDays = jwtSettings.RefreshTokenExpirationDays;
});

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;

    // Email confirmation
    options.SignIn.RequireConfirmedEmail = false; // For MVP
})
.AddEntityFrameworkStores<IdentityDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // For development
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

// Add Authorization
builder.Services.AddAuthorization();

// Add services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IIdentityService, MauiApp.IdentityService.Services.IdentityService>();

// Add controllers
builder.Services.AddControllers();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MauiApp Identity Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
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
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Identity" }));

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();

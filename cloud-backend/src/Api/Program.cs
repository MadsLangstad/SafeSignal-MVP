using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using AspNetCoreRateLimit;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;
using SafeSignal.Cloud.Infrastructure.Repositories;
using SafeSignal.Cloud.Api.Services;
using SafeSignal.Cloud.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Prioritize environment variable for connection string
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Database connection string not configured in environment variable DATABASE_CONNECTION_STRING or appsettings");

builder.Services.AddDbContext<SafeSignalDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register repositories
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISiteRepository, SiteRepository>();
builder.Services.AddScoped<IBuildingRepository, BuildingRepository>();
builder.Services.AddScoped<IFloorRepository, FloorRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();

// Register services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddSingleton<IPasswordValidator, PasswordValidator>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Register background services
builder.Services.AddHostedService<AuditRetentionService>();

// Configure JWT Authentication with strict validation
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

// Get secret key from environment variable OR configuration (development only)
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? jwtSettings["SecretKey"];

// Validate secret key
if (string.IsNullOrWhiteSpace(secretKey))
{
    throw new InvalidOperationException(
        "JWT SecretKey not configured. " +
        "Set JWT_SECRET_KEY environment variable in production or configure in appsettings.Development.json for development.");
}

if (secretKey.Length < 32)
{
    throw new InvalidOperationException(
        $"JWT SecretKey must be at least 32 characters long for security. Current length: {secretKey.Length}");
}

// Warn if using default development key in non-development environment
if (!builder.Environment.IsDevelopment() && secretKey.Contains("DEV_SECRET_KEY"))
{
    throw new InvalidOperationException(
        "Development JWT secret key detected in non-development environment. " +
        "Set JWT_SECRET_KEY environment variable with a secure production key.");
}

// Validate Issuer and Audience
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];

if (string.IsNullOrWhiteSpace(issuer))
{
    throw new InvalidOperationException("JWT Issuer not configured in appsettings");
}

if (string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException("JWT Audience not configured in appsettings");
}

// Log JWT configuration (without exposing secret key)
Console.WriteLine($"JWT Configuration: Issuer={issuer}, Audience={audience}, SecretKeyLength={secretKey.Length} chars");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero, // No tolerance for expired tokens
        RequireExpirationTime = true,
        RequireSignedTokens = true
    };

    // Add events for debugging
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"JWT Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"JWT Token validated successfully for user: {context.Principal?.Identity?.Name}");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine($"JWT Challenge: {context.Error}, {context.ErrorDescription}");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Add Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Real-IP";
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/login",
            Period = "1m",
            Limit = 5
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/refresh",
            Period = "1m",
            Limit = 10
        },
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1s",
            Limit = 10
        },
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = 100
        }
    };
});
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// Add CORS for mobile app
builder.Services.AddCors(options =>
{
    options.AddPolicy("MobileApp", policy =>
    {
        policy.WithOrigins(
            "capacitor://localhost",
            "ionic://localhost",
            "http://localhost",
            "http://localhost:8100",  // Ionic dev server
            "http://localhost:3000"   // React/Next.js dev
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add controllers
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SafeSignal Cloud API",
        Version = "v1",
        Description = "Cloud backend API for SafeSignal emergency alert system. Provides authentication, organization management, building/room management, device registration, and alert handling.",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "SafeSignal Support",
            Email = "support@safesignal.com"
        }
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.\n\n" +
                     "Enter 'Bearer' [space] and then your token in the text input below.\n\n" +
                     "Example: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Add XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Group endpoints by controller
    c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "Unknown" });
    c.DocInclusionPredicate((name, api) => true);
});

var app = builder.Build();

// Global exception handler (must be first)
app.UseGlobalExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SafeSignal Cloud API v1");
    });
}

app.UseHttpsRedirection();

// CORS must come before Authentication/Authorization
app.UseCors("MobileApp");

// Rate limiting
app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

// Audit logging (after authentication so we can access user claims)
app.UseMiddleware<AuditLoggingMiddleware>();

app.MapControllers();

app.Run();

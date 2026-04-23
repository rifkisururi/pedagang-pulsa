using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PedagangPulsa.Application.DependencyInjection;
using PedagangPulsa.Api.Middleware;
using PedagangPulsa.Infrastructure.DependencyInjection;
using Scalar.AspNetCore;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PedagangPulsa.Tests")]

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection not configured");
builder.Services.AddInfrastructure(connectionString, ignorePendingModelChangesWarning: true);

// Configure Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string not configured");
builder.Services.AddRedisInfrastructure(redisConnectionString);

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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

builder.Services.AddAuthorization();

builder.Services.AddApplicationServices(
    jwtSecret: jwtKey,
    jwtIssuer: jwtIssuer,
    jwtAudience: jwtAudience);

// Add FormOptions for file upload
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5242880; // 5MB
});

// Pricing config
builder.Services.Configure<PedagangPulsa.Domain.Configuration.PricingConfig>(
    builder.Configuration.GetSection("Pricing"));

// Google Auth config
builder.Services.Configure<PedagangPulsa.Domain.Configuration.GoogleAuthConfig>(
    builder.Configuration.GetSection("GoogleAuth"));

// SMS Gate config
builder.Services.Configure<PedagangPulsa.Domain.Configuration.SmsGateConfig>(
    builder.Configuration.GetSection("SmsGate"));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.RouteConstraintName = "apiVersion";
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// API Explorer & Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await app.ApplyDatabaseMigrationsAsync();

// Configure the HTTP request pipeline
var enableSwagger = builder.Configuration.GetValue<bool?>("EnableSwagger") ?? app.Environment.IsDevelopment();
var enableScalar = builder.Configuration.GetValue<bool?>("EnableScalar") ?? app.Environment.IsDevelopment();

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Skip HTTPS redirection on Cloud Run (TLS handled by load balancer)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (enableScalar)
{
    app.MapScalarApiReference("/scalar", options =>
    {
        options.WithTitle("PedagangPulsa API Reference")
            .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json")
            .DisableAgent();
    });
}

// Enable static files for uploads
app.UseStaticFiles();

// Use rate limiting middleware (Redis-backed distributed)
app.UseRateLimiting();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => "ok");

app.Run();

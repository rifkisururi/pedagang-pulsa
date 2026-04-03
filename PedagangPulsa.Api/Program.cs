using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using PedagangPulsa.Api.Middleware;
using PedagangPulsa.Api.Controllers;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Infrastructure.Suppliers;
using Scalar.AspNetCore;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PedagangPulsa.Tests")]

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

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

// Application Services
builder.Services.AddScoped<PedagangPulsa.Application.Services.TransactionService>();
builder.Services.AddScoped<PedagangPulsa.Application.Services.TopupService>();
builder.Services.AddScoped<PedagangPulsa.Application.Services.BalanceService>();
builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddScoped<ISupplierAdapterFactory, SupplierAdapterFactory>();
builder.Services.AddHttpClient();

// Add FormOptions for file upload
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5242880; // 5MB
});

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

// API Explorer & Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Run database migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();

        // Apply pending migrations
        await context.Database.MigrateAsync();

        // Seed minimum reference data required by the API.
        await DataSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred migrating or seeding the database.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapScalarApiReference("/scalar", options =>
    {
        options.WithTitle("PedagangPulsa API Reference")
            .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json")
            .DisableAgent();
    });
}

app.UseHttpsRedirection();

// Enable static files for uploads
app.UseStaticFiles();

// Use rate limiting middleware (DISABLED)
// app.UseRateLimiting();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

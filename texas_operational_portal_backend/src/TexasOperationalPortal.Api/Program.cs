using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.Text;
using TexasOperationalPortal.Application;
using TexasOperationalPortal.Infrastructure;
using TexasOperationalPortal.Infrastructure.Persistence;
using TexasOperationalPortal.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Options validations at startup
builder.Services.AddOptions<TexasOperationalPortal.Application.Common.Options.JwtOptions>()
    .Bind(builder.Configuration.GetSection(TexasOperationalPortal.Application.Common.Options.JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<TexasOperationalPortal.Application.Common.Options.SalesPortalOptions>()
    .Bind(builder.Configuration.GetSection(TexasOperationalPortal.Application.Common.Options.SalesPortalOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<TexasOperationalPortal.Application.Common.Options.StaffSyncOptions>()
    .Bind(builder.Configuration.GetSection(TexasOperationalPortal.Application.Common.Options.StaffSyncOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Add layers
builder.Services.AddMemoryCache();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRealtimeInfrastructure(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<TexasOperationalPortal.Api.Filters.DownstreamExceptionFilter>();
});
builder.Services.AddProblemDetails();

builder.Services.AddTransient<TexasOperationalPortal.Api.Middleware.UserActiveCheckMiddleware>();

// Built-in Fixed Window Rate Limiting for Auth endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("AuthLimiter", opt =>
    {
        opt.PermitLimit = builder.Environment.IsEnvironment("Test") ? 1000 : 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Texas Operational Portal API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Configure CORS for SvelteKit Frontends
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCorsPolicy", policy =>
    {
        policy.WithOrigins(
                  "https://accountstexas.runasp.net",
                  "https://operationstexas.runasp.net",
                  "https://operationtexas.runasp.net",
                  "https://salestexas.runasp.net",
                  "http://localhost:5173", 
                  "http://localhost:4173", 
                  "http://localhost:8001", 
                  "http://localhost:8002", 
                  "http://localhost:8003", 
                  "http://localhost:8005",
                  "http://localhost:5174", 
                  "http://localhost:5175",
                  "http://127.0.0.1:5173", 
                  "http://127.0.0.1:4173", 
                  "http://127.0.0.1:8001", 
                  "http://127.0.0.1:8002", 
                  "http://127.0.0.1:8003",
                  "http://127.0.0.1:8005")
              .SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// JWT Bearer Auth Configuration
var jwtKey = builder.Configuration["Jwt:Key"] ?? "tx_ops_super_secret_key_999_must_be_long_enough";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "texas_operational_portal";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "texas_operational_portal_clients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    options.Events ??= new JwtBearerEvents();
    options.Events.ConfigureJwtBearerQueryStringForSignalR();
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OperationalPortalAccess", policy =>
        policy.RequireRole("Admin", "Operations", "Accounts"));

    options.AddPolicy("CanViewVisas", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") ||
            ctx.User.HasClaim(c => c.Type == "permission" && c.Value == "visas")));

    options.AddPolicy("CanViewVfs", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") ||
            ctx.User.HasClaim(c => c.Type == "permission" && c.Value == "vfs")));

    options.AddPolicy("CanViewHolidays", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") ||
            ctx.User.HasClaim(c => c.Type == "permission" && c.Value == "holidays")));

    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") ||
            ctx.User.HasClaim(c => c.Type == "permission" && c.Value == "manage_users")));
});

var app = builder.Build();

app.UseCors("DevCorsPolicy");

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Migrate and seed database
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OperationalDbContext>();
    try
    {
        await db.Database.EnsureCreatedAsync();
    }
    catch
    {
        // fallback
    }
    await DbInitializer.SeedAsync(db, true);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
    }
});

app.UseAuthentication();
app.UseMiddleware<TexasOperationalPortal.Api.Middleware.UserActiveCheckMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapRealtimeHubs();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }

using System.Text;
using ContainerTracking.Api.Hubs;
using ContainerTracking.Api.Middleware;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using ContainerTracking.Infrastructure.Providers;
using ContainerTracking.Infrastructure.Services;
using ContainerTracking.Infrastructure.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Structured Logging ────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ContainerTracking.Api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341")
    .CreateLogger();
builder.Host.UseSerilog();

// ─── Database ──────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npg => npg.UseNetTopologySuite()
    ));

// ─── Identity ──────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(opts =>
    {
        opts.Password.RequireDigit = true;
        opts.Password.RequiredLength = 8;
        opts.Password.RequireUppercase = false;
        opts.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ─── JWT Authentication ────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(opts =>
    {
        opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!))
        };
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

// ─── Authorization Policies ────────────────────────────────────────────────
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.RequirePlatformAdmin, p => p.RequireRole(Roles.PlatformAdmin))
    .AddPolicy(Policies.RequireOrganizationAdmin, p => p.RequireRole(Roles.PlatformAdmin, Roles.OrganizationAdmin))
    .AddPolicy(Policies.RequireLogisticsManager, p => p.RequireRole(Roles.PlatformAdmin, Roles.OrganizationAdmin, Roles.LogisticsManager))
    .AddPolicy(Policies.RequireViewer, p => p.RequireRole(Roles.PlatformAdmin, Roles.OrganizationAdmin, Roles.LogisticsManager, Roles.Viewer))
    .AddPolicy(Policies.RequireOrganizationAccess, p => p.RequireClaim("org_id"));

// ─── Core Services ─────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentOrganizationContext, CurrentOrganizationContext>();
builder.Services.AddScoped<ITierEnforcementService, TierEnforcementService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddHttpClient<INotificationService, NotificationService>();

// ─── Tracking Providers ────────────────────────────────────────────────────
builder.Services.AddHttpClient<AisStreamProvider>();
builder.Services.AddScoped<ITrackingProvider, AisStreamProvider>();
builder.Services.AddScoped<IAisProvider, AisStreamProvider>();
builder.Services.AddScoped<ITrackingNormalizer, TrackingNormalizer>();

// Carrier providers
builder.Services.AddHttpClient<MaerskCarrierProvider>();
builder.Services.AddScoped<ITrackingProvider, MaerskCarrierProvider>();

// ─── SignalR ───────────────────────────────────────────────────────────────
var redisConn = builder.Configuration.GetConnectionString("Redis");
var signalRBuilder = builder.Services.AddSignalR(opts =>
{
    opts.EnableDetailedErrors = builder.Environment.IsDevelopment();
    opts.HandshakeTimeout = TimeSpan.FromSeconds(15);
    opts.KeepAliveInterval = TimeSpan.FromSeconds(15);
    opts.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});
if (!string.IsNullOrEmpty(redisConn))
    signalRBuilder.AddStackExchangeRedis(redisConn, o =>
        o.Configuration.ChannelPrefix = "ContainerTracking");

builder.Services.AddScoped<ISignalRPublisher, SignalRPublisher>();

// ─── Background Workers ────────────────────────────────────────────────────
builder.Services.AddHostedService<TrackingPollingWorker>();
builder.Services.AddHostedService<AlertProcessingWorker>();
builder.Services.AddHostedService<VesselPositionWorker>();

// ─── Alert & Manual Tracking Services ─────────────────────────────────────
builder.Services.AddScoped<AlertProcessingService>();
builder.Services.AddScoped<IManualTrackingProvider, ManualTrackingProvider>();
builder.Services.AddScoped<IPortMilestoneProvider, PortMilestoneProvider>();

// ─── Redis Cache ───────────────────────────────────────────────────────────
if (!string.IsNullOrEmpty(redisConn))
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
else
    builder.Services.AddMemoryCache();

// ─── Rate Limiting ─────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("api", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
    });
    opts.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(5);
    });
});

// ─── OpenTelemetry ─────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithTracing(tb => tb
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ContainerTracking.Api"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddOtlpExporter(o => o.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317")))
    .WithMetrics(mb => mb
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ContainerTracking.Api"))
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

// ─── Controllers / Swagger ─────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Container Tracking SaaS API",
        Version = "v1",
        Description = "Multi-tenant container and shipment tracking platform"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-Key"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, [] }
    });
});

builder.Services.AddCors(opts => opts.AddPolicy("AllowFrontend", p =>
    p.WithOrigins(builder.Configuration["Cors:AllowedOrigins"]?.Split(',') ?? ["http://localhost:5002"])
     .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database")
    .AddCheck<TrackingProviderHealthCheck>("tracking-providers");

// ─── Build App ─────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseTenantResolution();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TrackingHub>("/hubs/tracking");
app.MapPrometheusScrapingEndpoint("/metrics");
app.MapHealthChecks("/health");

// Apply migrations and seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seederLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db, seederLogger);
    }
    catch (Exception ex)
    {
        seederLogger.LogError(ex, "Database migration/seed failed");
    }
}

app.Run();

// ─── Health Check ──────────────────────────────────────────────────────────
public class TrackingProviderHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IEnumerable<ITrackingProvider> _providers;
    public TrackingProviderHealthCheck(IEnumerable<ITrackingProvider> providers) { _providers = providers; }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, object>();
        foreach (var p in _providers)
        {
            var health = await p.CheckHealthAsync(cancellationToken);
            results[p.ProviderCode] = health.IsHealthy ? "healthy" : $"unhealthy: {health.Message}";
        }
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Providers checked", results);
    }
}

public partial class Program { }

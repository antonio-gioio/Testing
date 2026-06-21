using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ContainerTracking.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ICurrentOrganizationContext _orgContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentOrganizationContext orgContext)
        : base(options)
    {
        _orgContext = orgContext;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserOrganization> UserOrganizations => Set<UserOrganization>();
    public DbSet<SubscriptionTier> SubscriptionTiers => Set<SubscriptionTier>();
    public DbSet<OrganizationSubscription> OrganizationSubscriptions => Set<OrganizationSubscription>();
    public DbSet<TierUsageCounter> TierUsageCounters => Set<TierUsageCounter>();
    public DbSet<Vessel> Vessels => Set<Vessel>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Container> Containers => Set<Container>();
    public DbSet<BillOfLading> BillsOfLading => Set<BillOfLading>();
    public DbSet<TrackingEvent> TrackingEvents => Set<TrackingEvent>();
    public DbSet<TrackingProvider> TrackingProviders => Set<TrackingProvider>();
    public DbSet<ProviderCredential> ProviderCredentials => Set<ProviderCredential>();
    public DbSet<DashboardLayout> DashboardLayouts => Set<DashboardLayout>();
    public DbSet<DashboardWidget> DashboardWidgets => Set<DashboardWidget>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("postgis");
        builder.HasPostgresExtension("uuid-ossp");

        ConfigureOrganization(builder);
        ConfigureUser(builder);
        ConfigureShipment(builder);
        ConfigureContainer(builder);
        ConfigureVessel(builder);
        ConfigureTrackingEvent(builder);
        ConfigureDashboard(builder);
        ConfigureAlerts(builder);
        ConfigureSubscription(builder);

        ApplyOrganizationScopedFilters(builder);
    }

    private void ConfigureOrganization(ModelBuilder builder)
    {
        builder.Entity<Organization>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<UserOrganization>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.OrganizationId }).IsUnique();
            e.HasOne(x => x.User).WithMany(x => x.UserOrganizations).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Organization).WithMany(x => x.UserOrganizations).HasForeignKey(x => x.OrganizationId);
            e.HasQueryFilter(x => !x.IsDeleted && x.IsActive);
        });
    }

    private void ConfigureUser(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("Users");
        });
        builder.Entity<ApplicationRole>(e =>
        {
            e.ToTable("Roles");
        });
    }

    private void ConfigureShipment(ModelBuilder builder)
    {
        builder.Entity<Shipment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Reference });
            e.HasIndex(x => x.OrganizationId);
            e.Property(x => x.CustomFields).HasColumnType("jsonb");
            e.HasOne(x => x.Organization).WithMany(x => x.Shipments).HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.Vessel).WithMany(x => x.Shipments).HasForeignKey(x => x.VesselId).IsRequired(false);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<BillOfLading>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.BolNumber });
            e.HasIndex(x => x.OrganizationId);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.Shipment).WithMany(x => x.BillsOfLading).HasForeignKey(x => x.ShipmentId).IsRequired(false);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    private void ConfigureContainer(ModelBuilder builder)
    {
        builder.Entity<Container>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.ContainerNumber });
            e.HasIndex(x => x.OrganizationId);
            e.HasIndex(x => x.PublicTrackingToken).IsUnique().HasFilter("public_tracking_token IS NOT NULL");
            e.Property(x => x.CustomFields).HasColumnType("jsonb");
            e.HasOne(x => x.Organization).WithMany(x => x.Containers).HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.Shipment).WithMany(x => x.Containers).HasForeignKey(x => x.ShipmentId).IsRequired(false);
            e.HasOne(x => x.BillOfLading).WithMany(x => x.Containers).HasForeignKey(x => x.BillOfLadingId).IsRequired(false);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    private void ConfigureVessel(ModelBuilder builder)
    {
        builder.Entity<Vessel>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Imo).IsUnique();
            e.HasIndex(x => x.Mmsi);
            e.Property(x => x.CurrentPosition).HasColumnType("geometry(Point,4326)");
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    private void ConfigureTrackingEvent(ModelBuilder builder)
    {
        builder.Entity<TrackingEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrganizationId);
            e.HasIndex(x => x.ContainerId);
            e.HasIndex(x => x.ShipmentId);
            e.HasIndex(x => new { x.OrganizationId, x.EventTime });
            e.HasIndex(x => new { x.ExternalEventId, x.ProviderType });
            e.Property(x => x.GeoPosition).HasColumnType("geometry(Point,4326)");
            e.Property(x => x.RawData).HasColumnType("jsonb");
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.Container).WithMany(x => x.TrackingEvents).HasForeignKey(x => x.ContainerId).IsRequired(false);
            e.HasOne(x => x.Shipment).WithMany(x => x.TrackingEvents).HasForeignKey(x => x.ShipmentId).IsRequired(false);
            e.HasOne(x => x.BillOfLading).WithMany(x => x.TrackingEvents).HasForeignKey(x => x.BillOfLadingId).IsRequired(false);
            e.HasOne(x => x.Vessel).WithMany(x => x.TrackingEvents).HasForeignKey(x => x.VesselId).IsRequired(false);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<TrackingProvider>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.ConfigSchema).HasColumnType("jsonb");
        });

        builder.Entity<ProviderCredential>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.TrackingProviderId });
            e.Property(x => x.EncryptedConfig).HasColumnType("jsonb");
            e.HasOne(x => x.Organization).WithMany(x => x.ProviderCredentials).HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.TrackingProvider).WithMany(x => x.Credentials).HasForeignKey(x => x.TrackingProviderId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    private void ConfigureDashboard(ModelBuilder builder)
    {
        builder.Entity<DashboardLayout>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrganizationId);
            e.HasIndex(x => new { x.OrganizationId, x.UserId });
            e.HasOne(x => x.Organization).WithMany(x => x.DashboardLayouts).HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.User).WithMany(x => x.DashboardLayouts).HasForeignKey(x => x.UserId).IsRequired(false);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<DashboardWidget>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DashboardLayoutId);
            e.Property(x => x.Config).HasColumnType("jsonb");
            e.HasOne(x => x.DashboardLayout).WithMany(x => x.Widgets).HasForeignKey(x => x.DashboardLayoutId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    private void ConfigureAlerts(ModelBuilder builder)
    {
        builder.Entity<Alert>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrganizationId);
            e.Property(x => x.Conditions).HasColumnType("jsonb");
            e.Property(x => x.NotificationChannels).HasColumnType("jsonb");
            e.Property(x => x.RecipientEmails).HasColumnType("jsonb");
            e.HasOne(x => x.Organization).WithMany(x => x.Alerts).HasForeignKey(x => x.OrganizationId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrganizationId);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.Status });
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.HasOne(x => x.Alert).WithMany(x => x.Notifications).HasForeignKey(x => x.AlertId).IsRequired(false);
            e.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).IsRequired(false);
        });

        builder.Entity<ApiKey>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrganizationId);
            e.HasIndex(x => x.KeyPrefix);
            e.Property(x => x.Scopes).HasColumnType("jsonb");
            e.HasOne(x => x.Organization).WithMany(x => x.ApiKeys).HasForeignKey(x => x.OrganizationId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrganizationId);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.HasOne(x => x.User).WithMany(x => x.AuditLogs).HasForeignKey(x => x.UserId).IsRequired(false);
        });

        builder.Entity<ExportJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrganizationId);
            e.HasIndex(x => x.DownloadToken);
            e.Property(x => x.Filters).HasColumnType("jsonb");
            e.HasOne(x => x.Organization).WithMany(x => x.ExportJobs).HasForeignKey(x => x.OrganizationId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    private void ConfigureSubscription(ModelBuilder builder)
    {
        builder.Entity<SubscriptionTier>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TierType).IsUnique();
        });

        builder.Entity<OrganizationSubscription>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrganizationId).IsUnique();
            e.Property(x => x.CustomLimitOverrides).HasColumnType("jsonb");
            e.HasOne(x => x.Organization).WithOne(x => x.Subscription).HasForeignKey<OrganizationSubscription>(x => x.OrganizationId);
            e.HasOne(x => x.SubscriptionTier).WithMany(x => x.Subscriptions).HasForeignKey(x => x.SubscriptionTierId);
        });

        builder.Entity<TierUsageCounter>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrganizationId).IsUnique();
            e.HasOne(x => x.Organization).WithOne(x => x.UsageCounter).HasForeignKey<TierUsageCounter>(x => x.OrganizationId);
        });
    }

    private void ApplyOrganizationScopedFilters(ModelBuilder builder)
    {
        // All OrganizationScopedEntity types already have IsDeleted filter
        // Additional runtime org filtering is done in repositories/services
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Added)
                entity.CreatedAt = DateTime.UtcNow;
        }
    }
}

public class ApplicationRole : Microsoft.AspNetCore.Identity.IdentityRole<Guid>
{
    public string? Description { get; set; }
}

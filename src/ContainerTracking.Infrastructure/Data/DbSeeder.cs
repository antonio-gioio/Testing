using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        await SeedSubscriptionTiersAsync(db, logger);
        await SeedTrackingProvidersAsync(db, logger);
    }

    private static async Task SeedSubscriptionTiersAsync(AppDbContext db, ILogger logger)
    {
        if (await db.SubscriptionTiers.AnyAsync()) return;

        var tiers = new[]
        {
            new SubscriptionTier
            {
                Id = Guid.NewGuid(),
                Name = "Free",
                TierType = SubscriptionTierType.Free,
                Description = "For individuals and small teams just getting started",
                MonthlyPriceUsd = 0m,
                AnnualPriceUsd = 0m,
                MaxContainers = 5,
                MaxUsers = 2,
                MaxShipments = 5,
                MaxAlerts = 3,
                UpdateIntervalMinutes = 360,
                HistoryRetentionDays = 7,
                WebSocketEnabled = false,
                ApiRateLimitPerHour = 50,
                ExportEnabled = false,
                AdvancedAnalyticsEnabled = false,
                CustomWidgetsEnabled = false,
                WhiteLabelEnabled = false,
                SlaLevel = "Community",
                ApiAccessEnabled = false,
                CsvImportEnabled = false,
                WebhookAlertsEnabled = false,
                CustomerTrackingLinksEnabled = false,
                DisplayOrder = 1,
                IsActive = true, IsPublic = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new SubscriptionTier
            {
                Id = Guid.NewGuid(),
                Name = "Starter",
                TierType = SubscriptionTierType.Starter,
                Description = "For growing logistics operations",
                MonthlyPriceUsd = 99m,
                AnnualPriceUsd = 990m,
                MaxContainers = 50,
                MaxUsers = 5,
                MaxShipments = 50,
                MaxAlerts = 20,
                UpdateIntervalMinutes = 60,
                HistoryRetentionDays = 30,
                WebSocketEnabled = true,
                ApiRateLimitPerHour = 500,
                ExportEnabled = true,
                AdvancedAnalyticsEnabled = false,
                CustomWidgetsEnabled = false,
                WhiteLabelEnabled = false,
                SlaLevel = "Standard",
                ApiAccessEnabled = true,
                CsvImportEnabled = true,
                WebhookAlertsEnabled = false,
                CustomerTrackingLinksEnabled = true,
                DisplayOrder = 2,
                IsActive = true, IsPublic = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new SubscriptionTier
            {
                Id = Guid.NewGuid(),
                Name = "Business",
                TierType = SubscriptionTierType.Business,
                Description = "For established freight forwarders and 3PLs",
                MonthlyPriceUsd = 399m,
                AnnualPriceUsd = 3990m,
                MaxContainers = 500,
                MaxUsers = 25,
                MaxShipments = 500,
                MaxAlerts = 100,
                UpdateIntervalMinutes = 15,
                HistoryRetentionDays = 90,
                WebSocketEnabled = true,
                ApiRateLimitPerHour = 5000,
                ExportEnabled = true,
                AdvancedAnalyticsEnabled = true,
                CustomWidgetsEnabled = true,
                WhiteLabelEnabled = false,
                SlaLevel = "Business",
                ApiAccessEnabled = true,
                CsvImportEnabled = true,
                WebhookAlertsEnabled = true,
                CustomerTrackingLinksEnabled = true,
                DisplayOrder = 3,
                IsActive = true, IsPublic = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new SubscriptionTier
            {
                Id = Guid.NewGuid(),
                Name = "Enterprise",
                TierType = SubscriptionTierType.Enterprise,
                Description = "Unlimited scale for large enterprises",
                MonthlyPriceUsd = 999m,
                AnnualPriceUsd = 9990m,
                MaxContainers = 10000,
                MaxUsers = 200,
                MaxShipments = 10000,
                MaxAlerts = 1000,
                UpdateIntervalMinutes = 5,
                HistoryRetentionDays = 365,
                WebSocketEnabled = true,
                ApiRateLimitPerHour = 50000,
                ExportEnabled = true,
                AdvancedAnalyticsEnabled = true,
                CustomWidgetsEnabled = true,
                WhiteLabelEnabled = true,
                SlaLevel = "Enterprise",
                ApiAccessEnabled = true,
                CsvImportEnabled = true,
                WebhookAlertsEnabled = true,
                CustomerTrackingLinksEnabled = true,
                DisplayOrder = 4,
                IsActive = true, IsPublic = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new SubscriptionTier
            {
                Id = Guid.NewGuid(),
                Name = "Custom",
                TierType = SubscriptionTierType.Custom,
                Description = "Tailored plans for strategic partnerships",
                MonthlyPriceUsd = 0m,
                AnnualPriceUsd = 0m,
                MaxContainers = int.MaxValue,
                MaxUsers = int.MaxValue,
                MaxShipments = int.MaxValue,
                MaxAlerts = int.MaxValue,
                UpdateIntervalMinutes = 1,
                HistoryRetentionDays = 3650,
                WebSocketEnabled = true,
                ApiRateLimitPerHour = int.MaxValue,
                ExportEnabled = true,
                AdvancedAnalyticsEnabled = true,
                CustomWidgetsEnabled = true,
                WhiteLabelEnabled = true,
                SlaLevel = "Enterprise+",
                ApiAccessEnabled = true,
                CsvImportEnabled = true,
                WebhookAlertsEnabled = true,
                CustomerTrackingLinksEnabled = true,
                DisplayOrder = 5,
                IsActive = true, IsPublic = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        };

        db.SubscriptionTiers.AddRange(tiers);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} subscription tiers", tiers.Length);
    }

    private static async Task SeedTrackingProvidersAsync(AppDbContext db, ILogger logger)
    {
        if (await db.TrackingProviders.AnyAsync()) return;

        var providers = new[]
        {
            new TrackingProvider
            {
                Id = Guid.NewGuid(), Name = "AISStream", Code = "aisstream",
                ProviderType = TrackingProviderType.Ais,
                Description = "Real-time AIS vessel position data via aisstream.io",
                BaseUrl = "https://api.aisstream.io/v0",
                IsEnabled = true, IsGlobal = true, RequiresCredentials = true,
                PollIntervalMinutes = 5, Priority = 10,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new TrackingProvider
            {
                Id = Guid.NewGuid(), Name = "Maersk", Code = "maersk",
                ProviderType = TrackingProviderType.Carrier,
                Description = "Maersk container tracking via official developer API",
                BaseUrl = "https://api.maersk.com",
                IsEnabled = true, IsGlobal = false, RequiresCredentials = true,
                PollIntervalMinutes = 60, Priority = 20,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new TrackingProvider
            {
                Id = Guid.NewGuid(), Name = "MSC", Code = "msc",
                ProviderType = TrackingProviderType.Carrier,
                Description = "MSC container tracking",
                BaseUrl = "https://www.msc.com/api/tracking",
                IsEnabled = false, IsGlobal = false, RequiresCredentials = true,
                PollIntervalMinutes = 120, Priority = 20,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new TrackingProvider
            {
                Id = Guid.NewGuid(), Name = "CMA CGM", Code = "cmacgm",
                ProviderType = TrackingProviderType.Carrier,
                Description = "CMA CGM container tracking",
                BaseUrl = "https://apis.cma-cgm.net",
                IsEnabled = false, IsGlobal = false, RequiresCredentials = true,
                PollIntervalMinutes = 120, Priority = 20,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new TrackingProvider
            {
                Id = Guid.NewGuid(), Name = "Hapag-Lloyd", Code = "hapag",
                ProviderType = TrackingProviderType.Carrier,
                Description = "Hapag-Lloyd container tracking",
                BaseUrl = "https://api.hapag-lloyd.com",
                IsEnabled = false, IsGlobal = false, RequiresCredentials = true,
                PollIntervalMinutes = 120, Priority = 20,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new TrackingProvider
            {
                Id = Guid.NewGuid(), Name = "Terminal49", Code = "TERMINAL49",
                ProviderType = TrackingProviderType.Carrier,
                Description = "Terminal49 — terminal availability, holds, and LFD for trucker dispatch",
                BaseUrl = "https://api.terminal49.com/api/v2",
                IsEnabled = true, IsGlobal = false, RequiresCredentials = true,
                PollIntervalMinutes = 30, Priority = 25,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new TrackingProvider
            {
                Id = Guid.NewGuid(), Name = "Manual", Code = "manual",
                ProviderType = TrackingProviderType.Manual,
                Description = "Manual event entry by operations staff",
                IsEnabled = true, IsGlobal = true, RequiresCredentials = false,
                PollIntervalMinutes = 0, Priority = 5,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        };

        db.TrackingProviders.AddRange(providers);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} tracking providers", providers.Length);
    }
}

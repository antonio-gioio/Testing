using System.Text.Json;
using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Core.Models;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController(AppDbContext db, ITrackingNormalizer normalizer, ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost("{orgWebhookToken}")]
    public async Task<IActionResult> Ingest(string orgWebhookToken, [FromBody] JsonElement payload)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.WebhookIngestToken == orgWebhookToken && o.IsActive && !o.IsDeleted);
        if (org == null)
        {
            logger.LogWarning("Inbound webhook with unknown token {Token}", orgWebhookToken);
            return Unauthorized();
        }

        var rawJson = payload.GetRawText();
        var sourceHint = Request.Headers["X-Source"].FirstOrDefault() ?? "generic";

        var providerType = sourceHint.ToLowerInvariant() switch
        {
            "maersk" or "msc" or "hapag" or "carrier" => TrackingProviderType.Carrier,
            "port" => TrackingProviderType.PortApi,
            _ => TrackingProviderType.Webhook
        };

        try
        {
            var rawData = new RawTrackingData
            {
                SourceProvider = sourceHint,
                RawJson = rawJson,
                ReceivedAt = DateTime.UtcNow
            };

            var normalized = normalizer.NormalizeBatch(new[] { rawData }, providerType).ToList();
            int saved = 0;

            foreach (var evt in normalized)
            {
                if (string.IsNullOrEmpty(evt.ContainerNumber)) continue;

                var container = await db.Containers.FirstOrDefaultAsync(c =>
                    c.OrganizationId == org.Id &&
                    c.ContainerNumber == evt.ContainerNumber &&
                    !c.IsDeleted);
                if (container == null) continue;

                if (!string.IsNullOrEmpty(evt.ExternalId))
                {
                    var dup = await db.TrackingEvents.AnyAsync(e =>
                        e.ContainerId == container.Id &&
                        e.ExternalEventId == evt.ExternalId &&
                        e.ProviderType == providerType);
                    if (dup) continue;
                }

                db.TrackingEvents.Add(new TrackingEvent
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = org.Id,
                    ContainerId = container.Id,
                    EventType = evt.EventType,
                    ProviderType = providerType,
                    Description = evt.Description,
                    Location = evt.Location,
                    ExternalEventId = evt.ExternalId,
                    EventTime = evt.EventTime,
                    ReceivedAt = DateTime.UtcNow,
                    ContainerStatusAfter = evt.StatusAfter,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                saved++;
            }

            if (saved > 0) await db.SaveChangesAsync();
            logger.LogInformation("Inbound webhook for org {OrgId}: {Count} events ingested", org.Id, saved);
            return Ok(new { accepted = true, eventsIngested = saved });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process inbound webhook for org {OrgId}", org.Id);
            return StatusCode(500, new { error = "Failed to process webhook payload." });
        }
    }

    [HttpGet("{orgWebhookToken}/ping")]
    public async Task<IActionResult> Ping(string orgWebhookToken)
    {
        var exists = await db.Organizations.AnyAsync(o => o.WebhookIngestToken == orgWebhookToken && o.IsActive && !o.IsDeleted);
        if (!exists) return Unauthorized();
        return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
    }
}

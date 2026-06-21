using System.Text.Json;
using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(AppDbContext db, IHttpContextAccessor http, ILogger<AuditLogService> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task LogAsync(
        string action, string entityType, Guid? entityId = null,
        Guid? organizationId = null, Guid? userId = null,
        object? oldValues = null, object? newValues = null,
        bool success = true, string? failureReason = null,
        CancellationToken ct = default)
    {
        try
        {
            var context = _http.HttpContext;
            var log = new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OrganizationId = organizationId,
                UserId = userId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context?.Request.Headers.UserAgent.ToString(),
                RequestPath = context?.Request.Path.ToString(),
                HttpMethod = context?.Request.Method,
                Success = success,
                FailureReason = failureReason
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for action {Action} on {Entity}", action, entityType);
        }
    }
}

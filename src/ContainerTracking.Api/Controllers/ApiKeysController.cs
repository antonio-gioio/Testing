using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using ContainerTracking.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/api-keys")]
[Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.PlatformAdmin}")]
public class ApiKeysController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITierEnforcementService _tierService;
    private readonly ICurrentOrganizationContext _orgContext;

    public ApiKeysController(AppDbContext db, ITierEnforcementService tierService, ICurrentOrganizationContext orgContext)
    {
        _db = db;
        _tierService = tierService;
        _orgContext = orgContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetApiKeys(CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var keys = await _db.ApiKeys
            .Where(k => k.OrganizationId == orgId)
            .Select(k => new
            {
                k.Id, k.Name, k.KeyPrefix, k.Scopes, k.IsActive,
                k.ExpiresAt, k.LastUsedAt, k.CallCount, k.CreatedAt
            })
            .ToListAsync(ct);
        return Ok(keys);
    }

    [HttpPost]
    public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKeyRequest request, CancellationToken ct = default)
    {
        var orgId = GetOrgId();

        var limits = await _tierService.GetLimitsAsync(orgId, ct);
        if (!limits.ApiAccessEnabled)
            return StatusCode(402, new { error = "API access requires Starter tier or higher." });

        var rawKey = GenerateApiKey();
        var prefix = rawKey[..8];
        var hashed = ApiKeyAuthMiddleware.HashApiKey(rawKey);

        var apiKey = new ApiKey
        {
            OrganizationId = orgId,
            Name = request.Name,
            KeyPrefix = prefix,
            HashedKey = hashed,
            Scopes = request.Scopes,
            ExpiresAt = request.ExpiresAt,
            CreatedByUserId = _orgContext.UserId
        };

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            id = apiKey.Id,
            name = apiKey.Name,
            key = rawKey,
            prefix = prefix,
            message = "Store this key securely — it will not be shown again."
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeApiKey(Guid id, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var key = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.OrganizationId == orgId, ct);
        if (key == null) return NotFound();
        key.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Guid GetOrgId()
    {
        var id = _orgContext.OrganizationId;
        if (!id.HasValue) throw new UnauthorizedAccessException();
        return id.Value;
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return "ct_" + Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
    }
}

public record CreateApiKeyRequest(string Name, List<string> Scopes, DateTime? ExpiresAt);

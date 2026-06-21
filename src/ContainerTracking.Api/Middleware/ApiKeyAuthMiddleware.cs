using ContainerTracking.Core.Enums;
using ContainerTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ContainerTracking.Api.Middleware;

/// <summary>
/// Handles API key authentication for machine-to-machine integrations.
/// API keys are passed in X-API-Key header or as bearer token.
/// Keys are stored as PBKDF2 hashes to prevent exposure on DB breach.
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeader = "X-API-Key";

    public ApiKeyAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            var rawKey = ExtractApiKey(context);
            if (!string.IsNullOrEmpty(rawKey))
            {
                await AuthenticateWithApiKey(context, db, rawKey);
            }
        }

        await _next(context);
    }

    private async Task AuthenticateWithApiKey(HttpContext context, AppDbContext db, string rawKey)
    {
        if (rawKey.Length < 8) return;

        var prefix = rawKey[..8];
        var apiKeys = await db.ApiKeys
            .Include(k => k.Organization)
            .Where(k => k.KeyPrefix == prefix && k.IsActive && !k.IsDeleted)
            .ToListAsync();

        foreach (var key in apiKeys)
        {
            if (key.ExpiresAt.HasValue && key.ExpiresAt < DateTime.UtcNow) continue;
            if (!VerifyApiKey(rawKey, key.HashedKey)) continue;

            key.LastUsedAt = DateTime.UtcNow;
            key.CallCount++;
            await db.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, $"apikey:{key.Id}"),
                new(ClaimTypes.Name, key.Name),
                new(ClaimTypes.Role, Roles.ApiClient),
                new("org_id", key.OrganizationId.ToString()),
                new("api_key_id", key.Id.ToString()),
                new("auth_method", "api_key")
            };

            foreach (var scope in key.Scopes)
                claims.Add(new Claim("scope", scope));

            var identity = new ClaimsIdentity(claims, "ApiKey");
            context.User = new ClaimsPrincipal(identity);
            return;
        }
    }

    private static string? ExtractApiKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ApiKeyHeader, out var header))
            return header.FirstOrDefault();

        var auth = context.Request.Headers.Authorization.FirstOrDefault();
        if (auth?.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase) == true)
            return auth[7..];

        return null;
    }

    public static string HashApiKey(string rawKey)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(rawKey, Encoding.UTF8.GetBytes("ct-salt-v1"), 100_000, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }

    private static bool VerifyApiKey(string rawKey, string storedHash)
    {
        var hash = HashApiKey(rawKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash),
            Encoding.UTF8.GetBytes(storedHash));
    }
}

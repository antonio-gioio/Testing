using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signIn,
        AppDbContext db, IConfiguration config)
    {
        _userManager = userManager;
        _signIn = signIn;
        _db = db;
        _config = config;
    }

    /// <summary>POST /api/v1/auth/register — register a new user and organization</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _userManager.FindByEmailAsync(request.Email) != null)
            return Conflict(new { error = "Email already registered." });

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        var org = new Organization
        {
            Name = request.OrganizationName,
            Slug = GenerateSlug(request.OrganizationName),
            ContactEmail = request.Email
        };
        _db.Organizations.Add(org);

        var freeTier = await _db.SubscriptionTiers.FirstOrDefaultAsync(t => t.TierType == SubscriptionTierType.Free);
        if (freeTier != null)
        {
            _db.OrganizationSubscriptions.Add(new OrganizationSubscription
            {
                OrganizationId = org.Id,
                SubscriptionTierId = freeTier.Id,
                StartDate = DateTime.UtcNow,
                Status = "Active",
                IsTrial = true,
                TrialEndDate = DateTime.UtcNow.AddDays(14)
            });
        }

        _db.TierUsageCounters.Add(new TierUsageCounter { OrganizationId = org.Id });

        _db.UserOrganizations.Add(new UserOrganization
        {
            UserId = user.Id,
            OrganizationId = org.Id,
            Role = Roles.OrganizationAdmin,
            AcceptedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        var token = await GenerateJwtToken(user, org.Id);
        return Ok(new AuthResponse(token.AccessToken, token.RefreshToken, token.ExpiresAt, user.Id, org.Id));
    }

    /// <summary>POST /api/v1/auth/login</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { error = "Invalid email or password." });

        if (!user.IsActive)
            return Unauthorized(new { error = "Account is disabled." });

        var orgMembership = await _db.UserOrganizations
            .Where(uo => uo.UserId == user.Id && uo.IsActive)
            .OrderBy(uo => uo.CreatedAt)
            .FirstOrDefaultAsync();

        var orgId = orgMembership?.OrganizationId ?? Guid.Empty;
        var token = await GenerateJwtToken(user, orgId, orgMembership?.Role);

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse(token.AccessToken, token.RefreshToken, token.ExpiresAt, user.Id, orgId));
    }

    /// <summary>POST /api/v1/auth/refresh</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        // In production: validate refresh token from DB/Redis, rotate it
        return Unauthorized(new { error = "Refresh token invalid or expired." });
    }

    /// <summary>POST /api/v1/auth/logout</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return Ok();
    }

    private async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> GenerateJwtToken(
        ApplicationUser user, Guid orgId, string? role = null)
    {
        var jwtSettings = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(double.Parse(jwtSettings["ExpiryHours"] ?? "8"));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("org_id", orgId.ToString()),
            new("first_name", user.FirstName),
            new("last_name", user.LastName)
        };

        if (user.IsPlatformAdmin)
            claims.Add(new(ClaimTypes.Role, Roles.PlatformAdmin));
        else if (role != null)
            claims.Add(new(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        var refreshToken = GenerateRefreshToken();
        return (new JwtSecurityTokenHandler().WriteToken(token), refreshToken, expiry);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .Aggregate(string.Empty, (s, c) => s + c)
            .Trim('-');
    }
}

public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string OrganizationName);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, Guid UserId, Guid OrganizationId);

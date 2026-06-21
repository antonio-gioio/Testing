using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(AppDbContext db, ICurrentOrganizationContext ctx, UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = ctx.UserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
        if (user == null) return NotFound();

        var orgId = ctx.OrganizationId;
        var membership = orgId.HasValue
            ? await db.UserOrganizations.FirstOrDefaultAsync(uo => uo.UserId == user.Id && uo.OrganizationId == orgId)
            : null;

        return Ok(new
        {
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.IsPlatformAdmin,
            Role = membership?.Role,
            OrganizationId = orgId
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest req)
    {
        var userId = ctx.UserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
        if (user == null) return NotFound();

        if (!string.IsNullOrEmpty(req.FirstName)) user.FirstName = req.FirstName;
        if (!string.IsNullOrEmpty(req.LastName)) user.LastName = req.LastName;
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.FirstName, user.LastName, user.Email });
    }

    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = ctx.UserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var result = await userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Ok(new { message = "Password changed successfully." });
    }

    [HttpGet]
    [Authorize(Policy = "RequireOrgAdmin")]
    public async Task<IActionResult> ListOrgUsers()
    {
        var orgId = ctx.OrganizationId;
        if (!orgId.HasValue) return Unauthorized();

        var users = await db.UserOrganizations
            .Include(uo => uo.User)
            .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
            .Select(uo => new
            {
                uo.User!.Id,
                uo.User.FirstName,
                uo.User.LastName,
                uo.User.Email,
                uo.Role,
                uo.JoinedAt,
                LastLogin = uo.User.LastLoginAt
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireOrgAdmin")]
    public async Task<IActionResult> RemoveFromOrg(Guid id)
    {
        var orgId = ctx.OrganizationId;
        if (!orgId.HasValue) return Unauthorized();

        var membership = await db.UserOrganizations
            .FirstOrDefaultAsync(uo => uo.UserId == id && uo.OrganizationId == orgId && uo.IsActive);
        if (membership == null) return NotFound();

        membership.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    public record UpdateProfileRequest(string? FirstName, string? LastName);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}

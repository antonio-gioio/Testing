using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ExportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IExportService _exportService;
    private readonly ITierEnforcementService _tierService;
    private readonly ICurrentOrganizationContext _orgContext;

    public ExportsController(AppDbContext db, IExportService exportService,
        ITierEnforcementService tierService, ICurrentOrganizationContext orgContext)
    {
        _db = db;
        _exportService = exportService;
        _tierService = tierService;
        _orgContext = orgContext;
    }

    [HttpPost]
    public async Task<IActionResult> RequestExport([FromBody] ExportRequest request, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var tierCheck = await _tierService.CanExportAsync(orgId, request.Format, ct);
        if (!tierCheck.Allowed)
            return StatusCode(402, new { error = tierCheck.DenialReason });

        var job = await _exportService.QueueExportAsync(orgId, userId.Value, request.Format, request.DataType, request.Filters, ct);

        // In production: queue Hangfire/background job
        _ = Task.Run(async () => await _exportService.ProcessExportJobAsync(job.Id, CancellationToken.None));

        return Ok(new { jobId = job.Id, status = job.Status.ToString(), message = "Export queued. Poll /exports/{id} for status." });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetExportStatus(Guid id, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var job = await _db.ExportJobs.FirstOrDefaultAsync(j => j.Id == id && j.OrganizationId == orgId, ct);
        if (job == null) return NotFound();

        return Ok(new
        {
            id = job.Id,
            status = job.Status.ToString(),
            format = job.Format.ToString(),
            recordCount = job.RecordCount,
            fileSizeBytes = job.FileSizeBytes,
            completedAt = job.CompletedAt,
            expiresAt = job.ExpiresAt,
            downloadUrl = job.Status == ExportJobStatus.Completed
                ? Url.Action(nameof(DownloadExport), new { id = job.Id, token = job.DownloadToken })
                : null
        });
    }

    [HttpGet("{id:guid}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadExport(Guid id, [FromQuery] string token, CancellationToken ct = default)
    {
        var filePath = await _exportService.GetSecureDownloadUrlAsync(id, token, ct);
        if (filePath == null) return NotFound(new { error = "Export not found, expired, or download limit reached." });

        var contentType = Path.GetExtension(filePath) switch
        {
            ".csv" => "text/csv",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType, Path.GetFileName(filePath));
    }

    [HttpGet]
    public async Task<IActionResult> GetExportHistory(CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var jobs = await _db.ExportJobs
            .Where(j => j.OrganizationId == orgId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(20)
            .Select(j => new { j.Id, j.Format, j.DataType, j.Status, j.CreatedAt, j.CompletedAt, j.RecordCount })
            .ToListAsync(ct);
        return Ok(jobs);
    }

    private Guid GetOrgId()
    {
        var id = _orgContext.OrganizationId;
        if (!id.HasValue) throw new UnauthorizedAccessException();
        return id.Value;
    }

    private Guid? GetUserId()
    {
        var id = _orgContext.UserId;
        return id != null && Guid.TryParse(id, out var g) ? g : null;
    }
}

public record ExportRequest(ExportFormat Format, ExportDataType DataType, Dictionary<string, string> Filters);

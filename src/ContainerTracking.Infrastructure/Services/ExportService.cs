using System.Text;
using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Services;

public class ExportService : IExportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ExportService> _logger;
    private readonly string _exportBasePath;

    public ExportService(AppDbContext db, ILogger<ExportService> logger)
    {
        _db = db;
        _logger = logger;
        _exportBasePath = Path.Combine(Path.GetTempPath(), "ct-exports");
        Directory.CreateDirectory(_exportBasePath);
    }

    public async Task<ExportJob> QueueExportAsync(
        Guid organizationId, Guid userId, ExportFormat format,
        ExportDataType dataType, Dictionary<string, string> filters,
        CancellationToken ct = default)
    {
        var job = new ExportJob
        {
            OrganizationId = organizationId,
            Format = format,
            DataType = dataType,
            Status = ExportJobStatus.Queued,
            RequestedByUserId = userId.ToString(),
            Filters = filters,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            DownloadToken = GenerateSecureToken()
        };

        _db.ExportJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return job;
    }

    public async Task ProcessExportJobAsync(Guid exportJobId, CancellationToken ct = default)
    {
        var job = await _db.ExportJobs.FindAsync([exportJobId], ct);
        if (job == null) return;

        job.Status = ExportJobStatus.Processing;
        job.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            var filePath = await GenerateExportFileAsync(job, ct);
            job.FilePath = filePath;
            job.FileSizeBytes = new FileInfo(filePath).Length;
            job.Status = ExportJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Export job {JobId} completed: {Path}", job.Id, filePath);
        }
        catch (Exception ex)
        {
            job.Status = ExportJobStatus.Failed;
            job.FailureReason = ex.Message;
            _logger.LogError(ex, "Export job {JobId} failed", exportJobId);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetSecureDownloadUrlAsync(Guid exportJobId, string token, CancellationToken ct = default)
    {
        var job = await _db.ExportJobs.FindAsync([exportJobId], ct);
        if (job == null || job.DownloadToken != token) return null;
        if (job.Status != ExportJobStatus.Completed) return null;
        if (job.ExpiresAt < DateTime.UtcNow) return null;
        if (job.DownloadCount >= job.MaxDownloads) return null;

        job.DownloadCount++;
        await _db.SaveChangesAsync(ct);
        return job.FilePath;
    }

    private async Task<string> GenerateExportFileAsync(ExportJob job, CancellationToken ct)
    {
        return job.Format switch
        {
            ExportFormat.Csv => await GenerateCsvAsync(job, ct),
            ExportFormat.Excel => await GenerateExcelAsync(job, ct),
            ExportFormat.Pdf => await GeneratePdfAsync(job, ct),
            _ => throw new NotSupportedException($"Export format {job.Format} not supported")
        };
    }

    private async Task<string> GenerateCsvAsync(ExportJob job, CancellationToken ct)
    {
        var fileName = $"{job.Id}_{job.DataType}.csv";
        var filePath = Path.Combine(_exportBasePath, fileName);

        await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        await writer.WriteLineAsync("ContainerNumber,Status,CurrentLocation,ETA,LastEventAt,ShipmentReference");

        var containers = await GetExportDataAsync<Container>(job, ct);
        foreach (var c in containers)
        {
            await writer.WriteLineAsync(
                $"{c.ContainerNumber},{c.Status},{EscapeCsv(c.CurrentLocation)},{c.EtaDestination:o},{c.LastEventAt:o},{EscapeCsv(c.Shipment?.Reference)}");
        }

        job.RecordCount = containers.Count;
        return filePath;
    }

    private async Task<string> GenerateExcelAsync(ExportJob job, CancellationToken ct)
    {
        var fileName = $"{job.Id}_{job.DataType}.xlsx";
        var filePath = Path.Combine(_exportBasePath, fileName);

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Containers");

        ws.Cell(1, 1).Value = "Container Number";
        ws.Cell(1, 2).Value = "Status";
        ws.Cell(1, 3).Value = "Current Location";
        ws.Cell(1, 4).Value = "ETA Destination";
        ws.Cell(1, 5).Value = "Last Event";
        ws.Cell(1, 6).Value = "Shipment Reference";

        var style = ws.Row(1).Style;
        style.Font.Bold = true;
        style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;

        var containers = await GetExportDataAsync<Container>(job, ct);
        int row = 2;
        foreach (var c in containers)
        {
            ws.Cell(row, 1).Value = c.ContainerNumber;
            ws.Cell(row, 2).Value = c.Status.ToString();
            ws.Cell(row, 3).Value = c.CurrentLocation ?? "";
            ws.Cell(row, 4).Value = c.EtaDestination?.ToString("g") ?? "";
            ws.Cell(row, 5).Value = c.LastEventAt?.ToString("g") ?? "";
            ws.Cell(row, 6).Value = c.Shipment?.Reference ?? "";
            row++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
        job.RecordCount = containers.Count;
        return filePath;
    }

    private async Task<string> GeneratePdfAsync(ExportJob job, CancellationToken ct)
    {
        var fileName = $"{job.Id}_{job.DataType}.pdf";
        var filePath = Path.Combine(_exportBasePath, fileName);

        using var writer = new iText.Kernel.Pdf.PdfWriter(filePath);
        using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
        using var doc = new iText.Layout.Document(pdf);

        var title = new iText.Layout.Element.Paragraph($"Container Export - {DateTime.UtcNow:g} UTC")
            .SetFontSize(16).SetBold();
        doc.Add(title);

        var table = new iText.Layout.Element.Table(6);
        table.AddHeaderCell("Container #");
        table.AddHeaderCell("Status");
        table.AddHeaderCell("Location");
        table.AddHeaderCell("ETA");
        table.AddHeaderCell("Last Event");
        table.AddHeaderCell("Shipment");

        var containers = await GetExportDataAsync<Container>(job, ct);
        foreach (var c in containers)
        {
            table.AddCell(c.ContainerNumber);
            table.AddCell(c.Status.ToString());
            table.AddCell(c.CurrentLocation ?? "");
            table.AddCell(c.EtaDestination?.ToString("d") ?? "");
            table.AddCell(c.LastEventAt?.ToString("g") ?? "");
            table.AddCell(c.Shipment?.Reference ?? "");
        }

        doc.Add(table);
        job.RecordCount = containers.Count;
        return filePath;
    }

    private async Task<List<T>> GetExportDataAsync<T>(ExportJob job, CancellationToken ct) where T : class
    {
        if (typeof(T) == typeof(Container))
        {
            var query = _db.Containers
                .Include(c => c.Shipment)
                .Where(c => c.OrganizationId == job.OrganizationId && !c.IsDeleted);

            if (job.Filters.TryGetValue("status", out var status) && Enum.TryParse<ContainerStatus>(status, out var cs))
                query = query.Where(c => c.Status == cs);

            return (List<T>)(object)await query.ToListAsync(ct);
        }

        return new List<T>();
    }

    private static string EscapeCsv(string? value)
    {
        if (value == null) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

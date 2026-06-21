using ContainerTracking.Core.Enums;

namespace ContainerTracking.Core.Entities;

public class ExportJob : OrganizationScopedEntity
{
    public ExportFormat Format { get; set; }
    public ExportDataType DataType { get; set; }
    public ExportJobStatus Status { get; set; } = ExportJobStatus.Queued;
    public string? RequestedByUserId { get; set; }
    public Dictionary<string, string> Filters { get; set; } = new();
    public string? FilePath { get; set; }
    public string? FileUrl { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? RecordCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? FailureReason { get; set; }
    public string? DownloadToken { get; set; }
    public int DownloadCount { get; set; } = 0;
    public int MaxDownloads { get; set; } = 5;
}

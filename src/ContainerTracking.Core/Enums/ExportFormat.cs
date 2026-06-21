namespace ContainerTracking.Core.Enums;

public enum ExportFormat
{
    Csv = 0,
    Excel = 1,
    Pdf = 2
}

public enum ExportJobStatus
{
    Queued = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Expired = 4
}

public enum ExportDataType
{
    Containers = 0,
    Shipments = 1,
    TrackingEvents = 2,
    BillsOfLading = 3,
    AuditLogs = 4
}

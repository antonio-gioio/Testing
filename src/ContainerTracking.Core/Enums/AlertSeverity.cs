namespace ContainerTracking.Core.Enums;

public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum AlertTriggerType
{
    StatusChange = 0,
    EtaChanged = 1,
    DelayDetected = 2,
    PortArrival = 3,
    PortDeparture = 4,
    VesselDiverted = 5,
    ManualAlert = 6,
    ProviderError = 7,
    TierLimitApproaching = 8
}

public enum NotificationChannel
{
    InApp = 0,
    Email = 1,
    Webhook = 2,
    Sms = 3
}

public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Read = 3
}

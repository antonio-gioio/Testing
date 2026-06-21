namespace ContainerTracking.Core.Enums;

public enum TrackingEventType
{
    BookingCreated = 0,
    BookingConfirmed = 1,
    ContainerGateIn = 2,
    ContainerLoaded = 3,
    VesselDeparted = 4,
    TransshipmentArrived = 5,
    TransshipmentDeparted = 6,
    VesselArrived = 7,
    ContainerDischarged = 8,
    ContainerGateOut = 9,
    ContainerDelivered = 10,
    EmptyReturned = 11,
    PortCallEvent = 12,
    AisPositionUpdate = 13,
    EtaUpdated = 14,
    DelayNotification = 15,
    HoldPlaced = 16,
    HoldReleased = 17,
    CustomsCleared = 18,
    ManualUpdate = 19,
    ImportedEvent = 20
}

public enum TrackingProviderType
{
    Ais = 0,
    Carrier = 1,
    PortApi = 2,
    Manual = 3,
    CsvImport = 4,
    Webhook = 5,
    OpenData = 6
}

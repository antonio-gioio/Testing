namespace ContainerTracking.Core.Enums;

public enum ContainerStatus
{
    Unknown = 0,
    BookingConfirmed = 1,
    GateIn = 2,
    Loaded = 3,
    Departed = 4,
    InTransit = 5,
    TransshipmentArrived = 6,
    TransshipmentDeparted = 7,
    Arrived = 8,
    Discharged = 9,
    GateOut = 10,
    Delivered = 11,
    Empty = 12,
    Delayed = 13,
    OnHold = 14,
    AvailableForPickup = 15  // All holds released, container grounded — trucker can pick up
}

namespace Core.Entities;

public enum BookingStatus
{
    PendingPayment,
    Authorized,
    Failed,
    Captured,
    Cancelled,
    CancelledWithFee,
    PartiallyRefunded,
    Refunded
}

namespace API.DTOs;

public class BookingDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string Model { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? StripeAccountId { get; set; }
    public required string PayerEmail { get; set; }
    public required string TravellerName { get; set; }
    public required string Currency { get; set; }
    public long QuoteAmount { get; set; }
    public long AuthorizedAmount { get; set; }
    public long CapturedAmount { get; set; }
    public long RefundedAmount { get; set; }
    public long ApplicationFee { get; set; }
    public long ApplicationFeeRefunded { get; set; }
    public int CommissionBps { get; set; }
    public long StripeFee { get; set; }
    public DateTime? SettlementDate { get; set; }
    public long TotalPaid { get; set; }      // captured - refunded
    public long NetCommission { get; set; }  // commission kept after refunds
    public long PlatformNet { get; set; }    // after Stripe fee
    public long SupplierNet { get; set; }
    public required string PaymentIntentId { get; set; }
    public required string Status { get; set; }
    public string? LastError { get; set; }
    public required List<string> Timeline { get; set; }
}

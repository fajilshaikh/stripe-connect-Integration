namespace Core.Entities;

public class Booking : BaseEntity
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public CommercialModel Model { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }  // agent model: the supplier acting as settlement merchant
    public string? StripeAccountId { get; init; }  // destination supplier account; null = Seller model
    public required string PayerEmail { get; init; }  // payer is not necessarily the traveller
    public required string TravellerName { get; init; }
    public required string Currency { get; init; }
    public long QuoteAmount { get; init; }
    public long AuthorizedAmount { get; init; }
    public int CommissionBps { get; init; }
    public long CapturedAmount { get; set; }
    public long RefundedAmount { get; set; }
    public long ApplicationFee { get; set; }
    public long ApplicationFeeRefunded { get; set; }  // commission returned on refunds (refund_application_fee)
    public long StripeFee { get; set; }  // processing fee from the charge's balance transaction (paid by the platform)
    public DateTime? SettlementDate { get; set; }  // when captured funds become available in the Stripe balance
    public string PaymentIntentId { get; set; } = "";
    public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;
    public string? LastError { get; set; }
    public List<string> Timeline { get; set; } = [];

    public bool IsAgent => Model == CommercialModel.Agent;
    public long FeeFor(long amount) => IsAgent ? amount * CommissionBps / 10_000 : 0;

    /// What the traveller (payer) has actually paid: captured minus refunded.
    public long TotalPaid => CapturedAmount - RefundedAmount;

    /// Commission kept after refunds (Agent); 0 for Seller.
    public long NetCommission => ApplicationFee - ApplicationFeeRefunded;

    /// Agent: commission kept minus the Stripe fee. Seller: everything paid minus the Stripe fee.
    /// Stripe does not return its processing fee on refunds, so this can go negative after a full refund.
    public long PlatformNet => (IsAgent ? NetCommission : TotalPaid) - StripeFee;

    /// Agent: what stays with the supplier after commission and reversed transfers. Seller: 0.
    public long SupplierNet => IsAgent ? TotalPaid - NetCommission : 0;

    /// Minor units (pence) to "54.00 GBP".
    public string M(long minor) => $"{minor / 100m:0.00} {Currency.ToUpperInvariant()}";

    /// Short line for the UI timeline.
    public void AddToTimeline(string message) => Timeline.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} {message}");
}

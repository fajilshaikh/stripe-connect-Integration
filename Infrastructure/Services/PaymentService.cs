using Core.Entities;
using Core.Interfaces;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Stripe;

namespace Infrastructure.Services;

/// Booking payment lifecycle: authorize (quote + buffer) -> capture final price | cancel (release or fee) -> refund.
/// Booking code only ever sees this narrow interface; Stripe objects are referenced by id.
public class PaymentService(IStripeClient client, IBookingRepository bookings, ISupplierRepository suppliers,
    IPaymentFlowLog flowLog, IOptions<StripeSettings> stripe) : IPaymentService
{
    private readonly StripeSettings s = stripe.Value;
    private PaymentIntentService Intents => new(client);

    public async Task<(Booking Booking, string ClientSecret)> CreateBookingAsync(CommercialModel model, Guid? supplierId,
        string payerEmail, string travellerName, long quoteAmount, int? commissionBps, CancellationToken ct)
    {
        if (quoteAmount <= 0) throw new InvalidOperationException("Quote amount must be positive.");
        Supplier? supplier = null;
        if (model == CommercialModel.Agent)
        {
            supplier = await suppliers.GetByIdAsync(supplierId ?? Guid.Empty, ct) ?? throw new InvalidOperationException("Supplier not found.");
            if (!supplier.IsPayable) throw new InvalidOperationException($"Supplier '{supplier.Name}' is not payable yet (status: {supplier.Status}).");
        }

        var booking = new Booking
        {
            Model = model,
            SupplierId = supplier?.Id,
            SupplierName = supplier?.Name,
            StripeAccountId = supplier?.StripeAccountId,
            PayerEmail = payerEmail,
            TravellerName = travellerName,
            Currency = s.Currency,
            QuoteAmount = quoteAmount,
            AuthorizedAmount = quoteAmount + quoteAmount * s.AuthBufferPercent / 100,
            CommissionBps = model == CommercialModel.Agent ? commissionBps ?? s.DefaultCommissionBps : 0,
        };

        var intent = await Intents.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = booking.AuthorizedAmount,
            Currency = booking.Currency,
            CaptureMethod = "manual",
            PaymentMethodTypes = ["card"],
            ReceiptEmail = booking.PayerEmail,
            Description = $"Jyrney booking {booking.Id}",
            // Agent model: destination charge. The supplier is the settlement merchant (on_behalf_of), funds go to
            // their account (transfer_data), and Jyrney keeps its commission as the application fee (re-set on capture).
            OnBehalfOf = booking.StripeAccountId,
            TransferData = booking.IsAgent ? new PaymentIntentTransferDataOptions { Destination = booking.StripeAccountId } : null,
            ApplicationFeeAmount = booking.IsAgent ? booking.FeeFor(booking.AuthorizedAmount) : null,
            Metadata = new() { ["booking_id"] = booking.Id.ToString(), ["model"] = booking.Model.ToString() },
        }, Options(booking, "authorize"), ct);

        booking.PaymentIntentId = intent.Id;
        var b = booking;
        Log(b, $"Authorization requested: {b.M(b.AuthorizedAmount)} to be held on the payer's card",
            $"""
              Payer: {b.PayerEmail} | Traveller: {b.TravellerName}
              AuthorizedAmount = Quote + Buffer
                               = {b.M(b.QuoteAmount)} + {s.AuthBufferPercent}% of {b.M(b.QuoteAmount)} ({b.M(b.AuthorizedAmount - b.QuoteAmount)})
                               = {b.M(b.AuthorizedAmount)}
              Why a buffer: the final fare can differ from the quote; we can capture up to the held amount, never more.
              Charge: {(b.IsAgent ? $"destination charge on the platform, on_behalf_of + transfer_data.destination = {b.StripeAccountId}" : "platform charge, Jyrney is merchant of record")}
              PaymentIntent {intent.Id}, capture_method = manual (money is only held, not taken yet)
            {(b.IsAgent ? $"  Provisional commission on held amount: {b.M(b.FeeFor(b.AuthorizedAmount))} (recalculated on the final price at capture)" : "")}
            """.TrimEnd());
        await bookings.SaveAsync(booking, ct);
        return (booking, intent.ClientSecret);
    }

    public async Task<Booking> CompleteAsync(Guid id, long finalAmount, CancellationToken ct)
    {
        var b = await RequireAsync(id, ct, BookingStatus.Authorized);
        if (finalAmount > b.AuthorizedAmount)
            throw new InvalidOperationException($"Final price {finalAmount} exceeds authorization {b.AuthorizedAmount}: needs an off-session top-up (not in this demo).");

        var fee = b.FeeFor(finalAmount);
        var captured = await Intents.CaptureAsync(b.PaymentIntentId, new PaymentIntentCaptureOptions
        {
            AmountToCapture = finalAmount,
            ApplicationFeeAmount = b.IsAgent ? fee : null,
            Expand = [ChargeBalanceTransaction],
        }, Options(b, $"capture:{finalAmount}"), ct);

        (b.CapturedAmount, b.ApplicationFee, b.Status) = (finalAmount, fee, BookingStatus.Captured);
        ApplySettlement(b, captured);
        Log(b, $"Captured {b.M(finalAmount)}, commission {b.M(fee)}; {b.M(b.AuthorizedAmount - finalAmount)} released",
            $"""
              Captured {b.M(finalAmount)} of the {b.M(b.AuthorizedAmount)} hold; the remaining {b.M(b.AuthorizedAmount - finalAmount)} is released to the payer's card.
            {Split(b, finalAmount)}
            {Settlement(b)}
            """);
        await bookings.SaveAsync(b, ct);
        return b;
    }

    /// Before capture: fee 0 releases the authorization, fee > 0 partially captures it.
    /// After capture: refunds everything except the cancellation fee.
    public async Task<Booking> CancelAsync(Guid id, long cancellationFee, CancellationToken ct)
    {
        var b = await bookings.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Booking not found.");
        if (b.Status == BookingStatus.Captured) return await RefundAsync(id, b.CapturedAmount - cancellationFee, "cancellation", ct);

        b = await RequireAsync(id, ct, BookingStatus.Authorized, BookingStatus.PendingPayment);
        if (cancellationFee <= 0)
        {
            await Intents.CancelAsync(b.PaymentIntentId, new PaymentIntentCancelOptions { CancellationReason = "requested_by_customer" }, Options(b, "cancel"), ct);
            b.Status = BookingStatus.Cancelled;
            Log(b, "Cancelled free of charge, authorization released",
                $"  PaymentIntent cancelled: the full hold of {b.M(b.AuthorizedAmount)} is released; the payer is not charged and no commission is earned.");
            await bookings.SaveAsync(b, ct);
            return b;
        }

        var completed = await CompleteAsync(id, cancellationFee, ct);
        completed.Status = BookingStatus.CancelledWithFee;
        Log(completed, $"Booking cancelled with a {completed.M(cancellationFee)} fee",
            "  The fee was captured from the hold (see the capture entry above); commission applies to the fee only.");
        await bookings.SaveAsync(completed, ct);
        return completed;
    }

    public Task<Booking> RefundAsync(Guid id, long amount, CancellationToken ct) => RefundAsync(id, amount, "refund", ct);

    /// Agent model: pull the funds back from the supplier (reverse_transfer) and reverse our commission proportionally.
    private async Task<Booking> RefundAsync(Guid id, long amount, string reason, CancellationToken ct)
    {
        var b = await RequireAsync(id, ct, BookingStatus.Captured, BookingStatus.CancelledWithFee, BookingStatus.PartiallyRefunded);
        if (amount <= 0 || amount > b.CapturedAmount - b.RefundedAmount) throw new InvalidOperationException("Invalid refund amount.");

        await new RefundService(client).CreateAsync(new RefundCreateOptions
        {
            PaymentIntent = b.PaymentIntentId,
            Amount = amount,
            RefundApplicationFee = b.IsAgent ? true : null,
            ReverseTransfer = b.IsAgent ? true : null,
            Metadata = new() { ["booking_id"] = b.Id.ToString(), ["reason"] = reason },
        }, Options(b, $"refund:{b.RefundedAmount}:{amount}"), ct);

        b.RefundedAmount += amount;
        b.Status = b.RefundedAmount == b.CapturedAmount ? BookingStatus.Refunded : BookingStatus.PartiallyRefunded;
        var feeBack = b.ApplicationFee * amount / b.CapturedAmount; // Stripe reverses the fee proportionally
        b.ApplicationFeeRefunded = Math.Min(b.ApplicationFee, b.ApplicationFeeRefunded + feeBack);
        Log(b, $"Refunded {b.M(amount)} ({reason})",
            $"""
              Refund {b.M(amount)} of {b.M(b.CapturedAmount)} captured; refunded so far {b.M(b.RefundedAmount)}, still kept {b.M(b.CapturedAmount - b.RefundedAmount)}.
            {(b.IsAgent
                ? $"""
                     Commission returned (refund_application_fee) = {b.M(b.ApplicationFee)} x {b.M(amount)} / {b.M(b.CapturedAmount)} ~ {b.M(feeBack)} (from Jyrney)
                     Pulled back from the supplier (reverse_transfer) ~ {b.M(amount - feeBack)} (from {b.StripeAccountId})
                   """
                : "  Seller model: the full refund comes out of Jyrney's balance.")}
            """);
        await bookings.SaveAsync(b, ct);
        return b;
    }

    /// Re-reads the PaymentIntent from Stripe, so duplicate / out-of-order webhooks are harmless.
    public async Task<Booking?> SyncAsync(string paymentIntentId, CancellationToken ct)
    {
        var b = await bookings.GetByPaymentIntentIdAsync(paymentIntentId, ct);
        if (b is null) return null;
        var pi = await Intents.GetAsync(paymentIntentId, new PaymentIntentGetOptions { Expand = [ChargeBalanceTransaction] }, cancellationToken: ct);

        var next = pi.Status switch
        {
            "requires_capture" when b.Status is BookingStatus.PendingPayment or BookingStatus.Failed => BookingStatus.Authorized,
            "requires_payment_method" when pi.LastPaymentError is not null && b.Status == BookingStatus.PendingPayment => BookingStatus.Failed,
            "succeeded" when b.Status == BookingStatus.Authorized => BookingStatus.Captured,     // captured outside the app (dashboard)
            "canceled" when b.Status is BookingStatus.Authorized or BookingStatus.PendingPayment => BookingStatus.Cancelled, // incl. expired auth
            _ => b.Status,
        };
        if (next == b.Status)
        {
            // The balance transaction can lag the capture slightly; pick up the fee and settlement date once it exists.
            if (b.CapturedAmount > 0 && b.SettlementDate is null && ApplySettlement(b, pi)) await bookings.SaveAsync(b, ct);
            return b;
        }

        b.Status = next;
        if (next == BookingStatus.Captured)
        {
            (b.CapturedAmount, b.ApplicationFee) = (pi.AmountReceived, pi.ApplicationFeeAmount ?? 0);
            ApplySettlement(b, pi);
        }
        if (next == BookingStatus.Failed) b.LastError = Explain(pi.LastPaymentError);
        Log(b, $"Stripe reports {pi.Status} -> {next}{(b.LastError is null ? "" : $": {b.LastError}")}");
        await bookings.SaveAsync(b, ct);
        return b;
    }

    /// Short line for the UI timeline; optional detail (the calculation) goes to the flow log file only.
    private void Log(Booking b, string message, string? detail = null)
    {
        b.AddToTimeline(message);
        flowLog.Write($"Booking {b.Id.ToString()[..8]} {b.Model}", detail is null ? message : $"{message}\n{detail}");
    }

    // The platform's balance transaction for the charge holds the real Stripe fee and the date funds become available.
    // Both models charge on the platform (destination charges for Agent), so the platform pays this fee.
    private const string ChargeBalanceTransaction = "latest_charge.balance_transaction";

    private static bool ApplySettlement(Booking b, PaymentIntent pi)
    {
        if (pi.LatestCharge?.BalanceTransaction is not { } txn) return false;
        (b.StripeFee, b.SettlementDate) = (txn.Fee, txn.AvailableOn);
        return true;
    }

    private static string Settlement(Booking b) => b.SettlementDate is null
        ? "  Stripe fee and settlement date not available yet (balance transaction pending); Sync picks them up later."
        : $"""
             Stripe fee (balance transaction) = {b.M(b.StripeFee)}, paid by Jyrney; funds available on {b.SettlementDate:yyyy-MM-dd}
             Net: Jyrney {b.M(b.PlatformNet)}{(b.IsAgent ? $" | supplier {b.M(b.SupplierNet)}" : "")}
           """;

    // Who gets what for a captured amount.
    private static string Split(Booking b, long amount) => b.IsAgent
        ? $"""
             Commission (application fee) = {b.M(amount)} x {b.CommissionBps / 100m:0.##}% ({b.CommissionBps} bps) = {b.M(b.FeeFor(amount))} -> Jyrney
             Supplier share = {b.M(amount)} - {b.M(b.FeeFor(amount))} = {b.M(amount - b.FeeFor(amount))} -> {b.StripeAccountId}
             Stripe processing fee is paid by the platform out of the commission (destination charge).
           """
        : $"  Seller model: Jyrney keeps the full {b.M(amount)} (no transfer, no commission); Stripe fee comes out of it.";

    private async Task<Booking> RequireAsync(Guid id, CancellationToken ct, params BookingStatus[] allowed)
    {
        var b = await bookings.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Booking not found.");
        if (!allowed.Contains(b.Status)) throw new InvalidOperationException($"Booking is {b.Status}; expected {string.Join(" or ", allowed)}.");
        return b;
    }

    // Both models charge on the platform account (destination charges for Agent), so no Stripe-Account header.
    private static RequestOptions Options(Booking b, string step) => new() { IdempotencyKey = $"booking:{b.Id}:{step}" };

    // Plain-language decline reasons for ops.
    private static string Explain(StripeError? e) => e?.DeclineCode switch
    {
        "insufficient_funds" => "Card has insufficient funds. Ask the payer for another card.",
        "expired_card" => "Card has expired. Request a new card.",
        "authentication_required" => "Bank requires 3DS authentication. Send the payer an authentication link.",
        "do_not_honor" or "generic_decline" => "Declined by the bank with no reason given. Payer should contact their bank.",
        _ => e?.Message ?? "Payment failed.",
    };
}

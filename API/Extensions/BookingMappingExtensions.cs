using API.DTOs;
using Core.Entities;

namespace API.Extensions;

public static class BookingMappingExtensions
{
    public static BookingDto ToDto(this Booking booking)
    {
        return new BookingDto
        {
            Id = booking.Id,
            CreatedAt = booking.CreatedAt,
            Model = booking.Model.ToString(),
            SupplierId = booking.SupplierId,
            SupplierName = booking.SupplierName,
            StripeAccountId = booking.StripeAccountId,
            PayerEmail = booking.PayerEmail,
            TravellerName = booking.TravellerName,
            Currency = booking.Currency,
            QuoteAmount = booking.QuoteAmount,
            AuthorizedAmount = booking.AuthorizedAmount,
            CapturedAmount = booking.CapturedAmount,
            RefundedAmount = booking.RefundedAmount,
            ApplicationFee = booking.ApplicationFee,
            ApplicationFeeRefunded = booking.ApplicationFeeRefunded,
            CommissionBps = booking.CommissionBps,
            StripeFee = booking.StripeFee,
            SettlementDate = booking.SettlementDate,
            TotalPaid = booking.TotalPaid,
            NetCommission = booking.NetCommission,
            PlatformNet = booking.PlatformNet,
            SupplierNet = booking.SupplierNet,
            PaymentIntentId = booking.PaymentIntentId,
            Status = booking.Status.ToString(),
            LastError = booking.LastError,
            Timeline = [.. booking.Timeline]
        };
    }
}

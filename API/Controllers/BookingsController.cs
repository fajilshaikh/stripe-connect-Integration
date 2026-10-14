using API.DTOs;
using API.Extensions;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// Booking payment lifecycle. Amounts are in minor units (pence).
public class BookingsController(IBookingRepository bookings, IPaymentService payments) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetBookings(CancellationToken ct)
    {
        return Ok((await bookings.ListAllAsync(ct)).Select(b => b.ToDto()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CreateBookingResponseDto>> CreateBooking(CreateBookingDto bookingDto, CancellationToken ct)
    {
        var (booking, clientSecret) = await payments.CreateBookingAsync(bookingDto.Model, bookingDto.SupplierId,
            bookingDto.PayerEmail, bookingDto.TravellerName, bookingDto.QuoteAmount, bookingDto.CommissionBps, ct);

        return new CreateBookingResponseDto { Booking = booking.ToDto(), ClientSecret = clientSecret };
    }

    [HttpPost("{id:guid}/sync")]
    public async Task<ActionResult<BookingDto>> SyncBooking(Guid id, CancellationToken ct)
    {
        var booking = await bookings.GetByIdAsync(id, ct);
        if (booking == null) return NotFoundError("Booking not found.");

        var synced = await payments.SyncAsync(booking.PaymentIntentId, ct);
        if (synced == null) return NotFoundError("Booking not found.");

        return synced.ToDto();
    }

    /// Capture the final price from the supplier (up to the authorized amount).
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<BookingDto>> CompleteBooking(Guid id, AmountDto amountDto, CancellationToken ct)
    {
        return (await payments.CompleteAsync(id, amountDto.Amount, ct)).ToDto();
    }

    /// Amount is the cancellation fee; 0 cancels free of charge.
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<BookingDto>> CancelBooking(Guid id, AmountDto amountDto, CancellationToken ct)
    {
        return (await payments.CancelAsync(id, amountDto.Amount, ct)).ToDto();
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<BookingDto>> RefundBooking(Guid id, AmountDto amountDto, CancellationToken ct)
    {
        return (await payments.RefundAsync(id, amountDto.Amount, ct)).ToDto();
    }
}

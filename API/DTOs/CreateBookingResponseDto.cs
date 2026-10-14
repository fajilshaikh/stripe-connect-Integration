namespace API.DTOs;

public class CreateBookingResponseDto
{
    public required BookingDto Booking { get; set; }
    public required string ClientSecret { get; set; }  // for the Payment Element in the browser
}

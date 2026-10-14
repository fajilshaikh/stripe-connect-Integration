using System.ComponentModel.DataAnnotations;
using Core.Entities;

namespace API.DTOs;

public class CreateBookingDto
{
    public CommercialModel Model { get; set; }

    public Guid? SupplierId { get; set; }  // required for the Agent model

    [Required]
    [EmailAddress]
    public string PayerEmail { get; set; } = string.Empty;

    [Required]
    public string TravellerName { get; set; } = string.Empty;

    public long QuoteAmount { get; set; }  // minor units (pence)

    public int? CommissionBps { get; set; }  // defaults to Stripe:DefaultCommissionBps
}

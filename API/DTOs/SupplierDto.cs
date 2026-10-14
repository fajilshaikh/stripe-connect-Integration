namespace API.DTOs;

public class SupplierDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string StripeAccountId { get; set; }
    public required string Status { get; set; }
    public bool ChargesEnabled { get; set; }
    public bool PayoutsEnabled { get; set; }
    public string? DisabledReason { get; set; }
    public required List<string> CurrentlyDue { get; set; }
    public bool IsPayable { get; set; }
}

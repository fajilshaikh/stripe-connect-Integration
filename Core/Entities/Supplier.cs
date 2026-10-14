namespace Core.Entities;

public class Supplier : BaseEntity
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string StripeAccountId { get; set; } = "";
    public OnboardingStatus Status { get; set; } = OnboardingStatus.Invited;
    public bool ChargesEnabled { get; set; }
    public bool PayoutsEnabled { get; set; }
    public string? DisabledReason { get; set; }
    public List<string> CurrentlyDue { get; set; } = [];
    public bool IsPayable => Status == OnboardingStatus.Enabled;
}

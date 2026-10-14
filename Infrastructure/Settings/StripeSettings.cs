namespace Infrastructure.Settings;

/// Bound from the "Stripe" config section (user-secrets locally, Key Vault in Azure).
public class StripeSettings
{
    public string SecretKey { get; set; } = "";
    public string PublishableKey { get; set; } = "";
    public string PlatformWebhookSecret { get; set; } = "";
    public string ConnectWebhookSecret { get; set; } = "";
    public string Country { get; set; } = "GB";
    public string Currency { get; set; } = "gbp";
    public int AuthBufferPercent { get; set; } = 20;
    public int DefaultCommissionBps { get; set; } = 1500;
}

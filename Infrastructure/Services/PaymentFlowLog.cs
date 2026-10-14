using Core.Interfaces;

namespace Infrastructure.Services;

/// Appends the payment flow audit trail to logs/payment-flow.txt, explaining each step and calculation.
public class PaymentFlowLog : IPaymentFlowLog
{
    private readonly object _gate = new();
    public string FilePath { get; set; } = Path.Combine("logs", "payment-flow.txt");

    public void Write(string subject, string message)
    {
        var stamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{subject}] ";
        var text = stamp + message.ReplaceLineEndings(Environment.NewLine + new string(' ', stamp.Length)) + Environment.NewLine;
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
            File.AppendAllText(FilePath, text);
        }
    }
}

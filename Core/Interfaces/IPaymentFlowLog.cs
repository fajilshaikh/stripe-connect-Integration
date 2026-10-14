namespace Core.Interfaces;

/// Human-readable audit trail of the payment flow (authorization buffer, commission, supplier share, refunds).
public interface IPaymentFlowLog
{
    void Write(string subject, string message);
}

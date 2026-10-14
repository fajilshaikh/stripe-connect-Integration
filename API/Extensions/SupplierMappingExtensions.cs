using API.DTOs;
using Core.Entities;

namespace API.Extensions;

public static class SupplierMappingExtensions
{
    public static SupplierDto ToDto(this Supplier supplier)
    {
        return new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            Email = supplier.Email,
            StripeAccountId = supplier.StripeAccountId,
            Status = supplier.Status.ToString(),
            ChargesEnabled = supplier.ChargesEnabled,
            PayoutsEnabled = supplier.PayoutsEnabled,
            DisabledReason = supplier.DisabledReason,
            CurrentlyDue = supplier.CurrentlyDue,
            IsPayable = supplier.IsPayable
        };
    }
}

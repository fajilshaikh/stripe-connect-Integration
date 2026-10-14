using API.DTOs;
using API.Extensions;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// Platform-led Connect onboarding: ops create the supplier's Express account and send them the hosted onboarding link.
public class SuppliersController(ISupplierRepository suppliers, ISupplierOnboardingService onboarding) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> GetSuppliers(CancellationToken ct)
    {
        return Ok((await suppliers.ListAllAsync(ct)).Select(s => s.ToDto()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SupplierDto>> CreateSupplier(CreateSupplierDto supplierDto, CancellationToken ct)
    {
        var supplier = await onboarding.CreateAsync(supplierDto.Name, supplierDto.Email, ct);
        return supplier.ToDto();
    }

    [HttpPost("import")]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> ImportSuppliers(CancellationToken ct)
    {
        await onboarding.ImportFromStripeAsync(ct);
        return await GetSuppliers(ct);
    }

    [HttpPost("{id:guid}/onboarding-link")]
    public async Task<ActionResult<OnboardingLinkDto>> CreateOnboardingLink(Guid id, CancellationToken ct)
    {
        var supplier = await suppliers.GetByIdAsync(id, ct);
        if (supplier == null) return NotFoundError("Supplier not found.");

        return new OnboardingLinkDto { Url = await onboarding.CreateOnboardingLinkAsync(supplier, ct) };
    }

    /// Stripe sends the supplier here when their onboarding link has expired: mint a fresh one and redirect.
    [HttpGet("{id:guid}/onboarding-refresh")]
    public async Task<IActionResult> RefreshOnboardingLink(Guid id, CancellationToken ct)
    {
        var supplier = await suppliers.GetByIdAsync(id, ct);
        if (supplier == null) return NotFoundError("Supplier not found.");

        return Redirect(await onboarding.CreateOnboardingLinkAsync(supplier, ct));
    }

    [HttpPost("{id:guid}/sync")]
    public async Task<ActionResult<SupplierDto>> SyncSupplier(Guid id, CancellationToken ct)
    {
        var supplier = await suppliers.GetByIdAsync(id, ct);
        if (supplier == null) return NotFoundError("Supplier not found.");

        var synced = await onboarding.SyncAsync(supplier.StripeAccountId, ct);
        if (synced == null) return NotFoundError("Supplier not found.");

        return synced.ToDto();
    }
}

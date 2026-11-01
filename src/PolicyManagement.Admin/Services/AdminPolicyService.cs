using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PolicyManagement.Admin.Infrastructure;
using PolicyManagement.Contracts;
using PolicyManagement.Data;

namespace PolicyManagement.Admin.Services;

public sealed class AdminPolicyService(
    IDbContextFactory<PolicyDbContext> contextFactory,
    TenantContext tenantContext,
    PolicySocketHub socketHub)
{
    public async Task<IReadOnlyList<TenantOption>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tenants.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new TenantOption(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenantSelected();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Policies.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new PolicyDto(
                x.Id, x.Name, x.Description, x.IsEnabled, x.Severity,
                x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<SaveResult> CreateAsync(PolicyEditModel model, CancellationToken cancellationToken = default)
    {
        EnsureTenantSelected();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedName = model.Name.Trim();
        if (await db.Policies.AnyAsync(x => x.Name == normalizedName, cancellationToken))
        {
            return SaveResult.Failure("A policy with this name already exists for the selected tenant.");
        }

        var now = DateTime.UtcNow;
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = Clean(model.Description),
            IsEnabled = model.IsEnabled,
            Severity = model.Severity,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Policies.Add(policy);
        return await SaveAndBroadcastAsync(db, policy, PolicyEventTypes.Created, cancellationToken);
    }

    public async Task<SaveResult> UpdateAsync(PolicyEditModel model, CancellationToken cancellationToken = default)
    {
        EnsureTenantSelected();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var policy = await db.Policies.SingleOrDefaultAsync(x => x.Id == model.Id, cancellationToken);
        if (policy is null)
        {
            return SaveResult.Failure("The policy no longer exists in the selected tenant.");
        }

        var normalizedName = model.Name.Trim();
        if (await db.Policies.AnyAsync(
                x => x.Id != model.Id && x.Name == normalizedName,
                cancellationToken))
        {
            return SaveResult.Failure("A policy with this name already exists for the selected tenant.");
        }

        policy.Name = normalizedName;
        policy.Description = Clean(model.Description);
        policy.IsEnabled = model.IsEnabled;
        policy.Severity = model.Severity;
        policy.UpdatedAtUtc = DateTime.UtcNow;
        return await SaveAndBroadcastAsync(db, policy, PolicyEventTypes.Updated, cancellationToken);
    }

    public async Task<SaveResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureTenantSelected();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var policy = await db.Policies.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (policy is null)
        {
            return SaveResult.Failure("The policy no longer exists in the selected tenant.");
        }

        db.Policies.Remove(policy);
        return await SaveAndBroadcastAsync(db, policy, PolicyEventTypes.Deleted, cancellationToken);
    }

    private async Task<SaveResult> SaveAndBroadcastAsync(
        PolicyDbContext db,
        Policy policy,
        string type,
        CancellationToken cancellationToken)
    {
        var result = await SaveAsync(db, cancellationToken);
        if (!result.Succeeded)
        {
            return result;
        }

        var dto = new PolicyDto(
            policy.Id, policy.Name, policy.Description, policy.IsEnabled, policy.Severity,
            policy.CreatedAtUtc, policy.UpdatedAtUtc);
        var message = new PolicyEventMessage(
            Guid.NewGuid(), type, tenantContext.TenantId!.Value, policy.Id, dto, DateTime.UtcNow);
        await socketHub.BroadcastAsync(message, cancellationToken);
        return result;
    }

    private static async Task<SaveResult> SaveAsync(
        PolicyDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return SaveResult.Success();
        }
        catch (DbUpdateException)
        {
            return SaveResult.Failure(
                "The change could not be saved. Check that the policy name is unique and try again.");
        }
    }

    private void EnsureTenantSelected()
    {
        if (!tenantContext.TenantId.HasValue)
        {
            throw new InvalidOperationException("Select a tenant before accessing policies.");
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class PolicyEditModel
{
    public Guid Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;
    public PolicySeverity Severity { get; set; } = PolicySeverity.Medium;
}

public sealed record TenantOption(Guid Id, string Name);

public sealed record SaveResult(bool Succeeded, string? Error)
{
    public static SaveResult Success() => new(true, null);
    public static SaveResult Failure(string error) => new(false, error);
}

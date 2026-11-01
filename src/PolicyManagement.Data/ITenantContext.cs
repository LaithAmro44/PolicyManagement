namespace PolicyManagement.Data;

public interface ITenantContext
{
    Guid? TenantId { get; }
}

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        }

        TenantId = tenantId;
    }
}

public static class SeedTenantIds
{
    public static readonly Guid BankA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid BankB = Guid.Parse("22222222-2222-2222-2222-222222222222");
}

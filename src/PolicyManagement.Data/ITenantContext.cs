namespace PolicyManagement.Data;

public interface ITenantContext
{
    Guid? TenantId { get; }
}

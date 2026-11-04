namespace PolicyManagement.Data;

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}

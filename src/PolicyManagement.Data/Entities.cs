using PolicyManagement.Contracts;

namespace PolicyManagement.Data;

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}

public sealed class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Policy> Policies { get; set; } = [];
}

public sealed class Policy : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public PolicySeverity Severity { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Tenant? Tenant { get; set; }
}

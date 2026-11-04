namespace PolicyManagement.Data;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Policy> Policies { get; set; } = [];
}

namespace PolicyManagement.Contracts;

public enum PolicySeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public sealed record PolicyDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsEnabled,
    PolicySeverity Severity,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PolicyEventMessage(
    Guid EventId,
    string Type,
    Guid TenantId,
    Guid? PolicyId,
    PolicyDto? Policy,
    DateTime OccurredAtUtc);

public static class PolicyEventTypes
{
    public const string Connected = "Connected";
    public const string Created = "PolicyCreated";
    public const string Updated = "PolicyUpdated";
    public const string Deleted = "PolicyDeleted";
}

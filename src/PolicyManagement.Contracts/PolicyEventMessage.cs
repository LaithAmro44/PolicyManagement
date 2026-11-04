namespace PolicyManagement.Contracts;

public sealed record PolicyEventMessage(
    Guid EventId,
    string Type,
    Guid TenantId,
    Guid? PolicyId,
    PolicyDto? Policy,
    DateTime OccurredAtUtc);

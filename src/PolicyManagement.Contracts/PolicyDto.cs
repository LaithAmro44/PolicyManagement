namespace PolicyManagement.Contracts;

public sealed record PolicyDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsEnabled,
    PolicySeverity Severity,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

using System.ComponentModel.DataAnnotations;
using PolicyManagement.Contracts;

namespace PolicyManagement.Admin.Services;

public sealed class PolicyEditModel
{
    public Guid Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;
    [EnumDataType(typeof(PolicySeverity))]
    public PolicySeverity Severity { get; set; } = PolicySeverity.Medium;
}

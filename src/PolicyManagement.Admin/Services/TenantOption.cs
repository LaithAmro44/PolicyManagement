using System.ComponentModel.DataAnnotations;
using PolicyManagement.Contracts;

namespace PolicyManagement.Admin.Services;

public sealed record TenantOption(Guid Id, string Name);

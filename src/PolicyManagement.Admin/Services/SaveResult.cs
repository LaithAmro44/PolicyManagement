using System.ComponentModel.DataAnnotations;
using PolicyManagement.Contracts;

namespace PolicyManagement.Admin.Services;

public sealed record SaveResult(bool Succeeded, string? Error)
{
    public static SaveResult Success() => new(true, null);
    public static SaveResult Failure(string error) => new(false, error);
}

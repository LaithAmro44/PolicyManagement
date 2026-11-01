using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolicyManagement.Contracts;
using PolicyManagement.Data;

namespace PolicyManagement.Admin.Controllers;

[ApiController]
[Route("api/policies")]
public sealed class PoliciesController(PolicyDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PolicyDto>>> GetAll(
        [FromQuery] bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = db.Policies.AsNoTracking();
        if (enabledOnly)
        {
            query = query.Where(x => x.IsEnabled);
        }

        return Ok(await query
            .OrderBy(x => x.Name)
            .Select(x => new PolicyDto(
                x.Id, x.Name, x.Description, x.IsEnabled, x.Severity,
                x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PolicyDto>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var policy = await db.Policies.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PolicyDto(
                x.Id, x.Name, x.Description, x.IsEnabled, x.Severity,
                x.CreatedAtUtc, x.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return policy is null
            ? NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Policy not found",
                Detail = "No policy with that id exists for the current tenant."
            })
            : Ok(policy);
    }
}

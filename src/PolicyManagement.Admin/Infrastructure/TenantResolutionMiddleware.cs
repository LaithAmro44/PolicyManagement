using Microsoft.EntityFrameworkCore;
using PolicyManagement.Data;

namespace PolicyManagement.Admin.Infrastructure;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        PolicyDbContext db)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var rawTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (!Guid.TryParse(rawTenantId, out var tenantId) ||
            !await db.Tenants.AsNoTracking().AnyAsync(x => x.Id == tenantId, context.RequestAborted))
        {
            await Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid tenant",
                detail: "Send a known tenant id in the X-Tenant-Id header.")
                .ExecuteAsync(context);
            return;
        }

        tenantContext.SetTenant(tenantId);
        await next(context);
    }
}

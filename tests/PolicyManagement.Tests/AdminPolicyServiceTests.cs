using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PolicyManagement.Admin.Infrastructure;
using PolicyManagement.Admin.Services;
using PolicyManagement.Contracts;
using PolicyManagement.Data;

namespace PolicyManagement.Tests;

public sealed class AdminPolicyServiceTests
{
    [Fact]
    public async Task Create_update_delete_preserve_tenant_scope_and_reject_duplicate_names()
    {
        var (service, tenant) = CreateService();
        var model = new PolicyEditModel { Name = "  Screen lock  ", Description = "  Five minutes  " };
        Assert.True((await service.CreateAsync(model)).Succeeded);
        var created = Assert.Single(await service.GetPoliciesAsync());
        Assert.Equal("Screen lock", created.Name);
        Assert.Equal("Five minutes", created.Description);
        Assert.True(created.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-1));
        Assert.False((await service.CreateAsync(model)).Succeeded);

        tenant.SetTenant(SeedTenantIds.BankB);
        Assert.Empty(await service.GetPoliciesAsync());
        Assert.True((await service.CreateAsync(model)).Succeeded);
        Assert.False((await service.UpdateAsync(new PolicyEditModel { Id = created.Id, Name = "Foreign edit" })).Succeeded);
        Assert.False((await service.DeleteAsync(created.Id)).Succeeded);

        tenant.SetTenant(SeedTenantIds.BankA);
        model.Id = created.Id;
        model.Name = "Updated screen lock";
        model.IsEnabled = false;
        Assert.True((await service.UpdateAsync(model)).Succeeded);
        var updated = Assert.Single(await service.GetPoliciesAsync());
        Assert.Equal(model.Name, updated.Name);
        Assert.False(updated.IsEnabled);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.UpdatedAtUtc >= created.UpdatedAtUtc);
        Assert.True((await service.DeleteAsync(created.Id)).Succeeded);
        Assert.Empty(await service.GetPoliciesAsync());
    }

    [Theory]
    [InlineData("   ", PolicySeverity.Low)]
    [InlineData("Valid name", (PolicySeverity)99)]
    public async Task Invalid_input_is_rejected_before_saving(string name, PolicySeverity severity)
    {
        var (service, _) = CreateService();
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new PolicyEditModel { Name = name, Severity = severity }));
        Assert.Empty(await service.GetPoliciesAsync());
    }

    private static (AdminPolicyService Service, TenantContext Tenant) CreateService()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(SeedTenantIds.BankA);
        var options = new DbContextOptionsBuilder<PolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return (new AdminPolicyService(new TestContextFactory(options, tenant), tenant, new PolicySocketHub()), tenant);
    }

    private sealed class TestContextFactory(DbContextOptions<PolicyDbContext> options, TenantContext tenant)
        : IDbContextFactory<PolicyDbContext>
    {
        public PolicyDbContext CreateDbContext() => new(options, tenant);
    }
}

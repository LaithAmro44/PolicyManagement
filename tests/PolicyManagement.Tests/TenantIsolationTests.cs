using Microsoft.EntityFrameworkCore;
using PolicyManagement.Contracts;
using PolicyManagement.Data;

namespace PolicyManagement.Tests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task Global_filter_hides_another_tenants_policy_even_by_id()
    {
        var options = CreateOptions();
        await CreateDatabaseAsync(options);

        await using var bankB = CreateContext(options, SeedTenantIds.BankB);
        var bankAPolicyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

        var policy = await bankB.Policies.SingleOrDefaultAsync(x => x.Id == bankAPolicyId);

        Assert.Null(policy);
    }

    [Fact]
    public async Task Added_rows_get_the_server_tenant_not_a_client_supplied_tenant()
    {
        var options = CreateOptions();
        await CreateDatabaseAsync(options);
        var id = Guid.NewGuid();

        await using (var bankA = CreateContext(options, SeedTenantIds.BankA))
        {
            var policy = new Policy
            {
                Id = id,
                TenantId = SeedTenantIds.BankB,
                Name = $"Test {id}",
                IsEnabled = true,
                Severity = PolicySeverity.Medium
            };
            bankA.Policies.Add(policy);
            await bankA.SaveChangesAsync();
            Assert.Equal(SeedTenantIds.BankA, policy.TenantId);
        }

        await using (var bankB = CreateContext(options, SeedTenantIds.BankB))
        {
            Assert.Null(await bankB.Policies.SingleOrDefaultAsync(x => x.Id == id));
        }
    }

    [Fact]
    public async Task Tenant_id_cannot_be_changed_on_an_existing_row()
    {
        var options = CreateOptions();
        await CreateDatabaseAsync(options);
        var id = Guid.NewGuid();

        await using (var bankA = CreateContext(options, SeedTenantIds.BankA))
        {
            var policy = new Policy
            {
                Id = id,
                Name = $"Immutable tenant {id}",
                IsEnabled = true,
                Severity = PolicySeverity.High
            };
            bankA.Policies.Add(policy);
            await bankA.SaveChangesAsync();

            policy.TenantId = SeedTenantIds.BankB;
            policy.Description = "Attempted tenant move";
            await bankA.SaveChangesAsync();

            Assert.Equal(SeedTenantIds.BankA, policy.TenantId);
        }

        await using var verification = CreateContext(options, SeedTenantIds.BankA);
        Assert.NotNull(await verification.Policies.SingleOrDefaultAsync(x => x.Id == id));
    }

    private static DbContextOptions<PolicyDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<PolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static async Task CreateDatabaseAsync(DbContextOptions<PolicyDbContext> options)
    {
        await using var context = CreateContext(options, SeedTenantIds.BankA);
        await context.Database.EnsureCreatedAsync();
    }

    private static PolicyDbContext CreateContext(
        DbContextOptions<PolicyDbContext> options,
        Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return new PolicyDbContext(options, tenantContext);
    }
}

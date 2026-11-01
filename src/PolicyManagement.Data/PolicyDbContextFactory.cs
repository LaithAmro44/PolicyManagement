using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PolicyManagement.Data;

public sealed class PolicyDbContextFactory : IDesignTimeDbContextFactory<PolicyDbContext>
{
    public PolicyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POLICYMANAGEMENT_CONNECTION_STRING")
            ?? "Server=(localdb)\\mssqllocaldb;Database=PolicyManagement;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<PolicyDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new PolicyDbContext(options, new TenantContext());
    }
}

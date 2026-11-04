using Microsoft.EntityFrameworkCore;
using PolicyManagement.Contracts;

namespace PolicyManagement.Data;

public sealed class PolicyDbContext(
    DbContextOptions<PolicyDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private Guid CurrentTenantId => tenantContext.TenantId ?? Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Policy> Policies => Set<Policy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Policy>(entity =>
        {
            entity.ToTable("Policies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.TenantId).IsConcurrencyToken();
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Policies)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x =>
                x.TenantId == CurrentTenantId);
        });

        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Tenant>().HasData(
            new Tenant { Id = SeedTenantIds.BankA, Name = "Bank A" },
            new Tenant { Id = SeedTenantIds.BankB, Name = "Bank B" });

        modelBuilder.Entity<Policy>().HasData(
            SeedPolicy("aaaaaaaa-0000-0000-0000-000000000001", SeedTenantIds.BankA, "Block USB Storage", PolicySeverity.High, created),
            SeedPolicy("aaaaaaaa-0000-0000-0000-000000000002", SeedTenantIds.BankA, "Require Screen Lock", PolicySeverity.Medium, created),
            SeedPolicy("bbbbbbbb-0000-0000-0000-000000000001", SeedTenantIds.BankB, "Encrypt Local Drives", PolicySeverity.Critical, created),
            SeedPolicy("bbbbbbbb-0000-0000-0000-000000000002", SeedTenantIds.BankB, "Restrict Clipboard", PolicySeverity.Low, created));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyServerRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyServerRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyServerRules()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added)
            {
                var tenantId = tenantContext.TenantId
                    ?? throw new InvalidOperationException("A current tenant is required for tenant-owned writes.");
                entry.Entity.TenantId = tenantId;
            }
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (CurrentTenantId == Guid.Empty ||
                    entry.OriginalValues.GetValue<Guid>(nameof(ITenantOwned.TenantId)) != CurrentTenantId)
                {
                    throw new InvalidOperationException("Cannot change a policy belonging to another tenant.");
                }
                entry.Entity.TenantId = entry.OriginalValues.GetValue<Guid>(nameof(ITenantOwned.TenantId));
                entry.Property(nameof(ITenantOwned.TenantId)).IsModified = false;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Policy>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.Id = entry.Entity.Id == Guid.Empty ? Guid.NewGuid() : entry.Entity.Id;
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(x => x.CreatedAtUtc).IsModified = false;
                entry.Entity.UpdatedAtUtc = now;
            }
        }

    }

    private static Policy SeedPolicy(
        string id,
        Guid tenantId,
        string name,
        PolicySeverity severity,
        DateTime created) => new()
    {
        Id = Guid.Parse(id),
        TenantId = tenantId,
        Name = name,
        Description = $"Seed policy for {name}.",
        IsEnabled = true,
        Severity = severity,
        CreatedAtUtc = created,
        UpdatedAtUtc = created
    };
}

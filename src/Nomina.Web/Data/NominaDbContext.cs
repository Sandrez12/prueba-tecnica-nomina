using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Nomina.Web.Data;

public sealed class NominaDbContext(DbContextOptions<NominaDbContext> options)
    : IdentityDbContext<Person>(options)
{
    public DbSet<Crew> Crews => Set<Crew>();
    public DbSet<TeamActivity> Activities => Set<TeamActivity>();
    public DbSet<WorkEvent> WorkEvents => Set<WorkEvent>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollLine> PayrollLines => Set<PayrollLine>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        // Identity roles and role claims store role assignments and permissions.
        model.Entity<IdentityUserRole<string>>().HasIndex(x => x.UserId).IsUnique();

        model.Entity<Person>(b =>
        {
            b.ToTable(t => t.HasCheckConstraint("CK_Person_Salary", "[MonthlySalary] >= 0"));
            b.Property(x => x.FirstName).HasMaxLength(100);
            b.Property(x => x.LastName).HasMaxLength(100);
            b.Property(x => x.Document).HasMaxLength(40);
            b.Property(x => x.MonthlySalary).HasPrecision(18, 0);
            b.HasIndex(x => x.Document).IsUnique();
            b.HasOne<Crew>().WithMany().HasForeignKey(x => x.CrewId).OnDelete(DeleteBehavior.NoAction);
        });

        model.Entity<Address>(b =>
        {
            b.Property(x => x.Value).HasMaxLength(250);
            b.HasOne<Person>().WithMany(x => x.Addresses).HasForeignKey(x => x.PersonId);
        });

        model.Entity<Phone>(b =>
        {
            b.Property(x => x.Number).HasMaxLength(40);
            b.HasOne<Person>().WithMany(x => x.Phones).HasForeignKey(x => x.PersonId);
        });

        model.Entity<Crew>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(100);
            b.HasOne<Person>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.NoAction);
        });

        model.Entity<TeamActivity>(b =>
        {
            b.ToTable(t => t.HasCheckConstraint("CK_Activity_Status", "[Status] IN (1, 2, 3)"));
            b.Property(x => x.Title).HasMaxLength(200);
            b.HasOne<Person>().WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.NoAction);
        });

        model.Entity<WorkEvent>(b =>
        {
            b.ToTable(t => t.HasCheckConstraint("CK_WorkEvent_Kind", "[Kind] IN (1, 2, 3, 4)"));
            b.HasIndex(x => new { x.PersonId, x.Date }).IsUnique();
            b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.NoAction);
        });

        model.Entity<PayrollPeriod>(b =>
        {
            b.ToTable(t => t.HasCheckConstraint("CK_PayrollPeriod_Date",
                "[Year] BETWEEN 1 AND 9999 AND [Month] BETWEEN 1 AND 12"));
            b.HasIndex(x => new { x.Year, x.Month }).IsUnique();
            b.Property(x => x.RuleVersion).HasMaxLength(40);
            b.Property(x => x.RuleParameters).HasMaxLength(1000);
            b.Property(x => x.Total).HasPrecision(18, 0);
            b.HasOne<Person>().WithMany().HasForeignKey(x => x.ClosedById).OnDelete(DeleteBehavior.NoAction);
        });

        model.Entity<PayrollLine>(b =>
        {
            b.HasKey(x => new { x.PeriodId, x.PersonId });
            b.Property(x => x.PersonName).HasMaxLength(201);
            b.Property(x => x.Document).HasMaxLength(40);
            b.Property(x => x.MonthlySalary).HasPrecision(18, 0);
            b.Property(x => x.AbsenceAdjustment).HasPrecision(18, 0);
            b.Property(x => x.SicknessAdjustment).HasPrecision(18, 0);
            b.Property(x => x.ExtraAdjustment).HasPrecision(18, 0);
            b.Property(x => x.MinimumAdjustment).HasPrecision(18, 0);
            b.Property(x => x.Total).HasPrecision(18, 0);
            b.HasOne<PayrollPeriod>().WithMany(x => x.Lines)
                .HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}

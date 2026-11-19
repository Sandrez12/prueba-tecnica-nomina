using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nomina.Web.Auth;
using Nomina.Web.Data;

namespace Nomina.Web.Payroll;

public sealed class PayrollService(NominaDbContext db)
{
    public async Task<PayrollReport?> FindClosed(int year, int month, string? onlyPersonId, CancellationToken ct)
    {
        var period = await db.PayrollPeriods.AsNoTracking()
            .Include(p => p.Lines.Where(l => onlyPersonId == null || l.PersonId == onlyPersonId))
            .SingleOrDefaultAsync(p => p.Year == year && p.Month == month, ct);
        if (period is null || (onlyPersonId is not null && period.Lines.Count == 0)) return null;
        return new(year, month, period.ClosedAt, period.RuleVersion, period.RuleParameters, null,
            Sorted(period.Lines), onlyPersonId is null ? period.Total : period.Lines.Sum(l => l.Total));
    }

    public async Task<PayrollReport> Preview(int year, int month, CancellationToken ct)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var eligible = db.Users.AsNoTracking().Where(p => p.IsActive &&
            !(from link in db.UserRoles
              join role in db.Roles on link.RoleId equals role.Id
              where link.UserId == p.Id && role.Name == Roles.Admin
              select link.UserId).Any());
        var people = await eligible.Select(p => new PayrollPerson(p.Id, p.FirstName + " " + p.LastName,
            p.Document, p.MonthlySalary)).ToListAsync(ct);
        var events = await (from entry in db.WorkEvents.AsNoTracking()
                            join person in eligible on entry.PersonId equals person.Id
                            where entry.Date >= start && entry.Date < end
                            select new PayrollEvent(entry.PersonId, entry.Date, entry.Kind)).ToListAsync(ct);
        var byPerson = events.ToLookup(e => e.PersonId);
        var lines = Sorted(people.Select(p => PayrollCalculator.Calculate(p, byPerson[p.Id].Select(e => e.Kind))));
        return new(year, month, null, PayrollCalculator.RuleVersion, PayrollCalculator.RuleParameters,
            PayrollCalculator.Revision(year, month, people, events), lines, lines.Sum(l => l.Total));
    }

    public async Task<CloseOutcome> Close(int year, int month, string revision, string actorId, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await db.PayrollPeriods.AnyAsync(p => p.Year == year && p.Month == month, ct))
                return CloseOutcome.AlreadyClosed;
            var preview = await Preview(year, month, ct);
            if (!string.Equals(revision, preview.Revision, StringComparison.Ordinal))
                return CloseOutcome.Stale;
            if (preview.Lines.Count == 0) return CloseOutcome.Empty;

            db.PayrollPeriods.Add(new PayrollPeriod
            {
                Year = year, Month = month, ClosedById = actorId, ClosedAt = DateTimeOffset.UtcNow,
                RuleVersion = preview.RuleVersion, RuleParameters = preview.RuleParameters,
                Total = preview.Total, Lines = preview.Lines.ToList()
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return CloseOutcome.Closed;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 or 1205 })
        {
            return CloseOutcome.Stale;
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return CloseOutcome.Stale;
        }
    }

    private static List<PayrollLine> Sorted(IEnumerable<PayrollLine> lines) =>
        lines.OrderBy(l => l.PersonName, StringComparer.Ordinal).ThenBy(l => l.PersonId, StringComparer.Ordinal).ToList();
}

public enum CloseOutcome { Closed, AlreadyClosed, Stale, Empty }
public sealed record PayrollReport(int Year, int Month, DateTimeOffset? ClosedAt, string RuleVersion,
    string RuleParameters, string? Revision, IReadOnlyList<PayrollLine> Lines, decimal Total);

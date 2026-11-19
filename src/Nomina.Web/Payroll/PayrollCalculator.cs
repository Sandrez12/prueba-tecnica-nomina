using System.Security.Cryptography;
using System.Text.Json;
using Nomina.Web.Data;

namespace Nomina.Web.Payroll;

public static class PayrollCalculator
{
    public const string RuleVersion = "v1";
    private const int Days = 30;
    private const int SicknessDivisor = 3;
    public static readonly string RuleParameters = JsonSerializer.Serialize(new
    {
        days = Days, absenceDaily = 1, sicknessDeductionDaily = "1/3",
        extraDaily = 1, rounding = "COP por concepto, midpoint away from zero", floor = 0
    });

    public static PayrollLine Calculate(PayrollPerson person, IEnumerable<WorkEventKind> events)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(person.Salary);
        var counts = events.CountBy(kind => kind).ToDictionary();
        var line = new PayrollLine
        {
            PersonId = person.Id, PersonName = person.Name, Document = person.Document,
            MonthlySalary = person.Salary,
            WorkedDays = counts.GetValueOrDefault(WorkEventKind.Worked),
            AbsentDays = counts.GetValueOrDefault(WorkEventKind.Absent),
            SickDays = counts.GetValueOrDefault(WorkEventKind.Sick),
            ExtraDays = counts.GetValueOrDefault(WorkEventKind.Extra)
        };
        line.AbsenceAdjustment = Round(-person.Salary * line.AbsentDays / Days);
        line.SicknessAdjustment = Round(-person.Salary * line.SickDays / (Days * SicknessDivisor));
        line.ExtraAdjustment = Round(person.Salary * line.ExtraDays / Days);
        var subtotal = line.MonthlySalary + line.AbsenceAdjustment + line.SicknessAdjustment + line.ExtraAdjustment;
        line.MinimumAdjustment = Math.Max(0, -subtotal);
        line.Total = subtotal + line.MinimumAdjustment;
        return line;
    }

    public static string Revision(int year, int month, IEnumerable<PayrollPerson> people,
        IEnumerable<PayrollEvent> events)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            year, month, RuleVersion, RuleParameters,
            people = people.OrderBy(p => p.Id, StringComparer.Ordinal),
            events = events.OrderBy(e => e.PersonId, StringComparer.Ordinal).ThenBy(e => e.Date)
        });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static decimal Round(decimal amount) => Math.Round(amount, 0, MidpointRounding.AwayFromZero);
}

public sealed record PayrollPerson(string Id, string Name, string Document, decimal Salary);
public sealed record PayrollEvent(string PersonId, DateOnly Date, WorkEventKind Kind);

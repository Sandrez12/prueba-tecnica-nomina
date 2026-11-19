using Nomina.Web.Data;
using Nomina.Web.Payroll;
using Xunit;

namespace Nomina.Tests;

public sealed class PayrollTests
{
    private static readonly PayrollPerson Person = new("demo", "Persona ficticia", "DEMO-01", 1_500_000);

    [Fact]
    public void AppliesDocumentedExample()
    {
        var line = PayrollCalculator.Calculate(Person,
            [WorkEventKind.Worked, WorkEventKind.Absent, WorkEventKind.Sick, WorkEventKind.Extra, WorkEventKind.Extra]);
        Assert.Equal(1, line.WorkedDays);
        Assert.Equal(-50_000m, line.AbsenceAdjustment);
        Assert.Equal(-16_667m, line.SicknessAdjustment);
        Assert.Equal(100_000m, line.ExtraAdjustment);
        Assert.Equal(1_533_333m, line.Total);
    }

    [Fact]
    public void EmptyMonthKeepsBaseAndLongAbsenceDoesNotPayNegative()
    {
        Assert.Equal(Person.Salary, PayrollCalculator.Calculate(Person, []).Total);
        var line = PayrollCalculator.Calculate(Person, Enumerable.Repeat(WorkEventKind.Absent, 31));
        Assert.Equal(50_000m, line.MinimumAdjustment);
        Assert.Equal(0m, line.Total);
    }

    [Fact]
    public void RoundsHalfAwayFromZeroWithoutRoundingDailyRateFirst()
    {
        var line = PayrollCalculator.Calculate(Person with { Salary = 15 },
            [WorkEventKind.Absent, WorkEventKind.Extra]);
        Assert.Equal(-1m, line.AbsenceAdjustment);
        Assert.Equal(1m, line.ExtraAdjustment);
    }

    [Fact]
    public void RevisionChangesWithSalaryAndWithEventDateEvenIfTotalIsTheSame()
    {
        var item = new PayrollEvent(Person.Id, new DateOnly(2026, 11, 1), WorkEventKind.Absent);
        var version = PayrollCalculator.Revision(2026, 11, [Person], [item]);
        Assert.NotEqual(version, PayrollCalculator.Revision(2026, 11, [Person with { Salary = 2_000_000 }], [item]));
        Assert.NotEqual(version, PayrollCalculator.Revision(2026, 11, [Person], [item with { Date = item.Date.AddDays(1) }]));
    }
}

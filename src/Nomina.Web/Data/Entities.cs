using Microsoft.AspNetCore.Identity;

namespace Nomina.Web.Data;

public sealed class Person : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Document { get; set; } = "";
    public decimal MonthlySalary { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CrewId { get; set; }
    public List<Address> Addresses { get; set; } = [];
    public List<Phone> Phones { get; set; } = [];
}

public sealed class Address
{
    public int Id { get; set; }
    public string PersonId { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class Phone
{
    public int Id { get; set; }
    public string PersonId { get; set; } = "";
    public string Number { get; set; } = "";
}

public sealed class Crew
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LeadId { get; set; } = "";
}

public enum ActivityStatus { Pending = 1, InProgress = 2, Finished = 3 }

public sealed class TeamActivity
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string AssigneeId { get; set; } = "";
    public ActivityStatus Status { get; set; } = ActivityStatus.Pending;
}

public enum WorkEventKind { Worked = 1, Sick = 2, Absent = 3, Extra = 4 }

public sealed class WorkEvent
{
    public int Id { get; set; }
    public string PersonId { get; set; } = "";
    public DateOnly Date { get; set; }
    public WorkEventKind Kind { get; set; }
}

public sealed class WorkEventCorrection
{
    public int Id { get; set; }
    public int WorkEventId { get; set; }
    public string ActorId { get; set; } = "";
    public WorkEventKind PreviousKind { get; set; }
    public WorkEventKind NewKind { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset ChangedAt { get; set; }
}

public sealed class PayrollPeriod
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTimeOffset ClosedAt { get; set; }
    public string ClosedById { get; set; } = "";
    public string RuleVersion { get; set; } = "";
    public string RuleParameters { get; set; } = "";
    public decimal Total { get; set; }
    public List<PayrollLine> Lines { get; set; } = [];
}

public sealed class PayrollLine
{
    public int PeriodId { get; set; }
    public string PersonId { get; set; } = "";
    public string PersonName { get; set; } = "";
    public string Document { get; set; } = "";
    public decimal MonthlySalary { get; set; }
    public int WorkedDays { get; set; }
    public int AbsentDays { get; set; }
    public int SickDays { get; set; }
    public int ExtraDays { get; set; }
    public decimal AbsenceAdjustment { get; set; }
    public decimal SicknessAdjustment { get; set; }
    public decimal ExtraAdjustment { get; set; }
    public decimal MinimumAdjustment { get; set; }
    public decimal Total { get; set; }
}

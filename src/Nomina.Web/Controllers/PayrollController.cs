using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nomina.Web.Auth;
using Nomina.Web.Payroll;

namespace Nomina.Web.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Accountant + "," + Roles.Worker)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PayrollController(PayrollService payroll) : Controller
{
    [HttpGet("/nomina")]
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken ct)
    {
        var today = WorkEventsController.Today();
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        if (!ModelState.IsValid || !ValidPeriod(y, m))
            return BadRequest("Selecciona un período válido que no sea futuro.");
        var report = await GetReport(y, m, ct);
        return View(new PayrollPage(y, m, report, User.IsInRole(Roles.Admin)));
    }

    [HttpGet("/api/nomina/{year:int}/{month:int}")]
    public async Task<IActionResult> ReportApi(int year, int month, CancellationToken ct)
    {
        if (!ValidPeriod(year, month)) return BadRequest(new { error = "Período no válido." });
        var report = await GetReport(year, month, ct);
        return report is null ? NotFound() : Ok(report);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("/nomina/cerrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(PayrollCloseInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid || !ValidInput(input))
            return BadRequest("Período o revisión inválidos.");
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actorId is null) return Forbid();
        var result = await payroll.Close(input.Year, input.Month, input.Revision!, actorId, ct);
        TempData["PayrollMessage"] = Message(result);
        return RedirectToAction(nameof(Index), new { year = input.Year, month = input.Month });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("/api/nomina/cerrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseApi([FromBody] PayrollCloseInput? input, CancellationToken ct)
    {
        if (input is null || !ModelState.IsValid || !ValidInput(input))
            return BadRequest(new { error = "Período o revisión inválidos." });
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actorId is null) return Forbid();
        var result = await payroll.Close(input.Year, input.Month, input.Revision!, actorId, ct);
        if (result != CloseOutcome.Closed) return Conflict(new { error = Message(result) });
        return Ok(await payroll.FindClosed(input.Year, input.Month, null, ct));
    }

    private async Task<PayrollReport?> GetReport(int year, int month, CancellationToken ct)
    {
        var worker = !User.IsInRole(Roles.Admin) && User.IsInRole(Roles.Worker);
        var personId = worker ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        if (worker && personId is null) return null;
        return await payroll.FindClosed(year, month, personId, ct)
            ?? (User.IsInRole(Roles.Admin) ? await payroll.Preview(year, month, ct) : null);
    }

    private static bool ValidInput(PayrollCloseInput input) =>
        ValidPeriod(input.Year, input.Month) && input.Revision is { Length: 64 };

    private static bool ValidPeriod(int year, int month)
    {
        var today = WorkEventsController.Today();
        return year >= 1 && month is >= 1 and <= 12
            && (year < today.Year || (year == today.Year && month <= today.Month));
    }

    private static string Message(CloseOutcome outcome) => outcome switch
    {
        CloseOutcome.Closed => "Nómina cerrada. Los importes quedaron guardados.",
        CloseOutcome.AlreadyClosed => "El período ya estaba cerrado.",
        CloseOutcome.Empty => "No hay personas activas para liquidar.",
        _ => "Los datos cambiaron o hubo otra operación simultánea. Revisa el cálculo actualizado antes de cerrar."
    };
}

public sealed record PayrollPage(int Year, int Month, PayrollReport? Report, bool CanClose);

public sealed class PayrollCloseInput
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string? Revision { get; set; }
}

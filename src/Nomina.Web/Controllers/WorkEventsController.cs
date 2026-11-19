using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nomina.Web.Auth;
using Nomina.Web.Data;

namespace Nomina.Web.Controllers;

[Authorize(Policy = Access.EventsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkEventsController(NominaDbContext db) : Controller
{
    [HttpGet("/novedades")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await Page(new WorkEventInput { Date = Today() }, ct));

    [HttpPost("/novedades")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(WorkEventInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var outcome = await Save(input, ct);
            if (outcome.Status == 201)
            {
                TempData["WorkEventMessage"] = "Novedad registrada.";
                return RedirectToAction(nameof(Index));
            }
            if (outcome.Status == 403) return Forbid();
            ModelState.AddModelError("", outcome.Message);
        }
        return View("Index", await Page(input, ct));
    }

    [HttpGet("/api/novedades")]
    public async Task<IActionResult> ListApi(CancellationToken ct) => Ok(await Recent(ct));

    [HttpGet("/api/novedades/token")]
    public IActionResult Token([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { header = tokens.HeaderName, token = tokens.RequestToken });
    }

    [HttpPost("/api/novedades")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterApi([FromBody] WorkEventInput? input, CancellationToken ct)
    {
        if (input is null || !ModelState.IsValid)
            return BadRequest(new { error = "Persona, fecha y tipo válidos son obligatorios." });
        var outcome = await Save(input, ct);
        if (outcome.Status != 201) return StatusCode(outcome.Status, new { error = outcome.Message });
        return StatusCode(201, new { id = outcome.Id });
    }

    [HttpGet("/novedades/{id:int}/corregir")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var entry = await FindVisible(id, ct);
        if (entry is null) return NotFound();
        return View(await EditPage(entry, new() { Kind = entry.Kind, PreviousKind = entry.Kind }, ct));
    }

    [HttpPost("/novedades/{id:int}/corregir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, CorrectionInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await Correct(id, input, ct);
            if (result.Status == 404) return NotFound();
            if (result.Status == 204)
            {
                TempData["WorkEventMessage"] = "Novedad corregida y auditada.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError("", result.Message);
        }
        var entry = await FindVisible(id, ct);
        return entry is null ? NotFound() : View(await EditPage(entry, input, ct));
    }

    [HttpPatch("/api/novedades/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CorrectApi([FromRoute] int id, [FromBody] CorrectionInput? input, CancellationToken ct)
    {
        if (input is null || !ModelState.IsValid) return BadRequest(new { error = "Tipo anterior, tipo nuevo y motivo válidos son obligatorios." });
        var result = await Correct(id, input, ct);
        return result.Status == 204 ? NoContent() : StatusCode(result.Status, new { error = result.Message });
    }

    private Task<WorkEventRow?> FindVisible(int id, CancellationToken ct) =>
        (from entry in db.WorkEvents.AsNoTracking()
         join person in ScopedPeople() on entry.PersonId equals person.Id
         where entry.Id == id
         select new WorkEventRow(entry.Id, entry.PersonId, person.FirstName + " " + person.LastName, entry.Date, entry.Kind))
        .SingleOrDefaultAsync(ct);

    private Task<bool> IsClosed(DateOnly date, CancellationToken ct) =>
        db.PayrollPeriods.AnyAsync(p => p.Year == date.Year && p.Month == date.Month, ct);

    private async Task<CorrectionPage> EditPage(WorkEventRow entry, CorrectionInput input, CancellationToken ct) =>
        new(entry, input, await IsClosed(entry.Date, ct), await
            (from change in db.WorkEventCorrections.AsNoTracking()
             join actor in db.Users on change.ActorId equals actor.Id
             where change.WorkEventId == entry.Id
             orderby change.ChangedAt descending, change.Id descending
             select new CorrectionRow(actor.FirstName + " " + actor.LastName, change.PreviousKind,
                 change.NewKind, change.Reason, change.ChangedAt)).ToListAsync(ct));

    private async Task<SaveOutcome> Correct(int id, CorrectionInput input, CancellationToken ct)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return new(403, "No tienes permiso para realizar la corrección.");
        if (!Enum.IsDefined(input.Kind) || !Enum.IsDefined(input.PreviousKind) || input.Kind == input.PreviousKind)
            return new(400, "Selecciona un tipo diferente al actual.");

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var people = ScopedPeople();
            var entry = await db.WorkEvents.SingleOrDefaultAsync(e => e.Id == id &&
                people.Any(p => p.Id == e.PersonId), ct);
            if (entry is null) return new(404, "Novedad no encontrada.");
            if (await IsClosed(entry.Date, ct)) return new(409, "Ese período ya está cerrado.");
            if (entry.Kind != input.PreviousKind) return new(409, "La novedad ya cambió. Abre de nuevo el formulario.");

            db.WorkEventCorrections.Add(new()
            {
                WorkEventId = entry.Id, ActorId = actor, PreviousKind = entry.Kind,
                NewKind = input.Kind, Reason = input.Reason.Trim(), ChangedAt = DateTimeOffset.UtcNow
            });
            entry.Kind = input.Kind;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(204, "");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 1205 })
        {
            return new(409, "Hubo un cambio simultáneo. Abre de nuevo el formulario.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return new(409, "Hubo un cambio simultáneo. Abre de nuevo el formulario.");
        }
    }

    private IQueryable<Person> ScopedPeople()
    {
        if (User.Allows(Access.EventsAll)) return db.Users;
        var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (actorId is null) return db.Users.Where(_ => false);
        return db.Users.Where(p => db.Crews.Any(c => c.Id == p.CrewId && c.LeadId == actorId));
    }

    private IQueryable<Person> EligiblePeople() => ScopedPeople().Where(p => p.IsActive &&
        !(from link in db.UserRoles
          join role in db.Roles on link.RoleId equals role.Id
          where link.UserId == p.Id && role.Name == Roles.Admin
          select link.UserId).Any());

    private async Task<List<WorkEventRow>> Recent(CancellationToken ct) =>
        await (from entry in db.WorkEvents.AsNoTracking()
               join person in ScopedPeople() on entry.PersonId equals person.Id
               orderby entry.Date descending, entry.Id descending
               select new WorkEventRow(entry.Id, entry.PersonId, person.FirstName + " " + person.LastName,
                   entry.Date, entry.Kind)).Take(100).ToListAsync(ct);

    private async Task<WorkEventsPage> Page(WorkEventInput input, CancellationToken ct) =>
        new(input, await EligiblePeople().OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Select(p => new WorkPerson(p.Id, p.FirstName + " " + p.LastName + " · " + p.Document))
            .ToListAsync(ct), await Recent(ct));

    private async Task<SaveOutcome> Save(WorkEventInput input, CancellationToken ct)
    {
        if (input.Date is not { } date || date > Today() || !Enum.IsDefined(input.Kind))
            return new(400, "Selecciona un tipo válido y una fecha que no sea futura.");

        try
        {
            // Payroll close must use the same isolation and recheck the period and source rows.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await EligiblePeople().AnyAsync(p => p.Id == input.PersonId, ct))
                return new(403, "No puedes registrar novedades para esa persona.");
            if (await db.PayrollPeriods.AnyAsync(p => p.Year == date.Year && p.Month == date.Month, ct))
                return new(409, "Ese período ya está cerrado.");
            if (await db.WorkEvents.AnyAsync(e => e.PersonId == input.PersonId && e.Date == date, ct))
                return new(409, "Ya existe una novedad para esa persona y fecha.");

            var entry = new WorkEvent { PersonId = input.PersonId, Date = date, Kind = input.Kind };
            db.WorkEvents.Add(entry);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(201, "", entry.Id);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return new(409, "Ya existe una novedad para esa persona y fecha.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 1205 })
        {
            return new(409, "Hubo un cambio concurrente. Vuelve a intentarlo.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return new(409, "Hubo un cambio concurrente. Vuelve a intentarlo.");
        }
    }

    internal static DateOnly Today() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).DateTime);
    private sealed record SaveOutcome(int Status, string Message, int? Id = null);
}

public sealed class WorkEventInput
{
    [Required, StringLength(450)]
    public string PersonId { get; set; } = "";
    [Required, DataType(DataType.Date)]
    public DateOnly? Date { get; set; }
    [EnumDataType(typeof(WorkEventKind))]
    public WorkEventKind Kind { get; set; }
}

public sealed record WorkPerson(string Id, string Name);
public sealed record WorkEventRow(int Id, string PersonId, string Name, DateOnly Date, WorkEventKind Kind);
public sealed record WorkEventsPage(WorkEventInput Input, IReadOnlyList<WorkPerson> People,
    IReadOnlyList<WorkEventRow> Events);

public sealed class CorrectionInput
{
    [EnumDataType(typeof(WorkEventKind))]
    public WorkEventKind PreviousKind { get; set; }
    [EnumDataType(typeof(WorkEventKind))]
    public WorkEventKind Kind { get; set; }
    [Required(ErrorMessage = "Explica el motivo de la corrección."), StringLength(250)]
    public string Reason { get; set; } = "";
}

public sealed record CorrectionRow(string Actor, WorkEventKind PreviousKind, WorkEventKind NewKind,
    string Reason, DateTimeOffset ChangedAt);
public sealed record CorrectionPage(WorkEventRow Event, CorrectionInput Input, bool Closed,
    IReadOnlyList<CorrectionRow> History);

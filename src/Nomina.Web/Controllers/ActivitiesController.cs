using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nomina.Web.Auth;
using Nomina.Web.Data;

namespace Nomina.Web.Controllers;

[Authorize(Policy = Access.BoardPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("actividades")]
public sealed class ActivitiesController(NominaDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await Page(new(), ct));

    [HttpGet("{id:int}/editar")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var item = await db.Activities.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
        return item is null ? NotFound() : View("Index", await Page(new()
        {
            Id = item.Id, Title = item.Title, AssigneeId = item.AssigneeId
        }, ct));
    }

    [HttpPost("guardar")]
    [Authorize(Roles = Roles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ActivityInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Index", await Page(input, ct));
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var item = input.Id == 0 ? new TeamActivity() :
                await db.Activities.SingleOrDefaultAsync(a => a.Id == input.Id, ct);
            if (item is null) return NotFound();
            if (item.AssigneeId != input.AssigneeId && !await Assignable().AnyAsync(p => p.Id == input.AssigneeId, ct))
            {
                ModelState.AddModelError(nameof(input.AssigneeId), "Selecciona una persona activa disponible.");
                return View("Index", await Page(input, ct));
            }
            item.Title = input.Title.Trim();
            item.AssigneeId = input.AssigneeId;
            if (input.Id == 0) db.Activities.Add(item);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsConcurrency(ex))
        {
            return Conflict("La actividad cambió. Vuelve a abrir el tablero.");
        }
    }

    [HttpPost("{id:int}/estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move([FromRoute] int id, ActivityStatus next, CancellationToken ct)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(next)) return BadRequest("Estado no válido.");
        var visible = VisiblePeople();
        var changed = await db.Activities.Where(a => a.Id == id && visible.Any(p => p.Id == a.AssigneeId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Status, next), ct);
        return changed == 0 ? NotFound() : RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/eliminar")]
    [Authorize(Roles = Roles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        var changed = await db.Activities.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
        return changed == 0 ? NotFound() : RedirectToAction(nameof(Index));
    }

    private IQueryable<Person> VisiblePeople()
    {
        if (User.IsInRole(Roles.Admin)) return db.Users;
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actorId is null) return db.Users.Where(_ => false);
        return User.Allows(Access.BoardCrew)
            ? db.Users.Where(p => p.Id == actorId || db.Crews.Any(c => c.Id == p.CrewId && c.LeadId == actorId))
            : db.Users.Where(p => p.Id == actorId);
    }

    private IQueryable<Person> Assignable() =>
        Access.WithAny(db, [Access.BoardOwn, Access.BoardCrew], includeAdmin: true).Where(p => p.IsActive);

    private async Task<ActivityBoard> Page(ActivityInput input, CancellationToken ct)
    {
        var admin = User.IsInRole(Roles.Admin);
        var people = await (from activity in db.Activities.AsNoTracking()
                            join person in VisiblePeople() on activity.AssigneeId equals person.Id
                            orderby activity.Id descending
                            select new ActivityRow(activity.Id, activity.Title, activity.Status,
                                activity.AssigneeId, person.FirstName + " " + person.LastName)).ToListAsync(ct);
        var options = admin
            ? await Assignable().OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
                .Select(p => new ActivityPerson(p.Id, p.FirstName + " " + p.LastName)).ToListAsync(ct)
            : [];
        var current = people.SingleOrDefault(a => a.Id == input.Id && a.AssigneeId == input.AssigneeId);
        if (admin && current is not null && options.All(option => option.Id != current.AssigneeId))
            options.Add(new(current.AssigneeId, current.Assignee + " (asignación actual)"));
        return new(admin, input, people, options);
    }

    private static bool IsConcurrency(Exception exception) =>
        (exception is SqlException { Number: 1205 }) ||
        (exception is DbUpdateException { InnerException: SqlException { Number: 1205 } });
}

public sealed class ActivityInput
{
    [Range(0, int.MaxValue)] public int Id { get; set; }
    [Required(ErrorMessage = "Escribe una actividad."), StringLength(200)] public string Title { get; set; } = "";
    [Required(ErrorMessage = "Selecciona un responsable."), StringLength(450)] public string AssigneeId { get; set; } = "";
}

public sealed record ActivityRow(int Id, string Title, ActivityStatus Status, string AssigneeId, string Assignee);
public sealed record ActivityPerson(string Id, string Name);
public sealed record ActivityBoard(bool CanManage, ActivityInput Input, IReadOnlyList<ActivityRow> Rows,
    IReadOnlyList<ActivityPerson> People);

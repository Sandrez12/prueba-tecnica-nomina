using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nomina.Web.Auth;
using Nomina.Web.Data;

namespace Nomina.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("cuadrillas")]
public sealed class CrewsController(NominaDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await
        (from crew in db.Crews.AsNoTracking()
         join lead in db.Users on crew.LeadId equals lead.Id
         orderby crew.Name
         select new CrewRow(crew.Id, crew.Name, lead.FirstName + " " + lead.LastName,
             db.Users.Count(p => p.CrewId == crew.Id))).ToListAsync(ct));

    [HttpGet("nueva")]
    public async Task<IActionResult> Create(CancellationToken ct) => View("Edit", await Page(null, new(), ct));

    [HttpGet("{id:int}/editar")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var crew = await db.Crews.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (crew is null) return NotFound();
        var members = await db.Users.Where(p => p.CrewId == id).Select(p => p.Id).ToListAsync(ct);
        return View(await Page(id, new() { Name = crew.Name, LeadId = crew.LeadId, MemberIds = members }, ct));
    }

    [HttpPost("nueva")]
    [HttpPost("{id:int}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromRoute] int? id, CrewInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", await Page(id, input, ct));

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var crew = id is null ? new Crew() : await db.Crews.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (crew is null) return NotFound();
            var chosen = input.MemberIds.Distinct(StringComparer.Ordinal).ToArray();
            var members = await AvailableMembers(id).Where(p => chosen.Contains(p.Id)).ToListAsync(ct);
            if (!await Leads().AnyAsync(p => p.Id == input.LeadId, ct))
                ModelState.AddModelError(nameof(input.LeadId), "Selecciona una cuenta activa con permiso de cuadrilla.");
            if (members.Count != chosen.Length)
                ModelState.AddModelError(nameof(input.MemberIds), "Alguna persona no está disponible. Actualiza la selección.");
            if (!ModelState.IsValid) return View("Edit", await Page(id, input, ct));

            crew.Name = input.Name.Trim();
            crew.LeadId = input.LeadId;
            if (id is null) db.Crews.Add(crew);
            await db.SaveChangesAsync(ct);

            if (id is not null)
            {
                var removed = await db.Users.Where(p => p.CrewId == id && !chosen.Contains(p.Id)).ToListAsync(ct);
                foreach (var person in removed) person.CrewId = null;
            }
            foreach (var person in members) person.CrewId = crew.Id;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("La cuadrilla cambió mientras la editabas. Vuelve a abrirla e intenta nuevamente.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 1205 })
        {
            return Conflict("Hubo un cambio concurrente. Vuelve a abrir la cuadrilla.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return Conflict("Hubo un cambio concurrente. Vuelve a abrir la cuadrilla.");
        }

        TempData["CrewMessage"] = "Cuadrilla guardada.";
        return RedirectToAction(nameof(Index));
    }

    private IQueryable<Person> WithRole(string name) =>
        from user in db.Users
        where (from link in db.UserRoles
               join role in db.Roles on link.RoleId equals role.Id
               where link.UserId == user.Id && role.Name == name
               select link.UserId).Any()
        select user;

    private IQueryable<Person> Leads() => Access.WithAny(db, [Access.EventsCrew, Access.BoardCrew]).Where(p => p.IsActive);

    private IQueryable<Person> AvailableMembers(int? id)
    {
        var admins = WithRole(Roles.Admin);
        return db.Users.Where(p => !admins.Any(a => a.Id == p.Id) &&
            ((p.IsActive && p.CrewId == null) || (id != null && p.CrewId == id)));
    }

    private async Task<CrewPage> Page(int? id, CrewInput input, CancellationToken ct) =>
        new(id, input, await Options(Leads(), ct), await Options(AvailableMembers(id), ct));

    private static Task<List<CrewOption>> Options(IQueryable<Person> people, CancellationToken ct) =>
        people.AsNoTracking().OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Select(p => new CrewOption(p.Id, p.FirstName + " " + p.LastName, p.Document, p.IsActive)).ToListAsync(ct);
}

public sealed class CrewInput
{
    [Required(ErrorMessage = "Escribe el nombre de la cuadrilla."), StringLength(100)]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "Selecciona una jefatura."), StringLength(450)]
    public string LeadId { get; set; } = "";
    public List<string> MemberIds { get; set; } = [];
}

public sealed record CrewRow(int Id, string Name, string Lead, int Members);
public sealed record CrewOption(string Id, string Name, string Document, bool Active);
public sealed record CrewPage(int? Id, CrewInput Input, IReadOnlyList<CrewOption> Leads, IReadOnlyList<CrewOption> Members);

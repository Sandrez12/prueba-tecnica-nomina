using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nomina.Web.Auth;
using Nomina.Web.Data;

namespace Nomina.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("roles")]
public sealed class RolesController(NominaDbContext db, RoleManager<IdentityRole> roles) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await db.Roles.AsNoTracking()
        .OrderBy(r => r.Name).Select(r => new RoleRow(r.Id, r.Name!, db.UserRoles.Count(u => u.RoleId == r.Id)))
        .ToListAsync(ct));

    [HttpGet("nuevo")]
    public IActionResult Create() => View("Edit", new RoleForm());

    [HttpGet("{id}/editar")]
    public async Task<IActionResult> Edit([FromRoute] string id, CancellationToken ct)
    {
        var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return NotFound();
        if (role.Name == Roles.Admin) return Forbid();
        var claims = await roles.GetClaimsAsync(role);
        return View(new RoleForm
        {
            Id = id, Name = role.Name!, Stamp = role.ConcurrencyStamp,
            FixedName = IsBuiltIn(role.Name),
            Permissions = (claims.Any(c => c.Type == Access.Configured && c.Value == "1")
                ? claims.Where(c => c.Type == Access.Claim).Select(c => c.Value) : Access.Defaults(role.Name)).ToList()
        });
    }

    [HttpPost("nuevo")]
    [HttpPost("{id}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromRoute] string? id, RoleForm input, CancellationToken ct)
    {
        input.Id = id;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var role = id is null ? new IdentityRole() : await roles.FindByIdAsync(id);
            if (role is null) return NotFound();
            if (role.Name == Roles.Admin) return Forbid();
            if (id is not null && !string.Equals(input.Stamp, role.ConcurrencyStamp, StringComparison.Ordinal))
                return Conflict("El rol cambió. Vuelve a abrir el formulario.");

            input.FixedName = IsBuiltIn(role.Name);
            var name = input.FixedName ? role.Name! : input.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 70)
                ModelState.AddModelError(nameof(input.Name), "Escribe un nombre de hasta 70 caracteres.");
            var selected = input.Permissions.ToHashSet(StringComparer.Ordinal);
            if (selected.Any(p => !Access.Catalog.ContainsKey(p)))
                ModelState.AddModelError(nameof(input.Permissions), "Hay permisos desconocidos.");
            if (selected.Contains(Access.PayrollExport) && !selected.Contains(Access.PayrollRead))
                ModelState.AddModelError(nameof(input.Permissions), "Exportar requiere consulta global de nómina.");
            if (!ModelState.IsValid) return View("Edit", input);

            role.Name = name;
            var result = id is null ? await roles.CreateAsync(role) : await roles.UpdateAsync(role);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "No fue posible guardar el rol. Comprueba que el nombre no esté en uso.");
                return View("Edit", input);
            }

            var previous = await db.RoleClaims.Where(c => c.RoleId == role.Id &&
                (c.ClaimType == Access.Claim || c.ClaimType == Access.Configured)).ToListAsync(ct);
            db.RoleClaims.RemoveRange(previous);
            db.RoleClaims.Add(new() { RoleId = role.Id, ClaimType = Access.Configured, ClaimValue = "1" });
            foreach (var permission in selected)
                db.RoleClaims.Add(new() { RoleId = role.Id, ClaimType = Access.Claim, ClaimValue = permission });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            TempData["RoleMessage"] = "Rol guardado.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ConflictSql(ex))
        {
            return Conflict("Hubo un cambio simultáneo. Vuelve a abrir el formulario.");
        }
    }

    [HttpPost("{id}/eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromRoute] string id, string? stamp, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var role = await roles.FindByIdAsync(id);
            if (role is null) return NotFound();
            if (IsBuiltIn(role.Name)) return Forbid();
            if (stamp != role.ConcurrencyStamp)
                return Conflict("El rol cambió. Vuelve a abrir el formulario.");
            if (await db.UserRoles.AnyAsync(link => link.RoleId == id, ct))
                return Conflict("El rol está asignado. Reasigna a esas personas antes de eliminarlo.");
            if (!(await roles.DeleteAsync(role)).Succeeded)
                return Conflict("No se pudo eliminar el rol.");
            await tx.CommitAsync(ct);
            TempData["RoleMessage"] = "Rol eliminado.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ConflictSql(ex))
        {
            return Conflict("El rol cambió. Vuelve a abrir el formulario.");
        }
    }

    public static bool IsBuiltIn(string? name) => name is Roles.Admin or Roles.Worker or Roles.Lead or Roles.Accountant;

    private static bool ConflictSql(Exception ex) =>
        ex is SqlException { Number: 1205 } ||
        ex is DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 or 547 } };
}

public sealed class RoleForm
{
    public string? Id { get; set; }
    [Required, StringLength(70)] public string Name { get; set; } = "";
    public string? Stamp { get; set; }
    public bool FixedName { get; set; }
    public List<string> Permissions { get; set; } = [];
}

public sealed record RoleRow(string Id, string Name, int AssignedCount);

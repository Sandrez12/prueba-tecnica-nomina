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
[Route("personas")]
public sealed class PeopleController(NominaDbContext db, UserManager<Person> users) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await db.Users.AsNoTracking().OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Select(p => new PersonRow(p.Id, p.FirstName, p.LastName, p.Document, p.UserName!, p.IsActive,
                (from roleLink in db.UserRoles
                 join role in db.Roles on roleLink.RoleId equals role.Id
                 where roleLink.UserId == p.Id && role.Name == Roles.Admin
                 select roleLink.UserId).Any()))
            .ToListAsync(cancellationToken));

    [HttpGet("nueva")]
    public IActionResult Create() => View(new NewPersonInput());

    [HttpPost("nueva")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewPersonInput input, CancellationToken cancellationToken)
    {
        if (input.Role is not (Roles.Worker or Roles.Lead or Roles.Accountant))
            ModelState.AddModelError(nameof(input.Role), "Selecciona un rol válido.");
        if (!ModelState.IsValid)
            return Failed(input);

        var document = input.Document.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(p => p.Document == document, cancellationToken))
        {
            ModelState.AddModelError(nameof(input.Document), "Ese documento ya existe.");
            return Failed(input);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var person = new Person
        {
            FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(),
            Document = document, UserName = input.UserName.Trim(),
            MonthlySalary = input.MonthlySalary, IsActive = input.IsActive,
            Addresses = [new Address { Value = input.Address.Trim() }],
            Phones = [new Phone { Number = input.Phone.Trim() }]
        };
        try
        {
            var result = await users.CreateAsync(person, input.Password);
            if (result.Succeeded)
                result = await users.AddToRoleAsync(person, input.Role);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", TranslateError(error.Code));
                return Failed(input);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError("", "El documento o nombre de usuario ya existe.");
            return Failed(input);
        }

        TempData["PeopleMessage"] = "Persona creada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    private ViewResult Failed(NewPersonInput input)
    {
        // Keep validation errors, but never render the submitted password.
        input.Password = "";
        return View("Create", input);
    }

    [HttpGet("{id}/editar")]
    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        var person = await FindWithContacts(id, ct);
        if (person is null) return NotFound();
        if (await users.IsInRoleAsync(person, Roles.Admin)) return Forbid();
        return View(new PersonInput
        {
            Id = person.Id, FirstName = person.FirstName, LastName = person.LastName,
            Document = person.Document, UserName = person.UserName!,
            Address = person.Addresses.OrderBy(a => a.Id).FirstOrDefault()?.Value ?? "",
            Phone = person.Phones.OrderBy(p => p.Id).FirstOrDefault()?.Number ?? "",
            MonthlySalary = (long)person.MonthlySalary, IsActive = person.IsActive,
            Role = (await users.GetRolesAsync(person)).FirstOrDefault() ?? Roles.Worker
        });
    }

    [HttpPost("{id}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] string id, PersonInput input, CancellationToken ct)
    {
        var person = await FindWithContacts(id, ct);
        if (person is null) return NotFound();
        if (await users.IsInRoleAsync(person, Roles.Admin)) return Forbid();
        input.Id = id;
        if (input.Role is not (Roles.Worker or Roles.Lead or Roles.Accountant))
            ModelState.AddModelError(nameof(input.Role), "Selecciona un rol válido.");
        if (!ModelState.IsValid) return View(input);

        var document = input.Document.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(p => p.Id != id && p.Document == document, ct))
        {
            ModelState.AddModelError(nameof(input.Document), "Ese documento ya existe.");
            return View(input);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        person.FirstName = input.FirstName.Trim();
        person.LastName = input.LastName.Trim();
        person.UserName = input.UserName.Trim();
        person.Document = document;
        person.MonthlySalary = input.MonthlySalary;
        person.IsActive = input.IsActive;
        var address = person.Addresses.OrderBy(a => a.Id).FirstOrDefault();
        if (address is null) person.Addresses.Add(address = new Address());
        address.Value = input.Address.Trim();
        var phone = person.Phones.OrderBy(p => p.Id).FirstOrDefault();
        if (phone is null) person.Phones.Add(phone = new Phone());
        phone.Number = input.Phone.Trim();

        try
        {
            var roles = await users.GetRolesAsync(person);
            var result = await users.UpdateSecurityStampAsync(person);
            if (result.Succeeded && !roles.Contains(input.Role))
            {
                result = await users.RemoveFromRolesAsync(person, roles);
                if (result.Succeeded) result = await users.AddToRoleAsync(person, input.Role);
            }
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError("", TranslateError(error.Code));
                return View(input);
            }
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError("", "El documento o nombre de usuario ya existe.");
            return View(input);
        }
        TempData["PeopleMessage"] = "Persona actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id}/eliminar")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var person = await db.Users.FindAsync([id], ct);
        if (person is null) return NotFound();
        if (await users.IsInRoleAsync(person, Roles.Admin)) return Forbid();
        return View(new DeletePerson(id, $"{person.FirstName} {person.LastName}", await HasReferences(id, ct)));
    }

    [HttpPost("{id}/eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDelete([FromRoute] string id, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var person = await db.Users.FindAsync([id], ct);
            if (person is null) return NotFound();
            if (await users.IsInRoleAsync(person, Roles.Admin)) return Forbid();
            var retained = await HasReferences(id, ct);
            IdentityResult result;
            if (retained)
            {
                person.IsActive = false;
                result = await users.UpdateSecurityStampAsync(person);
            }
            else
            {
                result = await users.DeleteAsync(person);
            }
            if (!result.Succeeded) return Conflict("No se pudo completar el cambio. Vuelve a cargar la ficha.");
            await transaction.CommitAsync(ct);
            TempData["PeopleMessage"] = retained ? "Persona desactivada; se conservaron sus registros."
                : "Persona eliminada.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 or 1205 })
        {
            return Conflict("La persona tiene cambios concurrentes. Vuelve a cargar la ficha.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return Conflict("La persona tiene cambios concurrentes. Vuelve a cargar la ficha.");
        }
    }

    private Task<Person?> FindWithContacts(string id, CancellationToken ct) =>
        db.Users.Include(p => p.Addresses).Include(p => p.Phones).AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    private async Task<bool> HasReferences(string id, CancellationToken ct) =>
        await db.PayrollLines.AnyAsync(p => p.PersonId == id, ct) ||
        await db.PayrollPeriods.AnyAsync(p => p.ClosedById == id, ct) ||
        await db.WorkEvents.AnyAsync(e => e.PersonId == id, ct) ||
        await db.Activities.AnyAsync(a => a.AssigneeId == id, ct) ||
        await db.Crews.AnyAsync(c => c.LeadId == id, ct);

    private static string TranslateError(string code) => code switch
    {
        "DuplicateUserName" => "Ese nombre de usuario ya existe.",
        "InvalidUserName" => "El nombre de usuario contiene caracteres no permitidos.",
        "PasswordTooShort" or "PasswordRequiresUpper" or "PasswordRequiresLower" or
            "PasswordRequiresDigit" or "PasswordRequiresNonAlphanumeric" =>
            "La contraseña debe tener 12 caracteres como mínimo, mayúscula, minúscula, número y símbolo.",
        _ => "No se pudo guardar la cuenta."
    };
}

public sealed record PersonRow(string Id, string FirstName, string LastName, string Document, string UserName, bool IsActive, bool IsAdmin);

public sealed record DeletePerson(string Id, string Name, bool Retain);

public sealed class NewPersonInput : PersonInput
{
    [Required, StringLength(128, MinimumLength = 12), Display(Name = "Contraseña inicial")]
    public string Password { get; set; } = "";
}

public class PersonInput
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string Id { get; set; } = "";
    [Required, StringLength(100), Display(Name = "Nombres")]
    public string FirstName { get; set; } = "";
    [Required, StringLength(100), Display(Name = "Apellidos")]
    public string LastName { get; set; } = "";
    [Required, StringLength(40), Display(Name = "Documento")]
    public string Document { get; set; } = "";
    [Required, StringLength(250), Display(Name = "Dirección")]
    public string Address { get; set; } = "";
    [Required, StringLength(40), Display(Name = "Teléfono")]
    public string Phone { get; set; } = "";
    [Required, StringLength(256), Display(Name = "Nombre de usuario")]
    public string UserName { get; set; } = "";
    [Required, Display(Name = "Rol")]
    public string Role { get; set; } = Roles.Worker;
    [Range(0, 999_999_999), Display(Name = "Salario mensual (COP)")]
    public long MonthlySalary { get; set; }
    [Display(Name = "Cuenta activa")]
    public bool IsActive { get; set; } = true;
}

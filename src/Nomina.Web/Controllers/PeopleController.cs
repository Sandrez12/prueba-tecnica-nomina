using System.ComponentModel.DataAnnotations;
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
            .Select(p => new PersonRow(p.FirstName, p.LastName, p.Document, p.UserName!, p.IsActive))
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

public sealed record PersonRow(string FirstName, string LastName, string Document, string UserName, bool IsActive);

public sealed class NewPersonInput
{
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
    [Required, StringLength(128, MinimumLength = 12), Display(Name = "Contraseña inicial")]
    public string Password { get; set; } = "";
    [Required, Display(Name = "Rol")]
    public string Role { get; set; } = Roles.Worker;
    [Range(0, 999_999_999), Display(Name = "Salario mensual (COP)")]
    public long MonthlySalary { get; set; }
    [Display(Name = "Cuenta activa")]
    public bool IsActive { get; set; } = true;
}

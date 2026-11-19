using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nomina.Web.Auth;
using Nomina.Web.Data;

namespace Nomina.Web.Controllers;

[Route("cuenta")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(SignInManager<Person> signIn, UserManager<Person> users) : Controller
{
    [AllowAnonymous]
    [HttpGet("ingresar")]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction(nameof(Home)) : View(new LoginInput());

    [AllowAnonymous]
    [HttpPost("ingresar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (ModelState.IsValid)
        {
            var person = await users.FindByNameAsync(input.UserName.Trim());
            if (person is { IsActive: true }
                && (await signIn.PasswordSignInAsync(person, input.Password, false, lockoutOnFailure: true)).Succeeded)
                return RedirectToAction(nameof(Home));
            ModelState.AddModelError("", "No se pudo iniciar sesión con esos datos.");
        }
        ModelState.Remove(nameof(LoginInput.Password));
        return View(new LoginInput { UserName = input.UserName });
    }

    [Authorize]
    [HttpGet("inicio")]
    public IActionResult Home() => View();

    [Authorize]
    [HttpPost("salir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("administracion")]
    public IActionResult Administration() => Ok(new { status = "ok" });
}

public sealed class LoginInput
{
    [Required(ErrorMessage = "Escribe tu usuario."), StringLength(256)]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Escribe tu contraseña."), StringLength(1024)]
    public string Password { get; set; } = "";
}

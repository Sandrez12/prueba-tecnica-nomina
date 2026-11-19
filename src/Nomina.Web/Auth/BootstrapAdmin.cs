using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nomina.Web.Data;

namespace Nomina.Web.Auth;

public static class Roles
{
    public const string Admin = "Administracion";
    public const string Lead = "Jefatura";
    public const string Worker = "Trabajador";
    public const string Accountant = "Contabilidad";
}

internal static class BootstrapAdmin
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        var username = config["BootstrapAdmin:UserName"];
        var password = config["BootstrapAdmin:Password"];
        if (username is null && password is null)
            return;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Configura ambos valores del administrador inicial.");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NominaDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Person>>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        foreach (var role in new[] { Roles.Admin, Roles.Lead, Roles.Worker, Roles.Accountant })
        {
            if (!await roles.RoleExistsAsync(role))
                Ensure(await roles.CreateAsync(new IdentityRole(role)));
        }

        if ((await users.GetUsersInRoleAsync(Roles.Admin)).Count == 0)
        {
            var admin = new Person
            {
                UserName = username, Document = "DEMO-ADMIN",
                FirstName = "Cuenta", LastName = "Administrativa",
                Addresses = [new Address { Value = "Dirección ficticia (demo)" }],
                Phones = [new Phone { Number = "0000000000" }]
            };
            Ensure(await users.CreateAsync(admin, password));
            Ensure(await users.AddToRoleAsync(admin, Roles.Admin));
        }
        await transaction.CommitAsync();
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("No se pudo inicializar Identity. Códigos: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Nomina.Web.Data;

namespace Nomina.Web.Auth;

public static class Access
{
    public const string Claim = "nomina.permission";
    public const string Configured = "nomina.permissions.configured";
    public const string EventsCrew = "eventos.cuadrilla";
    public const string EventsAll = "eventos.todos";
    public const string BoardOwn = "tablero.propio";
    public const string BoardCrew = "tablero.cuadrilla";
    public const string PayrollOwn = "nomina.propia";
    public const string PayrollRead = "nomina.consulta";
    public const string PayrollExport = "nomina.exportacion";
    public const string EventsPolicy = "Novedades";
    public const string BoardPolicy = "Actividades";
    public const string PayrollPolicy = "ConsultarNomina";
    public const string ExportPolicy = "ExportarNomina";
    private static readonly string[] BuiltIn = [Roles.Lead, Roles.Worker, Roles.Accountant];

    public static readonly IReadOnlyDictionary<string, string> Catalog = new Dictionary<string, string>
    {
        [EventsCrew] = "Registrar y corregir novedades de las cuadrillas que lidera",
        [EventsAll] = "Registrar y corregir novedades de todas las personas",
        [BoardOwn] = "Ver y mover actividades propias",
        [BoardCrew] = "Ver y mover actividades propias y de las cuadrillas que lidera",
        [PayrollOwn] = "Consultar su estimación y su nómina cerrada",
        [PayrollRead] = "Consultar nóminas cerradas de toda la empresa",
        [PayrollExport] = "Exportar nóminas cerradas (requiere consulta global)"
    };

    public static IReadOnlyList<string> Defaults(string? role) => role switch
    {
        Roles.Lead => [EventsCrew, BoardCrew],
        Roles.Worker => [BoardOwn, PayrollOwn],
        Roles.Accountant => [PayrollRead, PayrollExport],
        _ => []
    };

    public static bool Allows(this ClaimsPrincipal user, params string[] permissions)
    {
        if (user.Identity?.IsAuthenticated != true) return false;
        if (user.IsInRole(Roles.Admin)) return true;
        if (user.HasClaim(Configured, "1"))
            return permissions.Any(permission => user.HasClaim(Claim, permission));
        return BuiltIn.Any(role => user.IsInRole(role) && Defaults(role).Any(permissions.Contains));
    }

    public static bool CanExport(this ClaimsPrincipal user) => user.Allows(PayrollRead) && user.Allows(PayrollExport);

    public static void Register(AuthorizationOptions options)
    {
        options.AddPolicy(EventsPolicy, p => p.RequireAssertion(a => a.User.Allows(EventsCrew, EventsAll)));
        options.AddPolicy(BoardPolicy, p => p.RequireAssertion(a => a.User.Allows(BoardOwn, BoardCrew)));
        options.AddPolicy(PayrollPolicy, p => p.RequireAssertion(a => a.User.Allows(PayrollOwn, PayrollRead)));
        options.AddPolicy(ExportPolicy, p => p.RequireAssertion(a => a.User.CanExport()));
    }

    public static IQueryable<Person> WithAny(NominaDbContext db, string[] permissions, bool includeAdmin = false)
    {
        var defaultRoles = BuiltIn.Where(role => Defaults(role).Any(permissions.Contains)).ToArray();
        return from person in db.Users
               join link in db.UserRoles on person.Id equals link.UserId
               join role in db.Roles on link.RoleId equals role.Id
               where (includeAdmin && role.Name == Roles.Admin) ||
                   (db.RoleClaims.Any(c => c.RoleId == role.Id && c.ClaimType == Configured && c.ClaimValue == "1")
                       ? db.RoleClaims.Any(c => c.RoleId == role.Id && c.ClaimType == Claim &&
                           c.ClaimValue != null && permissions.Contains(c.ClaimValue))
                       : role.Name != null && defaultRoles.Contains(role.Name))
               select person;
    }
}

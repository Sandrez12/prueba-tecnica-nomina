using System.Security.Claims;
using Nomina.Web.Auth;
using Xunit;

namespace Nomina.Tests;

public sealed class PermissionTests
{
    [Fact]
    public void LegacyRolesPreserveScopeUnlessExplicitlyConfigured()
    {
        Assert.True(User(Roles.Lead).Allows(Access.EventsCrew));
        Assert.False(User(Roles.Lead).Allows(Access.EventsAll));
        Assert.True(User(Roles.Accountant).CanExport());
        Assert.False(User(Roles.Accountant, new Claim(Access.Configured, "1")).CanExport());
    }

    [Fact]
    public void CustomRoleCannotUseUngrantablePrivilegesOrExportWithoutRead()
    {
        var custom = User("Analista", new(Access.Configured, "1"), new(Access.Claim, Access.PayrollExport));
        Assert.False(custom.CanExport());
        Assert.False(custom.IsInRole(Roles.Admin));
        Assert.False(custom.Allows(Access.EventsAll, Access.EventsCrew));
    }

    [Fact]
    public void CustomRoleCanReadOnlyItsGrantedScope()
    {
        var custom = User("Auxiliar", new(Access.Configured, "1"), new(Access.Claim, Access.EventsCrew));
        Assert.True(custom.Allows(Access.EventsCrew));
        Assert.False(custom.Allows(Access.EventsAll));
        Assert.False(custom.Allows(Access.PayrollRead));
    }

    private static ClaimsPrincipal User(string role, params Claim[] claims) =>
        new(new ClaimsIdentity(claims.Prepend(new(ClaimTypes.Role, role)), "Test"));
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nomina.Web.Data;

// Generating migration SQL is offline; the placeholder is not an actual database.
public sealed class NominaDesignFactory : IDesignTimeDbContextFactory<NominaDbContext>
{
    public NominaDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Nomina")
            ?? "Server=localhost;Database=NominaDesignOnly;Integrated Security=True";
        return new NominaDbContext(new DbContextOptionsBuilder<NominaDbContext>()
            .UseSqlServer(connection).Options);
    }
}

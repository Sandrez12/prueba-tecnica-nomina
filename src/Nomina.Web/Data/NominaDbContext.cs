using Microsoft.EntityFrameworkCore;

namespace Nomina.Web.Data;

public class NominaDbContext(DbContextOptions<NominaDbContext> options) : DbContext(options);

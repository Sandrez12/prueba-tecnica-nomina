using Microsoft.EntityFrameworkCore;
using Nomina.Web.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
var connectionString = builder.Configuration.GetConnectionString("Nomina");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<NominaDbContext>(options =>
        options.UseSqlServer(connectionString));
}

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/db", async (HttpContext context, CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var db = context.RequestServices.GetService<NominaDbContext>();
    if (db is null)
        return Results.Json(new { status = "unavailable" }, statusCode: 503);

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(5));
    return await db.Database.CanConnectAsync(timeout.Token)
        ? Results.Ok(new { status = "ok" })
        : Results.Json(new { status = "unavailable" }, statusCode: 503);
});
app.MapDefaultControllerRoute();
app.Run();

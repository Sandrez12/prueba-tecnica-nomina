using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nomina.Web.Auth;
using Nomina.Web.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
var connectionString = builder.Configuration.GetConnectionString("Nomina");
builder.Services.AddDbContext<NominaDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddIdentity<Person, IdentityRole>(options =>
{
    options.Password.RequiredLength = 12;
}).AddEntityFrameworkStores<NominaDbContext>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/cuenta/ingresar";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy =
    builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always);
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);

var app = builder.Build();
await BootstrapAdmin.RunAsync(app.Services, app.Configuration);
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps && !HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = 403;
            return;
        }
        await next(context);
    });
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/db", async (HttpContext context, CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Json(new { status = "unavailable" }, statusCode: 503);
    var db = context.RequestServices.GetRequiredService<NominaDbContext>();

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(5));
    return await db.Database.CanConnectAsync(timeout.Token)
        ? Results.Ok(new { status = "ok" })
        : Results.Json(new { status = "unavailable" }, statusCode: 503);
});
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/cuenta/ingresar"));
app.Run();

public partial class Program;

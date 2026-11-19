using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Nomina.Web.Auth;
using Nomina.Web.Data;
using Xunit;

namespace Nomina.Tests;

public sealed class SqlReleaseTests
{
    private const string Initial = "20261119180245_InitialSchema";
    private static readonly string Password = "Nomina!1" + Guid.NewGuid().ToString("N");

    [ReleaseSqlFact]
    public async Task UpgradePreservesHistoryAndEnforcesRealIdentityScopes()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NOMINA_TEST_SQL"));
        Assert.True(connection.DataSource.StartsWith("127.0.0.1,", StringComparison.Ordinal),
            "This suite only creates disposable databases on loopback.");
        connection.InitialCatalog = "NominaRelease_" + Guid.NewGuid().ToString("N");
        using var root = new WebApplicationFactory<Program>();
        using var app = root.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Nomina", connection.ConnectionString));
        string workerId, outsideId, leadId, crewId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NominaDbContext>();
            await db.Database.MigrateAsync(Initial);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Person>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { Roles.Admin, Roles.Lead, Roles.Worker, Roles.Accountant })
                Assert.True((await roles.CreateAsync(new(role))).Succeeded);
            var admin = await Create(users, "AdminDemo", Roles.Admin, 0);
            var lead = await Create(users, "LeadDemo", Roles.Lead, 1_000_000);
            var worker = await Create(users, "WorkerDemo", Roles.Worker, 1_500_000);
            var outside = await Create(users, "OutsideDemo", Roles.Worker, 1_800_000);
            await Create(users, "AccountantDemo", Roles.Accountant, 1_400_000);
            var crew = new Crew { Name = "Norte", LeadId = lead.Id };
            db.Crews.Add(crew);
            await db.SaveChangesAsync();
            worker.CrewId = crew.Id;
            workerId = worker.Id;
            outsideId = outside.Id;
            leadId = lead.Id;
            crewId = crew.Id.ToString();
            db.WorkEvents.Add(new() { Date = new(2025, 1, 3), Kind = WorkEventKind.Sick, PersonId = workerId });
            db.PayrollPeriods.Add(new()
            {
                Year = 2025, Month = 1, ClosedAt = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero),
                ClosedById = admin.Id, RuleVersion = "v1", RuleParameters = "reglas originales",
                Total = 3_283_333m,
                Lines =
                [
                    new() { PersonId = workerId, PersonName = worker.FirstName, Document = worker.Document,
                        MonthlySalary = 1_500_000m, SicknessAdjustment = -16_667m, SickDays = 1, Total = 1_483_333m },
                    new() { PersonId = outsideId, PersonName = outside.FirstName, Document = outside.Document,
                        MonthlySalary = 1_800_000m, Total = 1_800_000m }
                ]
            });
            await db.SaveChangesAsync();
        }

        using var adminClient = await Login(app, "AdminDemo");
        var before = await adminClient.GetStringAsync("/api/nomina/2025/1");
        var csvBefore = await adminClient.GetByteArrayAsync("/nomina/2025/1/csv");
        string closedId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NominaDbContext>();
            var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
            var batches = Regex.Split(script, @"(?im)^\s*GO\s*$").Where(batch => !string.IsNullOrWhiteSpace(batch)).ToArray();
            await db.Database.OpenConnectionAsync();
            foreach (var batch in batches.Concat(batches))
                await db.Database.ExecuteSqlRawAsync(batch);
            await db.Database.CloseConnectionAsync();
            var employee = await db.Users.SingleAsync(p => p.Id == workerId);
            employee.MonthlySalary = 2_200_000;
            await db.SaveChangesAsync();
            closedId = (await db.WorkEvents.SingleAsync()).Id.ToString();
        }
        Assert.Equal(before, await adminClient.GetStringAsync("/api/nomina/2025/1"));
        Assert.Equal(csvBefore, await adminClient.GetByteArrayAsync("/nomina/2025/1/csv"));
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync($"/cuadrillas/{crewId}/editar")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Form(adminClient, "/cuadrillas/nueva", "/cuadrillas/nueva",
            ("Name", "Sur"), ("LeadId", leadId), ("MemberIds", outsideId))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Form(adminClient, $"/cuadrillas/{crewId}/editar", $"/cuadrillas/{crewId}/editar",
            ("Name", "Norte"), ("LeadId", leadId), ("MemberIds", workerId))).StatusCode);
        // Sur is assigned to the same demo lead so that its withdrawal can be checked as well.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NominaDbContext>();
            var outside = await db.Users.SingleAsync(p => p.Id == outsideId);
            Assert.NotNull(outside.CrewId);
            outside.CrewId = null;
            await db.SaveChangesAsync();
        }

        using var leadClient = await Login(app, "LeadDemo");
        Assert.Equal(HttpStatusCode.Forbidden, (await leadClient.GetAsync("/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leadClient.GetAsync("/api/nomina/2025/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Write(leadClient, HttpMethod.Post, "/api/novedades",
            new { personId = outsideId, date = "2025-02-04", kind = 2 })).StatusCode);
        var logged = await Write(leadClient, HttpMethod.Post, "/api/novedades",
            new { personId = workerId, date = "2025-02-04", kind = 2 });
        Assert.Equal(HttpStatusCode.Created, logged.StatusCode);
        var entry = (await logged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await Write(leadClient, HttpMethod.Post, "/api/novedades",
            new { personId = workerId, date = "2025-02-04", kind = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Write(leadClient, HttpMethod.Patch, $"/api/novedades/{entry}",
            new { previousKind = 2, kind = 3, reason = "Validación del supervisor" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(leadClient, HttpMethod.Patch, $"/api/novedades/{closedId}",
            new { previousKind = 2, kind = 3, reason = "No debe guardarse" })).StatusCode);
        Assert.Contains("Validación del supervisor", WebUtility.HtmlDecode(await leadClient.GetStringAsync($"/novedades/{entry}/corregir")));

        using var workerClient = await Login(app, "WorkerDemo");
        var mine = await workerClient.GetFromJsonAsync<JsonElement>("/api/nomina/2025/1");
        Assert.Single(mine.GetProperty("lines").EnumerateArray());
        Assert.Equal(1_483_333m, mine.GetProperty("total").GetDecimal());
        Assert.Equal(HttpStatusCode.Forbidden, (await workerClient.GetAsync("/nomina/2025/1/csv")).StatusCode);
        var estimate = await workerClient.GetFromJsonAsync<JsonElement>("/api/nomina/2025/2");
        Assert.Single(estimate.GetProperty("lines").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, estimate.GetProperty("revision").ValueKind);
        using var accountant = await Login(app, "AccountantDemo");
        Assert.Equal(csvBefore, await accountant.GetByteArrayAsync("/nomina/2025/1/csv"));
        Assert.Equal(HttpStatusCode.NotFound, (await accountant.GetAsync("/api/nomina/2025/2")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsJsonAsync("/api/nomina/cerrar", new { })).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await Form(adminClient, "/roles/nuevo", "/roles/nuevo",
            ("Name", "ConsultaDemo"), ("Permissions", Access.PayrollRead), ("Permissions", Access.PayrollExport))).StatusCode);
        string roleId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NominaDbContext>();
            roleId = (await db.Roles.SingleAsync(r => r.Name == "ConsultaDemo")).Id;
        }
        Assert.Equal(HttpStatusCode.Redirect, (await Form(adminClient, "/personas/nueva", "/personas/nueva",
            ("FirstName", "Analista"), ("LastName", "Prueba"), ("Document", "DEMO-ANALISTA"),
            ("Address", "Calle ficticia"), ("Phone", "00001"), ("UserName", "AnalistaDemo"),
            ("Password", Password), ("Role", "ConsultaDemo"), ("IsActive", "true"), ("MonthlySalary", "1200000"))).StatusCode);
        using var analyst = await Login(app, "AnalistaDemo");
        Assert.Equal(csvBefore, await analyst.GetByteArrayAsync("/nomina/2025/1/csv"));
        var roleEditor = $"/roles/{roleId}/editar";
        var stamp = await Field(adminClient, roleEditor, "Stamp");
        Assert.Equal(HttpStatusCode.Redirect, (await Form(adminClient, roleEditor, roleEditor,
            ("Name", "ConsultaDemo"), ("Stamp", stamp), ("Permissions", Access.PayrollRead))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync("/nomina/2025/1/csv")).StatusCode);
        stamp = await Field(adminClient, roleEditor, "Stamp");
        Assert.Equal(HttpStatusCode.Conflict, (await Form(adminClient, roleEditor, $"/roles/{roleId}/eliminar",
            ("stamp", stamp))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Form(adminClient, "/roles/nuevo", "/roles/nuevo",
            ("Name", "SinAsignarDemo"))).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NominaDbContext>();
            var unused = await db.Roles.SingleAsync(r => r.Name == "SinAsignarDemo");
            Assert.Equal(HttpStatusCode.Redirect, (await Form(adminClient, $"/roles/{unused.Id}/editar",
                $"/roles/{unused.Id}/eliminar", ("stamp", unused.ConcurrencyStamp!))).StatusCode);
            Assert.False(await db.Roles.AnyAsync(r => r.Id == unused.Id));
        }

        Assert.Equal(HttpStatusCode.Redirect, (await Form(adminClient, $"/personas/{workerId}/contactos", $"/personas/{workerId}/contactos",
            ("Addresses", "Calle 1\nCalle 2"), ("Phones", "100\n200"))).StatusCode);
        Assert.Contains("Calle 2", await adminClient.GetStringAsync($"/personas/{workerId}/contactos"));
        Assert.Equal(HttpStatusCode.Redirect, (await Form(adminClient, "/actividades", "/actividades/guardar",
            ("Title", "Inspección de prueba"), ("AssigneeId", workerId))).StatusCode);
        var board = WebUtility.HtmlDecode(await workerClient.GetStringAsync("/actividades"));
        Assert.Contains("Inspección de prueba", board);
        var move = Regex.Match(board, "action=\"(/actividades/\\d+/estado)\"").Groups[1].Value;
        Assert.NotEmpty(move);
        Assert.Equal(HttpStatusCode.Redirect, (await Form(workerClient, "/actividades", move, ("next", "2"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("/actividades")).StatusCode);

        var preview = await adminClient.GetFromJsonAsync<JsonElement>("/api/nomina/2025/2");
        var revision = preview.GetProperty("revision").GetString();
        Assert.False(string.IsNullOrWhiteSpace(revision));
        Assert.Equal(HttpStatusCode.OK, (await Write(adminClient, HttpMethod.Post, "/api/nomina/cerrar",
            new { year = 2025, month = 2, revision })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(adminClient, HttpMethod.Post, "/api/nomina/cerrar",
            new { year = 2025, month = 2, revision })).StatusCode);
        Assert.Equal(before, await adminClient.GetStringAsync("/api/nomina/2025/1"));
    }

    private static async Task<Person> Create(UserManager<Person> users, string name, string role, decimal salary)
    {
        var person = new Person { UserName = name, FirstName = name, Document = name, MonthlySalary = salary };
        Assert.True((await users.CreateAsync(person, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(person, role)).Succeeded);
        return person;
    }

    private static async Task<HttpClient> Login(WebApplicationFactory<Program> app, string user)
    {
        var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var result = await Form(client, "/cuenta/ingresar", "/cuenta/ingresar", ("UserName", user), ("Password", Password));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        return client;
    }

    private static async Task<HttpResponseMessage> Form(HttpClient client, string view, string action,
        params (string Key, string Value)[] fields)
    {
        var token = await Field(client, view, "__RequestVerificationToken");
        var values = fields.Append(("__RequestVerificationToken", token)).Select(x => KeyValuePair.Create(x.Item1, x.Item2));
        using var content = new FormUrlEncodedContent(values);
        return await client.PostAsync(action, content);
    }

    private static async Task<string> Field(HttpClient client, string path, string name)
    {
        var page = await client.GetStringAsync(path);
        var match = Regex.Match(page, $"<input\\b(?=[^>]*\\bname=\"{Regex.Escape(name)}\")(?=[^>]*\\bvalue=\"([^\"]*)\")[^>]*>");
        Assert.True(match.Success, $"Missing field {name} in {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> Write(HttpClient client, HttpMethod method, string path, object body)
    {
        var antiforgery = await client.GetFromJsonAsync<JsonElement>("/api/novedades/token");
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add(antiforgery.GetProperty("header").GetString()!, antiforgery.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
}

public sealed class ReleaseSqlFactAttribute : FactAttribute
{
    public ReleaseSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NOMINA_TEST_SQL")))
            Skip = "Set NOMINA_TEST_SQL to an isolated local SQL Server to run the release test.";
    }
}

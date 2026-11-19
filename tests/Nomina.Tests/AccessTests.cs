using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nomina.Web.Auth;
using Xunit;

namespace Nomina.Tests;

public sealed class AccessTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData(Roles.Worker, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Admin, HttpStatusCode.OK)]
    public async Task AdministrationChecksRole(string role, HttpStatusCode expected)
    {
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultForbidScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestRoleHandler>("Test", _ => { })));
        using var client = CreateClient(app);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        using var response = await client.GetAsync("/cuenta/administracion");
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("/personas/nueva")]
    [InlineData("/personas/demo-id/editar")]
    [InlineData("/personas/demo-id/eliminar")]
    [InlineData("/novedades")]
    [InlineData("/api/novedades")]
    public async Task WorkerCannotOpenOrSubmitProtectedRoutes(string path)
    {
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultForbidScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestRoleHandler>("Test", _ => { })));
        using var client = CreateClient(app);
        client.DefaultRequestHeaders.Add("X-Test-Role", Roles.Worker);
        using var form = new FormUrlEncodedContent([]);
        using var get = await client.GetAsync(path);
        using var post = await client.PostAsync(path, form);
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
    }

    [Fact]
    public async Task AnonymousUserIsRedirectedToLogin()
    {
        using var client = CreateClient(factory);
        using var response = await client.GetAsync("/cuenta/administracion");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/cuenta/ingresar", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task LoginRendersFormAndRejectsPostWithoutAntiforgeryToken()
    {
        using var client = CreateClient(factory);
        using var page = await client.GetAsync("/cuenta/ingresar");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("__RequestVerificationToken", await page.Content.ReadAsStringAsync());
        using var form = new FormUrlEncodedContent([]);
        using var response = await client.PostAsync("/cuenta/ingresar", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> app) =>
        app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    private sealed class TestRoleHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "test"), new Claim(ClaimTypes.Role, role)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}

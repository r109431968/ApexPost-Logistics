using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Authentication;

public class AuthenticationTests : IClassFixture<ApexFactory>
{
    private readonly ApexFactory _factory;

    public AuthenticationTests(ApexFactory factory) => _factory = factory;

    [Fact]
    public async Task Unauthenticated_access_redirects_to_the_login_page()
    {
        var client = _factory.CreateBrowser();

        var response = await client.GetAsync("/Dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Every_protected_area_requires_a_login()
    {
        var client = _factory.CreateBrowser();

        foreach (var path in new[] { "/Catalogue", "/Transactions", "/Reports", "/Audit", "/Notifications" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task A_wrong_password_is_rejected_with_one_generic_message()
    {
        var client = _factory.CreateBrowser();

        var wrongPassword = await client.LoginAsync(ApexFactory.ClerkEmail, "wrong-password");
        var unknownUser = await client.LoginAsync("nobody@apexpost.test", "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        var first = await wrongPassword.Content.ReadAsStringAsync();
        var second = await unknownUser.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", first);
        Assert.Contains("Invalid email or password.", second);
    }

    [Fact]
    public async Task A_correct_login_sets_a_hardened_cookie_and_redirects_to_the_dashboard()
    {
        var client = _factory.CreateBrowser();

        var response = await client.LoginAsync(ApexFactory.ClerkEmail, ApexFactory.DemoPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Dashboard", response.Headers.Location!.ToString());

        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(".ApexPost.Auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_form_post_without_an_antiforgery_token_is_rejected()
    {
        var client = _factory.CreateBrowser();

        var response = await client.PostFormAsync(
            "/Account/Login",
            new Dictionary<string, string> { ["Email"] = ApexFactory.ClerkEmail, ["Password"] = ApexFactory.DemoPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_signed_in_clerk_sees_the_dashboard_for_their_own_branch()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ClerkEmail);

        var page = await client.GetStringAsync("/Dashboard");

        Assert.Contains("BR001", page);
    }

    [Fact]
    public async Task A_clerk_cannot_look_at_another_branch()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ClerkEmail);

        var response = await client.GetAsync("/Dashboard?branch=BR002");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_auditor_can_look_at_another_branch()
    {
        var client = await _factory.SignedInAsync(ApexFactory.AuditorEmail);

        var response = await client.GetAsync("/Dashboard?branch=BR002");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_clerk_gets_403_on_a_void()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ClerkEmail);

        var response = await client.PostWithTokenAsync(
            "/Transactions/New",
            $"/Transactions/Void/{Guid.NewGuid()}",
            new Dictionary<string, string> { ["Reason"] = "customer changed mind" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_clerk_gets_403_on_the_administrator_pages_and_an_administrator_gets_200()
    {
        var clerk = await _factory.SignedInAsync(ApexFactory.ClerkEmail);
        var admin = await _factory.SignedInAsync(ApexFactory.AdminEmail);

        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.GetAsync("/Staff")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Staff")).StatusCode);
    }

    [Fact]
    public async Task An_administrator_cannot_open_the_sales_screen()
    {
        var admin = await _factory.SignedInAsync(ApexFactory.AdminEmail);

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/Transactions")).StatusCode);
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ClerkEmail);

        var logout = await client.PostWithTokenAsync("/Dashboard", "/Account/Logout", new Dictionary<string, string>());
        var after = await client.GetAsync("/Dashboard");

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains("/Account/Login", after.Headers.Location!.ToString());
    }
}

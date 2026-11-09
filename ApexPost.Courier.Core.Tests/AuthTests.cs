using System.Net;
using System.Text;
using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Auth;

namespace ApexPost.Courier.Core.Tests;

public class AuthTests
{
    /// <summary>A token with the claims the API puts in it. The signature is not checked by the app, so it is a dummy.</summary>
    public static string MakeToken(string sub = "courier007", string? role = "Courier", string? courierId = "courier-007", long? exp = null)
    {
        static string B64(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var claims = new List<string> { $"\"sub\":\"{sub}\"" };
        if (role is not null) claims.Add($"\"role\":\"{role}\"");
        if (courierId is not null) claims.Add($"\"courierId\":\"{courierId}\"");
        claims.Add($"\"exp\":{exp ?? DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds()}");
        return $"{B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{B64("{" + string.Join(",", claims) + "}")}.signature";
    }

    [Fact]
    public void The_claims_of_a_courier_token_are_read()
    {
        var info = JwtClaimReader.Read(MakeToken());

        Assert.NotNull(info);
        Assert.Equal("courier007", info.UserName);
        Assert.Equal("Courier", info.Role);
        Assert.Equal("courier-007", info.CourierId);
        Assert.False(info.IsExpired(DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b")]
    [InlineData("a.!!!.c")]
    public void Garbage_is_not_a_token(string? text) => Assert.Null(JwtClaimReader.Read(text));

    [Fact]
    public void An_old_exp_is_expired()
    {
        var info = JwtClaimReader.Read(MakeToken(exp: DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()));

        Assert.True(info!.IsExpired(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task The_handler_sends_the_saved_token()
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Json(HttpStatusCode.OK, Http.UploadOk)));
        using var harness = new Harness(network, token: "my.saved.token");

        await harness.Client.UploadScansAsync("APX-12345678", "courier-007", []);

        Assert.Equal("Bearer", network.Requests[0].Headers.Authorization!.Scheme);
        Assert.Equal("my.saved.token", network.Requests[0].Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task A_401_clears_the_token_and_tells_the_app_to_go_to_login()
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Problem(HttpStatusCode.Unauthorized, "Unauthorized")));
        using var harness = new Harness(network, token: "expired.token.value");
        var expired = 0;
        harness.Session.SessionExpired += (_, _) => expired++;

        var result = await harness.Client.UploadScansAsync("APX-12345678", "courier-007", []);

        Assert.Equal(ApiFailureKind.Unauthorized, result.Error!.Kind);
        Assert.Null(await harness.Tokens.GetAsync());
        Assert.Equal(1, expired);
    }

    [Fact]
    public async Task Signing_in_keeps_the_token_and_the_courier_id()
    {
        var token = MakeToken();
        var network = new StubHandler((_, _) => Task.FromResult(Http.Json(HttpStatusCode.OK, $$"""{"accessToken":"{{token}}","tokenType":"Bearer","expiresInSeconds":900}""")));
        using var harness = new Harness(network);

        var outcome = await harness.Auth.SignInAsync("courier007", "secret");

        Assert.True(outcome.Succeeded);
        Assert.Equal("courier-007", harness.Auth.Current!.CourierId);
        Assert.Equal(token, await harness.Tokens.GetAsync());
    }

    [Fact]
    public async Task An_admin_token_is_not_accepted_by_the_courier_app()
    {
        var token = MakeToken(sub: "admin", role: "Admin", courierId: null);
        var network = new StubHandler((_, _) => Task.FromResult(Http.Json(HttpStatusCode.OK, $$"""{"accessToken":"{{token}}","tokenType":"Bearer","expiresInSeconds":900}""")));
        using var harness = new Harness(network);

        var outcome = await harness.Auth.SignInAsync("admin", "secret");

        Assert.False(outcome.Succeeded);
        Assert.Null(harness.Auth.Current);
        Assert.Null(await harness.Tokens.GetAsync());
    }

    [Fact]
    public async Task A_wrong_password_gives_a_plain_message()
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Problem(HttpStatusCode.Unauthorized, "Sign-in failed")));
        using var harness = new Harness(network);

        var outcome = await harness.Auth.SignInAsync("courier007", "wrong");

        Assert.False(outcome.Succeeded);
        Assert.Equal("The user name or the password is wrong.", outcome.Message);
    }

    [Fact]
    public async Task Signing_in_while_offline_says_so()
    {
        var network = new StubHandler((_, _) => throw new HttpRequestException("no route to host"));
        using var harness = new Harness(network);

        var outcome = await harness.Auth.SignInAsync("courier007", "secret");

        Assert.False(outcome.Succeeded);
        Assert.Contains("not reachable", outcome.Message);
    }

    [Fact]
    public async Task A_saved_valid_token_restores_the_session_and_an_expired_one_does_not()
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Json(HttpStatusCode.OK, "{}")));
        using var good = new Harness(network, token: MakeToken());
        Assert.True(await good.Auth.TryRestoreAsync());
        Assert.Equal("courier-007", good.Auth.Current!.CourierId);

        using var stale = new Harness(network, token: MakeToken(exp: DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds()));
        Assert.False(await stale.Auth.TryRestoreAsync());
        Assert.Null(await stale.Tokens.GetAsync());
    }
}

using System.Security.Claims;
using ApexPost.Api.Security;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.UnitTests.Api;

/// <summary>The rules of Part D that need no web server: token content and checks, the courierId rule, the demo users and the key comparison.</summary>
public class ApiSecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private const string Key = "unit-test-signing-key-0123456789-abcdef";

    private static JwtSettings Settings(string key = Key, int minutes = 15) =>
        new("apexpost-api", "apexpost-clients", TimeSpan.FromMinutes(minutes), System.Text.Encoding.UTF8.GetBytes(key), keyIsEphemeral: false);

    // The same rules the API applies (see SecurityRegistration). The test token is dated 2026-11-10, so most tests switch the lifetime check off.
    private static TokenValidationParameters ValidationFor(JwtSettings settings, bool validateLifetime = false, string? audience = null) => new()
    {
        ValidateIssuer = true, ValidIssuer = settings.Issuer,
        ValidateAudience = true, ValidAudience = audience ?? settings.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = settings.SigningKey,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = validateLifetime, ClockSkew = TimeSpan.Zero,
        NameClaimType = "sub", RoleClaimType = "role"
    };

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static ClaimsPrincipal Principal(string role, string? courierId)
    {
        var claims = new List<Claim> { new("role", role) };
        if (courierId is not null)
            claims.Add(new Claim(ApiClaims.CourierId, courierId));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "sub", "role"));
    }

    // ---- tokens ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_courier_token_carries_sub_role_and_courierId()
    {
        var settings = Settings();
        var token = new TokenService(settings, new FixedClock(Now)).Issue(new DemoUser("courier007", ApiRoles.Courier, "courier-007"));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.AccessToken, ValidationFor(settings));

        Assert.True(result.IsValid);
        Assert.Equal("courier007", result.Claims["sub"]);
        Assert.Equal("Courier", result.Claims["role"]);
        Assert.Equal("courier-007", result.Claims[ApiClaims.CourierId]);
        Assert.Equal(900, token.ExpiresInSeconds);
        Assert.Equal(Now.AddMinutes(15), token.ExpiresAtUtc);
    }

    [Fact]
    public async Task An_admin_token_has_no_courierId()
    {
        var settings = Settings();
        var token = new TokenService(settings, new FixedClock(Now)).Issue(new DemoUser("admin", ApiRoles.Admin, null));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.AccessToken, ValidationFor(settings));

        Assert.True(result.IsValid);
        Assert.False(result.Claims.ContainsKey(ApiClaims.CourierId));
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_refused()
    {
        var forged = new TokenService(Settings("another-signing-key-0123456789-abcdef!!"), new FixedClock(Now))
            .Issue(new DemoUser("admin", ApiRoles.Admin, null));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(forged.AccessToken, ValidationFor(Settings()));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        // Issued two hours ago with a 15 minute lifetime: expired long before the real clock of this test.
        var settings = Settings();
        var old = new TokenService(settings, new FixedClock(DateTimeOffset.UtcNow.AddHours(-2))).Issue(new DemoUser("admin", ApiRoles.Admin, null));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(old.AccessToken, ValidationFor(settings, validateLifetime: true));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_token_for_another_audience_is_refused()
    {
        var settings = Settings();
        var token = new TokenService(settings, new FixedClock(Now)).Issue(new DemoUser("admin", ApiRoles.Admin, null));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.AccessToken, ValidationFor(settings, audience: "someone-else"));

        Assert.False(result.IsValid);
    }

    // ---- the courierId rule ---------------------------------------------------------------------------------------

    [Fact]
    public void A_courier_may_upload_only_for_their_own_courierId()
    {
        var courier = Principal(ApiRoles.Courier, "courier-007");

        Assert.True(courier.MayUploadFor("courier-007"));
        Assert.False(courier.MayUploadFor("courier-008"));
        Assert.False(courier.MayUploadFor("COURIER-007"));   // exact match, no case tricks
        Assert.False(courier.MayUploadFor(""));
    }

    [Fact]
    public void An_admin_may_upload_for_anyone()
    {
        Assert.True(Principal(ApiRoles.Admin, null).MayUploadFor("courier-007"));
    }

    [Fact]
    public void A_courier_without_a_courierId_claim_and_a_partner_may_not_upload()
    {
        Assert.False(Principal(ApiRoles.Courier, null).MayUploadFor("courier-007"));
        Assert.False(Principal(ApiRoles.Partner, null).MayUploadFor("courier-007"));
    }

    // ---- demo users and secrets -----------------------------------------------------------------------------------

    [Fact]
    public void The_right_name_and_password_sign_in_and_the_courier_gets_a_courierId()
    {
        var users = new DemoUserDirectory(Config(("Auth:DemoPassword", "a-demo-password")));

        var courier = users.Validate("COURIER007", "a-demo-password");   // names are not case sensitive

        Assert.NotNull(courier);
        Assert.Equal(ApiRoles.Courier, courier.Role);
        Assert.Equal("courier-007", courier.CourierId);
        Assert.Equal(ApiRoles.Admin, users.Validate("admin", "a-demo-password")!.Role);
    }

    [Fact]
    public void A_wrong_password_or_an_unknown_name_is_refused()
    {
        var users = new DemoUserDirectory(Config(("Auth:DemoPassword", "a-demo-password")));

        Assert.Null(users.Validate("admin", "wrong"));
        Assert.Null(users.Validate("nobody", "a-demo-password"));
    }

    [Fact]
    public void Without_a_configured_password_nobody_can_sign_in()
    {
        Assert.Null(new DemoUserDirectory(Config()).Validate("admin", ""));
        Assert.Null(new DemoUserDirectory(Config()).Validate("admin", "anything"));
    }

    [Fact]
    public void Secrets_match_only_when_equal()
    {
        Assert.True(DemoUserDirectory.SecretsMatch("apx_123", "apx_123"));
        Assert.False(DemoUserDirectory.SecretsMatch("apx_123", "apx_124"));
        Assert.False(DemoUserDirectory.SecretsMatch("", "apx_123"));
    }

    // ---- signing key ----------------------------------------------------------------------------------------------

    [Fact]
    public void Outside_Development_a_missing_signing_key_stops_the_start()
    {
        Assert.Throws<InvalidOperationException>(() => JwtSettings.From(Config(), new FakeEnvironment(Environments.Production)));
    }

    [Fact]
    public void In_Development_a_missing_signing_key_gives_a_random_key_for_this_run()
    {
        var first = JwtSettings.From(Config(), new FakeEnvironment(Environments.Development));
        var second = JwtSettings.From(Config(), new FakeEnvironment(Environments.Development));

        Assert.True(first.KeyIsEphemeral);
        Assert.NotEqual(first.SigningKey.Key, second.SigningKey.Key);
    }

    [Fact]
    public void A_short_signing_key_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JwtSettings.From(Config(("Auth:Jwt:SigningKey", "too-short")), new FakeEnvironment(Environments.Production)));
    }

    [Fact]
    public void A_configured_key_issuer_audience_and_lifetime_are_used()
    {
        var settings = JwtSettings.From(
            Config(("Auth:Jwt:SigningKey", Key), ("Auth:Jwt:Issuer", "issuer-x"), ("Auth:Jwt:Audience", "audience-x"), ("Auth:Jwt:LifetimeMinutes", "5")),
            new FakeEnvironment(Environments.Production));

        Assert.False(settings.KeyIsEphemeral);
        Assert.Equal("issuer-x", settings.Issuer);
        Assert.Equal("audience-x", settings.Audience);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.Lifetime);
    }
}

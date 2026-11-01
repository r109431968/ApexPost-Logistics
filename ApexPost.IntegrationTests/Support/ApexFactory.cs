using System.Net;
using System.Text.RegularExpressions;
using ApexPost.Infrastructure.Persistence.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Support;

/// <summary>
/// Starts the real application in memory against its OWN disposable SQL Server database (ApexPostDb_Test_&lt;guid&gt;),
/// with fast password hashing and test settings. The database is created by the real migrations, filled by the real
/// seeder, and dropped when the factory is disposed. Nothing is mocked: the real middleware pipeline, filters,
/// services, EF Core and SQL Server run. The server is LocalDB with Windows authentication (no password anywhere);
/// set the environment variable APEXPOST_TEST_SERVER to use another server with Windows authentication.
/// </summary>
public sealed class ApexFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Test-Passw0rd-2026!";
    public const string ApiKey = "integration-test-key-123";
    public const string ApprovedOrigin = "https://tracking.partner.example";

    public const string ClerkEmail = "clerk1.br001@apexpost.test";
    public const string ManagerEmail = "manager.br001@apexpost.test";
    public const string AdminEmail = "admin1@apexpost.test";
    public const string AuditorEmail = "auditor1@apexpost.test";

    private readonly Dictionary<string, string?> _overrides;

    public ApexFactory() : this(null) { }

    internal ApexFactory(Dictionary<string, string?>? overrides)
    {
        _overrides = overrides ?? new Dictionary<string, string?>();
    }

    private static string SqlServerName =>
       Environment.GetEnvironmentVariable("APEXPOST_TEST_SERVER") ?? @"(localdb)\MSSQLLocalDB";

    /// <summary>A new database name per factory, so test classes never share or disturb each other's data.</summary>
    public string DatabaseName { get; } = "ApexPostDb_Test_" + Guid.NewGuid().ToString("N");

    public string ConnectionString =>
        $"Server={SqlServerName};Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>Opens a new DbContext on the test database (for assertions that look inside the database).</summary>
    public ApexPostDbContext CreateDbContext() =>
        Services.GetRequiredService<IDbContextFactory<ApexPostDbContext>>().CreateDbContext();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApexPostDb"] = ConnectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Seed:Enabled"] = "true",
                // 40 transactions are enough for the app tests. The volume test starts its own factory with more.
                ["Seed:TransactionCount"] = "40",
                ["Seed:DemoPassword"] = DemoPassword,
                ["Passwords:Iterations"] = "1000",
                ["Integration:ApiKeys:TestPartner"] = ApiKey,
                ["Courier:TimeoutMilliseconds"] = "400",
                ["Courier:SimulatedLatencyMilliseconds"] = "0",
                // High by default so ordinary tests never hit the limit. Rate limit tests lower it.
                ["RateLimiting:LoginPermitLimit"] = "1000",
                ["RateLimiting:PartnerPermitLimit"] = "1000",
                ["Integration:ApiKeys:TestPartner"] = ApiKey
            };

            foreach (var pair in _overrides)
                settings[pair.Key] = pair.Value;

            configuration.AddInMemoryCollection(settings);
        });

        // Gives each request a fake remote address, because the in-memory test server has none.
        // Tests choose the address with the X-Test-Ip header (default 127.0.0.1).
        builder.ConfigureTestServices(services => services.AddTransient<IStartupFilter, FakeRemoteIpStartupFilter>());
    }

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    public async Task<HttpClient> SignedInAsync(string email)
    {
        var client = CreateBrowser();
        var response = await client.LoginAsync(email, DemoPassword);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        DropDatabase();
    }

    private void DropDatabase()
    {
        try
        {
            SqlConnection.ClearAllPools();

            using var connection = new SqlConnection(
                $"Server={SqlServerName};Database=master;Trusted_Connection=True;TrustServerCertificate=True");
            connection.Open();

            using var command = connection.CreateCommand();
            // The name is generated in this class (ApexPostDb_Test_ + hex digits), never typed by a user.
            command.CommandText = $@"
                IF DB_ID(N'{DatabaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{DatabaseName}];
                END";
            command.ExecuteNonQuery();
        }
        catch (SqlException)
        {
            // Cleanup is best effort. A leftover ApexPostDb_Test_* database can be dropped by hand in SSMS.
        }
    }
}

public sealed class FakeRemoteIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, following) =>
        {
            var header = context.Request.Headers["X-Test-Ip"].ToString();
            context.Connection.RemoteIpAddress = IPAddress.TryParse(header, out var address)
                ? address
                : IPAddress.Loopback;

            await following();
        });

        next(app);
    };
}

public static partial class HttpExtensions
{
    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"<option value=""([0-9a-fA-F\-]{36})"">")]
    private static partial Regex GuidOptionPattern();

    public static async Task<string> AntiforgeryTokenAsync(this HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = TokenPattern().Match(html);
        Assert.True(match.Success, $"No antiforgery token found on {path}.");
        return match.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password)
    {
        var token = await client.AntiforgeryTokenAsync("/Account/Login");
        return await client.PostFormAsync("/Account/Login", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        });
    }

    public static Task<HttpResponseMessage> PostFormAsync(
        this HttpClient client, string path, Dictionary<string, string> fields, string? referer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
        if (referer is not null)
            request.Headers.Referrer = new Uri(referer);
        return client.SendAsync(request);
    }

    /// <summary>Posts a form from a signed-in browser, fetching a fresh antiforgery token from a page first.</summary>
    public static async Task<HttpResponseMessage> PostWithTokenAsync(
        this HttpClient client, string tokenPage, string path, Dictionary<string, string> fields, string? referer = null)
    {
        fields["__RequestVerificationToken"] = await client.AntiforgeryTokenAsync(tokenPage);
        return await client.PostFormAsync(path, fields, referer);
    }

    /// <summary>Opens a transaction at the signed-in user's branch and returns its ID.</summary>
    public static async Task<Guid> OpenTransactionAsync(this HttpClient client)
    {
        var page = await client.GetStringAsync("/Transactions/New");
        var counterId = GuidOptionPattern().Match(page).Groups[1].Value;
        Assert.NotEmpty(counterId);

        var response = await client.PostWithTokenAsync(
            "/Transactions/New", "/Transactions/Open", new Dictionary<string, string> { ["counterId"] = counterId });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        return Guid.Parse(location[(location.LastIndexOf('/') + 1)..]);
    }
}

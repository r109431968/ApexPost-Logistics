using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Specs.Support;

/// <summary>
/// The real ApexPost.Api in memory, with its OWN disposable SQL Server test database per scenario
/// (ApexPostDb_Specs_&lt;guid&gt;, created by the real migrations, dropped when the scenario ends).
/// Same idea as ApexPost.Api.IntegrationTests; no password and no real secret is stored anywhere.
/// </summary>
public sealed class SpecsApiFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Test-Passw0rd-2026!";
    private const string SigningKey = "test-only-signing-key-0123456789-abcdef";

    private static string SqlServerName =>
        Environment.GetEnvironmentVariable("APEXPOST_TEST_SERVER") ?? @"(localdb)\MSSQLLocalDB";

    public string DatabaseName { get; } = "ApexPostDb_Specs_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApexPostDb"] =
                    $"Server={SqlServerName};Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True",
                ["Auth:DemoPassword"] = DemoPassword,
                ["Auth:Jwt:SigningKey"] = SigningKey
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        try
        {
            SqlConnection.ClearAllPools();

            using var connection = new SqlConnection(
                $"Server={SqlServerName};Database=master;Trusted_Connection=True;TrustServerCertificate=True");
            connection.Open();

            using var command = connection.CreateCommand();
            // The name is generated in this class (ApexPostDb_Specs_ + hex digits), never typed by a user.
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
            // Best effort. A leftover ApexPostDb_Specs_* database can be dropped by hand in SSMS.
        }
    }
}

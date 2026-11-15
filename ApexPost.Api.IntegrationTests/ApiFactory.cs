using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Api.IntegrationTests;

/// <summary>
/// Starts the real ApexPost.Api in memory against its OWN disposable SQL Server database (ApexPostDb_ApiTest_&lt;guid&gt;).
/// The database is created by the real migrations, filled with the fictional Development shipments (APX-10000001 to APX-10000006),
/// and dropped when the factory is disposed. The real middleware, JWT authentication, versioning, EF Core and SQL Server run;
/// nothing is mocked. The server is LocalDB with Windows authentication (no password anywhere). Set the environment variable
/// APEXPOST_TEST_SERVER to use another server with Windows authentication.
///
/// Test authentication: the factory sets a fictional demo password and a fictional signing key through test configuration
/// (no secret is stored in the repository). The tests sign in through the real /api/v2/auth/token endpoint and get a real JWT.
///
/// This is a separate project from ApexPost.IntegrationTests on purpose: ApexPost.Web and ApexPost.Api both declare
/// "public partial class Program" in the global namespace, so one project cannot reference both without an extern alias.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Test-Passw0rd-2026!";

    private const string SigningKey = "test-only-signing-key-0123456789-abcdef";

    private static string SqlServerName =>
        Environment.GetEnvironmentVariable("APEXPOST_TEST_SERVER") ?? @"(localdb)\MSSQLLocalDB";

    /// <summary>A new database name per factory, so test classes never share or disturb each other's data.</summary>
    public string DatabaseName { get; } = "ApexPostDb_ApiTest_" + Guid.NewGuid().ToString("N");

    private string ConnectionString =>
        $"Server={SqlServerName};Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApexPostDb"] = ConnectionString,
                ["Auth:DemoPassword"] = DemoPassword,
                ["Auth:Jwt:SigningKey"] = SigningKey
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
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
            // The name is generated in this class (ApexPostDb_ApiTest_ + hex digits), never typed by a user.
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
            // Cleanup is best effort. A leftover ApexPostDb_ApiTest_* database can be dropped by hand in SSMS.
        }
    }
}

using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Filters;

public class IdentityVerificationTests : IClassFixture<ApexFactory>
{
    private readonly ApexFactory _factory;

    public IdentityVerificationTests(ApexFactory factory) => _factory = factory;

    [Fact]
    public async Task A_clerk_cannot_force_a_passed_result_the_system_decides()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ClerkEmail);
        var id = await client.OpenTransactionAsync();
        var details = $"/Transactions/Details/{id}";

        await client.PostWithTokenAsync(details, $"/Transactions/AddMoneyOrder/{id}", new Dictionary<string, string>
        {
            ["Sender"] = "Ann",
            ["Recipient"] = "Bob",
            ["Amount"] = "800"
        });

        // The clerk tries to send Result=Passed together with a document the system rejects.
        await client.PostWithTokenAsync(details, $"/Transactions/Verify/{id}", new Dictionary<string, string>
        {
            ["DocumentType"] = "Passport",
            ["DocumentNumber"] = "FAIL9999",
            ["Result"] = "Passed"
        });
        var afterCheck = await client.GetStringAsync(details);
        Assert.Contains("Failed", afterCheck);

        var pay = await client.PostWithTokenAsync(
            details, $"/Transactions/Pay/{id}", new Dictionary<string, string> { ["Method"] = "Cash" },
            referer: "https://localhost" + details);

        Assert.Equal(HttpStatusCode.Redirect, pay.StatusCode);
        Assert.Contains("Open", await client.GetStringAsync(details));
    }
}
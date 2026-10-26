using System.Net;
using System.Text.Json;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Filters;

public class AuditAndFilterTests : IClassFixture<ApexFactory>
{
    private readonly ApexFactory _factory;

    public AuditAndFilterTests(ApexFactory factory) => _factory = factory;

    private static async Task<List<JsonElement>> ReadAuditAsync(ApexFactory factory)
    {
        var path = Path.Combine(factory.DataDirectory, "audit.jsonl");
        if (!File.Exists(path))
            return [];

        var lines = await File.ReadAllLinesAsync(path);
        return lines.Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToList();
    }

    [Fact]
    public async Task Opening_a_transaction_writes_an_audit_entry_with_user_branch_and_correlation_id()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ClerkEmail);
        var page = await client.GetStringAsync("/Transactions/New");
        var counterId = System.Text.RegularExpressions.Regex.Match(page, "<option value=\"([0-9a-fA-F-]{36})\"").Groups[1].Value;

        var request = new HttpRequestMessage(HttpMethod.Post, "/Transactions/Open");
        request.Headers.Add("X-Correlation-Id", "audit-test-001");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["counterId"] = counterId,
            ["__RequestVerificationToken"] = await client.AntiforgeryTokenAsync("/Transactions/New")
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var entry = (await ReadAuditAsync(_factory)).Last(e => e.GetProperty("correlationId").GetString() == "audit-test-001");
        Assert.Equal("TransactionOpened", entry.GetProperty("operation").GetString());
        Assert.Equal("CounterClerk", entry.GetProperty("role").GetString());
        Assert.Equal("BR001", entry.GetProperty("branchCode").GetString());
        Assert.Equal("Transactions", entry.GetProperty("controller").GetString());
        Assert.Equal(302, entry.GetProperty("statusCode").GetInt32());
        Assert.True(entry.GetProperty("durationMs").GetInt64() >= 0);
        Assert.NotEqual(Guid.Empty, entry.GetProperty("staffId").GetGuid());
    }

    private static bool HasOutcome(JsonElement entry, string expected) =>
    entry.GetProperty("details").TryGetProperty("outcome", out var outcome)
    && outcome.GetString() == expected;

    [Fact]
    public async Task A_successful_login_and_a_failed_login_are_both_audited_without_the_password()
    {
        var good = _factory.CreateBrowser();
        await good.LoginAsync(ApexFactory.ManagerEmail, ApexFactory.DemoPassword);
        var bad = _factory.CreateBrowser();
        await bad.LoginAsync(ApexFactory.ManagerEmail, "this-is-a-wrong-password");

        var audit = await ReadAuditAsync(_factory);
        var logins = audit.Where(e => e.GetProperty("operation").GetString() == "Login").ToList();
        var raw = await File.ReadAllTextAsync(Path.Combine(_factory.DataDirectory, "audit.jsonl"));

        //Assert.Contains(logins, e => e.GetProperty("details").GetProperty("outcome").GetString() == "success");
        //Assert.Contains(logins, e => e.GetProperty("details").GetProperty("outcome").GetString() == "failed");
        Assert.Contains(logins, e => HasOutcome(e, "success"));
        Assert.Contains(logins, e => HasOutcome(e, "failed"));
        Assert.DoesNotContain(ApexFactory.DemoPassword, raw);
        Assert.DoesNotContain("this-is-a-wrong-password", raw);
    }

    [Fact]
    public async Task The_full_document_number_never_reaches_the_audit_log_or_the_page_only_the_masked_value()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ClerkEmail);
        var id = await client.OpenTransactionAsync();
        var details = $"/Transactions/Details/{id}";

        await client.PostWithTokenAsync(details, $"/Transactions/AddMoneyOrder/{id}", new Dictionary<string, string>
        {
            ["Sender"] = "Ann", ["Recipient"] = "Bob", ["Amount"] = "500"
        });
        await client.PostWithTokenAsync(details, $"/Transactions/Verify/{id}", new Dictionary<string, string>
        {
            ["DocumentType"] = "Passport", ["DocumentNumber"] = "P1234567", ["Result"] = "Passed"
        });
        var page = await client.GetStringAsync(details);

        var raw = await File.ReadAllTextAsync(Path.Combine(_factory.DataDirectory, "audit.jsonl"));
        var stored = await File.ReadAllTextAsync(Path.Combine(_factory.DataDirectory, "transactions.json"));

        Assert.Contains("IdentityVerification", raw);
        Assert.DoesNotContain("P1234567", raw);
        Assert.DoesNotContain("P1234567", stored);
        Assert.DoesNotContain("P1234567", page);
        Assert.Contains("****4567", page);
    }

    [Fact]
    public async Task A_money_order_above_the_threshold_cannot_be_paid_without_verification_and_the_user_sees_why()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ClerkEmail);
        var id = await client.OpenTransactionAsync();
        var details = $"/Transactions/Details/{id}";

        await client.PostWithTokenAsync(details, $"/Transactions/AddMoneyOrder/{id}", new Dictionary<string, string>
        {
            ["Sender"] = "Ann", ["Recipient"] = "Bob", ["Amount"] = "800"
        });
        var pay = await client.PostWithTokenAsync(
            details, $"/Transactions/Pay/{id}", new Dictionary<string, string> { ["Method"] = "Cash" },
            referer: "https://localhost" + details);

        Assert.Equal(HttpStatusCode.Redirect, pay.StatusCode);
        Assert.EndsWith(details, pay.Headers.Location!.ToString());

        var page = await client.GetStringAsync(details);
        Assert.Contains("identity verification", page);
        Assert.Contains("Open", page);
    }

    [Fact]
    public async Task An_invalid_form_goes_back_to_the_page_with_a_message_instead_of_an_error_page()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ClerkEmail);
        var id = await client.OpenTransactionAsync();
        var details = $"/Transactions/Details/{id}";

        var response = await client.PostWithTokenAsync(
            details, $"/Transactions/AddItem/{id}", new Dictionary<string, string> { ["Sku"] = "bad", ["Quantity"] = "1" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith(details, response.Headers.Location!.ToString());
        Assert.Contains("Choose a product", await client.GetStringAsync(details));
    }

    [Fact]
    public async Task The_login_form_is_redisplayed_when_the_model_is_invalid()
    {
        var client = _factory.CreateBrowser();

        var response = await client.PostWithTokenAsync(
            "/Account/Login", "/Account/Login", new Dictionary<string, string> { ["Email"] = "", ["Password"] = "" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Email is required.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_denied_action_is_audited()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ClerkEmail);
        var denied = await client.GetAsync("/Staff");

        var audit = await ReadAuditAsync(_factory);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains(audit, e => e.GetProperty("operation").GetString() == "AccessDenied");
    }

    [Fact]
    public async Task The_audit_page_is_for_auditors_and_administrators_only()
    {
        var clerk = await _factory.SignedInAsync(ApexFactory.ClerkEmail);
        var manager = await _factory.SignedInAsync(ApexFactory.ManagerEmail);
        var auditor = await _factory.SignedInAsync(ApexFactory.AuditorEmail);

        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.GetAsync("/Audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/Audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/Audit")).StatusCode);
    }

    [Fact]
    public async Task A_manager_can_void_a_paid_transaction_with_a_reason_and_it_is_audited()
    {
        var client = await _factory.SignedInAsync(ApexFactory.ManagerEmail);
        var id = await client.OpenTransactionAsync();
        var details = $"/Transactions/Details/{id}";

        await client.PostWithTokenAsync(details, $"/Transactions/AddMoneyOrder/{id}", new Dictionary<string, string>
        {
            ["Sender"] = "Ann", ["Recipient"] = "Bob", ["Amount"] = "50"
        });
        await client.PostWithTokenAsync(details, $"/Transactions/Pay/{id}", new Dictionary<string, string> { ["Method"] = "Cash" });
        var voided = await client.PostWithTokenAsync(
            details, $"/Transactions/Void/{id}", new Dictionary<string, string> { ["Reason"] = "keyed in twice" });

        Assert.Equal(HttpStatusCode.Redirect, voided.StatusCode);
        Assert.Contains("Voided", await client.GetStringAsync(details));
        Assert.Contains(
            await ReadAuditAsync(_factory),
            e => e.GetProperty("operation").GetString() == "Void" && e.GetProperty("role").GetString() == "BranchManager");
    }
}

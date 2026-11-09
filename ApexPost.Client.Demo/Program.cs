using System.Net;
using System.Net.Http.Headers;
using ApexPost.Client;
using Microsoft.Extensions.DependencyInjection;

// A console consumer of the generated client (ApexPost.Client, generated from contracts/v2.json).
// Start the API first:  dotnet run --project ApexPost.Api --launch-profile https
// The demo password is a secret. Do not type it into code: set it in the environment for this terminal only:
//   $env:APEXPOST_DEMO_PASSWORD = "<the value printed by scripts/set-dev-secrets.ps1>"

var baseUrl = args.Length > 0 ? args[0] : "https://localhost:7241/";
var demoPassword = Environment.GetEnvironmentVariable("APEXPOST_DEMO_PASSWORD")
    ?? throw new InvalidOperationException("Set the environment variable APEXPOST_DEMO_PASSWORD first (see the comment at the top of this file).");

// 1. Register the generated client with AddHttpClient. The factory owns the HttpClient and its handlers.
//    BearerHandler adds "Authorization: Bearer {token}" to every call once a token is known.
var tokenHolder = new TokenHolder();
var services = new ServiceCollection();
services.AddSingleton(tokenHolder);
services.AddTransient<BearerHandler>();
var http = services
    .AddHttpClient<IApexPostClient, ApexPostClient>(client => client.BaseAddress = new Uri(baseUrl))
    .AddHttpMessageHandler<BearerHandler>();
#if DEBUG
// Debug only, and only for localhost: accept the ASP.NET Core development certificate. Never compiled into a Release build.
http.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (request, _, _, _) => request.RequestUri?.IsLoopback == true
});
#endif

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IApexPostClient>();

// 2. Without a token the API answers 401.
try
{
    await client.GetShipmentV2Async("APX-10000001");
}
catch (ApiException unauthorized) when (unauthorized.StatusCode == (int)HttpStatusCode.Unauthorized)
{
    Console.WriteLine("No token: 401 as expected.");
}

// 3. Sign in as the admin and create a shipment (creating is Admin only).
tokenHolder.Token = (await client.CreateTokenV2Async(new TokenRequest { UserName = "admin", Password = demoPassword })).AccessToken;

var tracking = $"APX-{Random.Shared.Next(40_000_000, 49_999_999)}";
var created = await client.CreateShipmentV2Async(new CreateShipmentRequest
{
    TrackingNumber = tracking,
    WeightKg = 2.5,
    RecipientName = "Hana Ito",
    RecipientPhone = "+44 20 7946 0002",
    RecipientAddress = "3 Quay Street",
    RecipientCity = "Brightport"
});
Console.WriteLine($"Created {created.TrackingNumber}: {created.Status}, recipient {created.Recipient.Name} in {created.Recipient.City}");

var fetched = await client.GetShipmentV2Async(tracking);
Console.WriteLine($"Fetched {fetched.TrackingNumber}: {fetched.Status}, latest scan: {(fetched.LatestScan is null ? "none" : fetched.LatestScan.Location)}");

// 4. Sign in as courier-007 and upload two scans (the same call twice shows that it is idempotent).
tokenHolder.Token = (await client.CreateTokenV2Async(new TokenRequest { UserName = "courier007", Password = demoPassword })).AccessToken;

var upload = new ScanUploadRequest
{
    CourierId = "courier-007",
    Scans =
    [
        new ScanItemRequest
        {
            ScanId = ApexPostClientExtensions.NewScanId(), ScanType = ParcelScanType.Pickup,
            Location = "Brightport depot", ScannedAtUtc = DateTimeOffset.UtcNow.AddHours(-3)
        },
        new ScanItemRequest
        {
            ScanId = ApexPostClientExtensions.NewScanId(), ScanType = ParcelScanType.InTransit,
            Location = "Northvale hub", ScannedAtUtc = DateTimeOffset.UtcNow.AddHours(-1), Notes = "Sorted"
        }
    ]
};
var first = await client.UploadScansV2Async(tracking, upload);
var second = await client.UploadScansV2Async(tracking, upload);
Console.WriteLine($"Upload 1: accepted {first.Accepted}, duplicates {first.DuplicatesSkipped}, rejected {first.Rejected.Count}");
Console.WriteLine($"Upload 2: accepted {second.Accepted}, duplicates {second.DuplicatesSkipped}, rejected {second.Rejected.Count}");

var after = await client.GetShipmentV2Async(tracking);
Console.WriteLine($"After the scans: status {after.Status}, latest scan {after.LatestScan?.ScanType} at {after.LatestScan?.Location}");

// 5. A courier cannot upload under another courier's id (403), and cannot create shipments (403).
try
{
    await client.UploadScansV2Async(tracking, new ScanUploadRequest { CourierId = "courier-008", Scans = upload.Scans });
}
catch (ApiException forbidden) when (forbidden.StatusCode == (int)HttpStatusCode.Forbidden)
{
    Console.WriteLine("Another courier's id: 403 as expected.");
}

// 6. A 404 arrives as the generated exception type (ApiException<ProblemDetails>).
try
{
    await client.GetShipmentV2Async("APX-99999999");
}
catch (ApiException<ProblemDetails> notFound) when (notFound.StatusCode == (int)HttpStatusCode.NotFound)
{
    Console.WriteLine($"404 as expected: {notFound.Result.Title} - {notFound.Result.Detail}");
}

/// <summary>Holds the current access token. A real app keeps it in secure storage (the MAUI app uses SecureStorage).</summary>
internal sealed class TokenHolder
{
    public string? Token { get; set; }
}

/// <summary>Adds the Bearer token to every request.</summary>
internal sealed class BearerHandler(TokenHolder holder) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (holder.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", holder.Token);
        return base.SendAsync(request, cancellationToken);
    }
}

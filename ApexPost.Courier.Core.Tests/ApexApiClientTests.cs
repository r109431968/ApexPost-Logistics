using System.Net;
using ApexPost.Courier.Core.Api;

namespace ApexPost.Courier.Core.Tests;

public class ApexApiClientTests
{
    private static readonly IReadOnlyList<ScanItemDto> Scans =
        [new("scan::3f2504e0-4f89-41d3-9a0c-0305e82c3301", "Pickup", "Depot", DateTimeOffset.UtcNow, null, null)];

    [Fact]
    public async Task A_good_answer_becomes_a_value_and_the_request_has_the_right_shape()
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Json(HttpStatusCode.OK, Http.UploadOk)));
        using var harness = new Harness(network);

        var result = await harness.Client.UploadScansAsync("APX-12345678", "courier-007", Scans);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Accepted);
        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.test/api/v2/shipments/APX-12345678/scans", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task A_400_with_problem_details_becomes_a_validation_error_with_the_messages()
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Problem(
            HttpStatusCode.BadRequest, "One or more validation errors occurred.", errorsJson: """{"Scans[0].RecipientSignatureName":["A Delivered scan needs the name of the person who signed."]}""")));
        using var harness = new Harness(network);

        var result = await harness.Client.UploadScansAsync("APX-12345678", "courier-007", Scans);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.Validation, result.Error!.Kind);
        Assert.False(result.Error.IsTransient);
        Assert.Contains("needs the name of the person who signed", result.Error.Message);
        Assert.Equal(1, network.Calls);   // a 4xx is never retried
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ApiFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ApiFailureKind.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, ApiFailureKind.NotFound)]
    [InlineData(HttpStatusCode.Conflict, ApiFailureKind.Conflict)]
    public async Task Client_errors_map_to_their_kind_and_are_not_retried(HttpStatusCode status, ApiFailureKind kind)
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Problem(status, "Nope")));
        using var harness = new Harness(network, token: "a.b.c");

        var result = await harness.Client.UploadScansAsync("APX-12345678", "courier-007", Scans);

        Assert.Equal(kind, result.Error!.Kind);
        Assert.Equal(1, network.Calls);
    }

    [Fact]
    public async Task An_answer_that_is_not_json_still_gives_the_right_kind()
    {
        var network = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var harness = new Harness(network);

        var result = await harness.Client.UploadScansAsync("APX-12345678", "courier-007", Scans);

        Assert.Equal(ApiFailureKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task A_wrong_password_is_a_401_but_not_an_expired_session()
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Problem(HttpStatusCode.Unauthorized, "Sign-in failed")));
        using var harness = new Harness(network, token: "old.token.value");
        var expired = 0;
        harness.Session.SessionExpired += (_, _) => expired++;

        var result = await harness.Client.SignInAsync("courier007", "wrong");

        Assert.Equal(ApiFailureKind.Unauthorized, result.Error!.Kind);
        Assert.Equal(0, expired);   // the sign-in call carries no token, so a 401 here is only a wrong password
        Assert.Null(network.Requests[0].Headers.Authorization);
    }
}

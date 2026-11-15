using System.Net;
using ApexPost.Specs.Support;
using Reqnroll;

namespace ApexPost.Specs.Steps;

[Binding]
public sealed class CommonSteps(ApiContext api)
{
    [Given("the API is running with a clean test database")]
    public async Task GivenTheApiIsRunning()
    {
        // The warm-up call also proves the whole pipeline is up: without a token the answer must be 401.
        using var response = await api.Http.GetAsync("/api/v2/shipments/APX-10000001");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Given("I am signed in as \"(.*)\"")]
    public Task GivenIAmSignedInAs(string userName) => api.SignInAsync(userName);

    [Then("the response status is (.*)")]
    public void ThenTheResponseStatusIs(int expected)
    {
        Assert.NotNull(api.Response);
        Assert.Equal(expected, (int)api.Response!.StatusCode);
    }
}

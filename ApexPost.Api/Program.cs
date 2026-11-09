using System.Text.Json.Serialization;
using ApexPost.Api.Composition;
using ApexPost.Api.Development;
using ApexPost.Api.Errors;
using ApexPost.Api.Security;

// Minimal hosting: this file is only the composition root and the request pipeline. There is no Startup class.
var builder = WebApplication.CreateBuilder(args);

// A scoped service captured by a singleton fails when the app starts, not in production.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddApexPostApi();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums are written and read as text ("InTransit"). Version 1 DTOs expose the status as a number on purpose (see VERSIONING.md).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// The OpenAPI generator reads these (minimal API) JSON options, not the MVC ones above, so the enum converter is added here too.
// Strict numbers: otherwise the generator describes every number as "number or string".
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.AddApexPostVersioning();   // /api/v1/... and /api/v2/...
builder.Services.AddApexPostOpenApi();      // one OpenAPI document per version, built by the .NET generator

// Who may call what: JWT bearer for couriers and admins, an API key for the partner route (see SECURITY.md).
builder.Services.AddApexPostSecurity(builder.Configuration, builder.Environment);

// HTTPS only: plain http is redirected (308, method and body are kept) and browsers are told to stay on https (HSTS).
// Behind a reverse proxy set ASPNETCORE_HTTPS_PORT (and forward the original scheme) so the redirect knows the https port.
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});
builder.Services.AddHttpsRedirection(options => options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect);

// Every error, from validation (400) to a crash (500), is returned as application/problem+json.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();   // IExceptionHandler: turns exceptions into ProblemDetails
app.UseStatusCodePages();    // bodyless 404/415/... also become ProblemDetails

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseRateLimiter();        // limits the sign-in endpoint
app.UseAuthentication();     // reads the Bearer token (or the API key on the partner route)
app.UseAuthorization();      // roles, policies and the fallback "must be signed in" rule
app.MapControllers();

if (app.Services.GetRequiredService<JwtSettings>().KeyIsEphemeral)
{
    app.Logger.LogWarning(
        "Auth:Jwt:SigningKey is not set, so a random key was made for this run. Tokens stop working when the API restarts. " +
        "Run scripts/set-dev-secrets.ps1 once to store a key in user-secrets.");
}

if (app.Environment.IsDevelopment())
{
    // The documents and the interactive page exist in Development only. Hiding them is not security;
    // the endpoints themselves are exactly as reachable as before.
    app.MapOpenApi().AllowAnonymous();   // /openapi/v1.json and /openapi/v2.json (the fallback policy would otherwise ask for a token)

    app.UseSwaggerUI(options =>  // /swagger, reads the documents above
    {
        options.DocumentTitle = "ApexPost Logistics API";
        options.SwaggerEndpoint("/openapi/v2.json", "v2");
        options.SwaggerEndpoint("/openapi/v1.json", "v1 (deprecated)");
    });

    await app.PrepareAsync();
}

app.Run();

// Lets integration tests start the real API with WebApplicationFactory<Program>.
public partial class Program;

using System.Text.Json.Serialization;
using ApexPost.Api.Composition;
using ApexPost.Api.Development;
using ApexPost.Api.Errors;

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

// Every error, from validation (400) to a crash (500), is returned as application/problem+json.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();   // IExceptionHandler: turns exceptions into ProblemDetails
app.UseStatusCodePages();    // bodyless 404/415/... also become ProblemDetails

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    // The documents and the interactive page exist in Development only. Hiding them is not security;
    // the endpoints themselves are exactly as reachable as before.
    app.MapOpenApi();            // /openapi/v1.json and /openapi/v2.json

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

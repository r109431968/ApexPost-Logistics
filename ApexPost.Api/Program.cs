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
        // Accept "InTransit" as well as 2 in requests. Responses spell enums as numbers or strings per DTO (see the v1/v2 contracts).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

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
    await app.PrepareAsync();

app.Run();

// Lets integration tests start the real API with WebApplicationFactory<Program>.
public partial class Program;

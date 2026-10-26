using ApexPost.Web.Composition;
using ApexPost.Web.Middleware;

// Program.cs is only the composition root and the pipeline. No business logic lives here.
var builder = WebApplication.CreateBuilder(args);

// Catch lifetime mistakes (a scoped service captured by a singleton) when the app starts, not at 3 a.m.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddApexPost();
builder.Services.AddApexPostWeb();

var app = builder.Build();

app.Services.AttachEventSubscribers();
await app.SeedDemoDataAsync();

// Pipeline order. The reason for every position is explained in DESIGN.md.
app.UseMiddleware<CorrelationIdMiddleware>();       // 1. first, so every later log line and error has an ID
app.UseMiddleware<RequestLoggingMiddleware>();      // 2. times everything below it, even failures (try/finally)
app.UseMiddleware<ExceptionHandlingMiddleware>();   // 3. catches what the rest of the pipeline throws
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/integration"),
    branch => branch.UseStatusCodePagesWithReExecute("/Error/Status/{0}"));   // 4. friendly pages for 403/404 (not for APIs)
app.UseMiddleware<SecurityHeadersMiddleware>();     // 5. headers are set (OnStarting) before any response is written

if (!app.Environment.IsDevelopment())
    app.UseHsts();                                  // 6. tells browsers to insist on HTTPS (never in Development)

app.UseHttpsRedirection();                          // 7. plain HTTP is bounced before any work is done
app.UseMiddleware<MaintenanceModeMiddleware>();     // 8. cheap 503 before routing and auth (keeps /health)
app.UseStaticFiles();                               // 9. css needs no routing, auth or limits

app.UseRouting();                                   // 10. endpoint metadata is known from here on
app.UseMiddleware<IpRestrictionMiddleware>();       // 11. cheapest check first, reads [IpRestricted] metadata
app.UseCors();                                      // 12. answers preflight before the API key check
app.UseMiddleware<ApiKeyMiddleware>();              // 13. reads [RequireApiKey] metadata, sets the partner name
app.UseRateLimiter();                               // 14. needs the partner name from step 13 (login uses the IP)
app.UseAuthentication();                            // 15. who is this?
app.UseAuthorization();                             // 16. may they do this? (policies, fallback policy)

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Lets the integration tests start the real app with WebApplicationFactory<Program>.
public partial class Program;

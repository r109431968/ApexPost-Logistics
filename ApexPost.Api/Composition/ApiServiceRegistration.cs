using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Persistence.Sql;
using ApexPost.Infrastructure.Persistence.Sql.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Api.Composition;

/// <summary>
/// Registers the existing Week 3 DbContext and unit of work, plus the parcel repository and service.
/// The connection string is read from user-secrets (ConnectionStrings:ApexPostDb); it is never in a committed file.
/// </summary>
public static class ApiServiceRegistration
{
    public const string ConnectionName = "ApexPostDb";

    public static IServiceCollection AddApexPostApi(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContextFactory<ApexPostDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName)
                ?? throw new InvalidOperationException(
                    $"The connection string '{ConnectionName}' is missing. Set it with: dotnet user-secrets set \"ConnectionStrings:{ConnectionName}\" \"...\" --project ApexPost.Api");

            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 3));
        });

        // One DbContext per request, shared by the repository and the unit of work (same as ApexPost.Web).
        services.AddScoped(serviceProvider => serviceProvider.GetRequiredService<IDbContextFactory<ApexPostDbContext>>().CreateDbContext());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IParcelRepository, ParcelRepository>();
        services.AddScoped<IParcelService, ParcelService>();

        return services;
    }
}

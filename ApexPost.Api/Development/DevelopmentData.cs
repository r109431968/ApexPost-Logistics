using ApexPost.Application.Abstractions;
using ApexPost.Application.Parcels;
using ApexPost.Domain.Parcels;
using ApexPost.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Api.Development;

/// <summary>
/// Development only: applies the migrations and adds a few fictional shipments, so "Try it out" has something to show.
/// It goes through the same service as the controllers, so the data obeys the same rules.
/// </summary>
public static class DevelopmentData
{
    public static async Task PrepareAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();

        await db.Database.MigrateAsync();

        if (await db.Parcels.AnyAsync())
            return;

        var parcels = scope.ServiceProvider.GetRequiredService<IParcelService>();
        var people = new (string Tracking, string Name, string City, ParcelStatus Target)[]
        {
            ("APX-10000001", "Asha Verma", "Brightport", ParcelStatus.Created),
            ("APX-10000002", "Ben Okafor", "Brightport", ParcelStatus.Created),
            ("APX-10000003", "Chen Li", "Northvale", ParcelStatus.PickedUp),
            ("APX-10000004", "Dana Petrova", "Northvale", ParcelStatus.InTransit),
            ("APX-10000005", "Eli Santos", "Brightport", ParcelStatus.OutForDelivery),
            ("APX-10000006", "Farah Haddad", "Southmere", ParcelStatus.Delivered)
        };

        var path = new[] { ParcelStatus.PickedUp, ParcelStatus.InTransit, ParcelStatus.OutForDelivery, ParcelStatus.Delivered };

        foreach (var (tracking, name, city, target) in people)
        {
            await parcels.CreateAsync(
                new NewParcel(tracking, 2.5m, name, "+44 20 7946 0000", "14 Harbour Lane", city), default);

            foreach (var step in path.TakeWhile(step => step <= target))
                await parcels.ChangeStatusAsync(tracking, step, default);
        }

        // Two scans for one parcel, with fixed ids so the data is the same on every machine.
        await parcels.UploadScansAsync(
            "APX-10000004",
            "courier-007",
            [
                new ScanInput("scan::11111111-1111-4111-8111-111111111111", ParcelScanType.Pickup, "Northvale depot",
                    DateTimeOffset.UtcNow.AddHours(-5), "Picked up at the counter", null),
                new ScanInput("scan::22222222-2222-4222-8222-222222222222", ParcelScanType.InTransit, "Brightport hub",
                    DateTimeOffset.UtcNow.AddHours(-2), null, null)
            ],
            default);
    }
}

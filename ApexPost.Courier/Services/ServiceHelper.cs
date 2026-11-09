using Microsoft.Extensions.DependencyInjection;
namespace ApexPost.Courier.Services;

/// <summary>For the few places that cannot take constructor injection (a ContentView created from XAML).</summary>
public static class ServiceHelper
{
    public static IServiceProvider Services { get; set; } = default!;

    public static T Get<T>() where T : notnull => Services.GetRequiredService<T>();
}

using ApexPost.Courier.Core.Resilience;
using ApexPost.Courier.Services;

namespace ApexPost.Courier.Views;

/// <summary>The red bar "Server unavailable – working offline". It is on every page and shows while the circuit is open or a call just failed.</summary>
public partial class OfflineBanner : ContentView
{
    public OfflineBanner()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.Get<ConnectionStatus>();
    }
}

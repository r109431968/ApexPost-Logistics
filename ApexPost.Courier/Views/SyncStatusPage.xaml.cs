using ApexPost.Courier.ViewModels;

namespace ApexPost.Courier.Views;

public partial class SyncStatusPage : ContentPage
{
    private readonly SyncStatusViewModel _viewModel;

    public SyncStatusPage(SyncStatusViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Refresh();
    }
}

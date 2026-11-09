using ApexPost.Courier.ViewModels;

namespace ApexPost.Courier.Views;

public partial class PendingScansPage : ContentPage
{
    private readonly PendingScansViewModel _viewModel;

    public PendingScansPage(PendingScansViewModel viewModel)
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

using ApexPost.Courier.ViewModels;

namespace ApexPost.Courier.Views;

public partial class NewScanPage : ContentPage
{
    private readonly NewScanViewModel _viewModel;

    public NewScanPage(NewScanViewModel viewModel)
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

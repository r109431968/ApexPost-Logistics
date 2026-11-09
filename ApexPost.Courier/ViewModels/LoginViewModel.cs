using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ApexPost.Courier.ViewModels;

public partial class LoginViewModel(AuthService auth) : ObservableObject
{
    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    public bool IsNotBusy => !IsBusy;

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Message = null;
        try
        {
            var outcome = await auth.SignInAsync(UserName, Password);
            if (!outcome.Succeeded)
            {
                Message = outcome.Message;
                return;
            }

            Password = string.Empty;   // the password is never kept
            await MainThread.InvokeOnMainThreadAsync(async () => await Shell.Current.GoToAsync("//main"));
        }
        finally
        {
            IsBusy = false;
        }
    }
}

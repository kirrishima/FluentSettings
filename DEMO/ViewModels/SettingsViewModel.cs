using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentSettings.Generator.DEMO.Services;

namespace FluentSettings.Generator.DEMO.ViewModels
{
    // ViewModel demonstrating two‑way binding to SettingsService
    public partial class SettingsViewModel : ObservableObject
    {
        public SettingsViewModel(SettingsService settingsService)
        {
            Settings = settingsService;
        }

        // Bound to UI input for login
        [ObservableProperty]
        public partial string Login { get; set; } = string.Empty;

        // Bound to UI input for password
        [ObservableProperty]
        public partial string Password { get; set; } = string.Empty;

        // Expose the generated settings service to commands
        public SettingsService Settings { get; }

        // Saves the login into LocalSettings via generated code
        [RelayCommand]
        public void SaveLogin()
        {
            Settings.Login = Login;
        }

        // Saves the password into LocalSettings via generated code
        [RelayCommand]
        public void SavePassword()
        {
            Settings.Password = Password;
        }

        // Serializes both fields into the Form setting
        [RelayCommand]
        public void SaveForm()
        {
            Settings.Form = new() { Login = Login, Password = Password };
        }
    }
}

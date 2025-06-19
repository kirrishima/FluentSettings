using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using FluentSettings.Generator.DEMO.Services;
using FluentSettings.Generator.DEMO.Views;
using FluentSettings.Generator.DEMO.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FluentSettings.Generator.DEMO
{

    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = Host.Services.GetRequiredService<MainWindow>();
            m_window.Activate();
        }

        private Window? m_window;

        public static IHost Host { get; private set; } = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<SettingsService>();
                    services.AddTransient<SettingsView>();
                    services.AddTransient<SettingsViewModel>();
                })
            .Build();
    }
}

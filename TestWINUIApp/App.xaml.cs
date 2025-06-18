using Microsoft.UI.Xaml;
using FluentSettings;
using System.Diagnostics;
using System.Xml.Serialization;
using TestWINUIApp.A.B.C.D.E.F.Gay;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TestWINUIApp
{
    public partial class UserSettings : LocalSettingsBase
    {
        [LocalSetting]
        public partial bool IsSelected { get; set; }

        [LocalSetting]
        [XmlElement("Aboba")]
        [property: XmlElement("Aboba 2")]
        public partial bool Hello { get; set; }

        [LocalSetting]
        public partial MyClass Cl { get; set; }

        partial void OnIsSelectedChanging(bool oldValue, bool newValue, ref bool cancel)
        {
            Debug.WriteLine($"{oldValue} {newValue}");
            //cancel = true;
        }
        partial void OnIsSelectedChanged(bool newValue)
        {
            Debug.WriteLine($"{newValue}");
        }

        public UserSettings() { }
    }

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

            var settings = new UserSettings();

            Debug.WriteLine($"value before set: {settings.IsSelected}");
            settings.IsSelected = !settings.IsSelected;
            Debug.WriteLine($"value after set: {settings.IsSelected}");
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window? m_window;
    }
}

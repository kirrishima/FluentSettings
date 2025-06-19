using Microsoft.UI.Xaml;

namespace FluentSettings.Generator.DEMO
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            RootFrame.Navigate(typeof(Views.SettingsView));
        }
    }
}

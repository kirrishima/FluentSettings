namespace FluentSettings.Generator.DEMO.Model
{
    // Simple POCO that will be serialized and stored via LocalSetting
    public class Form
    {
        public Form() { }

        // User's login name
        public string Login { get; set; } = string.Empty;

        // User's password
        public string Password { get; set; } = string.Empty;
    }
}

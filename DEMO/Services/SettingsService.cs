using FluentSettings;
using FluentSettings.Generator.DEMO.Model;

namespace FluentSettings.Generator.DEMO.Services
{
    // This partial class derives from LocalSettingsBase to enable code generation
    public partial class SettingsService : LocalSettingsBase
    {
        // On startup, ensure the Form JSON is initialized
        public SettingsService()
        {
            UpdateFormSerialized(Form);
        }

        // Will generate a string property backed by LocalSettings["Login"]
        [LocalSetting]
        public partial string Login { get; set; }

        // Stores under key "PSWD" instead of property name
        [LocalSetting(Key = "PSWD")]
        public partial string Password { get; set; }

        // Complex object – will be JSON‑serialized automatically
        [LocalSetting]
        public partial Form Form { get; set; }

        // Hook called after Form changes: update the JSON snapshot
        partial void OnFormChanged(Form newValue)
        {
            UpdateFormSerialized(newValue);
        }

        // Exposed JSON representation for UI/debugging
        public string FormSerialized { get; set; } = string.Empty;

        // Helper to produce a pretty‑printed JSON and raise change notification
        private void UpdateFormSerialized(Form value)
        {
            var options = new System.Text.Json.JsonSerializerOptions()
            {
                WriteIndented = true
            };

            var json = System.Text.Json.JsonSerializer.Serialize(value, options);
            FormSerialized = json;
            OnPropertyChanged(nameof(FormSerialized));
        }
    }
}

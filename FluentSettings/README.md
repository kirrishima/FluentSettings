# FluentSettings.Generator

FluentSettings.Generator — a C# source generator for WinUI 3 that automates storing and retrieving app settings via ApplicationData.Current.LocalSettings.

## What it does:

- Injects a base class LocalSettingsBase (inherits ObservableObject from CommunityToolkit.Mvvm) with helper methods: GetSetting<T>, GetSettingOrDefault<T>, SetSetting<T>, and GetSetting(key, Type).
- Injects a \[LocalSetting] attribute you can place on partial properties to mark them for code generation.
- Generates for each marked property:
  • a partial getter that calls GetSetting<T>(key)
  • a partial setter that checks for change, calls SetSetting, raises OnPropertyChanged, and invokes optional partial hooks OnXxxChanging/OnXxxChanged

Installation:
Run in your WinUI 3 project directory:
dotnet add package FluentSettings.Generator --version 1.0.3

(The package will bring in CommunityToolkit.Mvvm >= 8.4.0 automatically.)

## Usage:

1. Create your settings class inheriting LocalSettingsBase:

```cs
   public partial class AppSettings : LocalSettingsBase
   {
        [LocalSetting]
        public partial string UserName { get; set; }

        [LocalSetting(Key = "Age")]
        public partial int UserAge { get; set; }
   }
```

2. Build the project. Generated files:

   - LocalSettingsBase.g.cs contains the base class.
   - AppSettings.LocalSettings.g.cs contains the implementations for your properties.

3. In code:

```cs
   var settings = new AppSettings();
   settings.UserName = "Alice";
   string name = settings.UserName; // reads from LocalSettings
```

## Key points:

- Partial hooks: define partial methods OnUserNameChanging(old, new, ref bool cancel) or OnUserNameChanged(new) in your partial class to intervene.
- Duplicate key detection and diagnostic errors if two properties use the same key.
- Error if your partial class does not inherit LocalSettingsBase.

Repository:
[https://github.com/kirrishima/FluentSettings](https://github.com/kirrishima/FluentSettings)

NuGet:
[https://www.nuget.org/packages/FluentSettings.Generator](https://www.nuget.org/packages/FluentSettings.Generator)

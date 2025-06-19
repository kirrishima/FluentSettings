using FluentSettings.Extensions;
using FluentSettings.MySettingsGenerator;
using System.Reflection.Metadata;

namespace FluentSettings
{
    internal class CodeAsStrings
    {
        public static readonly string WinUISettingsClassName = "LocalSettingsBase";

        /// <summary>
        /// НЕ включая Attribute: LocalSetting для фактического названия класса LocalSettingAttribute
        /// </summary>
        public static readonly string LocalSettingAttributePrefix = "LocalSetting";

        public static string WinUISettingsClass = $@"using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Windows.Storage;

#pragma warning disable
#nullable enable

namespace FluentSettings
{{
    [global::System.CodeDom.Compiler.GeneratedCode(""{typeof(SettingsGenerator).FullName}"", ""{typeof(SettingsGenerator).Assembly.GetName().Version}"" )]
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class {WinUISettingsClassName} : ObservableObject
    {{
        protected T? GetSetting<T>(string key)
        {{
            var values = ApplicationData.Current.LocalSettings.Values;

            if (typeof(T).IsPrimitive || typeof(T) == typeof(string))
            {{
                if (values.TryGetValue(key, out var o) && o is T value)
                    return value;
                return default;
            }}

            if (values.TryGetValue(key, out var raw) && raw is string json)
            {{
                try
                {{
                    return JsonSerializer.Deserialize<T>(json);
                }}
                catch
                {{
                    return default;
                }}
            }}

            return default;
        }}

        protected void SetSetting<T>(string key, T value)
        {{
            var values = ApplicationData.Current.LocalSettings.Values;

            if (value is null)
            {{
                values.Remove(key);
                return;
            }}

            if (value is string || value.GetType().IsPrimitive)
            {{
                values[key] = value;
            }}
            else
            {{
                values[key] = JsonSerializer.Serialize(value);
            }}
        }}

        protected T GetSettingOrDefault<T>(string key)
        {{
            var val = GetSetting<T>(key);
            return val != null ? val : default!;
        }}
    }}
}}
";


        public static readonly string LocalSettingAttribute = $@"using System;

namespace {typeof(SettingsGenerator).GetParentNamespace()}
{{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class {LocalSettingAttributePrefix}Attribute : Attribute
    {{
        /// <summary>
        /// Ключ в ApplicationData.Current.LocalSettings.Values
        /// (по умолчанию – имя свойства).
        /// </summary>
        public string Key {{ get; set; }}
    }}
}}
";
    }
}

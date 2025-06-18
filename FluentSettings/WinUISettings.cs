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
using Windows.Storage;

#pragma warning disable
#nullable enable

namespace FluentSettings
{{
    /// <summary>
    /// Базовый класс для работы с локальными настройками приложения через ApplicationData.LocalSettings.
    /// Base class for working with application local settings via ApplicationData.LocalSettings.
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCode(""{typeof(SettingsGenerator).FullName}"", ""{typeof(SettingsGenerator).Assembly.GetName().Version}"" )]
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class {WinUISettingsClassName} : ObservableObject
    {{
        /// <summary>
        /// Получает сохранённую настройку по ключу, если она существует и соответствует заданному типу.
        /// Gets a saved setting by key if it exists and matches the given type.
        /// </summary>
        /// <param name=""key"">Ключ настройки / Setting key</param>
        /// <param name=""type"">Тип значения / Value type</param>
        /// <returns>Значение или null / The value or null</returns>
        protected object? GetSetting(string key, Type type)
        {{
            var values = ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue(key, out var o) && o?.GetType() == type)
                return o;

            return null;
        }}

        /// <summary>
        /// Получает сохранённую настройку по ключу, если она существует и соответствует типу T.
        /// Gets a saved setting by key if it exists and matches type T.
        /// </summary>
        /// <typeparam name=""T"">Тип значения / Value type</typeparam>
        /// <param name=""key"">Ключ настройки / Setting key</param>
        /// <returns>Значение типа T или значение по умолчанию / Value of type T or default</returns>
        protected T? GetSetting<T>(string key)
        {{
            var values = ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue(key, out var o) && o is T value)
                return value;

            return default;
        }}

        /// <summary>
        /// Устанавливает или обновляет значение настройки по ключу.
        /// Sets or updates a setting value by key.
        /// </summary>
        /// <typeparam name=""T"">Тип значения / Value type</typeparam>
        /// <param name=""key"">Ключ настройки / Setting key</param>
        /// <param name=""value"">Новое значение / New value</param>
        protected void SetSetting<T>(string key, T value)
        {{
            var values = ApplicationData.Current.LocalSettings.Values;
            values[key] = value!;
        }}

        /// <summary>
        /// Получает настройку по ключу или значение по умолчанию, если она отсутствует.
        /// Gets the setting value or default(T) if the setting is missing.
        /// </summary>
        /// <typeparam name=""T"">Тип значения / Value type</typeparam>
        /// <param name=""key"">Ключ настройки / Setting key</param>
        /// <returns>Значение настройки или значение по умолчанию / Setting value or default</returns>
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

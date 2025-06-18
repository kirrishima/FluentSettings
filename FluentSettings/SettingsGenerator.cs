using FluentSettings.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace FluentSettings.MySettingsGenerator
{
    [Generator(LanguageNames.CSharp)]
    public class SettingsGenerator : IIncrementalGenerator
    {
        // Обёртка для результата синтаксического преобразования
        private class PropInfo
        {
            public PropertyDeclarationSyntax Syntax { get; }
            public IPropertySymbol Symbol { get; }
            public IReadOnlyList<string> CopiedAttrs { get; }

            public PropInfo(
                PropertyDeclarationSyntax syntax,
                IPropertySymbol symbol,
                IReadOnlyList<string> copiedAttrs)
            {
                Syntax = syntax;
                Symbol = symbol;
                CopiedAttrs = copiedAttrs;
            }
        }

        public void Initialize(IncrementalGeneratorInitializationContext ctx)
        {
            // 1) Встраиваем атрибут AutoSettingAttribute
            ctx.RegisterPostInitializationOutput(pi =>
            {
                pi.AddSource("LocalSettingAttribute.g.cs", SourceText.From(@$"
using System;

namespace {GetType().GetParentNamespace()}
{{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class LocalSettingAttribute : Attribute
    {{
        /// <summary>
        /// Ключ в ApplicationData.Current.LocalSettings.Values
        /// (по умолчанию – имя свойства).
        /// </summary>
        public string Key {{ get; set; }}
    }}
}}
", Encoding.UTF8));
            });

            // 2) Находим все partial-свойства с атрибутом [AutoSetting]
            // Находим все partial-свойства с атрибутом [AutoSetting]
            var candidates = ctx.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) =>
                        node is PropertyDeclarationSyntax p
                        && p.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
                        && p.AttributeLists.Count > 0,
                    transform: Transform
                )
                .Where(x => x != null)!;


            // 3) Собираем их в ImmutableArray<PropInfo>
            var allProps = candidates.Collect();

            ctx.RegisterSourceOutput(allProps, GenerateAll);

            //// 3) Генерируем код для каждого найденного свойства
            //ctx.RegisterSourceOutput(candidates, Generate);
        }

        private static PropInfo Transform(GeneratorSyntaxContext syntaxContext, CancellationToken _)
        {
            var propSyntax = (PropertyDeclarationSyntax)syntaxContext.Node;
            var symbol = syntaxContext.SemanticModel.GetDeclaredSymbol(propSyntax) as IPropertySymbol;
            if (symbol == null)
                return null;

            // Проверяем наличие нашего атрибута
            if (!symbol.GetAttributes().Any(ad =>
                    ad.AttributeClass?.ToDisplayString() == "FluentSettings.LocalSettingAttribute"))
                return null;

            // Собираем остальные атрибуты (кроме AutoSetting)
            var copied = new List<string>();

            foreach (var attrList in propSyntax.AttributeLists)
            {
                if (attrList.Target?.Identifier.Text != "property")
                    continue;

                foreach (var attr in attrList.Attributes)
                {
                    var name = attr.Name.ToString();
                    if (name == "LocalSetting" || name.EndsWith(".LocalSetting"))
                        continue;

                    var attrType = syntaxContext.SemanticModel
                        .GetSymbolInfo(attr).Symbol?
                        .ContainingType as INamedTypeSymbol;
                    if (attrType == null)
                        continue;

                    // берем FQN, включая global::
                    var fqName = attrType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var argText = attr.ArgumentList?.ToFullString() ?? string.Empty;
                    copied.Add(fqName + argText);
                }
            }

            return new PropInfo(propSyntax, symbol, copied);
        }




        private static void GenerateAll(SourceProductionContext spc,
                                ImmutableArray<PropInfo> allProps)
        {
            if (allProps.IsDefaultOrEmpty)
                return;

            // Группируем по namespace + классу
            var groups = allProps
                .GroupBy(pi => (
                    ns: pi.Symbol.ContainingNamespace.ToDisplayString(),
                    cls: pi.Symbol.ContainingType.Name))
                .ToDictionary(g => g.Key, g => g.ToList());

            var version = typeof(SettingsGenerator)
                .Assembly.GetName().Version?.ToString() ?? "1.0.0";

            // Для каждого класса делаем свой StringBuilder и свой файл
            foreach (var kv in groups)
            {
                var ns = kv.Key.ns;
                var clsName = kv.Key.cls;
                var props = kv.Value;

                var sb = new StringBuilder();

                // общие using’ы
                sb.AppendLine("using System;");
                sb.AppendLine("using System.Collections.Generic;");
                sb.AppendLine("using CommunityToolkit.Mvvm.ComponentModel;");
                sb.AppendLine("using Windows.Storage;");
                sb.AppendLine();

                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
                sb.AppendLine($"    public partial class {clsName} : ObservableObject");
                sb.AppendLine("    {");

                foreach (var pi in props)
                {
                    var propType = pi.Symbol.Type.ToDisplayString();
                    var propName = pi.Symbol.Name;

                    // GeneratedCode-атрибут
                    var genAttr =
                        $"        [global::System.CodeDom.Compiler.GeneratedCode(\"" +
                        $"{typeof(SettingsGenerator).FullName}\", \"{version}\")]";

                    // Ключ из LocalSettingAttribute
                    var attrData = pi.Symbol.GetAttributes()
                        .First(ad => ad.AttributeClass?.Name == "LocalSettingAttribute");
                    var keyArg = attrData.NamedArguments
                        .FirstOrDefault(kv2 => kv2.Key == "Key")
                        .Value.Value as string;
                    var key = !string.IsNullOrEmpty(keyArg) ? keyArg : propName;

                    // partial-методы
                    sb.AppendLine(genAttr);
                    sb.AppendLine($"        partial void On{propName}Changing({propType} oldValue, {propType} newValue, ref bool cancel);");
                    sb.AppendLine();
                    sb.AppendLine(genAttr);
                    sb.AppendLine($"        partial void On{propName}Changed({propType} newValue);");
                    sb.AppendLine();

                    // скопированные атрибуты FQN
                    foreach (var fullAttr in pi.CopiedAttrs)
                        sb.AppendLine($"        [property: {fullAttr}]");

                    sb.AppendLine(genAttr);
                    sb.AppendLine("        [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
                    sb.AppendLine($"        public partial {propType} {propName}");
                    sb.AppendLine("        {");
                    sb.AppendLine("            get");
                    sb.AppendLine("            {");
                    sb.AppendLine("                var values = ApplicationData.Current.LocalSettings.Values;");
                    sb.AppendLine($"                if (values.TryGetValue(\"{key}\", out var o) && o is {propType} v) return v;");
                    sb.AppendLine($"                return default({propType});");
                    sb.AppendLine("            }");
                    sb.AppendLine("            set");
                    sb.AppendLine("            {");
                    sb.AppendLine("                var values = ApplicationData.Current.LocalSettings.Values;");
                    sb.AppendLine($"                {propType} oldValue = default;");
                    sb.AppendLine($"                if (values.TryGetValue(\"{key}\", out var o2) && o2 is {propType} ov) oldValue = ov;");
                    sb.AppendLine();
                    sb.AppendLine($"                if (EqualityComparer<{propType}>.Default.Equals(oldValue, value)) return;");
                    sb.AppendLine();
                    sb.AppendLine("                bool cancel = false;");
                    sb.AppendLine($"                On{propName}Changing(oldValue, value, ref cancel);");
                    sb.AppendLine("                if (cancel) return;");
                    sb.AppendLine();
                    sb.AppendLine($"                values[\"{key}\"] = value;");
                    sb.AppendLine($"                OnPropertyChanged(nameof({propName}));");
                    sb.AppendLine($"                On{propName}Changed(value);");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                    sb.AppendLine();
                }

                sb.AppendLine("    }");
                sb.AppendLine("}");

                // Здесь даём уникальное имя файлу по имени класса:
                spc.AddSource($"{clsName}.LocalSettings.g.cs",
                              SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        }


    }
}

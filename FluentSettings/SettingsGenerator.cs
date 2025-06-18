using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            public IReadOnlyList<(string fqName, string argText, string ns)> CopiedAttrs { get; }

            public PropInfo(
                PropertyDeclarationSyntax syntax,
                IPropertySymbol symbol,
                IReadOnlyList<(string fqName, string argText, string ns)> copiedAttrs)
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
                pi.AddSource("AutoSettingAttribute.g.cs", SourceText.From(@"
using System;

namespace FluentSettings
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class AutoSettingAttribute : Attribute
    {
        public AutoSettingAttribute() {}
        public string Key { get; set; }
    }
}
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
                    transform: (syntaxContext, _) =>
                    {
                        var propSyntax = (PropertyDeclarationSyntax)syntaxContext.Node;
                        var symbol = syntaxContext.SemanticModel.GetDeclaredSymbol(propSyntax) as IPropertySymbol;
                        if (symbol == null)
                            return null;

                        // Проверяем наличие нашего атрибута
                        if (!symbol.GetAttributes().Any(ad =>
                                ad.AttributeClass?.ToDisplayString() == "FluentSettings.AutoSettingAttribute"))
                            return null;

                        // Собираем остальные атрибуты (кроме AutoSetting)
                        var copied = new List<(string nameSyntax, string argText, string ns)>();

                        foreach (var attrList in propSyntax.AttributeLists)
                        {
                            // оставляем только списки вида [property: ...]
                            if (attrList.Target?.Identifier.Text != "property")
                                continue;

                            foreach (var attr in attrList.Attributes)
                            {
                                // фильтруем AutoSetting
                                var name = attr.Name.ToString();
                                if (name == "AutoSetting" || name.EndsWith(".AutoSetting"))
                                    continue;

                                // получаем символ атрибута
                                var attrType = syntaxContext.SemanticModel
                                    .GetSymbolInfo(attr).Symbol?
                                    .ContainingType as INamedTypeSymbol;
                                if (attrType == null)
                                    continue;

                                // записываем короткое имя и аргументы
                                var argText = attr.ArgumentList?.ToFullString() ?? string.Empty;
                                var ns = attrType.ContainingNamespace.ToDisplayString();
                                copied.Add((name, argText, ns));
                            }
                        }

                        return new PropInfo(propSyntax, symbol, copied);
                    }
                )
                .Where(x => x != null)!;

            // 3) Генерируем код для каждого найденного свойства
            ctx.RegisterSourceOutput(candidates, (spc, propInfo) =>
            {
                // Распаковываем
                var propSyntax = propInfo.Syntax;
                var propSymbol = propInfo.Symbol;

                var ns = propSymbol.ContainingNamespace.ToDisplayString();
                var clsName = propSymbol.ContainingType.Name;
                var propType = propSymbol.Type.ToDisplayString();
                var propName = propSymbol.Name;

                // Ключ из атрибута или имя свойства
                var attrData = propSymbol.GetAttributes()
                    .First(ad => ad.AttributeClass?.ToDisplayString() == "FluentSettings.AutoSettingAttribute");
                var keyArg = attrData.NamedArguments
                    .FirstOrDefault(kv => kv.Key == "Key")
                    .Value.Value as string;
                var key = !string.IsNullOrEmpty(keyArg) ? keyArg! : propName;

                var version = typeof(SettingsGenerator)
                    .Assembly
                    .GetName()
                    .Version?
                    .ToString() ?? "1.0.0";

                // Строим исходник

                var usings = new HashSet<string>(StringComparer.Ordinal)
                {
                    "System.Collections.Generic",
                    "CommunityToolkit.Mvvm.ComponentModel",
                    "Windows.Storage"
                };

                var autoGeneratedAttribute = "        [global::System.CodeDom.Compiler.GeneratedCode(" +
                                            $"\"{GetType().FullName}\", " +
                                            $"\"{version}\")]";

                var sb = new StringBuilder();

                foreach (var attr in propInfo.CopiedAttrs)
                    usings.Add(attr.ns);

                foreach (var u in usings)
                    sb.AppendLine($"using {u};");
                sb.AppendLine();


                sb.AppendLine("");
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
                sb.AppendLine($"    public partial class {clsName} : ObservableObject");
                sb.AppendLine("    {");
                sb.AppendLine(autoGeneratedAttribute);
                sb.AppendLine($"        partial void On{propName}Changing({propType} oldValue, {propType} newValue, ref bool cancel);");
                sb.AppendLine("");
                sb.AppendLine(autoGeneratedAttribute);
                sb.AppendLine($"        partial void On{propName}Changed({propType} newValue);");
                sb.AppendLine();

                foreach (var (fqName, argText, _) in propInfo.CopiedAttrs)
                {
                    sb.AppendLine($"        [property: {fqName}{argText}]");
                }

                sb.AppendLine(autoGeneratedAttribute);
                sb.AppendLine("        [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
                sb.AppendLine($"        public partial {propType} {propName}");
                sb.AppendLine("        {");
                sb.AppendLine("            get");
                sb.AppendLine("            {");
                sb.AppendLine("                var values = ApplicationData.Current.LocalSettings.Values;");
                sb.AppendLine($"                if (values.TryGetValue(\"{key}\", out var o) && o is {propType} v)");
                sb.AppendLine($"                    {{return v;}}");
                sb.AppendLine($"                return default({propType});");
                sb.AppendLine("            }");
                sb.AppendLine("            set");
                sb.AppendLine("            {");
                sb.AppendLine("                var values = ApplicationData.Current.LocalSettings.Values;");
                sb.AppendLine($"                {propType} oldValue = default;");
                sb.AppendLine($"                if (values.TryGetValue(\"{key}\", out var o2) && o2 is {propType} ov)");
                sb.AppendLine($"                    {{oldValue = ov;}}");
                sb.AppendLine();
                sb.AppendLine($"                if (EqualityComparer<{propType}>.Default.Equals(oldValue, value))");
                sb.AppendLine("                    return;");
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
                sb.AppendLine("    }");
                sb.AppendLine("}");

                // Добавляем в компиляцию
                spc.AddSource($"{clsName}_{propName}_AutoSetting.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            });
        }
    }
}

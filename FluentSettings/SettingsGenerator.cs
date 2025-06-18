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

        private static readonly DiagnosticDescriptor MissingBaseClassRule =
            new DiagnosticDescriptor(
                id: "FS001",
                title: $"Class must derive from {CodeAsStrings.WinUISettingsClassName}",
                messageFormat: "Partial class '{0}' must derive from '{1}' to use the WinUI 3 settings generator",
                category: $"FluentSettingsGenerator",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);


        public void Initialize(IncrementalGeneratorInitializationContext ctx)
        {
            // 1) Встраиваем атрибут AutoSettingAttribute
            ctx.RegisterPostInitializationOutput(pi =>
            {
                pi.AddSource($"{CodeAsStrings.LocalSettingAttributePrefix}Attribute.g.cs",
                             SourceText.From(CodeAsStrings.LocalSettingAttribute, Encoding.UTF8));

                pi.AddSource($"{CodeAsStrings.WinUISettingsClassName}.g.cs",
                             SourceText.From(CodeAsStrings.WinUISettingsClass, Encoding.UTF8));
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

            var compilation = ctx.CompilationProvider;

            var compilationAndProps = compilation.Combine(allProps);

            ctx.RegisterSourceOutput(compilationAndProps, (spc, source) =>
            {
                var comp = source.Left;
                var props = source.Right;
                GenerateAll(spc, comp, props);
            });

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
                    ad.AttributeClass?.ToDisplayString() == $"FluentSettings.{CodeAsStrings.LocalSettingAttributePrefix}Attribute"))
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
                    if (name == CodeAsStrings.LocalSettingAttributePrefix || name.EndsWith($".{CodeAsStrings.LocalSettingAttributePrefix}"))
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


        private static void GenerateAll(
         SourceProductionContext spc,
         Compilation compilation,
         ImmutableArray<PropInfo> allProps)
        {
            if (allProps.IsDefaultOrEmpty)
                return;

            var WinUISettings = $"FluentSettings.{CodeAsStrings.WinUISettingsClassName}";

            var winuiSettingsSym = compilation.GetTypeByMetadataName(WinUISettings);
            if (winuiSettingsSym == null)
            {
                // Вдруг что-то не так с референсами
                return;
            }

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
                var typeSym = props[0].Symbol.ContainingType;

                bool inherits = false;
                for (INamedTypeSymbol b = typeSym; b != null; b = b.BaseType)
                {
                    if (SymbolEqualityComparer.Default.Equals(b, winuiSettingsSym))
                    {
                        inherits = true;
                        break;
                    }
                }

                if (!inherits)
                {
                    // Находим синтаксис самого класса, чтобы выделить имя
                    var classDecl = typeSym.DeclaringSyntaxReferences[0]
                        .GetSyntax() as ClassDeclarationSyntax;
                    var location = classDecl!.Identifier.GetLocation();

                    spc.ReportDiagnostic(
                        Diagnostic.Create(
                            MissingBaseClassRule,
                            location,
                            clsName,
                            winuiSettingsSym.ToDisplayString()));

                    // Пропускаем генерацию кода для этого класса
                    continue;
                }

                var sb = new StringBuilder();

                // общие using’ы
                //sb.AppendLine("using System;");
                //sb.AppendLine("using System.Collections.Generic;");
                //sb.AppendLine("using CommunityToolkit.Mvvm.ComponentModel;");
                //sb.AppendLine("using Windows.Storage;");
                sb.AppendLine();

                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
                sb.AppendLine($"    public partial class {clsName} "); // удалено: : {WinUISettings}
                sb.AppendLine("    {");

                foreach (var pi in props)
                {
                    var propType = pi.Symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var propName = pi.Symbol.Name;

                    // GeneratedCode-атрибут
                    var genAttr =
                        $"        [global::System.CodeDom.Compiler.GeneratedCode(\"" +
                        $"{typeof(SettingsGenerator).FullName}\", \"{version}\")]";

                    // Ключ из LocalSettingAttribute
                    var attrData = pi.Symbol.GetAttributes()
                        .First(ad => ad.AttributeClass?.Name == $"{CodeAsStrings.LocalSettingAttributePrefix}Attribute");
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
                    sb.AppendLine($"                return GetSetting<{propType}>(\"{key}\");");
                    sb.AppendLine("            }");
                    sb.AppendLine("            set");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                {propType} oldValue = GetSetting<{propType}>(\"{key}\");");
                    sb.AppendLine();
                    sb.AppendLine($"                if (global::System.Collections.Generic.EqualityComparer<{propType}>.Default.Equals(oldValue, value)) return;");
                    sb.AppendLine();
                    sb.AppendLine("                bool cancel = false;");
                    sb.AppendLine($"                On{propName}Changing(oldValue, value, ref cancel);");
                    sb.AppendLine("                if (cancel) return;");
                    sb.AppendLine();
                    sb.AppendLine($"                SetSetting(\"{key}\", value);");
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

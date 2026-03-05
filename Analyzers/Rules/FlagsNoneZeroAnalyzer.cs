using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FlagsNoneZeroAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.FlagsEnumNoneZero);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startContext =>
            {
                var flagsAttr = startContext.Compilation.GetTypeByMetadataName("System.FlagsAttribute");
                if (flagsAttr is null) return;

                startContext.RegisterSymbolAction(symbolContext =>
                {
                    if (symbolContext.Symbol is not INamedTypeSymbol type) return;
                    if (!AnalyzerHelpers.ShouldAnalyze(type.Locations[0])) return;
                    if (type.IsImplicitlyDeclared) return;
                    if (type.DeclaringSyntaxReferences.Length == 0) return;
                    if (type.TypeKind != TypeKind.Enum) return;

                    if (!AnalyzerHelpers.HasAttribute(type, flagsAttr)) return;

                    var hasNoneZero = type.GetMembers()
                        .OfType<IFieldSymbol>()
                        .Where(f => f.HasConstantValue)
                        .Any(f => f.Name == "None" && IsZero(f.ConstantValue));

                    if (hasNoneZero) return;

                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.FlagsEnumNoneZero,
                        type.Locations[0],
                        type.Name));
                }, SymbolKind.NamedType);
            });
        }

        private static bool IsZero(object? value)
        {
            if (value is null) return false;
            try { return Convert.ToInt64(value) == 0; }
            catch { return false; }
        }
    }
}
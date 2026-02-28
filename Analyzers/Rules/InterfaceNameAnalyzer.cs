using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class InterfaceNameAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.InterfaceNaming);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
        }

        private static void Analyze(SymbolAnalysisContext context)
        {
            if (context.Symbol is not INamedTypeSymbol type) return;
            if (!AnalyzerHelpers.ShouldAnalyze(type.Locations[0])) return;
            if (type.IsImplicitlyDeclared) return;
            if (type.DeclaringSyntaxReferences.Length == 0) return;
            if (type.TypeKind != TypeKind.Interface) return;

            var name = type.Name;
            // I + PascalCase（単純・堅実な判定）
            if (name.Length >= 2 && name[0] == 'I' && char.IsUpper(name[1]) && !name.Contains("_"))
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InterfaceNaming,
                type.Locations[0],
                name));
        }
    }
}
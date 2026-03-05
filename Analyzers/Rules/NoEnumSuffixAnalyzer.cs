using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NoEnumSuffixAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.NoEnumSuffix);

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
            if (type.TypeKind != TypeKind.Enum) return;

            if (!type.Name.EndsWith("Enum", System.StringComparison.Ordinal)) return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NoEnumSuffix,
                type.Locations[0],
                type.Name));
        }
    }
}
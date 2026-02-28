using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NoUnderscoreParamAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.NoUnderscoreParameterPrefix);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeParam, SymbolKind.Parameter);
        }

        private static void AnalyzeParam(SymbolAnalysisContext context)
        {
            if (context.Symbol is not IParameterSymbol p) return;
            if (!AnalyzerHelpers.ShouldAnalyze(p.Locations[0])) return;
            if (p.IsImplicitlyDeclared) return;
            if (p.DeclaringSyntaxReferences.Length == 0) return;

            if (!p.Name.StartsWith("_", System.StringComparison.Ordinal)) return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NoUnderscoreParameterPrefix,
                p.Locations[0],
                p.Name));
        }
    }
}

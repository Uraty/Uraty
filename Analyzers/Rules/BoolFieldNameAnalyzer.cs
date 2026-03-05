using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BoolFieldNameAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.BoolFieldNaming);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(Analyze, SymbolKind.Field);
        }

        private static void Analyze(SymbolAnalysisContext context)
        {
            if (context.Symbol is not IFieldSymbol field) return;
            if (!AnalyzerHelpers.ShouldAnalyze(field.Locations[0])) return;
            if (field.IsImplicitlyDeclared) return;
            if (field.DeclaringSyntaxReferences.Length == 0) return;

            if (field.IsConst) return; // const の命名規則とは衝突するので除外
            if (!AnalyzerHelpers.IsBool(field.Type)) return;

            var n = field.Name;
            bool ok =
                AnalyzerHelpers.HasUpperAfterPrefix(n, "_is") ||
                AnalyzerHelpers.HasUpperAfterPrefix(n, "_has") ||
                AnalyzerHelpers.HasUpperAfterPrefix(n, "_can") ||
                AnalyzerHelpers.HasUpperAfterPrefix(n, "_should");

            if (ok) return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.BoolFieldNaming,
                field.Locations[0],
                n));
        }
    }
}
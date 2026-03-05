using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BoolPropMethodNameAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.BoolPropertyOrMethodNaming);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
        }

        private static void AnalyzeMethod(SymbolAnalysisContext context)
        {
            if (context.Symbol is not IMethodSymbol m) return;
            if (!AnalyzerHelpers.ShouldAnalyze(m.Locations[0])) return;
            if (m.IsImplicitlyDeclared) return;
            if (m.DeclaringSyntaxReferences.Length == 0) return;

            if (!AnalyzerHelpers.IsBool(m.ReturnType)) return;

            // 変更不能な名前は除外（override / 明示IF実装）
            if (AnalyzerHelpers.IsOverrideOrExplicitInterfaceImplementation(m)) return;

            var n = m.Name;
            if (AnalyzerHelpers.StartsWithAny(n, "Is", "Has", "Can", "Should")) return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.BoolPropertyOrMethodNaming,
                m.Locations[0],
                n));
        }

        private static void AnalyzeProperty(SymbolAnalysisContext context)
        {
            if (context.Symbol is not IPropertySymbol p) return;
            if (!AnalyzerHelpers.ShouldAnalyze(p.Locations[0])) return;
            if (p.IsImplicitlyDeclared) return;
            if (p.DeclaringSyntaxReferences.Length == 0) return;

            if (!AnalyzerHelpers.IsBool(p.Type)) return;

            if (AnalyzerHelpers.IsOverrideOrExplicitInterfaceImplementation(p)) return;

            var n = p.Name;
            if (AnalyzerHelpers.StartsWithAny(n, "Is", "Has", "Can", "Should")) return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.BoolPropertyOrMethodNaming,
                p.Locations[0],
                n));
        }
    }
}
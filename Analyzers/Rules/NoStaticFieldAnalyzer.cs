using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NoStaticFieldAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.NoNonConstStaticField);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private static void AnalyzeField(SymbolAnalysisContext context)
        {
            if (context.Symbol is not IFieldSymbol field)
                return;
            if (!AnalyzerHelpers.ShouldAnalyze(field.Locations[0])) 
                return;
            if (field.IsImplicitlyDeclared)
                return;
            if (field.DeclaringSyntaxReferences.Length == 0)
                return;
            if (!field.IsStatic)
                return;
            if (field.IsConst)
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NoNonConstStaticField,
                field.Locations[0],
                field.Name));
        }
    }
}

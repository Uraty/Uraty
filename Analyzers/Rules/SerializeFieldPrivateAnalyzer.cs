using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SerializeFieldPrivateAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.SerializeFieldMustBePrivate);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startContext =>
            {
                var serializeFieldAttr = startContext.Compilation.GetTypeByMetadataName("UnityEngine.SerializeField");
                if (serializeFieldAttr is null) return;

                startContext.RegisterSymbolAction(symbolContext =>
                {
                    if (symbolContext.Symbol is not IFieldSymbol field) return;
                    if (!AnalyzerHelpers.ShouldAnalyze(field.Locations[0])) return;
                    if (field.IsImplicitlyDeclared) return;
                    if (field.DeclaringSyntaxReferences.Length == 0) return;

                    if (!AnalyzerHelpers.HasAttribute(field, serializeFieldAttr)) return;

                    if (field.DeclaredAccessibility == Accessibility.Private) return;

                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.SerializeFieldMustBePrivate,
                        field.Locations[0],
                        field.Name));
                }, SymbolKind.Field);
            });
        }
    }
}
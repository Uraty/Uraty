using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnityPublicFieldAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.UnityPublicField);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startContext =>
            {
                var mono = startContext.Compilation.GetTypeByMetadataName("UnityEngine.MonoBehaviour");
                var so = startContext.Compilation.GetTypeByMetadataName("UnityEngine.ScriptableObject");
                if (mono is null || so is null) return;

                startContext.RegisterSymbolAction(symbolContext =>
                {
                    if (symbolContext.Symbol is not IFieldSymbol field) return;
                    if (!AnalyzerHelpers.ShouldAnalyze(field.Locations[0])) return;
                    if (field.IsImplicitlyDeclared) return;
                    if (field.DeclaringSyntaxReferences.Length == 0) return;

                    if (field.DeclaredAccessibility != Accessibility.Public) return;
                    if (field.IsConst) return;
                    if (field.IsReadOnly) return; // “可変”のみ対象

                    if (field.ContainingType is null) return;
                    if (!AnalyzerHelpers.IsUnityMonoBehaviourOrScriptableObject(field.ContainingType, mono, so)) return;

                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.UnityPublicField,
                        field.Locations[0],
                        field.Name));
                }, SymbolKind.Field);
            });
        }
    }
}
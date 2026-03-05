using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UratyNamespaceAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.NamespaceMustStartWithUraty);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeNamespace,
                SyntaxKind.NamespaceDeclaration,
                SyntaxKind.FileScopedNamespaceDeclaration);
        }

        private static void AnalyzeNamespace(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerHelpers.ShouldAnalyze(context.Node.GetLocation())) return;
            
            string ns = context.Node switch
            {
                NamespaceDeclarationSyntax n => n.Name.ToString(),
                FileScopedNamespaceDeclarationSyntax f => f.Name.ToString(),
                _ => ""
            };

            if (string.IsNullOrWhiteSpace(ns)) return;
            if (AnalyzerHelpers.NamespaceStartsWithUraty(ns)) return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NamespaceMustStartWithUraty,
                context.Node.GetLocation(),
                ns));
        }
    }
}
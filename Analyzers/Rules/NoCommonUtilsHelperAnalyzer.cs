using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NoCommonUtilsHelperAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.NoCommonUtilsHelperName);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeNamespace,
                SyntaxKind.NamespaceDeclaration,
                SyntaxKind.FileScopedNamespaceDeclaration);

            context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
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

            if (!AnalyzerHelpers.ContainsForbiddenSegment(ns)) return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NoCommonUtilsHelperName,
                context.Node.GetLocation(),
                ns));
        }

        private static void AnalyzeType(SymbolAnalysisContext context)
        {
            if (context.Symbol is not INamedTypeSymbol t) return;
            if (!AnalyzerHelpers.ShouldAnalyze(t.Locations[0])) return;
            if (t.IsImplicitlyDeclared) return;
            if (t.DeclaringSyntaxReferences.Length == 0) return;

            if (!AnalyzerHelpers.IsForbiddenTypeName(t.Name)) return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NoCommonUtilsHelperName,
                t.Locations[0],
                t.Name));
        }
    }
}
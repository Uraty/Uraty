using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NoEventAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.NoEvent);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EventFieldDeclaration, SyntaxKind.EventDeclaration);
        }

        private static void Analyze(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerHelpers.ShouldAnalyze(context.Node.GetLocation())) return;

            string name = context.Node switch
            {
                EventFieldDeclarationSyntax ef when ef.Declaration.Variables.Count > 0
                    => ef.Declaration.Variables[0].Identifier.ValueText,

                EventDeclarationSyntax ed => ed.Identifier.ValueText,

                _ => "(unknown)"
            };

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NoEvent,
                context.Node.GetLocation(),
                name));
        }
    }
}
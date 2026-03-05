using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BoolLocalParamNameAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.BoolLocalOrParameterNaming);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Local: Syntaxで拾う（Unityで安定）
            context.RegisterSyntaxNodeAction(AnalyzeLocalVariableDeclarator, SyntaxKind.VariableDeclarator);

            // Parameter: SymbolでOK
            context.RegisterSymbolAction(AnalyzeParam, SymbolKind.Parameter);
        }

        private static void AnalyzeLocalVariableDeclarator(SyntaxNodeAnalysisContext context)
        {
            var declarator = (VariableDeclaratorSyntax)context.Node;

            // ここで ILocalSymbol を取れたものだけ = ローカル変数
            if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not ILocalSymbol local)
                return;

            if (local.IsImplicitlyDeclared) return;

            // ★解析対象フォルダだけ
            var idLoc = declarator.Identifier.GetLocation();
            if (!AnalyzerHelpers.ShouldAnalyze(idLoc)) return;

            if (!AnalyzerHelpers.IsBool(local.Type)) return;

            if (AnalyzerHelpers.StartsWithAny(local.Name, "is", "has", "can", "should")) return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.BoolLocalOrParameterNaming,
                idLoc,
                local.Name));
        }

        private static void AnalyzeParam(SymbolAnalysisContext context)
        {
            if (context.Symbol is not IParameterSymbol p) return;
            if (p.IsImplicitlyDeclared) return;
            if (p.DeclaringSyntaxReferences.Length == 0) return;

            // ★解析対象フォルダだけ
            if (!AnalyzerHelpers.ShouldAnalyze(p.Locations[0])) return;

            if (!AnalyzerHelpers.IsBool(p.Type)) return;

            if (AnalyzerHelpers.StartsWithAny(p.Name, "is", "has", "can", "should")) return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.BoolLocalOrParameterNaming,
                p.Locations[0],
                p.Name));
        }
    }
}
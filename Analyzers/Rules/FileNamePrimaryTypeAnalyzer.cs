using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Uraty.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FileNamePrimaryTypeAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DiagnosticDescriptors.FileNameMustMatchPrimaryType);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startContext =>
            {
                var mono = startContext.Compilation.GetTypeByMetadataName("UnityEngine.MonoBehaviour");
                var so = startContext.Compilation.GetTypeByMetadataName("UnityEngine.ScriptableObject");
                // Unity以外でも動くようにnull許容（MB/SO優先判定だけ無効化）
                startContext.RegisterSyntaxTreeAction(treeContext =>
                    AnalyzeTree(treeContext, startContext.Compilation, mono, so));
            });
        }

        private static void AnalyzeTree(SyntaxTreeAnalysisContext context, Compilation compilation, INamedTypeSymbol? mono, INamedTypeSymbol? so)
        {
            var path = context.Tree.FilePath;
            // normalize
            var p = path.Replace('\\', '/');
            if (p.IndexOf("/Assets/_Features/", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                p.IndexOf("/Assets/_Shared/",   System.StringComparison.OrdinalIgnoreCase) < 0 &&
                p.IndexOf("/Assets/_Platform/", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return;

            var fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var root = context.Tree.GetRoot(context.CancellationToken);
            var model = compilation.GetSemanticModel(context.Tree);

            // top-level type declarations only
            var typeDecls = root.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Where(d => d.Parent is NamespaceDeclarationSyntax
                         || d.Parent is FileScopedNamespaceDeclarationSyntax
                         || d.Parent is CompilationUnitSyntax)
                .ToArray();

            if (typeDecls.Length == 0) return;

            var symbols = typeDecls
                .Select(d => model.GetDeclaredSymbol(d, context.CancellationToken) as INamedTypeSymbol)
                .Where(s => s is not null)
                .Cast<INamedTypeSymbol>()
                .ToArray();

            if (symbols.Length == 0) return;

            INamedTypeSymbol? primary = null;

            // 1) MB/SO が1つだけならそれを主要型
            if (mono is not null && so is not null)
            {
                var mbso = symbols.Where(s => AnalyzerHelpers.IsUnityMonoBehaviourOrScriptableObject(s, mono, so)).ToArray();
                if (mbso.Length == 1) primary = mbso[0];
            }

            // 2) public型が1つだけならそれ
            if (primary is null)
            {
                var publics = symbols.Where(s => s.DeclaredAccessibility == Accessibility.Public).ToArray();
                if (publics.Length == 1) primary = publics[0];
            }

            // 3) それでも決まらない場合：最初のpublic、なければ最初の型（新規コード前提の妥協）
            if (primary is null)
            {
                primary = symbols
                    .OrderBy(s => s.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue)
                    .FirstOrDefault(s => s.DeclaredAccessibility == Accessibility.Public)
                    ?? symbols.OrderBy(s => s.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue).First();
            }

            if (primary is null) return;

            if (string.Equals(fileName, primary.Name, StringComparison.Ordinal)) return;

            var loc = primary.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.FileNameMustMatchPrimaryType,
                loc,
                fileName,
                primary.Name));
        }
    }
}
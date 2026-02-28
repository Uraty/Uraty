using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Uraty.Analyzers
{
    internal static class AnalyzerHelpers
    {
        internal static bool ShouldAnalyze(Location location)
        {
            if (!location.IsInSource) return false;

            var path = location.SourceTree?.FilePath;
            if (string.IsNullOrWhiteSpace(path)) return false;

            var p = path.Replace('\\', '/');
            if (p.IndexOf("/Assets/_Features/", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (p.IndexOf("/Assets/_Shared/",   System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (p.IndexOf("/Assets/_Platform/", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        internal static bool IsOrInheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (INamedTypeSymbol? t = type; t != null; t = t.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(t, baseType))
                    return true;
            }
            return false;
        }

        internal static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
        {
            foreach (var a in symbol.GetAttributes())
            {
                if (a.AttributeClass is null) continue;
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType))
                    return true;
            }
            return false;
        }

        internal static bool IsUnityMonoBehaviourOrScriptableObject(INamedTypeSymbol type, INamedTypeSymbol monoBehaviour, INamedTypeSymbol scriptableObject)
            => IsOrInheritsFrom(type, monoBehaviour) || IsOrInheritsFrom(type, scriptableObject);

        internal static bool IsBool(ITypeSymbol type) => type.SpecialType == SpecialType.System_Boolean;

        internal static bool StartsWithAny(string name, params string[] prefixes)
            => prefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));

        internal static bool HasUpperAfterPrefix(string name, string prefix)
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) return false;
            if (name.Length <= prefix.Length) return false;
            return char.IsUpper(name[prefix.Length]);
        }

        internal static bool NamespaceStartsWithUraty(string ns)
            => ns == "Uraty" || ns.StartsWith("Uraty.", StringComparison.Ordinal);

        internal static bool ContainsForbiddenSegment(string dottedName)
        {
            foreach (var seg in dottedName.Split('.'))
            {
                if (seg == "Common" || seg == "Utils" || seg == "Helper")
                    return true;
            }
            return false;
        }

        internal static bool IsForbiddenTypeName(string typeName)
            => typeName == "Common" || typeName == "Utils" || typeName == "Helper";

        internal static bool IsOverrideOrExplicitInterfaceImplementation(IMethodSymbol method)
            => method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0;

        internal static bool IsOverrideOrExplicitInterfaceImplementation(IPropertySymbol prop)
            => prop.IsOverride || prop.ExplicitInterfaceImplementations.Length > 0;
    }
}

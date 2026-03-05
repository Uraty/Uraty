// DiagnosticDescriptors.cs
using Microsoft.CodeAnalysis;

namespace Uraty.Analyzers
{
    internal static class DiagnosticDescriptors
    {
        // Categories
        internal const string CategoryUnity = "Unity";
        internal const string CategoryDesign = "Design";
        internal const string CategoryNaming = "Naming";
        internal const string CategoryNamespace = "Namespace";
        internal const string CategoryFile = "File";

        // IDs (URATY0001 - URATY0014)
        internal const string UnityPublicFieldId = "URATY0001";
        internal const string SerializeFieldMustBePrivateId = "URATY0002";
        internal const string NoEventId = "URATY0003";
        internal const string NoNonConstStaticFieldId = "URATY0004";
        internal const string NoUnderscoreParameterPrefixId = "URATY0005";

        internal const string InterfaceNamingId = "URATY0006";
        internal const string NoEnumSuffixId = "URATY0007";
        internal const string FlagsEnumNoneZeroId = "URATY0008";
        internal const string BoolFieldNamingId = "URATY0009";
        internal const string BoolLocalOrParameterNamingId = "URATY0010";
        internal const string BoolPropertyOrMethodNamingId = "URATY0011";

        internal const string NamespaceMustStartWithUratyId = "URATY0012";
        internal const string NoCommonUtilsHelperNameId = "URATY0013";
        internal const string FileNameMustMatchPrimaryTypeId = "URATY0014";

        // A. アクセス修飾子・言語機能（5）
        internal static readonly DiagnosticDescriptor UnityPublicField =
            new DiagnosticDescriptor(
                id: UnityPublicFieldId,
                title: "MonoBehaviour/ScriptableObject の public 可変フィールド禁止",
                messageFormat: "MonoBehaviour / ScriptableObject の public 可変フィールド '{0}' を使用してはならない。Inspector 公開は [SerializeField] private を使用すること。",
                category: CategoryUnity,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Inspector 公開目的を含め、MonoBehaviour / ScriptableObject の可変フィールドを public にしてはならない。");

        internal static readonly DiagnosticDescriptor SerializeFieldMustBePrivate =
            new DiagnosticDescriptor(
                id: SerializeFieldMustBePrivateId,
                title: "[SerializeField] は private 必須",
                messageFormat: "フィールド '{0}' は [SerializeField] を付ける場合 private で宣言しなければならない。",
                category: CategoryUnity,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Inspector 公開が必要なフィールドは [SerializeField] private を使用しなければならない。");

        internal static readonly DiagnosticDescriptor NoEvent =
            new DiagnosticDescriptor(
                id: NoEventId,
                title: "C# event 宣言禁止",
                messageFormat: "C# の event '{0}' を宣言してはならない。通知は Stream（Observable）として公開すること。",
                category: CategoryDesign,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "新規コードで C# の event を使用してはならない。");

        internal static readonly DiagnosticDescriptor NoNonConstStaticField =
            new DiagnosticDescriptor(
                id: NoNonConstStaticFieldId,
                title: "const 以外の static フィールド禁止",
                messageFormat: "const 以外の static フィールド '{0}' を宣言してはならない。",
                category: CategoryDesign,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "静的な可変状態（static フィールド）を導入してはならない。実装上は const 以外の static フィールドを禁止する。");

        internal static readonly DiagnosticDescriptor NoUnderscoreParameterPrefix =
            new DiagnosticDescriptor(
                id: NoUnderscoreParameterPrefixId,
                title: "引数名 '_' プレフィックス禁止",
                messageFormat: "引数名 '{0}' に '_' プレフィックスを付けてはならない。",
                category: CategoryNaming,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "引数名に '_' プレフィックスを付けてはならない。");

        // B. 命名（6）
        internal static readonly DiagnosticDescriptor InterfaceNaming =
            new DiagnosticDescriptor(
                id: InterfaceNamingId,
                title: "インターフェース命名規則",
                messageFormat: "インターフェース '{0}' は 'I' + PascalCase で命名しなければならない。",
                category: CategoryNaming,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "インターフェースは I を接頭してインターフェースであることを明示しなければならない。");

        internal static readonly DiagnosticDescriptor NoEnumSuffix =
            new DiagnosticDescriptor(
                id: NoEnumSuffixId,
                title: "Enum 接尾辞禁止",
                messageFormat: "Enum 型 '{0}' に 'Enum' などの接尾辞を付けてはならない。",
                category: CategoryNaming,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Enum 型名に 'Enum' 等の接尾辞を付けてはならない。");

        internal static readonly DiagnosticDescriptor FlagsEnumNoneZero =
            new DiagnosticDescriptor(
                id: FlagsEnumNoneZeroId,
                title: "[Flags] Enum は None = 0 必須",
                messageFormat: "[Flags] Enum '{0}' は 'None = 0' を定義しなければならない。",
                category: CategoryNaming,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "[Flags] を使う場合は None = 0 を定義しなければならない。");

        internal static readonly DiagnosticDescriptor BoolFieldNaming =
            new DiagnosticDescriptor(
                id: BoolFieldNamingId,
                title: "bool フィールド命名規則",
                messageFormat: "bool フィールド '{0}' は '_is' / '_has' / '_can' / '_should' で開始しなければならない。",
                category: CategoryNaming,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "bool を表すフィールド名は _is/_has/_can/_should で始めなければならない。");

        internal static readonly DiagnosticDescriptor BoolLocalOrParameterNaming =
            new DiagnosticDescriptor(
                id: BoolLocalOrParameterNamingId,
                title: "bool ローカル/引数 命名規則",
                messageFormat: "bool 変数/引数 '{0}' は 'is' / 'has' / 'can' / 'should' で開始しなければならない。",
                category: CategoryNaming,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "bool を表す引数/ローカル変数名は is/has/can/should で始めなければならない。");

        internal static readonly DiagnosticDescriptor BoolPropertyOrMethodNaming =
            new DiagnosticDescriptor(
                id: BoolPropertyOrMethodNamingId,
                title: "bool プロパティ/関数 命名規則",
                messageFormat: "bool プロパティ/関数 '{0}' は 'Is' / 'Has' / 'Can' / 'Should' で開始しなければならない。",
                category: CategoryNaming,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "bool を表すプロパティ/関数名は Is/Has/Can/Should で始めなければならない。");

        // C. 名前空間・ファイル（3）
        internal static readonly DiagnosticDescriptor NamespaceMustStartWithUraty =
            new DiagnosticDescriptor(
                id: NamespaceMustStartWithUratyId,
                title: "名前空間は Uraty.* 必須",
                messageFormat: "名前空間 '{0}' は 'Uraty.' で開始しなければならない。",
                category: CategoryNamespace,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "名前空間は Uraty.機能名 の順で整理しなければならない。");

        internal static readonly DiagnosticDescriptor NoCommonUtilsHelperName =
            new DiagnosticDescriptor(
                id: NoCommonUtilsHelperNameId,
                title: "Common/Utils/Helper 命名禁止",
                messageFormat: "名前 '{0}' に 'Common' / 'Utils' / 'Helper' を含めてはならない。",
                category: CategoryNaming,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "新規に Common / Utils / Helper 命名の名前空間・クラスを追加してはならない。");

        internal static readonly DiagnosticDescriptor FileNameMustMatchPrimaryType =
            new DiagnosticDescriptor(
                id: FileNameMustMatchPrimaryTypeId,
                title: "ファイル名と主要型名の一致",
                messageFormat: "ファイル名 '{0}' は主要 public 型 '{1}' と一致しなければならない。",
                category: CategoryFile,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "public class / 主要型名とファイル名を一致させなければならない（MonoBehaviour/ScriptableObject は特に厳守）。");
    }
}
using System.IO;
using System.Linq;
using UnityEditor;

[InitializeOnLoad]
public static class RoslynAnalyzerLabeler
{
    private const string Label = "RoslynAnalyzer";
    private const string RootDir = "Assets/Analyzers";

    static RoslynAnalyzerLabeler()
    {
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        if (!Directory.Exists(RootDir)) return;

        var dllPaths = Directory.GetFiles(RootDir, "*.dll", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => p.StartsWith("Assets/"));

        foreach (var path in dllPaths)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null) continue;

            var labels = AssetDatabase.GetLabels(obj);
            if (!labels.Contains(Label))
            {
                AssetDatabase.SetLabels(obj, labels.Concat(new[] { Label }).Distinct().ToArray());
            }
        }
    }
}
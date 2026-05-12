#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public static class MotchiriUzumoreBuildMenu
    {
        const string OutFolder = "Assets/_motchiri_uzumore_test";

        [MenuItem("Tools/lilToon Shader Merger/Test/Build Motchiri + Uzumore")]
        public static void RunBuild()
        {
            if (AssetDatabase.IsValidFolder(OutFolder))
                AssetDatabase.DeleteAsset(OutFolder);
            AssetDatabase.CreateFolder("Assets", "_motchiri_uzumore_test");

            var settings = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
            settings.shaderName = "Merged/MotchiriUzumore";
            settings.sourceFolders = new[]
            {
                AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/motchiri_shader/Shader/Shaders"),
                AssetDatabase.LoadAssetAtPath<DefaultAsset>("Packages/jp.sigmal00.uzumore-shader/Runtime/Shaders"),
            };
            settings.outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(OutFolder);

            Debug.Log("[MotchiriUzumoreBuild] starting Build");
            var result = LilToonShaderMerger.Build(settings);
            Debug.Log($"[MotchiriUzumoreBuild] Success={result.Success}, Diagnostics={result.Diagnostics.Count}, WrittenFiles={result.WrittenFiles.Count}");
            foreach (var d in result.Diagnostics) Debug.Log("[MotchiriUzumoreBuild] diag: " + d);
            foreach (var f in result.WrittenFiles) Debug.Log("[MotchiriUzumoreBuild] wrote: " + Path.GetFileName(f));

            Object.DestroyImmediate(settings);
        }
    }
}
#endif

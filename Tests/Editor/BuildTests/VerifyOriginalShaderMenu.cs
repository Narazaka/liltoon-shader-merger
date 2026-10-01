#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public static class VerifyOriginalShaderMenu
    {
        const string SummaryFile = "Assets/_original_shader_verify.txt";

        // 合成前の各カスタムシェーダーを合成出力と同じ方法で検証する (合成で生じたエラーかの切り分け用)
        [MenuItem("Tools/lilToon Shader Merger/Test/Verify Original Shaders Compile")]
        public static void Run()
        {
            var sb = new StringBuilder("=== Original Shader Compile Verify ===\n");
            foreach (var (label, folder) in BatchMergeTestMenu.AllShaders)
            {
                var paths = new List<string>();
                if (Directory.Exists(folder))
                    foreach (var f in Directory.GetFiles(folder, "*.lilcontainer")) paths.Add(f.Replace('\\', '/'));
                var diags = new List<Diagnostic>();
                ShaderCompileVerifier.Verify(paths, diags);
                int errs = diags.FindAll(d => d.Severity == Severity.Error).Count;
                sb.AppendLine($"[{label}] containers={paths.Count} errors={errs} warnings={diags.Count - errs}");
                foreach (var d in diags) sb.Append("    ").AppendLine(d.ToString());
            }
            File.WriteAllText(SummaryFile.Replace("Assets/", Application.dataPath + "/"), sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
#endif

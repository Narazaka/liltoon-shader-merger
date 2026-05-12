#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public static class BatchMergeTestMenu
    {
        const string OutRoot = "Assets/_batch_merge_tests";

        // プロジェクト内の全カスタムシェーダー
        static readonly (string label, string folder)[] AllShaders = new[]
        {
            ("motchiri",                  "Assets/motchiri_shader/Shader/Shaders"),
            ("uzumore",                   "Packages/jp.sigmal00.uzumore-shader/Runtime/Shaders"),
            ("lilCustomClipper",          "Assets/BekoShop/lilToonCustomClipper/Shaders"),
            ("Customliltoon_ParallelThrough", "Assets/KuukuuVirtualFactory/K2Shader/Customliltoon_ParallelThrough/Shaders"),
            ("HawaseGimmickShader",       "Assets/KuukuuVirtualFactory/K2Shader/HawaseGimmickShader/Shaders"),
            ("lilToon_FresnelAlphaEx",    "Assets/lilToon_FresnelAlphaEx/Shaders"),
            ("unebeta",                   "Assets/lilToon_unebeta/Shaders"),
            ("MsdfMask",                  "Packages/org.kb10uy.liltoon-msdfmask/Shader"),
        };

        const string SummaryFile = OutRoot + "/_summary.txt";

        static void WriteSummary(string content)
        {
            File.WriteAllText(SummaryFile.Replace("Assets/", Application.dataPath + "/"), content);
            Debug.Log("[BatchMerge] summary written to " + SummaryFile + "\n" + content);
        }

        [MenuItem("Tools/lilToon Shader Merger/Test/Run All Single-Source Merges")]
        public static void RunAllSingleSource()
        {
            EnsureRoot();
            var summary = new StringBuilder("=== Single-Source Merge Tests ===\n");
            foreach (var (label, folder) in AllShaders)
            {
                summary.AppendLine(RunOne(new[] { (label, folder) }, $"single_{label}"));
            }
            WriteSummary(summary.ToString());
        }

        [MenuItem("Tools/lilToon Shader Merger/Test/Run All Pairs With Motchiri")]
        public static void RunAllPairsWithMotchiri()
        {
            EnsureRoot();
            var motchiri = AllShaders[0];
            var summary = new StringBuilder("=== Pair-With-Motchiri Merge Tests ===\n");
            for (int i = 1; i < AllShaders.Length; i++)
            {
                summary.AppendLine(RunOne(new[] { motchiri, AllShaders[i] }, $"pair_motchiri_{AllShaders[i].label}"));
            }
            WriteSummary(summary.ToString());
        }

        [MenuItem("Tools/lilToon Shader Merger/Test/Run All Pairs (8C2 = 28)")]
        public static void RunAllPairs()
        {
            EnsureRoot();
            var summary = new StringBuilder("=== All-Pairs Merge Tests (8C2 = 28) ===\n");
            for (int i = 0; i < AllShaders.Length; i++)
            {
                for (int j = i + 1; j < AllShaders.Length; j++)
                {
                    var a = AllShaders[i];
                    var b = AllShaders[j];
                    var label = $"pair_{a.label}_{b.label}";
                    summary.AppendLine(RunOne(new[] { a, b }, label));
                }
            }
            // すべてのペアを refresh して shader を実体化させる
            AssetDatabase.Refresh();
            // 続けてシェーダーコンパイル検証
            summary.AppendLine();
            summary.AppendLine("=== Shader Compile Verify ===");
            foreach (var sub in Directory.GetDirectories(Application.dataPath + "/_batch_merge_tests"))
            {
                var name = Path.GetFileName(sub);
                if (!name.StartsWith("pair_")) continue;
                var shaderName = $"BatchMerge/{name}/lilToon";
                var sh = Shader.Find(shaderName);
                if (sh == null)
                {
                    summary.AppendLine($"  {name}: NULL (build failed)");
                    continue;
                }
                var msgs = UnityEditor.ShaderUtil.GetShaderMessages(sh);
                int errs = 0, warns = 0;
                foreach (var m in msgs)
                {
                    if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) errs++;
                    else warns++;
                }
                summary.AppendLine($"  {name}: {errs} errors, {warns} warnings");
                if (errs > 0)
                {
                    foreach (var m in msgs)
                        if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                            summary.AppendLine($"    [ERR] {m.message.Substring(0, System.Math.Min(160, m.message.Length))}");
                }
            }
            WriteSummary(summary.ToString());
        }

        [MenuItem("Tools/lilToon Shader Merger/Test/Run Full Merge (All 8)")]
        public static void RunFullMerge()
        {
            EnsureRoot();
            var summary = new StringBuilder("=== Full Merge (All 8) ===\n");
            summary.AppendLine(RunOne(AllShaders, "full_all8"));
            WriteSummary(summary.ToString());
        }

        [MenuItem("Tools/lilToon Shader Merger/Test/Verify Merged Shaders Compile")]
        public static void VerifyShaderCompile()
        {
            var summary = new StringBuilder("=== Shader Compile Verify ===\n");
            if (!AssetDatabase.IsValidFolder(OutRoot))
            {
                Debug.LogWarning("[BatchMerge] no batch output to verify");
                return;
            }
            // 出力サブフォルダ毎に lts.lilcontainer (top-level) を Shader.Find して null 以外を確認
            foreach (var sub in Directory.GetDirectories(Application.dataPath + "/_batch_merge_tests"))
            {
                var name = Path.GetFileName(sub);
                var shaderName = $"BatchMerge/{name}/lilToon";
                var sh = Shader.Find(shaderName);
                summary.AppendLine($"  {name}: '{shaderName}' -> {(sh ? "FOUND" : "NULL")}");
                if (sh != null)
                {
                    // ShaderUtil.GetShaderMessages で compile messages を取得
                    var msgs = UnityEditor.ShaderUtil.GetShaderMessages(sh);
                    int errs = 0, warns = 0;
                    foreach (var m in msgs)
                    {
                        if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) errs++;
                        else warns++;
                    }
                    summary.AppendLine($"    compile messages: {errs} errors, {warns} warnings");
                    foreach (var m in msgs)
                        summary.AppendLine($"    [{m.severity}] {m.platform}: {m.message.Substring(0, System.Math.Min(120, m.message.Length))}");
                }
            }
            File.WriteAllText(SummaryFile.Replace("Assets/", Application.dataPath + "/"), summary.ToString());
            Debug.Log(summary.ToString());
        }

        [MenuItem("Tools/lilToon Shader Merger/Test/Clean Batch Output")]
        public static void Clean()
        {
            if (AssetDatabase.IsValidFolder(OutRoot))
                AssetDatabase.DeleteAsset(OutRoot);
            AssetDatabase.Refresh();
            Debug.Log("[BatchMerge] cleaned " + OutRoot);
        }

        static void EnsureRoot()
        {
            if (!AssetDatabase.IsValidFolder(OutRoot))
                AssetDatabase.CreateFolder("Assets", "_batch_merge_tests");
        }

        static string RunOne((string label, string folder)[] sources, string outName)
        {
            var outFolder = $"{OutRoot}/{outName}";
            if (AssetDatabase.IsValidFolder(outFolder))
                AssetDatabase.DeleteAsset(outFolder);
            AssetDatabase.CreateFolder(OutRoot, outName);

            var sourceFolders = new List<DefaultAsset>();
            var missing = new List<string>();
            foreach (var (label, folder) in sources)
            {
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
                if (asset == null) missing.Add($"{label}({folder})");
                else sourceFolders.Add(asset);
            }
            if (missing.Count > 0)
                return $"[{outName}] SKIPPED, missing source folders: {string.Join(", ", missing)}";

            var settings = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
            settings.shaderName = $"BatchMerge/{outName}";
            settings.sourceFolders = sourceFolders.ToArray();
            settings.outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(outFolder);
            settings.copyExtraFiles = true;

            var result = LilToonShaderMerger.Build(settings);
            Object.DestroyImmediate(settings);

            int errors = 0, warnings = 0;
            var diagSummary = new StringBuilder();
            foreach (var d in result.Diagnostics)
            {
                if (d.Severity == Severity.Error) errors++;
                else warnings++;
                diagSummary.Append("\n    ").Append(d.ToString());
            }

            var status = result.Success ? "OK" : "FAIL";
            return $"[{outName}] {status} files={result.WrittenFiles.Count} errors={errors} warnings={warnings}{diagSummary}";
        }
    }
}
#endif

using UnityEditor;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger
{
    [CustomEditor(typeof(LilToonShaderMergerSettings))]
    public class LilToonShaderMergerSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            var s = (LilToonShaderMergerSettings)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Project"))
                {
                    ProjectScanner.ShowPicker(s);
                }
            }
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build", GUILayout.Height(30)))
                {
                    var result = LilToonShaderMerger.Build(s);
                    ReportResult(result);
                }
                if (GUILayout.Button("Dry Run", GUILayout.Height(30)))
                {
                    var result = LilToonShaderMerger.DryRun(s);
                    ReportResult(result);
                }
            }
            if (GUILayout.Button(new GUIContent("Verify Compile", "Build 済みの出力シェーダーを実際にコンパイルして検証する (時間がかかる)")))
            {
                var result = LilToonShaderMerger.VerifyCompile(s);
                LogDiagnostics(result);
                int mergeErrors = 0, mergeWarnings = 0, preExisting = 0, other = 0;
                foreach (var d in result.Diagnostics)
                {
                    if (d.Category == ShaderCompileVerifier.MergeCausedCategory)
                    {
                        if (d.Severity == Severity.Error) mergeErrors++;
                        else mergeWarnings++;
                    }
                    else if (d.Category.StartsWith(ShaderCompileVerifier.PreExistingCategoryPrefix)) preExisting++;
                    else other++;
                }
                EditorUtility.DisplayDialog("lilToon Shader Merger",
                    $"Caused by merging: {mergeErrors} error(s), {mergeWarnings} warning(s)\n" +
                    $"Pre-existing in original shaders: {preExisting}\n" +
                    (other > 0 ? $"Other: {other}\n" : "") +
                    "\nDetails are in the Console.", "OK");
            }
        }

        static bool LogDiagnostics(BuildResult r)
        {
            var hasError = false;
            foreach (var d in r.Diagnostics)
            {
                if (d.Severity == Severity.Error) { Debug.LogError(d.ToString()); hasError = true; }
                else Debug.LogWarning(d.ToString());
            }
            return hasError;
        }

        static void ReportResult(BuildResult r)
        {
            if (LogDiagnostics(r))
                EditorUtility.DisplayDialog("lilToon Shader Merger", $"Build failed with {r.Diagnostics.Count} diagnostic(s). Check Console.", "OK");
            else
                EditorUtility.DisplayDialog("lilToon Shader Merger", $"Success. Wrote {r.WrittenFiles.Count} file(s).", "OK");
        }
    }
}

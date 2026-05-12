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
        }

        static void ReportResult(BuildResult r)
        {
            var hasError = false;
            foreach (var d in r.Diagnostics)
            {
                if (d.Severity == Severity.Error) { Debug.LogError(d.ToString()); hasError = true; }
                else Debug.LogWarning(d.ToString());
            }
            if (hasError)
                EditorUtility.DisplayDialog("lilToon Shader Merger", $"Build failed with {r.Diagnostics.Count} diagnostic(s). Check Console.", "OK");
            else
                EditorUtility.DisplayDialog("lilToon Shader Merger", $"Success. Wrote {r.WrittenFiles.Count} file(s).", "OK");
        }
    }
}

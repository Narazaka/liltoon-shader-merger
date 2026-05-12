using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class ProjectScanner
    {
        public class Candidate
        {
            public string FolderPath;
            public string ShaderName;
        }

        public static void ShowPicker(LilToonShaderMergerSettings target)
        {
            var candidates = ScanProject();
            var existing = new HashSet<string>();
            if (target.sourceFolders != null)
                foreach (var f in target.sourceFolders)
                    if (f != null) existing.Add(AssetDatabase.GetAssetPath(f));

            PickerWindow.Show(candidates, existing, picked =>
            {
                var list = new List<DefaultAsset>();
                if (target.sourceFolders != null) list.AddRange(target.sourceFolders);
                foreach (var p in picked)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(p);
                    if (asset != null && !list.Contains(asset)) list.Add(asset);
                }
                target.sourceFolders = list.ToArray();
                EditorUtility.SetDirty(target);
            });
        }

        public static List<Candidate> ScanProject()
        {
            var found = new List<Candidate>();
            var guids = AssetDatabase.FindAssets("lilCustomShaderDatas");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("lilCustomShaderDatas.lilblock")) continue;
                var folder = Path.GetDirectoryName(path).Replace('\\', '/');
                var d = LilBlockParser.ParseDatas(File.ReadAllText(path));
                found.Add(new Candidate { FolderPath = folder, ShaderName = d.ShaderName });
            }
            return found;
        }
    }

    public class PickerWindow : EditorWindow
    {
        List<ProjectScanner.Candidate> candidates;
        HashSet<string> existing;
        System.Action<List<string>> onPicked;
        Dictionary<string, bool> selected = new Dictionary<string, bool>();

        public static void Show(List<ProjectScanner.Candidate> cands, HashSet<string> existing, System.Action<List<string>> onPicked)
        {
            var w = CreateInstance<PickerWindow>();
            w.candidates = cands;
            w.existing = existing;
            w.onPicked = onPicked;
            w.titleContent = new GUIContent("Pick lilToon Custom Shader Folders");
            w.minSize = new Vector2(500, 400);
            w.ShowUtility();
        }

        Vector2 scroll;
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var c in candidates)
            {
                bool already = existing.Contains(c.FolderPath);
                using (new EditorGUI.DisabledScope(already))
                {
                    selected.TryGetValue(c.FolderPath, out var on);
                    on = EditorGUILayout.ToggleLeft($"{c.FolderPath}  [{c.ShaderName}]", on);
                    selected[c.FolderPath] = on;
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Selected"))
                {
                    var picked = new List<string>();
                    foreach (var kv in selected) if (kv.Value) picked.Add(kv.Key);
                    onPicked?.Invoke(picked);
                    Close();
                }
                if (GUILayout.Button("Cancel")) Close();
            }
        }
    }
}

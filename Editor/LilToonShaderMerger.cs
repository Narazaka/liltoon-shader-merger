using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public class BuildResult
    {
        public bool Success { get; set; }
        public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();
        public List<string> WrittenFiles { get; } = new List<string>();
    }

    public static class LilToonShaderMerger
    {
        public static BuildResult DryRun(LilToonShaderMergerSettings s) => RunInternal(s, emit: false);
        public static BuildResult Build(LilToonShaderMergerSettings s) => RunInternal(s, emit: true);

        static BuildResult RunInternal(LilToonShaderMergerSettings s, bool emit)
        {
            var result = new BuildResult();

            if (s == null)
            {
                result.Diagnostics.Add(new Diagnostic { Severity = Severity.Error, Category = "input", Message = "Settings is null" });
                return result;
            }
            if (s.sourceFolders == null || s.sourceFolders.Length == 0)
            {
                result.Diagnostics.Add(new Diagnostic { Severity = Severity.Error, Category = "input", Message = "No source folders" });
                return result;
            }
            if (string.IsNullOrWhiteSpace(s.shaderName))
            {
                result.Diagnostics.Add(new Diagnostic { Severity = Severity.Error, Category = "input", Message = "shaderName is required" });
                return result;
            }

            // Phase 1: Analyze
            var parsed = new List<ParsedSource>();
            foreach (var folder in s.sourceFolders)
            {
                if (folder == null) continue;
                var path = AssetDatabase.GetAssetPath(folder);
                var src = AnalyzeFolder(path, result.Diagnostics);
                if (src != null) parsed.Add(src);
            }

            if (HasErrors(result.Diagnostics)) return result;

            // Merge dimensions
            var hlslSources = new List<CustomHlslData>();
            foreach (var p in parsed) hlslSources.Add(p.Hlsl);
            var mergedHlsl = MacroMerger.Merge(hlslSources, s.functionConflict, result.Diagnostics);

            var propsSources = new List<(string, CustomProperties)>();
            foreach (var p in parsed) propsSources.Add((p.SourceKey, p.Properties));
            var mergedProps = PropertiesMerger.Merge(propsSources, s.propertyConflict, result.Diagnostics);

            var dataSources = new List<(string, CustomShaderDatas)>();
            foreach (var p in parsed) dataSources.Add((p.SourceKey, p.Datas));
            var mergedDatas = DataMerger.Merge(
                dataSources,
                s.shaderName,
                ResolveEditorName(s, parsed),
                s.replaceConflict,
                result.Diagnostics);

            // Inspector
            var inspectorSources = new List<(string, ParsedInspector)>();
            foreach (var p in parsed)
                if (p.Inspector != null) inspectorSources.Add((p.SourceKey, p.Inspector));
            string mergedInspectorCs = null;
            if (inspectorSources.Count > 0)
            {
                var className = !string.IsNullOrWhiteSpace(s.editorClassName)
                    ? s.editorClassName
                    : DeriveClassName(s.shaderName);
                mergedInspectorCs = InspectorMerger.Generate(inspectorSources, className, s.shaderName, "lilToon", result.Diagnostics);
            }

            if (HasErrors(result.Diagnostics)) return result;
            if (!emit) { result.Success = true; return result; }

            // Phase 2: Emit
            try
            {
                var outFolder = AssetDatabase.GetAssetPath(s.outputFolder);
                if (string.IsNullOrEmpty(outFolder))
                {
                    result.Diagnostics.Add(new Diagnostic { Severity = Severity.Error, Category = "output", Message = "Output folder not set" });
                    return result;
                }
                if (!Directory.Exists(outFolder)) Directory.CreateDirectory(outFolder);

                WriteAndTrack(result, Path.Combine(outFolder, "custom.hlsl"), HlslEmitter.EmitCustomHlsl(mergedHlsl));

                var insertBodies = new List<(string, string)>();
                foreach (var p in parsed)
                {
                    var ciPath = Path.Combine(p.FolderPath, "custom_insert.hlsl");
                    if (File.Exists(ciPath)) insertBodies.Add((p.SourceKey, File.ReadAllText(ciPath)));
                }
                WriteAndTrack(result, Path.Combine(outFolder, "custom_insert.hlsl"), HlslEmitter.EmitCustomInsertHlsl(insertBodies));

                WriteAndTrack(result, Path.Combine(outFolder, "lilCustomShaderProperties.lilblock"), LilBlockEmitter.EmitProperties(mergedProps));
                WriteAndTrack(result, Path.Combine(outFolder, "lilCustomShaderDatas.lilblock"), LilBlockEmitter.EmitDatas(mergedDatas));

                var insertBlockSources = new List<(string, string)>();
                foreach (var p in parsed)
                {
                    if (!string.IsNullOrEmpty(p.InsertBlockText))
                        insertBlockSources.Add((p.SourceKey, p.InsertBlockText));
                }
                WriteAndTrack(result, Path.Combine(outFolder, "lilCustomShaderInsert.lilblock"),
                    LilBlockEmitter.EmitInsertBlock(insertBlockSources, s.dedupeIdenticalIncludes));

                // .lilcontainer の union
                var folderPaths = new List<string>();
                foreach (var p in parsed) folderPaths.Add(p.FolderPath);
                var containerFiles = LilContainerEmitter.CollectContainerFiles(folderPaths);
                foreach (var fn in containerFiles)
                {
                    var srcs = new List<(string, string)>();
                    foreach (var p in parsed)
                    {
                        var fp = Path.Combine(p.FolderPath, fn);
                        if (File.Exists(fp)) srcs.Add((p.SourceKey, File.ReadAllText(fp)));
                    }
                    var merged = LilContainerEmitter.MergeContainerText(srcs, result.Diagnostics);
                    WriteAndTrack(result, Path.Combine(outFolder, fn), merged);
                }

                // テンプレ外ファイル (extra .hlsl 等) の検出
                CopyOrWarnExtraFiles(parsed, outFolder, s.copyExtraFiles, result);

                // Inspector
                if (mergedInspectorCs != null)
                {
                    var editorDir = Path.Combine(outFolder, "Editor");
                    if (!Directory.Exists(editorDir)) Directory.CreateDirectory(editorDir);
                    var className = !string.IsNullOrWhiteSpace(s.editorClassName) ? s.editorClassName : DeriveClassName(s.shaderName);
                    WriteAndTrack(result, Path.Combine(editorDir, $"{className}.cs"), mergedInspectorCs);
                    WriteAndTrack(result, Path.Combine(editorDir, $"{className}.Editor.asmdef"),
                        AsmdefEmitter.Emit($"{className}.Editor", FindLilToonEditorGuid()));

                    // Inspector .cs の同フォルダ内の sibling .cs (helper class 等) を出力側にコピー
                    // 例: HawaseGimmickShader の GUI_keys.cs / GUI_labels.cs (namespace KuukuuVirtualFactory.HawaseGimmickShader)
                    CopySiblingInspectorScripts(parsed, editorDir, result);
                }

                AssetDatabase.Refresh();
                result.Success = true;
            }
            catch (System.Exception e)
            {
                result.Diagnostics.Add(new Diagnostic { Severity = Severity.Error, Category = "io", Message = e.Message });
                foreach (var f in result.WrittenFiles)
                {
                    try { if (File.Exists(f)) File.Delete(f); } catch { }
                }
            }

            return result;
        }

        static void WriteAndTrack(BuildResult r, string path, string content)
        {
            File.WriteAllText(path, content);
            r.WrittenFiles.Add(path);
        }

        // Inspector .cs の同フォルダ内の sibling .cs (helper class 等) を merged Inspector の Editor フォルダにコピー
        // Inspector が lilToonInspector を継承する class なら、それと同居する helper .cs を持ってこないと参照が解決しない
        static void CopySiblingInspectorScripts(List<ParsedSource> parsed, string outEditorDir, BuildResult result)
        {
            var copiedNames = new Dictionary<string, string>(); // filename → sourceKey
            foreach (var p in parsed)
            {
                if (p.Inspector == null || !p.Inspector.PatternMatched) continue;
                if (string.IsNullOrEmpty(p.Inspector.InspectorCsPath)) continue;
                var dir = Path.GetDirectoryName(p.Inspector.InspectorCsPath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                var inspectorFile = Path.GetFileName(p.Inspector.InspectorCsPath);
                foreach (var f in Directory.GetFiles(dir, "*.cs"))
                {
                    var name = Path.GetFileName(f);
                    if (name == inspectorFile) continue; // 本体 Inspector はスキップ (merged class 側で扱う)

                    if (copiedNames.TryGetValue(name, out var prevKey) && prevKey != p.SourceKey)
                    {
                        result.Diagnostics.Add(new Diagnostic
                        {
                            Severity = Severity.Warning,
                            Category = "inspector-sibling",
                            Message = $"sibling .cs '{name}' collision between {prevKey} and {p.SourceKey}; using {prevKey} (first wins)"
                        });
                        continue;
                    }
                    var dest = Path.Combine(outEditorDir, name);
                    File.Copy(f, dest, true);
                    result.WrittenFiles.Add(dest);
                    copiedNames[name] = p.SourceKey;
                }
            }
        }

        // テンプレートに含まれない HLSL/その他のファイルを検出し、copyExtraFiles=true ならコピー
        static readonly HashSet<string> CanonicalFiles = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "custom.hlsl", "custom_insert.hlsl",
            "lilCustomShaderProperties.lilblock",
            "lilCustomShaderInsert.lilblock",
            "lilCustomShaderDatas.lilblock",
        };

        static void CopyOrWarnExtraFiles(List<ParsedSource> parsed, string outFolder, bool copyExtras, BuildResult result)
        {
            var copiedNames = new Dictionary<string, string>(); // name → first sourceKey copied
            foreach (var p in parsed)
            {
                if (!Directory.Exists(p.FolderPath)) continue;
                foreach (var f in Directory.GetFiles(p.FolderPath))
                {
                    var name = Path.GetFileName(f);
                    if (name.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.EndsWith(".lilcontainer", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (CanonicalFiles.Contains(name)) continue;

                    if (copyExtras)
                    {
                        if (copiedNames.TryGetValue(name, out var prevKey) && prevKey != p.SourceKey)
                        {
                            result.Diagnostics.Add(new Diagnostic
                            {
                                Severity = Severity.Warning,
                                Category = "extra-file",
                                Message = $"extra file '{name}' name collision between {prevKey} and {p.SourceKey}; using {prevKey} (first wins)"
                            });
                            continue;
                        }
                        var dest = Path.Combine(outFolder, name);
                        File.Copy(f, dest, true);
                        result.WrittenFiles.Add(dest);
                        copiedNames[name] = p.SourceKey;
                    }
                    else
                    {
                        result.Diagnostics.Add(new Diagnostic
                        {
                            Severity = Severity.Warning,
                            Category = "extra-file",
                            Message = $"extra file '{name}' in {p.SourceKey} not copied (set copyExtraFiles=true to include). Merged shader may fail to compile if it references this file."
                        });
                    }
                }
            }
        }

        static bool HasErrors(List<Diagnostic> diags)
        {
            foreach (var d in diags) if (d.Severity == Severity.Error) return true;
            return false;
        }

        static string ResolveEditorName(LilToonShaderMergerSettings s, List<ParsedSource> parsed)
        {
            bool hasInspector = false;
            foreach (var p in parsed) if (p.Inspector?.PatternMatched == true) { hasInspector = true; break; }
            if (!hasInspector) return "lilToon.lilToonInspector";
            var cls = !string.IsNullOrWhiteSpace(s.editorClassName) ? s.editorClassName : DeriveClassName(s.shaderName);
            return $"lilToon.{cls}";
        }

        static string DeriveClassName(string shaderName)
        {
            var safe = new System.Text.StringBuilder();
            foreach (var c in shaderName) safe.Append(char.IsLetterOrDigit(c) ? c : '_');
            return safe.ToString() + "Inspector";
        }

        static ParsedSource AnalyzeFolder(string folder, List<Diagnostic> diags)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                diags.Add(new Diagnostic { Severity = Severity.Error, Category = "input", Message = $"Folder not found: {folder}" });
                return null;
            }
            var src = new ParsedSource
            {
                FolderPath = folder,
                Hlsl = new CustomHlslData(),
                Datas = new CustomShaderDatas(),
                Properties = new CustomProperties()
            };

            var customHlsl = Path.Combine(folder, "custom.hlsl");
            if (File.Exists(customHlsl)) src.Hlsl = CustomHlslParser.Parse(File.ReadAllText(customHlsl));

            var datas = Path.Combine(folder, "lilCustomShaderDatas.lilblock");
            if (File.Exists(datas)) src.Datas = LilBlockParser.ParseDatas(File.ReadAllText(datas));

            // SourceKey: 優先順 = ShaderName (Datas) > フォルダ名 (フォルダが "Shaders" 等で衝突しやすいのを避ける)
            src.SourceKey = !string.IsNullOrEmpty(src.Datas.ShaderName)
                ? src.Datas.ShaderName
                : Path.GetFileName(folder.TrimEnd('/', '\\'));

            var props = Path.Combine(folder, "lilCustomShaderProperties.lilblock");
            if (File.Exists(props)) src.Properties = LilBlockParser.ParseProperties(File.ReadAllText(props));

            var insert = Path.Combine(folder, "lilCustomShaderInsert.lilblock");
            if (File.Exists(insert)) src.InsertBlockText = File.ReadAllText(insert);

            // Inspector .cs を EditorName から逆引き
            if (!string.IsNullOrEmpty(src.Datas.EditorName))
                src.Inspector = FindInspectorByEditorName(src.Datas.EditorName);

            return src;
        }

        static ParsedInspector FindInspectorByEditorName(string editorName)
        {
            var baseType = System.Type.GetType("lilToon.lilToonInspector,lilToon.Editor");
            if (baseType == null) return null;
            foreach (var t in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (t.FullName != editorName) continue;

                // クラス名とファイル名が一致しないケースがある (例: motchiriInspector が CustomInspector.cs 内)
                // すべての MonoScript を走査し GetClass() で一致確認
                foreach (var guid in AssetDatabase.FindAssets("t:MonoScript"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (ms == null) continue;
                    var cls = ms.GetClass();
                    if (cls != null && cls == t)
                    {
                        var parsed = InspectorCsParser.Parse(File.ReadAllText(path));
                        parsed.InspectorCsPath = path;
                        return parsed;
                    }
                }
            }
            return null;
        }

        static string FindLilToonEditorGuid()
        {
            var asmdef = AssetDatabase.FindAssets("lilToon.Editor t:asmdef");
            foreach (var guid in asmdef)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "lilToon.Editor.asmdef")
                {
                    var metaPath = path + ".meta";
                    if (File.Exists(metaPath))
                    {
                        foreach (var line in File.ReadAllLines(metaPath))
                        {
                            if (line.StartsWith("guid:")) return line.Substring(5).Trim();
                        }
                    }
                }
            }
            return "";
        }
    }
}

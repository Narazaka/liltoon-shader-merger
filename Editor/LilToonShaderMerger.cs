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
        public static BuildResult DryRun(LilToonShaderMergerSettings s) => RunInternal(s, emit: false, refreshAssetDatabase: false);
        // refreshAssetDatabase=false skips the trailing AssetDatabase.Refresh() — used by tests to
        // avoid Unity re-importing/recompiling unrelated project assets as a side effect.
        public static BuildResult Build(LilToonShaderMergerSettings s, bool refreshAssetDatabase = true) => RunInternal(s, emit: true, refreshAssetDatabase);

        static BuildResult RunInternal(LilToonShaderMergerSettings s, bool emit, bool refreshAssetDatabase)
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

                WriteAndTrack(result, outFolder, s.shaderName, Path.Combine(outFolder, "custom.hlsl"), HlslEmitter.EmitCustomHlsl(mergedHlsl));

                var insertBodies = new List<(string, string)>();
                foreach (var p in parsed)
                {
                    var ciPath = Path.Combine(p.FolderPath, "custom_insert.hlsl");
                    if (File.Exists(ciPath)) insertBodies.Add((p.SourceKey, File.ReadAllText(ciPath)));
                }
                WriteAndTrack(result, outFolder, s.shaderName, Path.Combine(outFolder, "custom_insert.hlsl"), HlslEmitter.EmitCustomInsertHlsl(insertBodies));

                WriteAndTrack(result, outFolder, s.shaderName, Path.Combine(outFolder, "lilCustomShaderProperties.lilblock"), LilBlockEmitter.EmitProperties(mergedProps));
                WriteAndTrack(result, outFolder, s.shaderName, Path.Combine(outFolder, "lilCustomShaderDatas.lilblock"), LilBlockEmitter.EmitDatas(mergedDatas));

                var insertBlockSources = new List<(string, string)>();
                foreach (var p in parsed)
                {
                    if (!string.IsNullOrEmpty(p.InsertBlockText))
                        insertBlockSources.Add((p.SourceKey, p.InsertBlockText));
                }
                WriteAndTrack(result, outFolder, s.shaderName, Path.Combine(outFolder, "lilCustomShaderInsert.lilblock"),
                    LilBlockEmitter.EmitInsertBlock(insertBlockSources, s.dedupeIdenticalIncludes));

                // .lilcontainer の union
                var folderPaths = new List<string>();
                foreach (var p in parsed) folderPaths.Add(p.FolderPath);
                var containerFiles = new List<string>(LilContainerEmitter.CollectContainerFiles(folderPaths));
                var lilcontainerImporter = LoadLilcontainerImporterBlock(parsed);
                if (lilcontainerImporter == null && containerFiles.Count > 0)
                {
                    result.Diagnostics.Add(new Diagnostic
                    {
                        Severity = Severity.Warning,
                        Category = "meta-importer",
                        Message = "no source .lilcontainer.meta found to derive ScriptedImporter block; emitting DefaultImporter as fallback. Unity will rewrite the importer body on Refresh but our deterministic guid is preserved."
                    });
                }
                foreach (var fn in containerFiles)
                {
                    var srcs = new List<(string, string)>();
                    foreach (var p in parsed)
                    {
                        var fp = Path.Combine(p.FolderPath, fn);
                        if (File.Exists(fp)) srcs.Add((p.SourceKey, File.ReadAllText(fp)));
                    }
                    var merged = LilContainerEmitter.MergeContainerText(srcs, result.Diagnostics);
                    WriteAndTrack(result, outFolder, s.shaderName, Path.Combine(outFolder, fn), merged, lilcontainerImporter);
                }

                CopyReferencedExtraFiles(parsed, outFolder, s.shaderName, result);

                // Inspector
                if (mergedInspectorCs != null)
                {
                    var editorDir = Path.Combine(outFolder, "Editor");
                    CreateDirAndTrackMeta(result, outFolder, s.shaderName, editorDir);
                    var className = !string.IsNullOrWhiteSpace(s.editorClassName) ? s.editorClassName : DeriveClassName(s.shaderName);
                    WriteAndTrack(result, outFolder, s.shaderName, Path.Combine(editorDir, $"{className}.cs"), mergedInspectorCs);
                    WriteAndTrack(result, outFolder, s.shaderName, Path.Combine(editorDir, $"{className}.Editor.asmdef"),
                        AsmdefEmitter.Emit($"{className}.Editor", FindLilToonEditorGuid()));

                    // Inspector .cs の同フォルダ内の sibling .cs (helper class 等) を出力側にコピー
                    // 例: HawaseGimmickShader の GUI_keys.cs / GUI_labels.cs (namespace KuukuuVirtualFactory.HawaseGimmickShader)
                    CopySiblingInspectorScripts(parsed, outFolder, editorDir, s.shaderName, result);
                }

                if (refreshAssetDatabase) AssetDatabase.Refresh();
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

        // shaderName/outFolder are captured at the call site and passed through for relative path derivation.
        static void WriteAndTrack(BuildResult r, string outFolder, string shaderName, string path, string content, string sourceImporterBlock = null)
        {
            File.WriteAllText(path, content);
            r.WrittenFiles.Add(path);
            EmitMetaAndTrack(r, outFolder, shaderName, path, sourceImporterBlock, isFolder: false);
        }

        static void CopyAndTrackMeta(BuildResult r, string outFolder, string shaderName, string srcPath, string destPath)
        {
            File.Copy(srcPath, destPath, true);
            r.WrittenFiles.Add(destPath);
            // For copied files, reuse the source .meta importer block when present (handles unknown extensions and
            // preserves things like ScriptedImporter script refs).
            string sourceImporter = null;
            var srcMeta = srcPath + ".meta";
            if (File.Exists(srcMeta)) sourceImporter = ExtractImporterBlock(File.ReadAllText(srcMeta));
            var ext = Path.GetExtension(destPath).ToLowerInvariant();
            if (sourceImporter == null && IsUnknownImporterExtension(ext))
            {
                r.Diagnostics.Add(new Diagnostic
                {
                    Severity = Severity.Warning,
                    Category = "meta-importer",
                    Message = $"no source .meta found for '{Path.GetFileName(srcPath)}'; falling back to DefaultImporter (Unity may overwrite importer body on Refresh)"
                });
            }
            EmitMetaAndTrack(r, outFolder, shaderName, destPath, sourceImporter, isFolder: false);
        }

        static bool IsUnknownImporterExtension(string ext)
        {
            switch (ext)
            {
                case ".hlsl":
                case ".lilblock":
                case ".cs":
                case ".asmdef":
                    return false;
                default:
                    return true;
            }
        }

        static void CreateDirAndTrackMeta(BuildResult r, string outFolder, string shaderName, string dirPath)
        {
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
            EmitMetaAndTrack(r, outFolder, shaderName, dirPath, sourceImporterBlock: null, isFolder: true);
        }

        static void EmitMetaAndTrack(BuildResult r, string outFolder, string shaderName, string assetPath, string sourceImporterBlock, bool isFolder)
        {
            var rel = MetaGuidEmitter.Relative(outFolder, assetPath);
            var guid = MetaGuidEmitter.DeterministicGuid(shaderName, rel);
            MetaGuidEmitter.WriteMeta(assetPath, guid, sourceImporterBlock, isFolder);
            r.WrittenFiles.Add(assetPath + ".meta");
        }

        // Extract everything after the "guid: ..." line from a .meta file's text.
        static string ExtractImporterBlock(string metaText)
        {
            var lines = metaText.Replace("\r\n", "\n").Split('\n');
            var sb = new System.Text.StringBuilder();
            bool capturing = false;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!capturing)
                {
                    if (line.StartsWith("guid:", System.StringComparison.Ordinal)) { capturing = true; continue; }
                    if (line.StartsWith("folderAsset:", System.StringComparison.Ordinal)) continue;
                    continue;
                }
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        // For .lilcontainer outputs we need an importer block whose ScriptedImporter script: GUID points at
        // lilToon's lilContainerImporter MonoScript. Pull it from the first source folder that has an lts.lilcontainer.meta.
        static string LoadLilcontainerImporterBlock(List<ParsedSource> parsed)
        {
            foreach (var p in parsed)
            {
                if (!Directory.Exists(p.FolderPath)) continue;
                foreach (var f in Directory.GetFiles(p.FolderPath, "*.lilcontainer"))
                {
                    var meta = f + ".meta";
                    if (File.Exists(meta)) return ExtractImporterBlock(File.ReadAllText(meta));
                }
            }
            return null;
        }

        // Inspector .cs の同フォルダ内の sibling .cs (helper class 等) を merged Inspector の Editor フォルダにコピー
        // Inspector が lilToonInspector を継承する class なら、それと同居する helper .cs を持ってこないと参照が解決しない
        static void CopySiblingInspectorScripts(List<ParsedSource> parsed, string outFolder, string outEditorDir, string shaderName, BuildResult result)
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
                    CopyAndTrackMeta(result, outFolder, shaderName, f, dest);
                    copiedNames[name] = p.SourceKey;
                }
            }
        }

        // 正規ファイル (merger が再生成するもの)。 これら以外で #include されているファイルをソースフォルダからコピーする
        static readonly HashSet<string> CanonicalFiles = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "custom.hlsl", "custom_insert.hlsl",
            "lilCustomShaderProperties.lilblock",
            "lilCustomShaderInsert.lilblock",
            "lilCustomShaderDatas.lilblock",
        };
        static readonly System.Text.RegularExpressions.Regex IncludeRegex =
            new System.Text.RegularExpressions.Regex(@"#include\s+""([^""]+)""");

        // lilToon の container importer は Assets/ Packages/ で始まらない #include にソースフォルダのパスを前置する。
        // そのため .hlsl / .lilcontainer / .lilblock が参照する同フォルダ内ファイルは出力側にも無いとコンパイルできない
        static void CopyReferencedExtraFiles(List<ParsedSource> parsed, string outFolder, string shaderName, BuildResult result)
        {
            var copiedNames = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase); // name → first sourceKey copied
            foreach (var p in parsed)
            {
                if (!Directory.Exists(p.FolderPath)) continue;
                var queue = new Queue<string>();
                foreach (var f in Directory.GetFiles(p.FolderPath))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".hlsl" || ext == ".lilcontainer" || ext == ".lilblock") queue.Enqueue(f);
                }
                var visited = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                while (queue.Count > 0)
                {
                    var file = queue.Dequeue();
                    if (!visited.Add(file)) continue;
                    foreach (System.Text.RegularExpressions.Match m in IncludeRegex.Matches(File.ReadAllText(file)))
                    {
                        var name = m.Groups[1].Value;
                        if (name.StartsWith("Assets/") || name.StartsWith("Packages/")) continue;
                        if (CanonicalFiles.Contains(name)) continue;
                        var src = Path.Combine(p.FolderPath, name);
                        if (!File.Exists(src)) continue; // lilToon 本体側の include 等
                        queue.Enqueue(src);
                        if (copiedNames.TryGetValue(name, out var prevKey))
                        {
                            if (prevKey != p.SourceKey)
                                result.Diagnostics.Add(new Diagnostic
                                {
                                    Severity = Severity.Warning,
                                    Category = "extra-file",
                                    Message = $"extra file '{name}' name collision between {prevKey} and {p.SourceKey}; using {prevKey} (first wins)"
                                });
                            continue;
                        }
                        var dest = Path.Combine(outFolder, name);
                        var destDir = Path.GetDirectoryName(dest);
                        if (destDir != outFolder) CreateDirAndTrackMeta(result, outFolder, shaderName, destDir);
                        CopyAndTrackMeta(result, outFolder, shaderName, src, dest);
                        copiedNames[name] = p.SourceKey;
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

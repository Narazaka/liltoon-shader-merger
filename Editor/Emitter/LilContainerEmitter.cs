using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class LilContainerEmitter
    {
        static readonly Regex HlslIncludeBlock = new Regex(
            @"HLSLINCLUDE\s*(.*?)\s*ENDHLSL",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // 同名 .lilcontainer ファイルの内容を merge。
        // customHlslVariants は CollectCustomHlslVariants の結果 (出力フォルダ相対パス)
        public static string MergeContainerText(IReadOnlyList<(string sourceKey, string text)> sources, ICollection<string> customHlslVariants, List<Diagnostic> diags)
        {
            if (sources.Count == 1) return sources[0].text;

            // HLSLINCLUDE は Shader 直下と SubShader 内など複数あり得るので、出現順のインデックスで対応付けて merge する
            var first = HlslIncludeBlock.Matches(sources[0].text);
            var others = new List<MatchCollection>();
            for (int i = 1; i < sources.Count; i++)
            {
                var ms = HlslIncludeBlock.Matches(sources[i].text);
                if (ms.Count != first.Count)
                    diags.Add(new Diagnostic
                    {
                        Severity = Severity.Warning,
                        Category = "lilcontainer",
                        Message = $"HLSLINCLUDE block count differs between {sources[0].sourceKey} ({first.Count}) and {sources[i].sourceKey} ({ms.Count}); blocks are matched by order and may be merged into the wrong place"
                    });
                others.Add(ms);
            }

            int blockIndex = 0;
            return HlslIncludeBlock.Replace(sources[0].text, firstMatch =>
            {
                var idx = blockIndex++;
                var mergedHlslLines = new List<string>();
                var seen = new HashSet<string>();
                void AddLines(Match m)
                {
                    foreach (var ln in m.Groups[1].Value.Replace("\r\n", "\n").Split('\n'))
                    {
                        // 条件ディレクティブまで重複除去すると 2 ソース目以降の #if と #endif の対応が崩れる
                        if (ConditionalDirective.IsMatch(ln) || seen.Add(ln.Trim())) mergedHlslLines.Add(ln);
                    }
                }
                AddLines(firstMatch);
                foreach (var ms in others)
                    if (idx < ms.Count) AddLines(ms[idx]);

                // 派生を使うソースがあれば merge 済み派生が他ソース分も含むので、custom.hlsl を並べて include すると二重定義になる
                var variants = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var ln in mergedHlslLines)
                {
                    var inc = LocalInclude(ln);
                    if (inc != null && customHlslVariants.Contains(inc)) variants.Add(inc);
                }
                if (variants.Count > 1)
                    diags.Add(new Diagnostic
                    {
                        Severity = Severity.Warning,
                        Category = "lilcontainer",
                        Message = $"HLSLINCLUDE block includes multiple custom.hlsl variants ({string.Join(", ", variants)}); the merged shader will likely fail to compile"
                    });
                if (variants.Count > 0) mergedHlslLines.RemoveAll(ln => IsCustomHlsl(LocalInclude(ln)));

                return "HLSLINCLUDE\n" + string.Join("\n", mergedHlslLines) + "\nENDHLSL";
            });
        }

        static readonly Regex IncludeLine = new Regex(@"^\s*#include\s+""([^""]+)""", RegexOptions.Compiled);
        static readonly Regex ConditionalDirective = new Regex(@"^\s*#\s*(if|ifdef|ifndef|elif|else|endif)\b", RegexOptions.Compiled);

        // Assets/ Packages/ 始まり以外の include (lilToon の importer がソースフォルダ相対に解決するもの) を正規化して返す
        static string LocalInclude(string line)
        {
            var m = IncludeLine.Match(line);
            if (!m.Success) return null;
            var path = m.Groups[1].Value.Replace('\\', '/');
            if (path.StartsWith("Assets/") || path.StartsWith("Packages/")) return null;
            while (path.StartsWith("./")) path = path.Substring(2);
            return path;
        }

        static bool IsCustomHlsl(string include) =>
            string.Equals(include, "custom.hlsl", System.StringComparison.OrdinalIgnoreCase);

        // あるソースの HLSLINCLUDE が custom.hlsl を include せず、ソースフォルダ内の別ファイルを include していたら
        // それを custom.hlsl の派生とみなす (例: もっちりの custom_fur.hlsl)。custom.hlsl と併記される helper は派生ではない
        public static IEnumerable<string> CollectCustomHlslVariants(IEnumerable<string> sourceFolders)
        {
            var names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var folder in sourceFolders)
            {
                if (!Directory.Exists(folder)) continue;
                var folderFull = Path.GetFullPath(folder);
                foreach (var f in Directory.GetFiles(folder, "*.lilcontainer"))
                    foreach (Match b in HlslIncludeBlock.Matches(File.ReadAllText(f)))
                    {
                        var locals = new List<string>();
                        bool hasCustom = false;
                        foreach (var ln in b.Groups[1].Value.Replace("\r\n", "\n").Split('\n'))
                        {
                            var inc = LocalInclude(ln);
                            if (inc == null) continue;
                            if (IsCustomHlsl(inc)) { hasCustom = true; break; }
                            var full = Path.GetFullPath(Path.Combine(folder, inc));
                            var rel = MetaGuidEmitter.TryRelative(folderFull, full);
                            if (rel != null && File.Exists(full)) locals.Add(rel);
                        }
                        if (!hasCustom) names.UnionWith(locals);
                    }
            }
            return names;
        }

        // ファイル名 union を返す
        public static IEnumerable<string> CollectContainerFiles(IEnumerable<string> sourceFolders)
        {
            var names = new HashSet<string>();
            foreach (var folder in sourceFolders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var f in Directory.GetFiles(folder, "*.lilcontainer"))
                    names.Add(Path.GetFileName(f));
            }
            return names;
        }
    }
}

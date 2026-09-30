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

        // 同名 .lilcontainer ファイルの内容を merge
        public static string MergeContainerText(IReadOnlyList<(string sourceKey, string text)> sources, List<Diagnostic> diags)
        {
            if (sources.Count == 1) return sources[0].text;

            // HLSLINCLUDE は Shader 直下と SubShader 内など複数あり得るので、出現順のインデックスで対応付けて merge する
            var others = new List<MatchCollection>();
            for (int i = 1; i < sources.Count; i++) others.Add(HlslIncludeBlock.Matches(sources[i].text));

            int blockIndex = 0;
            return HlslIncludeBlock.Replace(sources[0].text, firstMatch =>
            {
                var idx = blockIndex++;
                var mergedHlslLines = new List<string>();
                var seen = new HashSet<string>();
                var variants = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                void AddLines(Match m)
                {
                    foreach (var ln in m.Groups[1].Value.Replace("\r\n", "\n").Split('\n'))
                    {
                        if (seen.Add(ln.Trim())) mergedHlslLines.Add(ln);
                    }
                    variants.UnionWith(BlockVariants(m.Groups[1].Value));
                }
                AddLines(firstMatch);
                foreach (var ms in others)
                    if (idx < ms.Count) AddLines(ms[idx]);

                // 派生を使うソースがあれば merge 済み派生が他ソース分も含むので、custom.hlsl を並べて include すると二重定義になる
                if (variants.Count > 1)
                    diags.Add(new Diagnostic
                    {
                        Severity = Severity.Warning,
                        Category = "lilcontainer",
                        Message = $"HLSLINCLUDE block includes multiple custom.hlsl variants ({string.Join(", ", variants)}); the merged shader will likely fail to compile"
                    });
                if (variants.Count > 0) mergedHlslLines.RemoveAll(ln => CustomHlslInclude.IsMatch(ln));

                return "HLSLINCLUDE\n" + string.Join("\n", mergedHlslLines) + "\nENDHLSL";
            });
        }

        static readonly Regex LocalHlslInclude = new Regex(@"^\s*#include\s+""([^""/\\]+\.hlsl)""\s*$", RegexOptions.Compiled);
        static readonly Regex CustomHlslInclude = new Regex(@"^\s*#include\s+""custom\.hlsl""\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 1 ソースの HLSLINCLUDE が custom.hlsl を include せず同フォルダの別 .hlsl を include していたら、
        // それを custom.hlsl の派生とみなす (例: もっちりの custom_fur.hlsl)。custom.hlsl と併記される helper は派生ではない
        static List<string> BlockVariants(string blockBody)
        {
            var locals = new List<string>();
            foreach (var ln in blockBody.Replace("\r\n", "\n").Split('\n'))
            {
                if (CustomHlslInclude.IsMatch(ln)) return new List<string>();
                var m = LocalHlslInclude.Match(ln);
                if (m.Success) locals.Add(m.Groups[1].Value);
            }
            return locals;
        }

        public static IEnumerable<string> CollectCustomHlslVariants(IEnumerable<string> sourceFolders)
        {
            var names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var folder in sourceFolders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var f in Directory.GetFiles(folder, "*.lilcontainer"))
                    foreach (Match b in HlslIncludeBlock.Matches(File.ReadAllText(f)))
                        names.UnionWith(BlockVariants(b.Groups[1].Value));
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

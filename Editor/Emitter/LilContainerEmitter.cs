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

            var first = sources[0].text;
            var firstMatch = HlslIncludeBlock.Match(first);
            if (!firstMatch.Success) return first;  // HLSLINCLUDE ブロックがない場合は 1 個目を採用

            var mergedHlslLines = new List<string>();
            var seen = new HashSet<string>();
            foreach (var ln in firstMatch.Groups[1].Value.Replace("\r\n", "\n").Split('\n'))
            {
                var key = ln.Trim();
                if (seen.Add(key)) mergedHlslLines.Add(ln);
            }
            for (int i = 1; i < sources.Count; i++)
            {
                var m = HlslIncludeBlock.Match(sources[i].text);
                if (!m.Success) continue;
                foreach (var ln in m.Groups[1].Value.Replace("\r\n", "\n").Split('\n'))
                {
                    var key = ln.Trim();
                    if (seen.Add(key)) mergedHlslLines.Add(ln);
                }
            }

            var mergedBlock = "HLSLINCLUDE\n" + string.Join("\n", mergedHlslLines) + "\nENDHLSL";
            return HlslIncludeBlock.Replace(first, mergedBlock);
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

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
                void AddLines(Match m)
                {
                    foreach (var ln in m.Groups[1].Value.Replace("\r\n", "\n").Split('\n'))
                    {
                        if (seen.Add(ln.Trim())) mergedHlslLines.Add(ln);
                    }
                }
                AddLines(firstMatch);
                foreach (var ms in others)
                    if (idx < ms.Count) AddLines(ms[idx]);
                return "HLSLINCLUDE\n" + string.Join("\n", mergedHlslLines) + "\nENDHLSL";
            });
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

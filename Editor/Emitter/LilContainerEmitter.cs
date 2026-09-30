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
        public static string MergeContainerText(string fileName, IReadOnlyList<(string sourceKey, string text)> sources, ICollection<string> customHlslVariants, List<Diagnostic> diags)
        {
            if (sources.Count == 1) return sources[0].text;

            // HLSLINCLUDE は Shader 直下と SubShader 内など複数あり得る。ソースごとに SubShader を自前で書くか
            // lilSubShaderBRP 等に任せるかが違うので、出現順ではなく置かれた位置で対応付けて merge する
            var firstKeys = BlockKeys(sources[0].text, HlslIncludeBlock.Matches(sources[0].text));
            var firstKeySet = new HashSet<string>(firstKeys);
            var others = new List<Dictionary<string, Match>>();
            for (int i = 1; i < sources.Count; i++)
            {
                var ms = HlslIncludeBlock.Matches(sources[i].text);
                var keys = BlockKeys(sources[i].text, ms);
                var byKey = new Dictionary<string, Match>();
                for (int j = 0; j < ms.Count; j++)
                {
                    byKey[keys[j]] = ms[j];
                    // 出力は先頭ソースの構造を使うので、先頭ソースに無い位置のブロックは捨てられる
                    if (!firstKeySet.Contains(keys[j]))
                        diags.Add(new Diagnostic
                        {
                            Severity = Severity.Warning,
                            Category = "lilcontainer",
                            Message = $"{fileName}: HLSLINCLUDE block at {keys[j]} of {sources[i].sourceKey} has no counterpart in {sources[0].sourceKey} and is dropped"
                        });
                }
                others.Add(byKey);
            }

            int blockIndex = 0;
            return HlslIncludeBlock.Replace(sources[0].text, firstMatch =>
            {
                var key = firstKeys[blockIndex++];
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
                foreach (var byKey in others)
                    if (byKey.TryGetValue(key, out var m)) AddLines(m);

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

        // 各 HLSLINCLUDE ブロックの位置を "Shader#0/SubShader#0/HLSLINCLUDE#0" のように、
        // 囲んでいる { } ブロックのキーワードと同階層での出現順で表す
        static List<string> BlockKeys(string text, MatchCollection blocks)
        {
            var keys = new List<string>();
            var stack = new List<(string path, Dictionary<string, int> counts)> { ("", new Dictionary<string, int>()) };
            string Next(string kw)
            {
                var top = stack[stack.Count - 1];
                top.counts.TryGetValue(kw, out var n);
                top.counts[kw] = n + 1;
                return top.path + "/" + kw + "#" + n;
            }
            int pos = 0;
            foreach (Match b in blocks)
            {
                for (; pos < b.Index; pos++)
                {
                    var c = text[pos];
                    if (c == '/' && pos + 1 < text.Length && text[pos + 1] == '/')
                    {
                        var nl = text.IndexOf('\n', pos);
                        pos = nl < 0 ? text.Length : nl;
                    }
                    else if (c == '"')
                    {
                        var q = text.IndexOf('"', pos + 1);
                        pos = q < 0 ? text.Length : q;
                    }
                    else if (c == '{') stack.Add((Next(PrecedingKeyword(text, pos)), new Dictionary<string, int>()));
                    else if (c == '}' && stack.Count > 1) stack.RemoveAt(stack.Count - 1);
                }
                keys.Add(Next("HLSLINCLUDE").TrimStart('/'));
                pos = b.Index + b.Length;
            }
            return keys;
        }

        // `Shader "name" {` / `SubShader {` / `Pass {` の { 直前のキーワード
        static string PrecedingKeyword(string text, int bracePos)
        {
            int i = bracePos - 1;
            while (i >= 0 && char.IsWhiteSpace(text[i])) i--;
            if (i > 0 && text[i] == '"')
            {
                i = System.Math.Max(text.LastIndexOf('"', i - 1), 0) - 1;
                while (i >= 0 && char.IsWhiteSpace(text[i])) i--;
            }
            int end = i + 1;
            while (i >= 0 && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i--;
            return text.Substring(i + 1, end - (i + 1));
        }

        static readonly Regex IncludeLine =new Regex(@"^\s*#include\s+""([^""]+)""", RegexOptions.Compiled);
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

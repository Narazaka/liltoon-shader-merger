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

        // 同名 .lilcontainer ファイルの内容を merge。合成できなければ Error 診断を積んで null を返す。
        // customHlslVariants は CollectCustomHlslVariants の結果 (出力フォルダ相対パス)
        public static string MergeContainerText(string fileName, IReadOnlyList<(string sourceKey, string text)> sources, ICollection<string> customHlslVariants, List<Diagnostic> diags) =>
            MergeContainerText(fileName, sources, customHlslVariants, LilContainerTemplate.StructureHashes(fileName), diags);

        // templateStructureHashes: lilToon カスタムシェーダーテンプレートのままの構造の StructureHash
        public static string MergeContainerText(string fileName, IReadOnlyList<(string sourceKey, string text)> sources, ICollection<string> customHlslVariants, ICollection<string> templateStructureHashes, List<Diagnostic> diags)
        {
            if (sources.Count == 1) return sources[0].text;

            // HLSLINCLUDE 以外 (lilSubShaderBRP 等の指定や自前の SubShader) は合成できないので、どれか 1 ソースの構造を土台にする。
            // テンプレートから書き換えたソースがあればそれを土台にし、別々に書き換えたソースが複数あれば合成できない
            int baseIndex = -1;
            var customized = new Dictionary<string, List<string>>(); // StructureHash → sourceKeys
            for (int i = 0; i < sources.Count; i++)
            {
                var hash = StructureHash(sources[i].text);
                if (templateStructureHashes.Contains(hash)) continue;
                if (!customized.TryGetValue(hash, out var keys))
                {
                    customized[hash] = keys = new List<string>();
                    if (baseIndex < 0) baseIndex = i;
                }
                keys.Add(sources[i].sourceKey);
            }
            if (customized.Count > 1)
            {
                var groups = new List<string>();
                foreach (var keys in customized.Values) groups.Add("[" + string.Join(", ", keys) + "]");
                diags.Add(new Diagnostic
                {
                    Severity = Severity.Error,
                    Category = "lilcontainer",
                    Message = $"{fileName}: sources customize the shader structure (outside HLSLINCLUDE) differently: {string.Join(" / ", groups)}; these cannot be merged. Remove all but one of them from sourceFolders"
                });
                return null;
            }
            if (baseIndex < 0) baseIndex = 0;
            var baseText = sources[baseIndex].text;

            // HLSLINCLUDE は Shader 直下と SubShader 内など複数あり得る。ソースごとに SubShader を自前で書くか
            // lilSubShaderBRP 等に任せるかが違うので、出現順ではなく置かれた位置で対応付けて merge する
            var baseKeys = BlockKeys(baseText, HlslIncludeBlock.Matches(baseText));
            var baseKeySet = new HashSet<string>(baseKeys);
            var others = new List<Dictionary<string, Match>>();
            bool dropped = false;
            for (int i = 0; i < sources.Count; i++)
            {
                if (i == baseIndex) continue;
                var ms = HlslIncludeBlock.Matches(sources[i].text);
                var keys = BlockKeys(sources[i].text, ms);
                var byKey = new Dictionary<string, Match>();
                for (int j = 0; j < ms.Count; j++)
                {
                    byKey[keys[j]] = ms[j];
                    if (!baseKeySet.Contains(keys[j]))
                    {
                        dropped = true;
                        diags.Add(new Diagnostic
                        {
                            Severity = Severity.Error,
                            Category = "lilcontainer",
                            Message = $"{fileName}: HLSLINCLUDE block at {keys[j]} of {sources[i].sourceKey} has no place in the structure of {sources[baseIndex].sourceKey}, which the merged shader is built on"
                        });
                    }
                }
                others.Add(byKey);
            }
            if (dropped) return null;

            int blockIndex = 0;
            return HlslIncludeBlock.Replace(baseText, firstMatch =>
            {
                var key = baseKeys[blockIndex++];
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

        static readonly Regex LineComment = new Regex(@"//.*$", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex Spaces = new Regex(@"[ \t]+", RegexOptions.Compiled);

        // HLSLINCLUDE の中身・行コメント・空白の違いを除いた構造部分。
        // シェーダー名の "/*LIL_SHADER_NAME*/" を壊さないよう、ブロックコメントは除かない
        public static string NormalizeStructure(string text)
        {
            text = HlslIncludeBlock.Replace(text.Replace("\r\n", "\n"), "HLSLINCLUDE ENDHLSL");
            text = LineComment.Replace(text, "");
            var lines = new List<string>();
            foreach (var ln in text.Split('\n'))
            {
                var t = Spaces.Replace(ln, " ").Trim();
                if (t.Length > 0) lines.Add(t);
            }
            return string.Join("\n", lines);
        }

        public static string StructureHash(string text)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(NormalizeStructure(text)));
                return System.BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
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

        static readonly Regex ShaderNameDecl = new Regex(@"Shader\s+""([^""]*)""", RegexOptions.Compiled);

        // ユーザーが目にするシェーダーの種類名 (例: "Lite/Cutout", "[Optional] FakeShadow")。
        // container が宣言するシェーダー名の "*LIL_SHADER_NAME*/" より後ろ。ltspass_* は UsePass 用の内部シェーダー
        public static string VariantDisplayName(string fileName, string containerText)
        {
            var m = ShaderNameDecl.Match(containerText);
            if (!m.Success) return Path.GetFileNameWithoutExtension(fileName);
            var name = m.Groups[1].Value;
            const string placeholder = "*LIL_SHADER_NAME*/";
            var i = name.IndexOf(placeholder, System.StringComparison.Ordinal);
            if (i >= 0) name = name.Substring(i + placeholder.Length);
            return name.StartsWith("ltspass_") ? name + " (internal)" : name;
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

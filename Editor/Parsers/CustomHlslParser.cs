using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class CustomHlslParser
    {
        // 行頭 (空白許容) で // 始まりでない `#define <NAME> \` (multi-line macro head)
        // NAME パターンを限定せず任意の C 識別子を許容 (ソース固有 helper macro も拾う)
        static readonly Regex MultilineHead = new Regex(
            @"^(?<indent>[\t ]*)#define\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+\\\s*$",
            RegexOptions.Compiled);

        // 単一行 #define: `#define NAME` (body なし) or `#define NAME VALUE` (body あり、 1 行で完結)
        static readonly Regex SingleLineDefine = new Regex(
            @"^(?<indent>[\t ]*)#define\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s+(?<value>[^\\]*?))?\s*$",
            RegexOptions.Compiled);

        // フラグ系 (本体なし) の lilToon 公式パターン名 (これらは FlagMacros として記録)
        static readonly Regex FlagDefineName = new Regex(
            @"^(?:LIL_REQUIRE_APP_[A-Z0-9_]+|LIL_V2F_FORCE_[A-Z0-9_]+|LIL_CUSTOM_VERT_COPY)$",
            RegexOptions.Compiled);

        public static CustomHlslData Parse(string source)
        {
            var data = new CustomHlslData();
            var lines = source.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // コメント行を除外 ( // が行頭にあれば無視。 ブロックコメント /* */ は v1 未対応)
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//")) continue;

                var m = MultilineHead.Match(line);
                if (m.Success)
                {
                    var name = m.Groups["name"].Value;
                    var body = new List<string>();
                    i++;
                    for (; i < lines.Length; i++)
                    {
                        var bodyLine = lines[i];
                        var bodyTrim = bodyLine.Trim();
                        if (bodyTrim.TrimStart().StartsWith("//")) continue;
                        bool hasContinuation = bodyTrim.EndsWith("\\");
                        var content = hasContinuation
                            ? bodyTrim.Substring(0, bodyTrim.Length - 1).Trim()
                            : bodyTrim;
                        if (!string.IsNullOrEmpty(content)) body.Add(content);
                        if (!hasContinuation) break;
                    }
                    data.MultilineMacros[name] = body;
                    continue;
                }

                var s = SingleLineDefine.Match(line);
                if (s.Success)
                {
                    var name = s.Groups["name"].Value;
                    var value = s.Groups["value"].Value ?? "";
                    if (FlagDefineName.IsMatch(name))
                    {
                        // lilToon 公式フラグマクロ
                        data.FlagMacros.Add(name);
                    }
                    else
                    {
                        // ソース固有 #define (constant, FLAG, etc.)
                        data.ExtraDefines[name] = value.TrimEnd();
                    }
                }
            }

            return data;
        }
    }
}

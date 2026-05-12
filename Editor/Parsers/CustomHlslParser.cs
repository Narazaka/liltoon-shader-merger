using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Narazaka.LilToonShaderMerger
{
    public static class CustomHlslParser
    {
        // 行頭 (空白許容) で // 始まりでない `#define <NAME> \` をマッチ
        static readonly Regex MultilineHead = new Regex(
            @"^(?<indent>[\t ]*)#define\s+(?<name>LIL_[A-Z0-9_]+|BEFORE_[A-Z0-9_]+|OVERRIDE_[A-Z0-9_]+)\s+\\\s*$",
            RegexOptions.Compiled);

        // フラグ系: `#define <NAME>` (本体なし、 行末に \ なし)
        static readonly Regex FlagDefine = new Regex(
            @"^(?<indent>[\t ]*)#define\s+(?<name>LIL_REQUIRE_APP_[A-Z0-9_]+|LIL_V2F_FORCE_[A-Z0-9_]+|LIL_CUSTOM_VERT_COPY)\s*$",
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

                var f = FlagDefine.Match(line);
                if (f.Success)
                {
                    data.FlagMacros.Add(f.Groups["name"].Value);
                }
            }

            return data;
        }
    }
}

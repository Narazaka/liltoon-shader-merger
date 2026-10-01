using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class CustomHlslParser
    {
        static readonly Regex Directive = new Regex(@"^#\s*(?<kw>[A-Za-z_]+)\b\s*(?<rest>.*)$", RegexOptions.Compiled);

        // `NAME`, `NAME value`, `NAME(params) value` (引数部は名前の直後に空白なしで続く)
        static readonly Regex DefineHead = new Regex(
            @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<params>\([^)]*\))?\s*(?<value>.*)$",
            RegexOptions.Compiled);

        static readonly Regex UndefHead = new Regex(@"^(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

        public static CustomHlslData Parse(string source)
        {
            var data = new CustomHlslData();
            var lines = source.Replace("\r\n", "\n").Split('\n');
            var conditions = new List<List<string>>();
            bool inBlockComment = false;

            HlslEntry New(HlslEntryKind kind, string name = null) => new HlslEntry
            {
                Kind = kind,
                Name = name,
                Conditions = conditions.ConvertAll(f => new List<string>(f)),
            };

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (inBlockComment)
                {
                    if (trimmed.Contains("*/")) inBlockComment = false;
                    continue;
                }
                if (trimmed.Length == 0 || trimmed.StartsWith("//")) continue;
                if (trimmed.StartsWith("/*"))
                {
                    if (!trimmed.Contains("*/")) inBlockComment = true;
                    continue;
                }

                var d = Directive.Match(trimmed);
                if (!d.Success)
                {
                    // #define の外にある HLSL コード (関数定義等)。行単位でそのまま保持する
                    var other = New(HlslEntryKind.Other);
                    other.Body.Add(lines[i].TrimEnd());
                    data.Entries.Add(other);
                    continue;
                }

                var kw = d.Groups["kw"].Value;
                var rest = d.Groups["rest"].Value;
                switch (kw)
                {
                    case "if":
                    case "ifdef":
                    case "ifndef":
                        conditions.Add(new List<string> { trimmed });
                        break;
                    case "elif":
                    case "else":
                        if (conditions.Count > 0) conditions[conditions.Count - 1].Add(trimmed);
                        break;
                    case "endif":
                        if (conditions.Count > 0) conditions.RemoveAt(conditions.Count - 1);
                        break;
                    case "define":
                    {
                        var h = DefineHead.Match(StripContinuation(rest, out var continues));
                        if (!h.Success) break;
                        var def = New(HlslEntryKind.Define, h.Groups["name"].Value);
                        if (h.Groups["params"].Success) def.Params = h.Groups["params"].Value;
                        var value = h.Groups["value"].Value.Trim();
                        if (value.Length > 0) def.Body.Add(value);
                        while (continues && i + 1 < lines.Length)
                        {
                            i++;
                            var bodyLine = lines[i].Trim();
                            var content = StripContinuation(bodyLine, out continues).Trim();
                            if (content.StartsWith("//")) continue;
                            if (content.Length > 0) def.Body.Add(content);
                        }
                        data.Entries.Add(def);
                        break;
                    }
                    case "undef":
                    {
                        var h = UndefHead.Match(rest);
                        if (h.Success) data.Entries.Add(New(HlslEntryKind.Undef, h.Groups["name"].Value));
                        break;
                    }
                    default:
                    {
                        // #include / #pragma 等。行継続も含めてそのまま保持する
                        var other = New(HlslEntryKind.Other);
                        other.Body.Add(lines[i].TrimEnd());
                        while (lines[i].TrimEnd().EndsWith("\\") && i + 1 < lines.Length)
                        {
                            i++;
                            other.Body.Add(lines[i].TrimEnd());
                        }
                        data.Entries.Add(other);
                        break;
                    }
                }
            }
            return data;
        }

        static string StripContinuation(string line, out bool continues)
        {
            var t = line.TrimEnd();
            continues = t.EndsWith("\\");
            return continues ? t.Substring(0, t.Length - 1) : t;
        }
    }
}

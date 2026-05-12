using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class LilBlockParser
    {
        // クォート 2/3 連続マッチ用
        static readonly Regex QuotedArg = new Regex("\"([^\"]*)\"", RegexOptions.Compiled);

        public static CustomShaderDatas ParseDatas(string source)
        {
            var d = new CustomShaderDatas();
            foreach (var rawLine in source.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;

                if (line.StartsWith("ShaderName"))
                {
                    var m = QuotedArg.Match(line);
                    if (m.Success) d.ShaderName = m.Groups[1].Value;
                }
                else if (line.StartsWith("EditorName"))
                {
                    var m = QuotedArg.Match(line);
                    if (m.Success) d.EditorName = m.Groups[1].Value;
                }
                else if (line.StartsWith("Replace"))
                {
                    var matches = QuotedArg.Matches(line);
                    if (matches.Count == 2)
                        d.Replaces.Add(new ReplaceDirective {
                            From = matches[0].Groups[1].Value,
                            To   = matches[1].Groups[1].Value
                        });
                    else if (matches.Count == 3)
                        d.Replaces.Add(new ReplaceDirective {
                            Filter = matches[0].Groups[1].Value,
                            From   = matches[1].Groups[1].Value,
                            To     = matches[2].Groups[1].Value
                        });
                }
                else
                {
                    foreach (var key in new[] { "InsertPassPre", "InsertPassPost", "InsertUsePassPre", "InsertUsePassPost" })
                    {
                        if (line.StartsWith(key))
                        {
                            var m = QuotedArg.Match(line);
                            if (m.Success) d.Inserts[key] = m.Groups[1].Value;
                            break;
                        }
                    }
                }
            }
            return d;
        }
    }
}

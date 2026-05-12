using System.Collections.Generic;
using System.Text;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class LilBlockEmitter
    {
        public static string EmitDatas(MergedDatas d)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ShaderName \"{d.ShaderName}\"");
            sb.AppendLine($"EditorName \"{d.EditorName}\"");
            foreach (var r in d.Replaces)
            {
                if (string.IsNullOrEmpty(r.Filter))
                    sb.AppendLine($"Replace \"{r.From}\" \"{r.To}\"");
                else
                    sb.AppendLine($"Replace \"{r.Filter}\" \"{r.From}\" \"{r.To}\"");
            }
            foreach (var kv in d.InsertFilePaths)
            {
                // 各 Insert* は 1 ファイルにマージするので 1 行のみ
                sb.AppendLine($"{kv.Key} \"merged_{kv.Key}.lilblock\"");
            }
            return sb.ToString();
        }

        public static string EmitInsertBlock(IReadOnlyList<(string sourceKey, string text)> sources, bool dedupeIncludes)
        {
            var sb = new StringBuilder();
            var seenLines = new HashSet<string>();
            foreach (var (key, text) in sources)
            {
                foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
                {
                    var trimmed = raw.Trim();
                    if (dedupeIncludes && trimmed.StartsWith("#include"))
                    {
                        if (!seenLines.Add(trimmed)) continue;
                    }
                    sb.AppendLine(raw);
                }
            }
            return sb.ToString();
        }

        public static string EmitProperties(string mergedRawText) => mergedRawText;
    }
}

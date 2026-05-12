using System.Collections.Generic;
using System.Text;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class PropertiesMerger
    {
        public static string Merge(
            IReadOnlyList<(string sourceKey, CustomProperties props)> sources,
            ConflictStrategy strategy,
            List<Diagnostic> diags)
        {
            // 衝突検出: 異なるソース間のプロパティ名重複のみ衝突扱い (同一ソース内重複は Unity 自体が許容するので無視)
            var seen = new Dictionary<string, string>(); // name → sourceKey
            foreach (var (key, p) in sources)
            {
                var localSeen = new HashSet<string>();
                foreach (var name in p.PropertyNames)
                {
                    if (!localSeen.Add(name)) continue;  // 同一ソース内重複はスキップ
                    if (seen.TryGetValue(name, out var prevKey))
                    {
                        var d = new Diagnostic
                        {
                            Category = "property",
                            Message = $"property name '{name}' collision: {prevKey} / {key}",
                            Severity = strategy == ConflictStrategy.ErrorOut ? Severity.Error : Severity.Warning
                        };
                        diags.Add(d);
                    }
                    else seen[name] = key;
                }
            }

            // 出力 (strategy 別に取捨選択)
            var sb = new StringBuilder();
            foreach (var (key, p) in sources)
            {
                sb.AppendLine($"        // --- {key} ---");
                sb.AppendLine(p.RawText);
            }
            return sb.ToString();
        }
    }
}

using System.Collections.Generic;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class DataMerger
    {
        public static MergedDatas Merge(
            IReadOnlyList<(string sourceKey, CustomShaderDatas data)> sources,
            string newShaderName,
            string newEditorName,
            ConflictStrategy strategy,
            List<Diagnostic> diags)
        {
            var m = new MergedDatas
            {
                ShaderName = newShaderName,
                EditorName = newEditorName,
            };

            // Replaces を union + 衝突検出
            // key: (filter, from), value: (to, sourceKey, indexInMergedList)
            var seen = new Dictionary<(string filter, string from), (string to, string sourceKey, int index)>();
            foreach (var (key, d) in sources)
            {
                if (d == null) continue;

                foreach (var r in d.Replaces)
                {
                    if (r == null) continue;
                    var k = (r.Filter ?? "", r.From ?? "");
                    if (seen.TryGetValue(k, out var prev))
                    {
                        if (prev.to == r.To)
                        {
                            // 完全一致 → 重複削除
                            continue;
                        }

                        // 衝突
                        var diag = new Diagnostic
                        {
                            Category = "replace",
                            Message = $"Replace 'from={r.From}' conflict: {prev.sourceKey}->'{prev.to}' / {key}->'{r.To}'",
                            Severity = strategy == ConflictStrategy.ErrorOut ? Severity.Error : Severity.Warning,
                        };
                        diags.Add(diag);

                        if (strategy == ConflictStrategy.PreferLast)
                        {
                            m.Replaces[prev.index] = r;
                            seen[k] = (r.To, key, prev.index);
                        }
                        // ErrorOut / PreferFirst: 既存維持
                        continue;
                    }

                    seen[k] = (r.To, key, m.Replaces.Count);
                    m.Replaces.Add(r);
                }

                foreach (var insKv in d.Inserts)
                {
                    if (!m.InsertFilePaths.TryGetValue(insKv.Key, out var list))
                    {
                        list = new List<string>();
                        m.InsertFilePaths[insKv.Key] = list;
                    }
                    list.Add(insKv.Value);
                }
            }

            return m;
        }
    }
}

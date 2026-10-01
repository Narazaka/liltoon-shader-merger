using System.Collections.Generic;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class MacroMerger
    {
        // 本体を連結して合成できるマクロ (lilToon は未定義なら空として扱う)
        static readonly HashSet<string> ChainableMacros = new HashSet<string>
        {
            "LIL_CUSTOM_PROPERTIES", "LIL_CUSTOM_TEXTURES",
            "LIL_CUSTOM_VERTEX_OS", "LIL_CUSTOM_VERTEX_WS",
            "LIL_CUSTOM_VERT_COPY",
        };

        static bool IsChainable(string name) => ChainableMacros.Contains(name) || name.StartsWith("BEFORE_");

        public static MergedHlsl Merge(
            IReadOnlyList<(string sourceKey, CustomHlslData data)> sources,
            ConflictStrategy overrideStrategy,
            List<Diagnostic> diags)
        {
            var merged = new MergedHlsl();

            // マクロ名 → ソースごとの定義列 (#define / #undef を出現順・条件付きのまま)
            var names = new List<string>();
            var perSource = new List<Dictionary<string, List<HlslEntry>>>();
            // #define 以外の行 (#include や関数定義) はソースごとのまとまりで扱う。行単位で重複除去すると "}" 等まで消える
            var othersPerSource = new List<List<HlslEntry>>();
            foreach (var (_, data) in sources)
            {
                var byName = new Dictionary<string, List<HlslEntry>>();
                var others = new List<HlslEntry>();
                othersPerSource.Add(others);
                foreach (var e in data.Entries)
                {
                    if (e.Kind == HlslEntryKind.Other)
                    {
                        others.Add(e);
                        continue;
                    }
                    if (!byName.TryGetValue(e.Name, out var seq))
                    {
                        byName[e.Name] = seq = new List<HlslEntry>();
                        if (!names.Contains(e.Name)) names.Add(e.Name);
                    }
                    // 無条件の #undef はそれまでの定義を打ち消すだけなので、定義列から落として比較しやすくする
                    if (e.Kind == HlslEntryKind.Undef && e.Conditions.Count == 0) seq.Clear();
                    else seq.Add(e);
                }
                perSource.Add(byName);
            }

            foreach (var name in names)
            {
                var defining = new List<(int index, List<HlslEntry> seq)>();
                for (int i = 0; i < sources.Count; i++)
                    if (perSource[i].TryGetValue(name, out var seq) && seq.Count > 0) defining.Add((i, seq));
                if (defining.Count == 0) continue;

                if (defining.Count == 1 || defining.TrueForAll(d => SameSequence(d.seq, defining[0].seq)))
                {
                    merged.Entries.AddRange(defining[0].seq);
                }
                else if (IsChainable(name) && defining.TrueForAll(d => d.seq.TrueForAll(e => e.Params == null)))
                {
                    Chain(name, sources, defining, merged);
                }
                else
                {
                    var d = new Diagnostic
                    {
                        Category = "macro",
                        Message = name.StartsWith("OVERRIDE_")
                            ? $"{name} appears in multiple sources; this is an override hook"
                            : $"#define {name} differs across sources",
                        Severity = overrideStrategy == ConflictStrategy.ErrorOut ? Severity.Error : Severity.Warning,
                    };
                    diags.Add(d);
                    var chosen = overrideStrategy == ConflictStrategy.PreferLast ? defining[defining.Count - 1] : defining[0];
                    merged.Entries.AddRange(chosen.seq);
                }
            }

            for (int i = 0; i < othersPerSource.Count; i++)
            {
                if (othersPerSource.GetRange(0, i).Exists(prev => SameSequence(prev, othersPerSource[i]))) continue;
                merged.Entries.AddRange(othersPerSource[i]);
            }
            return merged;
        }

        static void Chain(string name, IReadOnlyList<(string sourceKey, CustomHlslData data)> sources,
            List<(int index, List<HlslEntry> seq)> defining, MergedHlsl merged)
        {
            // 全ソースが無条件の #define 1 つずつなら本体をそのまま連結する
            if (defining.TrueForAll(d => d.seq.Count == 1 && d.seq[0].Kind == HlslEntryKind.Define && d.seq[0].Conditions.Count == 0))
            {
                var def = defining[0].seq[0].Clone();
                for (int i = 1; i < defining.Count; i++) def.Body.AddRange(defining[i].seq[0].Body);
                merged.Entries.Add(def);
                return;
            }

            // 条件付きの定義があれば、ソースごとの定義を補助マクロに改名して元の条件のまま置き、
            // 条件が外れたとき用に空定義を補ってから、公開マクロで補助マクロを並べる
            var combined = new HlslEntry { Kind = HlslEntryKind.Define, Name = name };
            foreach (var (index, seq) in defining)
            {
                var helper = $"LSM_SOURCE{index}_{name}";
                foreach (var e in seq) merged.Entries.Add(e.Clone(helper));
                merged.Entries.Add(new HlslEntry
                {
                    Kind = HlslEntryKind.Define,
                    Name = helper,
                    Conditions = new List<List<string>> { new List<string> { $"#ifndef {helper}" } },
                });
                combined.Body.Add($"{helper} /* {sources[index].sourceKey.Replace("*/", "* /")} */");
            }
            merged.Entries.Add(combined);
        }

        static bool SameSequence(List<HlslEntry> a, List<HlslEntry> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!a[i].SameAs(b[i])) return false;
            return true;
        }
    }
}

using System.Collections.Generic;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class MacroMerger
    {
        // 合成戦略: chain（連結 OK） or override（排他、衝突戦略適用）
        static readonly HashSet<string> ChainableMacros = new HashSet<string>
        {
            "LIL_CUSTOM_PROPERTIES", "LIL_CUSTOM_TEXTURES",
            "LIL_CUSTOM_VERTEX_OS", "LIL_CUSTOM_VERTEX_WS",
        };

        public static MergedHlsl Merge(
            IReadOnlyList<CustomHlslData> sources,
            ConflictStrategy overrideStrategy,
            List<Diagnostic> diags)
        {
            var merged = new MergedHlsl();

            // フラグマクロは union
            foreach (var s in sources)
                foreach (var f in s.FlagMacros)
                    merged.FlagMacros.Add(f);

            // ExtraDefines (ソース固有 #define): 同名で body 一致なら dedup、 違えば衝突戦略
            foreach (var s in sources)
            {
                foreach (var kv in s.ExtraDefines)
                {
                    if (merged.ExtraDefines.TryGetValue(kv.Key, out var prevVal))
                    {
                        if (prevVal == kv.Value) continue;  // 完全一致 → スキップ
                        var d = new Diagnostic
                        {
                            Category = "define",
                            Message = $"#define {kv.Key} differs across sources: '{prevVal}' vs '{kv.Value}'",
                            Severity = overrideStrategy == ConflictStrategy.ErrorOut ? Severity.Error : Severity.Warning
                        };
                        diags.Add(d);
                        if (overrideStrategy == ConflictStrategy.PreferLast)
                            merged.ExtraDefines[kv.Key] = kv.Value;
                        // PreferFirst / ErrorOut は既存を保持
                    }
                    else merged.ExtraDefines[kv.Key] = kv.Value;
                }
            }

            // 複数行マクロ
            foreach (var s in sources)
            {
                foreach (var kv in s.MultilineMacros)
                {
                    var name = kv.Key;
                    var body = kv.Value;
                    bool isChain = ChainableMacros.Contains(name) || name.StartsWith("BEFORE_");
                    bool isOverride = name.StartsWith("OVERRIDE_") || name == "LIL_CUSTOM_V2F_MEMBER";

                    if (!merged.MultilineMacros.TryGetValue(name, out var existing))
                    {
                        merged.MultilineMacros[name] = new List<string>(body);
                        continue;
                    }

                    if (isChain)
                    {
                        existing.AddRange(body);
                    }
                    else if (isOverride)
                    {
                        var d = new Diagnostic
                        {
                            Category = "macro",
                            Message = $"{name} appears in multiple sources; this is an override hook"
                        };
                        switch (overrideStrategy)
                        {
                            case ConflictStrategy.ErrorOut:
                                d.Severity = Severity.Error;
                                diags.Add(d);
                                break;
                            case ConflictStrategy.PreferFirst:
                                d.Severity = Severity.Warning;
                                diags.Add(d);
                                // 既存をそのまま (PreferFirst)
                                break;
                            case ConflictStrategy.PreferLast:
                                d.Severity = Severity.Warning;
                                diags.Add(d);
                                merged.MultilineMacros[name] = new List<string>(body);
                                break;
                        }
                    }
                    else
                    {
                        // ソース固有 helper macro (PRECALCEDUV 等): 同じ body なら dedup、 異なれば衝突戦略
                        if (BodyEquals(existing, body)) continue;
                        var d = new Diagnostic
                        {
                            Category = "macro",
                            Message = $"#define {name} (multi-line) differs across sources",
                            Severity = overrideStrategy == ConflictStrategy.ErrorOut ? Severity.Error : Severity.Warning
                        };
                        diags.Add(d);
                        if (overrideStrategy == ConflictStrategy.PreferLast)
                            merged.MultilineMacros[name] = new List<string>(body);
                    }
                }
            }
            return merged;
        }

        static bool BodyEquals(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}

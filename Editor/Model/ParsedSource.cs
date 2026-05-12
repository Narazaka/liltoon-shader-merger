using System.Collections.Generic;

namespace Narazaka.LilToonShaderMerger
{
    public class CustomHlslData
    {
        // マクロ名 → 本体行 (連続)。 例: "LIL_CUSTOM_PROPERTIES" → ["float _effect;", "float _strength;", ...]
        public Dictionary<string, List<string>> MultilineMacros { get; } = new Dictionary<string, List<string>>();
        // フラグ系マクロ (本体なし)。 例: "LIL_REQUIRE_APP_POSITION"
        public HashSet<string> FlagMacros { get; } = new HashSet<string>();
    }

    public class ParsedSource
    {
        public string SourceKey { get; set; }    // 識別子 (フォルダ名等)
        public string FolderPath { get; set; }
        public CustomHlslData Hlsl { get; set; }
    }
}

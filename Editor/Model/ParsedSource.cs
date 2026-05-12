using System.Collections.Generic;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public class CustomHlslData
    {
        // マクロ名 → 本体行 (連続)。 例: "LIL_CUSTOM_PROPERTIES" → ["float _effect;", "float _strength;", ...]
        public Dictionary<string, List<string>> MultilineMacros { get; } = new Dictionary<string, List<string>>();
        // フラグ系マクロ (本体なし)。 例: "LIL_REQUIRE_APP_POSITION"
        public HashSet<string> FlagMacros { get; } = new HashSet<string>();
    }

    public class CustomShaderDatas
    {
        public string ShaderName { get; set; } = "";
        public string EditorName { get; set; } = "";
        public List<ReplaceDirective> Replaces { get; } = new List<ReplaceDirective>();
        public Dictionary<string, string> Inserts { get; } = new Dictionary<string, string>();
    }

    public class ReplaceDirective
    {
        public string Filter { get; set; }
        public string From { get; set; }
        public string To { get; set; }
    }

    public class CustomProperties
    {
        public string RawText { get; set; } = "";
        public List<string> PropertyNames { get; } = new List<string>();
    }

    public class ParsedSource
    {
        public string SourceKey { get; set; }    // 識別子 (フォルダ名等)
        public string FolderPath { get; set; }
        public CustomHlslData Hlsl { get; set; }
        public CustomShaderDatas Datas { get; set; }
        public CustomProperties Properties { get; set; }
        public string InsertBlockText { get; set; }
    }
}

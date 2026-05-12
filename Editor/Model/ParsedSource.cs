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

    public class ParsedInspector
    {
        public bool PatternMatched { get; set; }
        public string ShaderNameConst { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string Namespace { get; set; } = "lilToon";
        public List<string> MaterialPropertyFields { get; } = new List<string>();
        public List<string> FindPropertyNames { get; } = new List<string>();
        // field 名 → shader property 名 (FindProperty 代入文から抽出)
        public Dictionary<string, string> FieldToPropertyName { get; } = new Dictionary<string, string>();
        public List<string> DrawCustomPropertiesBodyLines { get; } = new List<string>();
        public string FoldoutTitle { get; set; } = "Custom Properties";
    }

    public class ParsedSource
    {
        public string SourceKey { get; set; }    // 識別子 (フォルダ名等)
        public string FolderPath { get; set; }
        public CustomHlslData Hlsl { get; set; }
        public CustomShaderDatas Datas { get; set; }
        public CustomProperties Properties { get; set; }
        public string InsertBlockText { get; set; }
        public ParsedInspector Inspector { get; set; }
    }
}

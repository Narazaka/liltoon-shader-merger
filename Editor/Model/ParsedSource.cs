using System.Collections.Generic;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public class CustomHlslData
    {
        // マクロ名 → 本体行 (連続)。 例: "LIL_CUSTOM_PROPERTIES" → ["float _effect;", "float _strength;", ...]
        public Dictionary<string, List<string>> MultilineMacros { get; } = new Dictionary<string, List<string>>();
        // フラグ系マクロ (本体なし)。 例: "LIL_REQUIRE_APP_POSITION"
        public HashSet<string> FlagMacros { get; } = new HashSet<string>();
        // ソース固有のシンプル #define (例: "K2S_DCS_VMATRIXTEXTURERESOLUTION" → "4", "LIL_FEATURE_DECAL" → "")
        public Dictionary<string, string> ExtraDefines { get; } = new Dictionary<string, string>();
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
        // private static bool isShow* フィールド (Foldout 状態変数。 merged 後にソース毎に一意化される)
        public List<string> IsShowFields { get; } = new List<string>();
        // 元 inspector .cs の using directive (例: "System.Collections.Generic")。 merged 側で union 出力
        public List<string> Usings { get; } = new List<string>();
        // canonical 以外のクラスメンバ (helper method, 非 MaterialProperty field, nested type 等) のソーステキスト
        // merged class に「そのまま」挿入される (名前は元のまま保持。 ソース間衝突は名前リネームか warn)
        public List<string> ExtraMembers { get; } = new List<string>();
        // 同 .cs 内の sibling type (lilToonInspector 派生でない並列定義の class/struct/enum)。
        // namespace block 内に並列で配置される。
        public List<string> SiblingTypes { get; } = new List<string>();
        // Inspector .cs ファイルパス (sibling .cs を発見するために使用)
        public string InspectorCsPath { get; set; }
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

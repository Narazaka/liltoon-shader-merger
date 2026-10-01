using System.Collections.Generic;

namespace Narazaka.Unity.LilToonShaderMerger
{
    // custom.hlsl をプリプロセッサ指示の並びとして保持する。#if 等の条件は各要素の Conditions に残す
    public class CustomHlslData
    {
        public List<HlslEntry> Entries { get; } = new List<HlslEntry>();

        // 最後に有効な #define (条件付きも含む)。テスト・診断用
        public HlslEntry FindDefine(string name) =>
            Entries.FindLast(e => e.Kind == HlslEntryKind.Define && e.Name == name);
    }

    public enum HlslEntryKind { Define, Undef, Other }

    public class HlslEntry
    {
        public HlslEntryKind Kind { get; set; }
        // Define / Undef のマクロ名
        public string Name { get; set; }
        // 関数形式マクロの引数部 (例: "(id0,id1)")。オブジェクト形式は null
        public string Params { get; set; }
        // Define: 本体行 (行継続の \ は除去済み)。Other: 原文の行
        public List<string> Body { get; set; } = new List<string>();
        // 外側から順の条件フレーム。各フレームはその時点までの分岐見出し (例: ["#if A", "#else"]) で、最後の見出しが有効な分岐
        public List<List<string>> Conditions { get; set; } = new List<List<string>>();

        public HlslEntry Clone(string name = null) => new HlslEntry
        {
            Kind = Kind,
            Name = name ?? Name,
            Params = Params,
            Body = new List<string>(Body),
            Conditions = Conditions.ConvertAll(f => new List<string>(f)),
        };

        public bool SameAs(HlslEntry o) =>
            Kind == o.Kind && Name == o.Name && Params == o.Params && SameLines(Body, o.Body) && SameConditions(o);

        public bool SameConditions(HlslEntry o)
        {
            if (Conditions.Count != o.Conditions.Count) return false;
            for (int i = 0; i < Conditions.Count; i++)
                if (!SameLines(Conditions[i], o.Conditions[i])) return false;
            return true;
        }

        static bool SameLines(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }
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

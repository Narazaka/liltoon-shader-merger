using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class InspectorCsParser
    {
        static readonly Regex Namespace = new Regex(@"namespace\s+([A-Za-z_][\w\.]*)", RegexOptions.Compiled);
        static readonly Regex ClassDecl = new Regex(@"class\s+(\w+)\s*:\s*lilToonInspector", RegexOptions.Compiled);
        static readonly Regex ShaderNameConst = new Regex(
            @"private\s+const\s+string\s+shaderName\s*=\s*""([^""]+)""\s*;",
            RegexOptions.Compiled);
        static readonly Regex MaterialPropertyField = new Regex(
            @"MaterialProperty\s+(_?\w+)\s*;",
            RegexOptions.Compiled);
        // <field> = FindProperty("<propName>", props);
        static readonly Regex FindPropertyAssignment = new Regex(
            @"(_?\w+)\s*=\s*FindProperty\s*\(\s*""([^""]+)""\s*,\s*props\s*\)\s*;",
            RegexOptions.Compiled);
        static readonly Regex FoldoutCall = new Regex(
            @"Foldout\s*\(\s*""([^""]+)""",
            RegexOptions.Compiled);
        // private static bool isShow*; (Foldout 状態変数)
        static readonly Regex IsShowField = new Regex(
            @"(?:private|protected|internal|public)\s+(?:static\s+)?bool\s+(isShow\w*)\s*[;=]",
            RegexOptions.Compiled);
        // class 直下のメソッド宣言 (修飾子 + 戻り型 + 名前 + 括弧)
        static readonly Regex MethodDecl = new Regex(
            @"(?:public|private|protected|internal|static|override|virtual|abstract|sealed|async|new|\s)+\s+\S+\s+(\w+)\s*\(",
            RegexOptions.Compiled);

        // 公式 lilToon カスタムシェーダーテンプレートに含まれるメソッド名
        static readonly HashSet<string> CanonicalMethodNames = new HashSet<string>
        {
            "LoadCustomProperties",
            "DrawCustomProperties",
            "ReplaceToCustomShaders",
            "ConvertMaterialToCustomShaderMenu",
            "LoadCustomLanguage",
        };

        // フィールド宣言 (access modifier 必須、 末尾 ; or =)
        static readonly Regex FieldDecl = new Regex(
            @"^\s*(?:(?:public|private|protected|internal)\s+)+(?:static\s+|readonly\s+|const\s+)*(?<type>[A-Za-z_][\w\.\<\>\[\]]*)\s+(?<name>\w+)\s*[;=]",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static ParsedInspector Parse(string source)
        {
            var p = new ParsedInspector();

            var nm = Namespace.Match(source);
            if (nm.Success) p.Namespace = nm.Groups[1].Value;

            var cm = ClassDecl.Match(source);
            if (!cm.Success) return p;  // PatternMatched=false
            p.ClassName = cm.Groups[1].Value;

            var sm = ShaderNameConst.Match(source);
            if (sm.Success) p.ShaderNameConst = sm.Groups[1].Value;

            // MaterialProperty フィールド + isShow* bool フィールド抽出 (コメントアウト行を除外)
            foreach (var rawLine in source.Replace("\r\n", "\n").Split('\n'))
            {
                if (rawLine.TrimStart().StartsWith("//")) continue;
                foreach (Match m in MaterialPropertyField.Matches(rawLine))
                    p.MaterialPropertyFields.Add(m.Groups[1].Value);
                foreach (Match m in IsShowField.Matches(rawLine))
                {
                    var name = m.Groups[1].Value;
                    if (!p.IsShowFields.Contains(name)) p.IsShowFields.Add(name);
                }
            }
            // 元 inspector に isShow* が無いケースでも、 元 body が isShowCustomProperties を使ってる (lilToon template) なら追加
            if (!p.IsShowFields.Contains("isShowCustomProperties"))
                p.IsShowFields.Add("isShowCustomProperties");

            // DrawCustomProperties メソッド本文を切り出し
            int drawIdx = source.IndexOf("DrawCustomProperties");
            if (drawIdx >= 0)
            {
                int openBrace = source.IndexOf('{', drawIdx);
                if (openBrace > 0)
                {
                    int depth = 1; int end = openBrace + 1;
                    while (end < source.Length && depth > 0)
                    {
                        if (source[end] == '{') depth++;
                        else if (source[end] == '}') depth--;
                        end++;
                    }
                    var body = source.Substring(openBrace + 1, end - openBrace - 2);
                    foreach (var ln in body.Replace("\r\n", "\n").Split('\n'))
                        p.DrawCustomPropertiesBodyLines.Add(ln);

                    foreach (var ln in body.Replace("\r\n", "\n").Split('\n'))
                    {
                        if (ln.TrimStart().StartsWith("//")) continue;
                        var fm = FoldoutCall.Match(ln);
                        if (fm.Success) { p.FoldoutTitle = fm.Groups[1].Value; break; }
                    }
                }
            }

            // LoadCustomProperties メソッド本文から FindProperty 代入を抽出 (コメントアウト行を除外)
            int loadIdx = source.IndexOf("LoadCustomProperties");
            if (loadIdx >= 0)
            {
                int openBrace = source.IndexOf('{', loadIdx);
                if (openBrace > 0)
                {
                    int depth = 1; int end = openBrace + 1;
                    while (end < source.Length && depth > 0)
                    {
                        if (source[end] == '{') depth++;
                        else if (source[end] == '}') depth--;
                        end++;
                    }
                    var body = source.Substring(openBrace + 1, end - openBrace - 2);
                    foreach (var ln in body.Replace("\r\n", "\n").Split('\n'))
                    {
                        if (ln.TrimStart().StartsWith("//")) continue;
                        var m = FindPropertyAssignment.Match(ln);
                        if (m.Success)
                        {
                            var field = m.Groups[1].Value;
                            var propName = m.Groups[2].Value;
                            p.FindPropertyNames.Add(propName);
                            p.FieldToPropertyName[field] = propName;
                        }
                    }
                }
            }

            // クラス内に canonical 以外のメソッドや nested type があれば非定型と判定
            bool hasNonCanonical = HasNonCanonicalMembers(source);

            // パターン適合判定: クラス名 + Find/Draw のどちらかに該当 + 非 canonical メンバなし
            p.PatternMatched = !string.IsNullOrEmpty(p.ClassName)
                && (p.FindPropertyNames.Count > 0 || p.DrawCustomPropertiesBodyLines.Count > 0)
                && !hasNonCanonical;

            return p;
        }

        // クラス body 内に canonical 以外のメソッド / nested class / nested enum / 非 MaterialProperty フィールドがあるか
        static bool HasNonCanonicalMembers(string source)
        {
            // class 宣言の `{` 位置を取得しブロックを切り出し
            var classMatch = ClassDecl.Match(source);
            if (!classMatch.Success) return false;
            int classOpen = source.IndexOf('{', classMatch.Index);
            if (classOpen < 0) return false;
            int depth = 1, end = classOpen + 1;
            while (end < source.Length && depth > 0)
            {
                if (source[end] == '{') depth++;
                else if (source[end] == '}') depth--;
                end++;
            }
            var classBody = source.Substring(classOpen + 1, end - classOpen - 2);

            // nested class / enum
            if (Regex.IsMatch(classBody, @"\b(class|enum|struct|interface)\s+\w+\s*[:{]"))
                return true;

            // 非 canonical メソッド
            foreach (Match m in MethodDecl.Matches(classBody))
            {
                var name = m.Groups[1].Value;
                if (CanonicalMethodNames.Contains(name)) continue;
                if (name == "if" || name == "while" || name == "for" || name == "foreach" || name == "switch" || name == "using" || name == "return" || name == "new" || name == "throw" || name == "lock") continue;
                if (name == ExtractClassName(source)) continue;
                return true;
            }

            // 非 canonical フィールド (access modifier 付き、 MaterialProperty/isShow*/shaderName 以外)
            foreach (Match m in FieldDecl.Matches(classBody))
            {
                var type = m.Groups["type"].Value;
                var name = m.Groups["name"].Value;
                if (type == "MaterialProperty") continue;
                if (type == "bool" && name.StartsWith("isShow")) continue;
                if (type == "string" && name == "shaderName") continue;
                return true;
            }
            return false;
        }

        static string ExtractClassName(string source)
        {
            var m = ClassDecl.Match(source);
            return m.Success ? m.Groups[1].Value : "";
        }
    }
}

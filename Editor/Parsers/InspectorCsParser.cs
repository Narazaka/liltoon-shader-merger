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
        static readonly Regex FindProperty = new Regex(
            @"FindProperty\s*\(\s*""([^""]+)""\s*,\s*props\s*\)",
            RegexOptions.Compiled);
        static readonly Regex FoldoutCall = new Regex(
            @"Foldout\s*\(\s*""([^""]+)""",
            RegexOptions.Compiled);

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

            foreach (Match m in MaterialPropertyField.Matches(source))
                p.MaterialPropertyFields.Add(m.Groups[1].Value);

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

                    var fm = FoldoutCall.Match(body);
                    if (fm.Success) p.FoldoutTitle = fm.Groups[1].Value;
                }
            }

            // LoadCustomProperties メソッド本文から FindProperty 抽出
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
                    foreach (Match m in FindProperty.Matches(body))
                        p.FindPropertyNames.Add(m.Groups[1].Value);
                }
            }

            // パターン適合判定: クラス名 + Find/Draw のどちらかに該当があれば OK
            p.PatternMatched = !string.IsNullOrEmpty(p.ClassName)
                && (p.FindPropertyNames.Count > 0 || p.DrawCustomPropertiesBodyLines.Count > 0);

            return p;
        }
    }
}

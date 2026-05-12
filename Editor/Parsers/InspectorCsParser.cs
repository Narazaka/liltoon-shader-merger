using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Narazaka.Unity.LilToonShaderMerger
{
    /// <summary>
    /// lilToon CustomInspector.cs を Roslyn AST で解析する。
    /// Microsoft.CodeAnalysis.* は Narazaka.Unity.LilToonShaderMerger.Roslyn.dll 内で internalize されており、
    /// InternalsVisibleTo 設定で本 Editor asmdef からのみアクセス可能。
    /// </summary>
    public static class InspectorCsParser
    {
        static readonly HashSet<string> CanonicalMethodNames = new HashSet<string>
        {
            "LoadCustomProperties",
            "DrawCustomProperties",
            "ReplaceToCustomShaders",
            "ConvertMaterialToCustomShaderMenu",
            "LoadCustomLanguage",
        };

        // lilToon CustomInspector.cs は #if UNITY_EDITOR で囲まれていることが多いため、
        // Roslyn のプリプロセッサに UNITY_EDITOR を定義しないと全コードが trivia 扱いになる
        static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
            .WithPreprocessorSymbols("UNITY_EDITOR");

        public static ParsedInspector Parse(string source)
        {
            var p = new ParsedInspector();
            var tree = CSharpSyntaxTree.ParseText(source, ParseOptions);
            var root = tree.GetRoot();

            // namespace
            var nsDecl = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            if (nsDecl != null) p.Namespace = nsDecl.Name.ToString();

            // lilToonInspector を継承する class
            var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.BaseList?.Types.Any(t => t.Type.ToString() == "lilToonInspector") == true);
            if (classDecl == null) return p; // PatternMatched=false

            p.ClassName = classDecl.Identifier.ValueText;

            bool hasNonCanonical = false;

            foreach (var member in classDecl.Members)
            {
                switch (member)
                {
                    case FieldDeclarationSyntax field:
                        AnalyzeField(field, p, ref hasNonCanonical);
                        break;
                    case MethodDeclarationSyntax method:
                        if (!CanonicalMethodNames.Contains(method.Identifier.ValueText))
                            hasNonCanonical = true;
                        else if (method.Identifier.ValueText == "LoadCustomProperties")
                            AnalyzeLoadCustomProperties(method, p);
                        else if (method.Identifier.ValueText == "DrawCustomProperties")
                            AnalyzeDrawCustomProperties(method, p);
                        break;
                    case PropertyDeclarationSyntax _:
                    case ConstructorDeclarationSyntax _:
                    case ClassDeclarationSyntax _:
                    case StructDeclarationSyntax _:
                    case EnumDeclarationSyntax _:
                    case InterfaceDeclarationSyntax _:
                    case DelegateDeclarationSyntax _:
                        hasNonCanonical = true;
                        break;
                }
            }

            // isShowCustomProperties は元 source に明示宣言がなくても body で使う場合があるので追加
            if (!p.IsShowFields.Contains("isShowCustomProperties"))
                p.IsShowFields.Add("isShowCustomProperties");

            p.PatternMatched = !string.IsNullOrEmpty(p.ClassName)
                && (p.FindPropertyNames.Count > 0 || p.DrawCustomPropertiesBodyLines.Count > 0)
                && !hasNonCanonical;

            return p;
        }

        static void AnalyzeField(FieldDeclarationSyntax field, ParsedInspector p, ref bool hasNonCanonical)
        {
            var type = field.Declaration.Type.ToString();
            foreach (var variable in field.Declaration.Variables)
            {
                var name = variable.Identifier.ValueText;
                if (type == "MaterialProperty")
                {
                    p.MaterialPropertyFields.Add(name);
                }
                else if (type == "bool" && name.StartsWith("isShow"))
                {
                    if (!p.IsShowFields.Contains(name)) p.IsShowFields.Add(name);
                }
                else if (type == "string" && name == "shaderName")
                {
                    if (variable.Initializer?.Value is LiteralExpressionSyntax lit && lit.Token.Value is string s)
                        p.ShaderNameConst = s;
                }
                else
                {
                    hasNonCanonical = true;
                }
            }
        }

        static void AnalyzeLoadCustomProperties(MethodDeclarationSyntax method, ParsedInspector p)
        {
            if (method.Body == null) return;
            foreach (var assign in method.Body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (!(assign.Left is IdentifierNameSyntax fieldName)) continue;
                if (!(assign.Right is InvocationExpressionSyntax invoke)) continue;
                if (!(invoke.Expression is IdentifierNameSyntax callName) || callName.Identifier.ValueText != "FindProperty") continue;
                var args = invoke.ArgumentList.Arguments;
                if (args.Count < 2) continue;
                if (!(args[0].Expression is LiteralExpressionSyntax propLit) || !(propLit.Token.Value is string propName)) continue;

                p.FindPropertyNames.Add(propName);
                p.FieldToPropertyName[fieldName.Identifier.ValueText] = propName;
            }
        }

        static void AnalyzeDrawCustomProperties(MethodDeclarationSyntax method, ParsedInspector p)
        {
            if (method.Body == null) return;

            // body のテキストをそのまま行単位で保存 (merged Inspector 生成時に再利用)
            var bodyText = method.Body.ToFullString();
            var inner = bodyText.Trim();
            if (inner.StartsWith("{")) inner = inner.Substring(1);
            if (inner.EndsWith("}")) inner = inner.Substring(0, inner.Length - 1);

            foreach (var line in inner.Replace("\r\n", "\n").Split('\n'))
                p.DrawCustomPropertiesBodyLines.Add(line);

            // 最初の Foldout("<title>", ...) 呼び出しから title 抽出
            foreach (var invoke in method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!(invoke.Expression is IdentifierNameSyntax callName) || callName.Identifier.ValueText != "Foldout") continue;
                var args = invoke.ArgumentList.Arguments;
                if (args.Count >= 1 && args[0].Expression is LiteralExpressionSyntax lit && lit.Token.Value is string title)
                {
                    p.FoldoutTitle = title;
                    break;
                }
            }
        }
    }
}

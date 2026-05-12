using System.Collections.Generic;
using System.Text;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class InspectorMerger
    {
        public static string Generate(
            IReadOnlyList<(string sourceKey, ParsedInspector ins)> sources,
            string newClassName,
            string newShaderName,
            string @namespace,
            List<Diagnostic> diags)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#if UNITY_EDITOR");
            // using directives: 基本 UnityEditor / UnityEngine + 各ソースの using を union
            var allUsings = new HashSet<string> { "UnityEditor", "UnityEngine" };
            foreach (var (_, ins) in sources)
            {
                if (!ins.PatternMatched) continue;
                foreach (var u in ins.Usings) allUsings.Add(u);
            }
            foreach (var u in allUsings) sb.AppendLine($"using {u};");
            sb.AppendLine();
            sb.AppendLine($"namespace {@namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {newClassName} : lilToonInspector");
            sb.AppendLine("    {");

            // フィールド宣言
            var declaredFields = new HashSet<string>();
            foreach (var (key, ins) in sources)
            {
                if (!ins.PatternMatched)
                {
                    diags.Add(new Diagnostic
                    {
                        Category = "inspector",
                        Severity = Severity.Warning,
                        Message = $"Source '{key}' inspector pattern not matched; skipping its custom UI"
                    });
                    continue;
                }
                foreach (var f in ins.MaterialPropertyFields)
                    if (declaredFields.Add(f))
                        sb.AppendLine($"        MaterialProperty {f};");
            }
            sb.AppendLine();
            // セクション毎に元 inspector の isShow* 系を全てリネーム宣言
            // sectionFieldRewrites[i] = {originalName → renamedName} for source i
            var sectionFieldRewrites = new Dictionary<string, string>[sources.Count];
            var usedFieldNames = new HashSet<string>();
            for (int i = 0; i < sources.Count; i++)
            {
                var (key, ins) = sources[i];
                if (!ins.PatternMatched) continue;
                var baseIdent = SafeIdent(string.IsNullOrEmpty(ins.ClassName) ? key : ins.ClassName);
                var rewriteMap = new Dictionary<string, string>();
                foreach (var original in ins.IsShowFields)
                {
                    var renamed = $"isShow_{baseIdent}_{original}";
                    int suffix = 1;
                    while (usedFieldNames.Contains(renamed))
                        renamed = $"isShow_{baseIdent}_{original}_{suffix++}";
                    usedFieldNames.Add(renamed);
                    rewriteMap[original] = renamed;
                    sb.AppendLine($"        private static bool {renamed};");
                }
                sectionFieldRewrites[i] = rewriteMap;
            }
            sb.AppendLine($"        private const string shaderName = \"{newShaderName}\";");
            sb.AppendLine();

            // LoadCustomProperties
            sb.AppendLine("        protected override void LoadCustomProperties(MaterialProperty[] props, Material material)");
            sb.AppendLine("        {");
            sb.AppendLine("            isCustomShader = true;");
            sb.AppendLine("            ReplaceToCustomShaders();");
            sb.AppendLine("            isShowRenderMode = !material.shader.name.Contains(\"Optional\");");
            // <field> = FindProperty("<propName>", props); 形式で代入 (元 inspector の field/property 関係を尊重)
            var loadedFields = new HashSet<string>();
            foreach (var (key, ins) in sources)
            {
                if (!ins.PatternMatched) continue;
                foreach (var kv in ins.FieldToPropertyName)
                    if (loadedFields.Add(kv.Key))
                        sb.AppendLine($"            {kv.Key} = FindProperty(\"{kv.Value}\", props);");
            }
            sb.AppendLine("        }");
            sb.AppendLine();

            // DrawCustomProperties: 各セクションの body 中の isShow* フィールドを section 毎にリネーム
            sb.AppendLine("        protected override void DrawCustomProperties(Material material)");
            sb.AppendLine("        {");
            for (int i = 0; i < sources.Count; i++)
            {
                var (key, ins) = sources[i];
                if (!ins.PatternMatched) continue;
                var rewriteMap = sectionFieldRewrites[i];
                sb.AppendLine($"            // --- {key} ---");
                foreach (var line in ins.DrawCustomPropertiesBodyLines)
                {
                    var trimmed = line.TrimEnd();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;
                    var rewritten = trimmed.TrimStart();
                    foreach (var kv in rewriteMap)
                    {
                        rewritten = System.Text.RegularExpressions.Regex.Replace(
                            rewritten,
                            @"\b" + System.Text.RegularExpressions.Regex.Escape(kv.Key) + @"\b",
                            kv.Value);
                    }
                    sb.AppendLine($"            {rewritten}");
                }
            }
            sb.AppendLine("        }");
            sb.AppendLine();

            // ExtraMembers (元 inspector の helper method / 非 MaterialProperty field / nested type) をソース毎に統合
            for (int i = 0; i < sources.Count; i++)
            {
                var (key, ins) = sources[i];
                if (!ins.PatternMatched) continue;
                if (ins.ExtraMembers.Count == 0) continue;
                sb.AppendLine($"        // --- Extra members from {key} ---");
                foreach (var memberText in ins.ExtraMembers)
                {
                    foreach (var rawLine in memberText.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
                    {
                        var trimmed = rawLine.TrimStart();
                        // プリプロセッサディレクティブ (#region/#endregion/#if/#endif/#else/#elif/#pragma 等) は
                        // ToFullString() で leading/trailing trivia として混入し対応が崩れるためスキップ
                        if (trimmed.StartsWith("#region") || trimmed.StartsWith("#endregion") ||
                            trimmed.StartsWith("#if") || trimmed.StartsWith("#endif") ||
                            trimmed.StartsWith("#else") || trimmed.StartsWith("#elif") ||
                            trimmed.StartsWith("#pragma") || trimmed.StartsWith("#define") || trimmed.StartsWith("#undef"))
                            continue;
                        sb.AppendLine($"        {trimmed}".TrimEnd());
                    }
                }
                sb.AppendLine();
            }

            // ReplaceToCustomShaders (lilToon テンプレート)
            sb.Append(GenerateReplaceToCustomShaders());

            sb.AppendLine("    }");

            // SiblingTypes (同 .cs 内に並列定義された他 class/struct/enum) を namespace ブロック内に追加
            // (例: FresnelAlphaEx の lilToonFresnelAlphaExEditorSetting)
            var emittedSiblingNames = new HashSet<string>();
            foreach (var (key, ins) in sources)
            {
                if (!ins.PatternMatched) continue;
                foreach (var siblingText in ins.SiblingTypes)
                {
                    sb.AppendLine();
                    foreach (var rawLine in siblingText.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
                    {
                        var trimmed = rawLine.TrimStart();
                        if (trimmed.StartsWith("#region") || trimmed.StartsWith("#endregion") ||
                            trimmed.StartsWith("#if") || trimmed.StartsWith("#endif") ||
                            trimmed.StartsWith("#else") || trimmed.StartsWith("#elif") ||
                            trimmed.StartsWith("#pragma") || trimmed.StartsWith("#define") || trimmed.StartsWith("#undef"))
                            continue;
                        sb.AppendLine($"    {trimmed}".TrimEnd());
                    }
                }
            }

            sb.AppendLine("}");
            sb.AppendLine("#endif");
            return sb.ToString();
        }

        static string SafeIdent(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Unnamed";
            var sb = new StringBuilder();
            foreach (var c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            if (char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }

        static string GenerateReplaceToCustomShaders()
        {
            var sb = new StringBuilder();
            sb.AppendLine("        protected override void ReplaceToCustomShaders()");
            sb.AppendLine("        {");
            string[] mappings = {
                "lts=lilToon", "ltsc=Cutout", "ltst=Transparent",
                "ltsot=OnePassTransparent", "ltstt=TwoPassTransparent",
                "ltso=OpaqueOutline", "ltsco=CutoutOutline", "ltsto=TransparentOutline",
                "ltsoto=OnePassTransparentOutline", "ltstto=TwoPassTransparentOutline",
                "ltsoo=[Optional] OutlineOnly/Opaque", "ltscoo=[Optional] OutlineOnly/Cutout", "ltstoo=[Optional] OutlineOnly/Transparent",
                "ltstess=Tessellation/Opaque", "ltstessc=Tessellation/Cutout", "ltstesst=Tessellation/Transparent",
                "ltstessot=Tessellation/OnePassTransparent", "ltstesstt=Tessellation/TwoPassTransparent",
                "ltstesso=Tessellation/OpaqueOutline", "ltstessco=Tessellation/CutoutOutline", "ltstessto=Tessellation/TransparentOutline",
                "ltstessoto=Tessellation/OnePassTransparentOutline", "ltstesstto=Tessellation/TwoPassTransparentOutline",
                "ltsl=lilToonLite", "ltslc=Lite/Cutout", "ltslt=Lite/Transparent",
                "ltslot=Lite/OnePassTransparent", "ltsltt=Lite/TwoPassTransparent",
                "ltslo=Lite/OpaqueOutline", "ltslco=Lite/CutoutOutline", "ltslto=Lite/TransparentOutline",
                "ltsloto=Lite/OnePassTransparentOutline", "ltsltto=Lite/TwoPassTransparentOutline",
                "ltsref=Refraction", "ltsrefb=RefractionBlur",
                "ltsfur=Fur", "ltsfurc=FurCutout", "ltsfurtwo=FurTwoPass",
                "ltsfuro=[Optional] FurOnly/Transparent", "ltsfuroc=[Optional] FurOnly/Cutout", "ltsfurotwo=[Optional] FurOnly/TwoPass",
                "ltsgem=Gem", "ltsfs=[Optional] FakeShadow",
                "ltsover=[Optional] Overlay", "ltsoover=[Optional] OverlayOnePass",
                "ltslover=[Optional] LiteOverlay", "ltsloover=[Optional] LiteOverlayOnePass",
                "ltsm=lilToonMulti", "ltsmo=MultiOutline", "ltsmref=MultiRefraction",
                "ltsmfur=MultiFur", "ltsmgem=MultiGem",
            };
            foreach (var pair in mappings)
            {
                var parts = pair.Split('=');
                var field = parts[0];
                var suffix = parts[1];
                bool isVisible = suffix.StartsWith("lilToon") || suffix.StartsWith("[Optional]");
                if (isVisible)
                    sb.AppendLine($"            {field} = Shader.Find(shaderName + \"/{suffix}\");");
                else
                    sb.AppendLine($"            {field} = Shader.Find(\"Hidden/\" + shaderName + \"/{suffix}\");");
            }
            sb.AppendLine("        }");
            return sb.ToString();
        }
    }
}

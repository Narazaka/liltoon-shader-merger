using System.Collections.Generic;
using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class InspectorMergerTests
    {
        [Test]
        public void Merge_PatternMatchedSources_GeneratesCanonicalClass()
        {
            var a = new ParsedInspector { PatternMatched = true, ClassName="motchiriInspector", ShaderNameConst="motchiri",
                FoldoutTitle="motchiri Settings" };
            a.MaterialPropertyFields.Add("_Mask"); a.MaterialPropertyFields.Add("_strength");
            a.FindPropertyNames.Add("_Mask"); a.FindPropertyNames.Add("_strength");
            a.DrawCustomPropertiesBodyLines.Add("isShowCustomProperties = Foldout(\"motchiri Settings\", \"motchiri Settings\", isShowCustomProperties);");
            a.DrawCustomPropertiesBodyLines.Add("if(isShowCustomProperties){");
            a.DrawCustomPropertiesBodyLines.Add("m_MaterialEditor.ShaderProperty(_Mask, \"Mask\");");
            a.DrawCustomPropertiesBodyLines.Add("}");

            var b = new ParsedInspector { PatternMatched = true, ClassName="UzumoreInspector", ShaderNameConst="Sigmal00/Uzumore",
                FoldoutTitle="埋もれ設定" };
            b.MaterialPropertyFields.Add("_UzumoreAmount");
            b.FindPropertyNames.Add("_UzumoreAmount");
            b.DrawCustomPropertiesBodyLines.Add("isShowCustomProperties = Foldout(\"埋もれ設定\", \"埋もれ設定\", isShowCustomProperties);");

            var diags = new List<Diagnostic>();
            var code = InspectorMerger.Generate(
                new[] { ("motchiri", a), ("uzumore", b) },
                newClassName: "MergedInspector",
                newShaderName: "Merged/MU",
                @namespace: "lilToon",
                diags: diags);

            Assert.That(code, Does.Contain("public class MergedInspector : lilToonInspector"));
            Assert.That(code, Does.Contain("MaterialProperty _Mask;"));
            Assert.That(code, Does.Contain("MaterialProperty _strength;"));
            Assert.That(code, Does.Contain("MaterialProperty _UzumoreAmount;"));
            Assert.That(code, Does.Contain("_Mask = FindProperty(\"_Mask\", props);"));
            Assert.That(code, Does.Contain("_UzumoreAmount = FindProperty(\"_UzumoreAmount\", props);"));
            Assert.That(code, Does.Contain("private const string shaderName = \"Merged/MU\";"));
            // Foldout per source
            Assert.That(code, Does.Contain("motchiri Settings"));
            Assert.That(code, Does.Contain("埋もれ設定"));
            Assert.That(code, Does.Contain("Shader.Find(shaderName + \"/lilToon\")"));
        }

        [Test]
        public void Merge_SkipsNonPatternMatched()
        {
            var a = new ParsedInspector { PatternMatched = true, ClassName="A" };
            a.MaterialPropertyFields.Add("_x");
            a.FindPropertyNames.Add("_x");
            var b = new ParsedInspector { PatternMatched = false };
            var diags = new List<Diagnostic>();
            var code = InspectorMerger.Generate(
                new[] { ("A", a), ("B", b) }, "M", "Merged/X", "lilToon", diags);
            Assert.That(code, Does.Contain("_x"));
            Assert.That(diags.Count, Is.GreaterThan(0));
            Assert.That(diags[0].Category, Is.EqualTo("inspector"));
        }
    }
}

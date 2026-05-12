using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class InspectorCsParserTests
    {
        [Test]
        public void Parse_ExtractsCanonicalInspectorPattern()
        {
            const string input = @"
namespace lilToon
{
    public class motchiriInspector : lilToonInspector
    {
        MaterialProperty _Mask;
        MaterialProperty _strength;
        private const string shaderName = ""motchiri"";

        protected override void LoadCustomProperties(MaterialProperty[] props, Material material)
        {
            isCustomShader = true;
            ReplaceToCustomShaders();
            _Mask = FindProperty(""_Mask"", props);
            _strength = FindProperty(""_strength"", props);
        }

        protected override void DrawCustomProperties(Material material)
        {
            isShowCustomProperties = Foldout(""motchiri Settings"", ""motchiri Settings"", isShowCustomProperties);
            if (isShowCustomProperties)
            {
                m_MaterialEditor.ShaderProperty(_Mask, ""Mask"");
                m_MaterialEditor.ShaderProperty(_strength, ""strength"");
            }
        }
    }
}";
            var ins = InspectorCsParser.Parse(input);
            Assert.That(ins.PatternMatched, Is.True);
            Assert.That(ins.ClassName, Is.EqualTo("motchiriInspector"));
            Assert.That(ins.Namespace, Is.EqualTo("lilToon"));
            Assert.That(ins.ShaderNameConst, Is.EqualTo("motchiri"));
            CollectionAssert.AreEquivalent(new[]{"_Mask","_strength"}, ins.MaterialPropertyFields);
            CollectionAssert.AreEquivalent(new[]{"_Mask","_strength"}, ins.FindPropertyNames);
            Assert.That(ins.FoldoutTitle, Is.EqualTo("motchiri Settings"));
            Assert.That(string.Join("\n", ins.DrawCustomPropertiesBodyLines),
                Does.Contain("ShaderProperty(_Mask"));
        }

        [Test]
        public void Parse_NonCanonical_PatternMatchedFalse()
        {
            const string input = "public class Foo { }";
            var ins = InspectorCsParser.Parse(input);
            Assert.That(ins.PatternMatched, Is.False);
        }
    }
}

using NUnit.Framework;

namespace Narazaka.LilToonShaderMerger.Tests
{
    public class CustomHlslParserTests
    {
        [Test]
        public void Parse_ExtractsLilCustomProperties_Multiline()
        {
            const string input = @"
#define LIL_CUSTOM_PROPERTIES \
    float _effect;\
    float _strength;\
    float4 _color;
";
            var d = CustomHlslParser.Parse(input);
            CollectionAssert.AreEqual(
                new[] { "float _effect;", "float _strength;", "float4 _color;" },
                d.MultilineMacros["LIL_CUSTOM_PROPERTIES"]
            );
        }

        [Test]
        public void Parse_ExtractsLilCustomTextures()
        {
            const string input = @"
#define LIL_CUSTOM_TEXTURES \
    sampler2D _Mask;
";
            var d = CustomHlslParser.Parse(input);
            CollectionAssert.AreEqual(
                new[] { "sampler2D _Mask;" },
                d.MultilineMacros["LIL_CUSTOM_TEXTURES"]
            );
        }

        [Test]
        public void Parse_IgnoresCommentedOutMacros()
        {
            const string input = @"
//#define LIL_CUSTOM_PROPERTIES \
//    float _commented;
#define LIL_CUSTOM_PROPERTIES \
    float _real;
";
            var d = CustomHlslParser.Parse(input);
            CollectionAssert.AreEqual(
                new[] { "float _real;" },
                d.MultilineMacros["LIL_CUSTOM_PROPERTIES"]
            );
        }

        [Test]
        public void Parse_MissingMacro_NotInDictionary()
        {
            const string input = "// no macros";
            var d = CustomHlslParser.Parse(input);
            Assert.That(d.MultilineMacros.ContainsKey("LIL_CUSTOM_PROPERTIES"), Is.False);
        }
    }
}

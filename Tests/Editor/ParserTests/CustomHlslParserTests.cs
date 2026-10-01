using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
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
                d.FindDefine("LIL_CUSTOM_PROPERTIES").Body
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
                d.FindDefine("LIL_CUSTOM_TEXTURES").Body
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
                d.FindDefine("LIL_CUSTOM_PROPERTIES").Body
            );
        }

        [Test]
        public void Parse_MissingMacro_NotInDictionary()
        {
            const string input = "// no macros";
            var d = CustomHlslParser.Parse(input);
            Assert.That(d.FindDefine("LIL_CUSTOM_PROPERTIES"), Is.Null);
        }

        [Test]
        public void Parse_ExtractsLilRequireApp_Flags()
        {
            const string input = @"
#define LIL_REQUIRE_APP_POSITION
#define LIL_REQUIRE_APP_NORMAL
//#define LIL_REQUIRE_APP_COLOR
";
            var d = CustomHlslParser.Parse(input);
            Assert.That(d.FindDefine("LIL_REQUIRE_APP_POSITION"), Is.Not.Null);
            Assert.That(d.FindDefine("LIL_REQUIRE_APP_NORMAL"), Is.Not.Null);
            Assert.That(d.FindDefine("LIL_REQUIRE_APP_COLOR"), Is.Null);
        }

        [Test]
        public void Parse_ExtractsLilV2FForce_Flags()
        {
            const string input = "#define LIL_V2F_FORCE_TEXCOORD2";
            var d = CustomHlslParser.Parse(input);
            Assert.That(d.FindDefine("LIL_V2F_FORCE_TEXCOORD2"), Is.Not.Null);
        }

        [Test]
        public void Parse_ExtractsLilCustomVertCopy_Flag()
        {
            const string input = "#define LIL_CUSTOM_VERT_COPY";
            var d = CustomHlslParser.Parse(input);
            Assert.That(d.FindDefine("LIL_CUSTOM_VERT_COPY"), Is.Not.Null);
        }

        [Test]
        public void Parse_ExtractsLilCustomVertexOs_Multiline()
        {
            const string input = @"
#define LIL_CUSTOM_VERTEX_OS \
    positionOS.xyz += offset;\
    if(_normal==1){input.normalOS = newNormal;}
";
            var d = CustomHlslParser.Parse(input);
            CollectionAssert.AreEqual(
                new[] { "positionOS.xyz += offset;", "if(_normal==1){input.normalOS = newNormal;}" },
                d.FindDefine("LIL_CUSTOM_VERTEX_OS").Body
            );
        }

        [Test]
        public void Parse_ExtractsBeforeOutput()
        {
            const string input = @"
#define BEFORE_OUTPUT \
    fd.col *= myColor;
";
            var d = CustomHlslParser.Parse(input);
            CollectionAssert.AreEqual(new[] { "fd.col *= myColor;" }, d.FindDefine("BEFORE_OUTPUT").Body);
        }

        [Test]
        public void Parse_ExtractsOverrideStage()
        {
            const string input = @"
#define OVERRIDE_NORMAL \
    fd.N = newNormal;
";
            var d = CustomHlslParser.Parse(input);
            CollectionAssert.AreEqual(new[] { "fd.N = newNormal;" }, d.FindDefine("OVERRIDE_NORMAL").Body);
        }
    }
}

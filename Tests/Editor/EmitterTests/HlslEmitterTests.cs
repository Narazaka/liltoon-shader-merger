using System.Collections.Generic;
using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class HlslEmitterTests
    {
        [Test]
        public void EmitCustomHlsl_RendersMultilineAndFlagMacros()
        {
            var m = new MergedHlsl();
            m.MultilineMacros["LIL_CUSTOM_PROPERTIES"] = new List<string> { "float _a;", "float _b;" };
            m.FlagMacros.Add("LIL_REQUIRE_APP_POSITION");
            m.FlagMacros.Add("LIL_REQUIRE_APP_NORMAL");

            var txt = HlslEmitter.EmitCustomHlsl(m);

            Assert.That(txt, Does.Contain("#define LIL_CUSTOM_PROPERTIES \\"));
            Assert.That(txt, Does.Contain("float _a;\\"));
            Assert.That(txt, Does.Contain("float _b;"));  // 最後は \ なし
            Assert.That(txt, Does.Contain("#define LIL_REQUIRE_APP_POSITION"));
            Assert.That(txt, Does.Contain("#define LIL_REQUIRE_APP_NORMAL"));
        }

        [Test]
        public void EmitCustomInsertHlsl_ConcatenatesBodiesWithSeparators()
        {
            var sources = new[]
            {
                ("motchiri", "float foo() { return 1; }"),
                ("uzumore",  "float bar() { return 2; }"),
            };
            var txt = HlslEmitter.EmitCustomInsertHlsl(sources);
            Assert.That(txt, Does.Contain("// --- motchiri ---"));
            Assert.That(txt, Does.Contain("float foo()"));
            Assert.That(txt, Does.Contain("// --- uzumore ---"));
            Assert.That(txt, Does.Contain("float bar()"));
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class LilContainerEmitterTests
    {
        [Test]
        public void MergeContainerText_SingleSource_Identity()
        {
            const string c = @"Shader ""Hidden/*LIL_SHADER_NAME*/ltspass_opaque""
{
    HLSLINCLUDE
        #define LIL_RENDER 0
        #include ""custom.hlsl""
    ENDHLSL
    lilSubShaderInsert ""lilCustomShaderInsert.lilblock""
}";
            var diags = new List<Diagnostic>();
            var merged = LilContainerEmitter.MergeContainerText(new[] { ("a", c) }, diags);
            Assert.That(merged, Does.Contain("#include \"custom.hlsl\""));
            Assert.That(merged, Does.Contain("lilSubShaderInsert"));
        }

        [Test]
        public void MergeContainerText_HlslIncludeBlockMerged()
        {
            const string a = @"Shader ""X""
{
    HLSLINCLUDE
        #define LIL_RENDER 0
        #include ""custom.hlsl""
    ENDHLSL
}";
            const string b = @"Shader ""X""
{
    HLSLINCLUDE
        #define LIL_RENDER 0
        #include ""custom.hlsl""
        #define EXTRA_FROM_B 1
    ENDHLSL
}";
            var diags = new List<Diagnostic>();
            var merged = LilContainerEmitter.MergeContainerText(new[] { ("a", a), ("b", b) }, diags);
            Assert.That(merged, Does.Contain("#define LIL_RENDER 0"));
            Assert.That(merged, Does.Contain("#define EXTRA_FROM_B 1"));
            // LIL_RENDER は 1 回だけ
            int count = 0;
            foreach (var ln in merged.Replace("\r\n", "\n").Split('\n'))
                if (ln.Contains("#define LIL_RENDER 0")) count++;
            Assert.That(count, Is.EqualTo(1));
        }
    }
}

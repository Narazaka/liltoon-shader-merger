using System.Collections.Generic;
using System.IO;
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
            var merged = LilContainerEmitter.MergeContainerText("x.lilcontainer", new[] { ("a", c) }, new string[0], diags);
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
            var merged = LilContainerEmitter.MergeContainerText("x.lilcontainer", new[] { ("a", a), ("b", b) }, new string[0], diags);
            Assert.That(merged, Does.Contain("#define LIL_RENDER 0"));
            Assert.That(merged, Does.Contain("#define EXTRA_FROM_B 1"));
            // LIL_RENDER は 1 回だけ
            int count = 0;
            foreach (var ln in merged.Replace("\r\n", "\n").Split('\n'))
                if (ln.Contains("#define LIL_RENDER 0")) count++;
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void MergeContainerText_MultipleHlslIncludeBlocks_MergedPerBlock()
        {
            const string a = @"Shader ""X""
{
    HLSLINCLUDE
        #include ""custom.hlsl""
    ENDHLSL
    SubShader
    {
        HLSLINCLUDE
            #define LIL_TESSELLATION
        ENDHLSL
    }
}";
            var diags = new List<Diagnostic>();
            var merged = LilContainerEmitter.MergeContainerText("x.lilcontainer", new[] { ("a", a), ("b", a) }, new string[0], diags);
            var subShader = merged.Substring(merged.IndexOf("SubShader"));
            Assert.That(subShader, Does.Contain("#define LIL_TESSELLATION"));
            Assert.That(subShader, Does.Not.Contain("custom.hlsl"));
        }

        [Test]
        public void MergeContainerText_CustomHlslVariant_ReplacesCustomHlslInclude()
        {
            const string a = @"Shader ""X""
{
    HLSLINCLUDE
        #define LIL_RENDER 2
        #include ""custom_fur.hlsl""
    ENDHLSL
}";
            const string b = @"Shader ""X""
{
    HLSLINCLUDE
        #define LIL_RENDER 2
        #include ""custom.hlsl""
        #include ""helper.hlsl""
    ENDHLSL
}";
            var diags = new List<Diagnostic>();
            var merged = LilContainerEmitter.MergeContainerText("x.lilcontainer", new[] { ("a", a), ("b", b) }, new[] { "custom_fur.hlsl" }, diags);
            Assert.That(merged, Does.Contain("#include \"custom_fur.hlsl\""));
            Assert.That(merged, Does.Not.Contain("#include \"custom.hlsl\""));
            Assert.That(merged, Does.Contain("#include \"helper.hlsl\""));
            Assert.That(diags, Is.Empty);
        }

        [Test]
        public void MergeContainerText_ConditionalDirectivesNotDeduped()
        {
            const string a = @"Shader ""X""
{
    HLSLINCLUDE
        #if defined(A)
            #define FROM_A
        #endif
    ENDHLSL
}";
            const string b = @"Shader ""X""
{
    HLSLINCLUDE
        #if defined(B)
            #define FROM_B
        #endif
    ENDHLSL
}";
            var diags = new List<Diagnostic>();
            var merged = LilContainerEmitter.MergeContainerText("x.lilcontainer", new[] { ("a", a), ("b", b) }, new string[0], diags);
            Assert.That(merged.Split(new[] { "#endif" }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(2));
        }

        [Test]
        public void MergeContainerText_BlocksMatchedByPosition()
        {
            // もっちりの tess 系 (SubShader 自前) とうずもれ (lilSubShaderBRP 任せ) の組み合わせ
            const string a = @"Shader ""Hidden/*LIL_SHADER_NAME*/x""
{
    HLSLINCLUDE
        #define A
    ENDHLSL
    // lilSubShaderBRP ""DefaultTessellation""
    SubShader
    {
        HLSLINCLUDE
            #define A2
        ENDHLSL
    }
}";
            const string b = @"Shader ""Hidden/*LIL_SHADER_NAME*/x""
{
    HLSLINCLUDE
        #define B
    ENDHLSL
    lilSubShaderBRP ""DefaultTessellation""
}";
            var template = new[] { LilContainerEmitter.StructureHash(b) };
            // 書き換えた側 (a) がどの順番でも土台になる
            foreach (var sources in new[] { new[] { ("a", a), ("b", b) }, new[] { ("b", b), ("a", a) } })
            {
                var diags = new List<Diagnostic>();
                var merged = LilContainerEmitter.MergeContainerText("x.lilcontainer", sources, new string[0], template, diags);
                var subShader = merged.Substring(merged.IndexOf("    SubShader"));
                Assert.That(merged.Substring(0, merged.IndexOf("    SubShader")), Does.Contain("#define B"));
                Assert.That(subShader, Does.Contain("#define A2"));
                Assert.That(subShader, Does.Not.Contain("#define B"));
                Assert.That(merged, Does.Not.Contain("\n    lilSubShaderBRP"));
                Assert.That(diags, Is.Empty);
            }
        }

        [Test]
        public void MergeContainerText_DifferentCustomizations_Error()
        {
            const string template = @"Shader ""X""
{
    HLSLINCLUDE
    ENDHLSL
    lilSubShaderBRP ""Default""
}";
            const string a = @"Shader ""X""
{
    HLSLINCLUDE
    ENDHLSL
    lilSubShaderBRP ""DefaultUsePass""
}";
            const string b = @"Shader ""X""
{
    HLSLINCLUDE
    ENDHLSL
    SubShader { }
}";
            var diags = new List<Diagnostic>();
            var merged = LilContainerEmitter.MergeContainerText("x.lilcontainer", new[] { ("t", template), ("a", a), ("b", b) }, new string[0],
                new[] { LilContainerEmitter.StructureHash(template) }, diags);
            Assert.That(merged, Is.Null);
            Assert.That(diags.Exists(d => d.Severity == Severity.Error && d.Message.Contains("[a]") && d.Message.Contains("[b]")));
        }

        [Test]
        public void StructureHash_IgnoresCommentsWhitespaceAndHlslIncludeBody()
        {
            const string a = "Shader \"X\"\n{\n    HLSLINCLUDE\n        #define A\n    ENDHLSL\n    lilSubShaderBRP \"Default\"\n}";
            const string b = "Shader  \"X\"\r\n{\r\n\r\n    // memo\r\n    HLSLINCLUDE\r\n    ENDHLSL\r\n\tlilSubShaderBRP \"Default\" // memo\r\n}";
            Assert.That(LilContainerEmitter.StructureHash(b), Is.EqualTo(LilContainerEmitter.StructureHash(a)));
        }

        [Test]
        public void MergeContainerText_BlockWithoutPlaceInBase_Error()
        {
            const string template = @"Shader ""X""
{
    HLSLINCLUDE
        #define T
    ENDHLSL
}";
            // Shader 直下の HLSLINCLUDE を持たない書き換え
            const string custom = @"Shader ""X""
{
    SubShader { HLSLINCLUDE
        #define C
    ENDHLSL }
}";
            var diags = new List<Diagnostic>();
            var merged = LilContainerEmitter.MergeContainerText("x.lilcontainer", new[] { ("t", template), ("c", custom) }, new string[0],
                new[] { LilContainerEmitter.StructureHash(template) }, diags);
            Assert.That(merged, Is.Null);
            Assert.That(diags.Exists(d => d.Severity == Severity.Error && d.Message.Contains("Shader#0/HLSLINCLUDE#0")));
        }

        [Test]
        public void CollectCustomHlslVariants_DetectsExistingReplacementsOnly()
        {
            var dir = Path.Combine(Path.GetTempPath(), "lsm_variant_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "sub"));
            try
            {
                File.WriteAllText(Path.Combine(dir, "sub", "custom_fur.cginc"), "");
                File.WriteAllText(Path.Combine(dir, "helper.hlsl"), "");
                // 置き換え (サブフォルダ・拡張子不問) / custom.hlsl と併記の helper / 実在しない include
                File.WriteAllText(Path.Combine(dir, "lts_fur.lilcontainer"), "HLSLINCLUDE\n#include \"sub/custom_fur.cginc\"\nENDHLSL");
                File.WriteAllText(Path.Combine(dir, "lts.lilcontainer"), "HLSLINCLUDE\n#include \"custom.hlsl\"\n#include \"helper.hlsl\"\nENDHLSL");
                File.WriteAllText(Path.Combine(dir, "lts_x.lilcontainer"), "HLSLINCLUDE\n#include \"missing.hlsl\"\nENDHLSL");
                CollectionAssert.AreEquivalent(new[] { "sub/custom_fur.cginc" }, LilContainerEmitter.CollectCustomHlslVariants(new[] { dir }));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void ExpandCustomHlslInclude_InlinesSourceCustomHlsl()
        {
            var dir = Path.Combine(Path.GetTempPath(), "lsm_expand_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "custom.hlsl"), "#define BEFORE_OUTPUT \\\n    fd.col *= input.uv23.x;\n#define KEEP 1\n");
                var variant = Path.Combine(dir, "custom_fur.hlsl");
                File.WriteAllText(variant, "#include \"custom.hlsl\"\n#undef BEFORE_OUTPUT\n");
                var d = MacroMerger.Merge(new[] { ("a", CustomHlslParser.Parse(LilToonShaderMerger.ExpandCustomHlslInclude(variant, dir))) }, ConflictStrategy.ErrorOut, new List<Diagnostic>());
                Assert.That(d.FindDefine("BEFORE_OUTPUT"), Is.Null);
                CollectionAssert.AreEqual(new[] { "1" }, d.FindDefine("KEEP").Body);
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}

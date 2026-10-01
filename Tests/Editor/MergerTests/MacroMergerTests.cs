using System.Collections.Generic;
using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class MacroMergerTests
    {
        static MergedHlsl Merge(ConflictStrategy strategy, List<Diagnostic> diags, params string[] sources)
        {
            var list = new List<(string, CustomHlslData)>();
            for (int i = 0; i < sources.Length; i++) list.Add(($"s{i}", CustomHlslParser.Parse(sources[i])));
            return MacroMerger.Merge(list, strategy, diags);
        }

        [Test]
        public void Merge_CustomProperties_Concatenated()
        {
            var diags = new List<Diagnostic>();
            var m = Merge(ConflictStrategy.ErrorOut, diags,
                "#define LIL_CUSTOM_PROPERTIES \\\n    float _a;",
                "#define LIL_CUSTOM_PROPERTIES \\\n    float _b;");
            CollectionAssert.AreEqual(new[] { "float _a;", "float _b;" }, m.FindDefine("LIL_CUSTOM_PROPERTIES").Body);
            Assert.That(diags, Is.Empty);
        }

        [Test]
        public void Merge_IdenticalDefines_Deduped()
        {
            var diags = new List<Diagnostic>();
            var m = Merge(ConflictStrategy.ErrorOut, diags,
                "#define LIL_REQUIRE_APP_POSITION\n#define LIL_REQUIRE_APP_NORMAL",
                "#define LIL_REQUIRE_APP_POSITION");
            Assert.That(m.Entries.FindAll(e => e.Name == "LIL_REQUIRE_APP_POSITION").Count, Is.EqualTo(1));
            Assert.That(m.FindDefine("LIL_REQUIRE_APP_NORMAL"), Is.Not.Null);
            Assert.That(diags, Is.Empty);
        }

        [Test]
        public void Merge_OverrideStage_ConflictErrorOut()
        {
            var diags = new List<Diagnostic>();
            Merge(ConflictStrategy.ErrorOut, diags,
                "#define OVERRIDE_NORMAL \\\n    fd.N = a;",
                "#define OVERRIDE_NORMAL \\\n    fd.N = b;");
            Assert.That(diags.Count, Is.GreaterThan(0));
            Assert.That(diags[0].Severity, Is.EqualTo(Severity.Error));
        }

        [Test]
        public void Merge_OverrideStage_PreferFirst()
        {
            var diags = new List<Diagnostic>();
            var m = Merge(ConflictStrategy.PreferFirst, diags,
                "#define OVERRIDE_NORMAL \\\n    fd.N = a;",
                "#define OVERRIDE_NORMAL \\\n    fd.N = b;");
            CollectionAssert.AreEqual(new[] { "fd.N = a;" }, m.FindDefine("OVERRIDE_NORMAL").Body);
        }

        [Test]
        public void Merge_ConditionalDefine_KeepsCondition()
        {
            // msdfmask の形
            var diags = new List<Diagnostic>();
            var m = Merge(ConflictStrategy.ErrorOut, diags,
                "#define LIL_CUSTOM_PROPERTIES \\\n    float _a;",
                "#if !defined(LIL_LITE)\n    #define OVERRIDE_EMISSION_1ST lilEmissionX(fd);\n#endif");
            var def = m.FindDefine("OVERRIDE_EMISSION_1ST");
            Assert.That(def.Conditions.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "#if !defined(LIL_LITE)" }, def.Conditions[0]);
            var txt = HlslEmitter.EmitCustomHlsl(m).Replace("\r\n", "\n");
            Assert.That(txt, Does.Contain("#if !defined(LIL_LITE)\n#define OVERRIDE_EMISSION_1ST lilEmissionX(fd);\n#endif"));
            Assert.That(diags, Is.Empty);
        }

        [Test]
        public void Merge_ElseBranch_KeepsBranchHeaders()
        {
            var m = Merge(ConflictStrategy.ErrorOut, new List<Diagnostic>(),
                "#ifdef A\n#define X 1\n#else\n#define X 2\n#endif");
            var defs = m.Entries.FindAll(e => e.Name == "X");
            CollectionAssert.AreEqual(new[] { "#ifdef A" }, defs[0].Conditions[0]);
            CollectionAssert.AreEqual(new[] { "#ifdef A", "#else" }, defs[1].Conditions[0]);
        }

        [Test]
        public void Merge_ChainWithConditionalSource_UsesPerSourceHelpers()
        {
            var diags = new List<Diagnostic>();
            var m = Merge(ConflictStrategy.ErrorOut, diags,
                "#define BEFORE_OUTPUT \\\n    fd.col *= a;",
                "#if !defined(LIL_LITE)\n#define BEFORE_OUTPUT \\\n    fd.col *= b;\n#endif");
            Assert.That(diags, Is.Empty);
            var txt = HlslEmitter.EmitCustomHlsl(m).Replace("\r\n", "\n");
            Assert.That(txt, Does.Contain("#define LSM_SOURCE0_BEFORE_OUTPUT fd.col *= a;"));
            Assert.That(txt, Does.Contain("#if !defined(LIL_LITE)\n#define LSM_SOURCE1_BEFORE_OUTPUT fd.col *= b;\n#endif"));
            Assert.That(txt, Does.Contain("#ifndef LSM_SOURCE1_BEFORE_OUTPUT\n#define LSM_SOURCE1_BEFORE_OUTPUT\n#endif"));
            var combined = m.FindDefine("BEFORE_OUTPUT");
            Assert.That(combined.Conditions, Is.Empty);
            Assert.That(string.Join(" ", combined.Body), Does.Contain("LSM_SOURCE0_BEFORE_OUTPUT").And.Contain("LSM_SOURCE1_BEFORE_OUTPUT"));
        }

        [Test]
        public void Merge_FunctionLikeMacro_Kept()
        {
            var m = Merge(ConflictStrategy.ErrorOut, new List<Diagnostic>(),
                "#define LIL_CUSTOM_V2F_MEMBER(id0,id1,id2,id3,id4,id5,id6,id7) \\\n    float4 custom : TEXCOORD##id0;");
            var def = m.FindDefine("LIL_CUSTOM_V2F_MEMBER");
            Assert.That(def.Params, Is.EqualTo("(id0,id1,id2,id3,id4,id5,id6,id7)"));
            Assert.That(HlslEmitter.EmitCustomHlsl(m), Does.Contain("#define LIL_CUSTOM_V2F_MEMBER(id0,id1,id2,id3,id4,id5,id6,id7) float4 custom : TEXCOORD##id0;"));
        }

        [Test]
        public void Merge_NonDefineCode_KeptPerSource()
        {
            const string fn = "float helper(float x)\n{\n    return x;\n}";
            var m = Merge(ConflictStrategy.ErrorOut, new List<Diagnostic>(),
                "#include \"a.hlsl\"\n" + fn,
                "#include \"a.hlsl\"\n" + fn,   // 同一のまとまりは 1 回だけ
                "float other()\n{\n    return 0;\n}");
            var txt = HlslEmitter.EmitCustomHlsl(m).Replace("\r\n", "\n");
            Assert.That(txt, Does.Contain("#include \"a.hlsl\""));
            Assert.That(txt.Split(new[] { "float helper" }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            // "}" のような行が他ソースのものと重複除去されない
            Assert.That(txt.Split(new[] { "\n}" }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(2));
        }
    }
}

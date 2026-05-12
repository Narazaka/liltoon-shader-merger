using System.Collections.Generic;
using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class MacroMergerTests
    {
        static CustomHlslData Make(params (string key, string[] body)[] macros)
        {
            var d = new CustomHlslData();
            foreach (var (k, b) in macros) d.MultilineMacros[k] = new List<string>(b);
            return d;
        }

        [Test]
        public void Merge_CustomProperties_Concatenated()
        {
            var a = Make(("LIL_CUSTOM_PROPERTIES", new[] { "float _a;" }));
            var b = Make(("LIL_CUSTOM_PROPERTIES", new[] { "float _b;" }));
            var diags = new List<Diagnostic>();
            var m = MacroMerger.Merge(new[] { a, b }, ConflictStrategy.ErrorOut, diags);

            CollectionAssert.AreEqual(
                new[] { "float _a;", "float _b;" },
                m.MultilineMacros["LIL_CUSTOM_PROPERTIES"]
            );
            Assert.That(diags.Count, Is.EqualTo(0));
        }

        [Test]
        public void Merge_FlagMacros_Union()
        {
            var a = new CustomHlslData(); a.FlagMacros.Add("LIL_REQUIRE_APP_POSITION");
            var b = new CustomHlslData(); b.FlagMacros.Add("LIL_REQUIRE_APP_NORMAL");
            var diags = new List<Diagnostic>();
            var m = MacroMerger.Merge(new[] { a, b }, ConflictStrategy.ErrorOut, diags);
            CollectionAssert.AreEquivalent(
                new[] { "LIL_REQUIRE_APP_POSITION", "LIL_REQUIRE_APP_NORMAL" },
                m.FlagMacros
            );
        }

        [Test]
        public void Merge_VertexOs_Chained()
        {
            var a = Make(("LIL_CUSTOM_VERTEX_OS", new[] { "positionOS.xyz += offsetA;" }));
            var b = Make(("LIL_CUSTOM_VERTEX_OS", new[] { "positionOS.xyz += offsetB;" }));
            var diags = new List<Diagnostic>();
            var m = MacroMerger.Merge(new[] { a, b }, ConflictStrategy.ErrorOut, diags);
            CollectionAssert.AreEqual(
                new[] { "positionOS.xyz += offsetA;", "positionOS.xyz += offsetB;" },
                m.MultilineMacros["LIL_CUSTOM_VERTEX_OS"]
            );
        }

        [Test]
        public void Merge_OverrideStage_ConflictErrorOut()
        {
            var a = Make(("OVERRIDE_NORMAL", new[] { "fd.N = a;" }));
            var b = Make(("OVERRIDE_NORMAL", new[] { "fd.N = b;" }));
            var diags = new List<Diagnostic>();
            var m = MacroMerger.Merge(new[] { a, b }, ConflictStrategy.ErrorOut, diags);
            Assert.That(diags.Count, Is.GreaterThan(0));
            Assert.That(diags[0].Severity, Is.EqualTo(Severity.Error));
        }

        [Test]
        public void Merge_OverrideStage_PreferFirst()
        {
            var a = Make(("OVERRIDE_NORMAL", new[] { "fd.N = a;" }));
            var b = Make(("OVERRIDE_NORMAL", new[] { "fd.N = b;" }));
            var diags = new List<Diagnostic>();
            var m = MacroMerger.Merge(new[] { a, b }, ConflictStrategy.PreferFirst, diags);
            CollectionAssert.AreEqual(new[] { "fd.N = a;" }, m.MultilineMacros["OVERRIDE_NORMAL"]);
        }
    }
}

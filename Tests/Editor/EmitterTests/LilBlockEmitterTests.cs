using System.Collections.Generic;
using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class LilBlockEmitterTests
    {
        [Test]
        public void EmitDatas_RendersShaderAndEditorName()
        {
            var d = new MergedDatas { ShaderName = "Merged/MU", EditorName = "lilToon.MergedInspector" };
            d.Replaces.Add(new ReplaceDirective { From = "foo", To = "bar" });
            d.Replaces.Add(new ReplaceDirective { Filter = "ltspass", From = "x", To = "y" });

            var txt = LilBlockEmitter.EmitDatas(d);
            Assert.That(txt, Does.Contain("ShaderName \"Merged/MU\""));
            Assert.That(txt, Does.Contain("EditorName \"lilToon.MergedInspector\""));
            Assert.That(txt, Does.Contain("Replace \"foo\" \"bar\""));
            Assert.That(txt, Does.Contain("Replace \"ltspass\" \"x\" \"y\""));
        }

        [Test]
        public void EmitInsertBlock_ConcatenatesSources_WithDedupe()
        {
            var sources = new[] {
                ("a", "#include \"custom_insert.hlsl\""),
                ("b", "#include \"custom_insert.hlsl\""),  // 重複
                ("c", "// extra hlsl\nfloat foo() { return 0; }"),
            };
            var txt = LilBlockEmitter.EmitInsertBlock(sources, dedupeIncludes: true);
            // dedupe で #include は 1 回だけ
            int count = 0;
            foreach (var line in txt.Replace("\r\n", "\n").Split('\n'))
                if (line.Trim() == "#include \"custom_insert.hlsl\"") count++;
            Assert.That(count, Is.EqualTo(1));
            Assert.That(txt, Does.Contain("float foo()"));
        }
    }
}

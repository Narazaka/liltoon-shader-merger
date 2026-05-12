using System.Collections.Generic;
using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class DataMergerTests
    {
        [Test]
        public void Merge_Replaces_UnionDeduped()
        {
            var a = new CustomShaderDatas();
            a.Replaces.Add(new ReplaceDirective { From = "foo", To = "bar" });
            a.Replaces.Add(new ReplaceDirective { From = "baz", To = "qux" });
            var b = new CustomShaderDatas();
            b.Replaces.Add(new ReplaceDirective { From = "foo", To = "bar" });  // 重複
            b.Replaces.Add(new ReplaceDirective { From = "x", To = "y" });
            var diags = new List<Diagnostic>();
            var m = DataMerger.Merge(new[] { ("A", a), ("B", b) }, "newName", "newEditor", ConflictStrategy.ErrorOut, diags);
            Assert.That(m.Replaces.Count, Is.EqualTo(3));  // foo/bar dedup
        }

        [Test]
        public void Merge_ReplaceConflict_SameFromDifferentTo_ErrorOut()
        {
            var a = new CustomShaderDatas();
            a.Replaces.Add(new ReplaceDirective { From = "foo", To = "bar1" });
            var b = new CustomShaderDatas();
            b.Replaces.Add(new ReplaceDirective { From = "foo", To = "bar2" });
            var diags = new List<Diagnostic>();
            DataMerger.Merge(new[] { ("A", a), ("B", b) }, "n", "e", ConflictStrategy.ErrorOut, diags);
            Assert.That(diags.Count, Is.GreaterThan(0));
            Assert.That(diags[0].Severity, Is.EqualTo(Severity.Error));
        }

        [Test]
        public void Merge_AssignsShaderAndEditorName()
        {
            var a = new CustomShaderDatas();
            var diags = new List<Diagnostic>();
            var m = DataMerger.Merge(new[] { ("A", a) }, "Merged/Foo", "lilToon.MyInspector", ConflictStrategy.ErrorOut, diags);
            Assert.That(m.ShaderName, Is.EqualTo("Merged/Foo"));
            Assert.That(m.EditorName, Is.EqualTo("lilToon.MyInspector"));
        }
    }
}

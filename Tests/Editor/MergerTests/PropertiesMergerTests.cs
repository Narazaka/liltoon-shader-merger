using System.Collections.Generic;
using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class PropertiesMergerTests
    {
        [Test]
        public void Merge_NoConflict_Concatenated()
        {
            var a = new CustomProperties { RawText = "_a (\"A\", float) = 0" };
            a.PropertyNames.Add("_a");
            var b = new CustomProperties { RawText = "_b (\"B\", float) = 0" };
            b.PropertyNames.Add("_b");
            var diags = new List<Diagnostic>();
            var m = PropertiesMerger.Merge(new[] { ("A", a), ("B", b) }, ConflictStrategy.ErrorOut, diags);
            Assert.That(m, Does.Contain("_a"));
            Assert.That(m, Does.Contain("_b"));
            Assert.That(diags.Count, Is.EqualTo(0));
        }

        [Test]
        public void Merge_NameConflict_ErrorOut()
        {
            var a = new CustomProperties(); a.PropertyNames.Add("_Mask");
            var b = new CustomProperties(); b.PropertyNames.Add("_Mask");
            var diags = new List<Diagnostic>();
            PropertiesMerger.Merge(new[] { ("A", a), ("B", b) }, ConflictStrategy.ErrorOut, diags);
            Assert.That(diags.Count, Is.GreaterThan(0));
            Assert.That(diags[0].Message, Does.Contain("_Mask"));
            Assert.That(diags[0].Severity, Is.EqualTo(Severity.Error));
        }
    }
}

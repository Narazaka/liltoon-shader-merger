using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class AsmdefEmitterTests
    {
        [Test]
        public void Emit_RendersAsmdefWithLilToonReference()
        {
            var json = AsmdefEmitter.Emit("MergedMU.Editor", "e7f0f8dffe955d640bbc76d1d4f4986e");
            Assert.That(json, Does.Contain("\"name\": \"MergedMU.Editor\""));
            Assert.That(json, Does.Contain("GUID:e7f0f8dffe955d640bbc76d1d4f4986e"));
            Assert.That(json, Does.Contain("\"Editor\""));
        }
    }
}

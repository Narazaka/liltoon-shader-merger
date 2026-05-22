using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class MetaGuidEmitterTests
    {
        [Test]
        public void DeterministicGuid_SameInputs_SameOutput()
        {
            var a = MetaGuidEmitter.DeterministicGuid("Merged/Foo", "custom.hlsl");
            var b = MetaGuidEmitter.DeterministicGuid("Merged/Foo", "custom.hlsl");
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void DeterministicGuid_DifferentShaderName_DifferentOutput()
        {
            var a = MetaGuidEmitter.DeterministicGuid("Merged/Foo", "custom.hlsl");
            var b = MetaGuidEmitter.DeterministicGuid("Merged/Bar", "custom.hlsl");
            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void DeterministicGuid_DifferentPath_DifferentOutput()
        {
            var a = MetaGuidEmitter.DeterministicGuid("Merged/Foo", "custom.hlsl");
            var b = MetaGuidEmitter.DeterministicGuid("Merged/Foo", "Editor/Inspector.cs");
            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void DeterministicGuid_Is32LowercaseHex()
        {
            var g = MetaGuidEmitter.DeterministicGuid("Merged/Foo", "custom.hlsl");
            Assert.That(g.Length, Is.EqualTo(32));
            Assert.That(g, Does.Match("^[0-9a-f]{32}$"));
        }

        [Test]
        public void DeterministicGuid_NormalizesBackslashesInPath()
        {
            var fwd = MetaGuidEmitter.DeterministicGuid("Merged/Foo", "Editor/Inspector.cs");
            var bwd = MetaGuidEmitter.DeterministicGuid("Merged/Foo", "Editor\\Inspector.cs");
            Assert.That(fwd, Is.EqualTo(bwd));
        }

        [Test]
        public void Relative_StripsRootAndNormalizesSeparators()
        {
            Assert.That(
                MetaGuidEmitter.Relative("Assets/Out", "Assets/Out/Editor/Foo.cs"),
                Is.EqualTo("Editor/Foo.cs"));
            Assert.That(
                MetaGuidEmitter.Relative("Assets/Out", "Assets\\Out\\Editor\\Foo.cs"),
                Is.EqualTo("Editor/Foo.cs"));
            Assert.That(
                MetaGuidEmitter.Relative("Assets/Out/", "Assets/Out/custom.hlsl"),
                Is.EqualTo("custom.hlsl"));
        }
    }
}

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

        [Test]
        public void BuildMetaText_Hlsl_HasShaderIncludeImporter()
        {
            var text = MetaGuidEmitter.BuildMetaText("foo.hlsl", "abcdef0123456789abcdef0123456789", null);
            StringAssert.Contains("guid: abcdef0123456789abcdef0123456789", text);
            StringAssert.Contains("ShaderIncludeImporter:", text);
            StringAssert.Contains("fileFormatVersion: 2", text);
        }

        [Test]
        public void BuildMetaText_Lilblock_HasDefaultImporter()
        {
            var text = MetaGuidEmitter.BuildMetaText("foo.lilblock", "00000000000000000000000000000001", null);
            StringAssert.Contains("DefaultImporter:", text);
            StringAssert.Contains("guid: 00000000000000000000000000000001", text);
        }

        [Test]
        public void BuildMetaText_Cs_HasMonoImporter()
        {
            var text = MetaGuidEmitter.BuildMetaText("Foo.cs", "00000000000000000000000000000002", null);
            StringAssert.Contains("MonoImporter:", text);
        }

        [Test]
        public void BuildMetaText_Asmdef_HasAssemblyDefinitionImporter()
        {
            var text = MetaGuidEmitter.BuildMetaText("Foo.asmdef", "00000000000000000000000000000003", null);
            StringAssert.Contains("AssemblyDefinitionImporter:", text);
        }

        [Test]
        public void BuildMetaText_Folder_HasFolderAssetMarker()
        {
            var text = MetaGuidEmitter.BuildMetaText("Editor", "00000000000000000000000000000004", null, isFolder: true);
            StringAssert.Contains("folderAsset: yes", text);
            StringAssert.Contains("DefaultImporter:", text);
        }

        [Test]
        public void BuildMetaText_Lilcontainer_UsesProvidedSourceImporterBlock()
        {
            var sourceImporterBlock =
                "ScriptedImporter:\n" +
                "  internalIDToNameTable: []\n" +
                "  externalObjects: {}\n" +
                "  serializedVersion: 2\n" +
                "  userData: \n" +
                "  assetBundleName: \n" +
                "  assetBundleVariant: \n" +
                "  script: {fileID: 11500000, guid: 3089979ac9fdd004ba564a7e5418ee8d, type: 3}\n";
            var text = MetaGuidEmitter.BuildMetaText(
                "lts.lilcontainer",
                "00000000000000000000000000000005",
                sourceImporterBlock);
            StringAssert.Contains("ScriptedImporter:", text);
            StringAssert.Contains("guid: 3089979ac9fdd004ba564a7e5418ee8d", text); // lilToon importer script ref preserved
            StringAssert.Contains("guid: 00000000000000000000000000000005", text); // our deterministic guid
        }

        [Test]
        public void BuildMetaText_UnknownExtension_FallsBackToDefaultImporter()
        {
            var text = MetaGuidEmitter.BuildMetaText("foo.xyz", "00000000000000000000000000000006", null);
            StringAssert.Contains("DefaultImporter:", text);
        }
    }
}

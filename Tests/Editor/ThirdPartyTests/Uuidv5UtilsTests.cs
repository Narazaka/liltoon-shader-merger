using System;
using Narazaka.Unity.LilToonShaderMerger.ThirdParty.Uuidv5;
using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests.ThirdParty
{
    public class Uuidv5UtilsTests
    {
        // RFC 9562 Appendix A.4 / widely-cited UUIDv5 test vector:
        //   namespace = DNS (RFC 4122 §C)
        //   name      = "www.example.com"
        //   expected  = 2ed6657d-e927-568b-95e1-2665a8aea6a2
        static readonly Guid DnsNamespace = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        [Test]
        public void GenerateGuid_DnsExampleCom_MatchesRfcVector()
        {
            var g = Uuidv5Utils.GenerateGuid(DnsNamespace, "www.example.com");
            Assert.That(g.ToString("D"), Is.EqualTo("2ed6657d-e927-568b-95e1-2665a8aea6a2"));
        }

        [Test]
        public void GenerateGuid_SameInput_SameOutput()
        {
            var a = Uuidv5Utils.GenerateGuid(DnsNamespace, "foo");
            var b = Uuidv5Utils.GenerateGuid(DnsNamespace, "foo");
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void GenerateGuid_DifferentName_DifferentOutput()
        {
            var a = Uuidv5Utils.GenerateGuid(DnsNamespace, "foo");
            var b = Uuidv5Utils.GenerateGuid(DnsNamespace, "bar");
            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void GenerateGuid_VersionAndVariantBits_AreV5RFC4122()
        {
            var g = Uuidv5Utils.GenerateGuid(DnsNamespace, "v5-bits");
            // Canonical hex: NNNNNNNN-NNNN-VNNN-vNNN-NNNNNNNNNNNN
            //                              ^ version nibble = 5
            //                                   ^ variant top bits = 10xx
            var hex = g.ToString("N"); // 32 lowercase hex
            Assert.That(hex[12], Is.EqualTo('5'), "version nibble must be 5");
            var variantNibble = Convert.ToInt32(hex[16].ToString(), 16);
            Assert.That(variantNibble & 0xC, Is.EqualTo(0x8), "variant top two bits must be 10");
        }
    }
}

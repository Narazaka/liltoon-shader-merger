using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class LilBlockParserTests
    {
        [Test]
        public void ParseDatas_ShaderAndEditorName()
        {
            const string input = @"ShaderName ""motchiri""
EditorName ""lilToon.motchiriInspector""";
            var d = LilBlockParser.ParseDatas(input);
            Assert.That(d.ShaderName, Is.EqualTo("motchiri"));
            Assert.That(d.EditorName, Is.EqualTo("lilToon.motchiriInspector"));
        }

        [Test]
        public void ParseDatas_TwoArgReplace()
        {
            const string input = @"Replace ""#pragma vertex vert"" ""#pragma vertex vertCustom""";
            var d = LilBlockParser.ParseDatas(input);
            Assert.That(d.Replaces.Count, Is.EqualTo(1));
            Assert.That(d.Replaces[0].Filter, Is.Null.Or.Empty);
            Assert.That(d.Replaces[0].From, Is.EqualTo("#pragma vertex vert"));
            Assert.That(d.Replaces[0].To, Is.EqualTo("#pragma vertex vertCustom"));
        }

        [Test]
        public void ParseDatas_ThreeArgReplace_WithFilter()
        {
            const string input = @"Replace ""ltspass"" ""foo"" ""bar""";
            var d = LilBlockParser.ParseDatas(input);
            Assert.That(d.Replaces[0].Filter, Is.EqualTo("ltspass"));
            Assert.That(d.Replaces[0].From, Is.EqualTo("foo"));
            Assert.That(d.Replaces[0].To, Is.EqualTo("bar"));
        }

        [Test]
        public void ParseDatas_InsertPassPre()
        {
            const string input = @"InsertPassPre ""my_insert.lilblock""";
            var d = LilBlockParser.ParseDatas(input);
            Assert.That(d.Inserts["InsertPassPre"], Is.EqualTo("my_insert.lilblock"));
        }
    }
}

using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class SettingsTests
    {
        [Test]
        public void CreateInstance_HasDefaults()
        {
            var s = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
            Assert.That(s.shaderName, Is.Empty);
            Assert.That(s.dedupeIdenticalIncludes, Is.True);
            Assert.That(s.copyExtraFiles, Is.False);
            Assert.That(s.propertyConflict, Is.EqualTo(ConflictStrategy.ErrorOut));
            Assert.That(s.functionConflict, Is.EqualTo(ConflictStrategy.ErrorOut));
            Assert.That(s.replaceConflict,  Is.EqualTo(ConflictStrategy.ErrorOut));
            Assert.That(s.textureConflict,  Is.EqualTo(ConflictStrategy.ErrorOut));
            Object.DestroyImmediate(s);
        }
    }
}

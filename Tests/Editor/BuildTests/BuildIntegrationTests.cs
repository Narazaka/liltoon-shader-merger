using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class BuildIntegrationTests
    {
        const string FixtureRoot =
            "Packages/net.narazaka.unity.liltoon-shader-merger/Tests/Editor/Fixtures";

        [Test]
        public void DryRun_SingleSource_NoDiagnostics()
        {
            var settings = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
            settings.shaderName = "Test/SampleA";
            settings.sourceFolders = new[] {
                AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_a")
            };
            var result = LilToonShaderMerger.DryRun(settings);
            var msgs = string.Join("; ", result.Diagnostics);
            Assert.That(result.Diagnostics, Is.Empty, "expected no diagnostics, got: " + msgs);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void DryRun_TwoSources_NoConflict_NoDiagnostics()
        {
            var settings = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
            settings.shaderName = "Test/SampleAB";
            settings.sourceFolders = new[] {
                AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_a"),
                AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_b"),
            };
            var result = LilToonShaderMerger.DryRun(settings);
            var msgs = string.Join("; ", result.Diagnostics);
            Assert.That(result.Diagnostics, Is.Empty, "expected no diagnostics, got: " + msgs);
            Object.DestroyImmediate(settings);
        }
    }
}

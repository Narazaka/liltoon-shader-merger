using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class BuildIntegrationTests
    {
        const string FixtureRoot =
            "Packages/net.narazaka.unity.liltoon-shader-merger/Tests/Editor/Fixtures";

        [SetUp]
        public void SetUp()
        {
            // 既存の workspace 出力 (バッチテスト等の手動実行で残った壊れた upstream Fur シェーダー) を削除
            // → Unity が AssetDatabase.Refresh で再コンパイル試行する際の Unhandled なシェーダーエラーを防ぐ
            foreach (var dir in new[] { "Assets/_batch_merge_tests", "Assets/_motchiri_uzumore_test", "Assets/_temp_merge_out" })
            {
                if (AssetDatabase.IsValidFolder(dir)) AssetDatabase.DeleteAsset(dir);
            }
            // テスト中の他要因 (lilToon 自体のシェーダー再 import warning 等) も拾わない
            LogAssert.ignoreFailingMessages = true;
        }

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

        [Test]
        public void Build_TwoTimes_ProducesIdenticalFiles()
        {
            var outFolder = "Assets/_temp_merge_out";
            if (System.IO.Directory.Exists(outFolder))
                AssetDatabase.DeleteAsset(outFolder);
            AssetDatabase.CreateFolder("Assets", "_temp_merge_out");

            var settings = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
            settings.shaderName = "Test/Idempotent";
            settings.sourceFolders = new[] {
                AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_a"),
                AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_b"),
            };
            settings.outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(outFolder);

            var r1 = LilToonShaderMerger.Build(settings);
            Assert.That(r1.Success, Is.True, "build 1 failed: " + string.Join("; ", r1.Diagnostics));

            var snapshot1 = new Dictionary<string, string>();
            foreach (var f in System.IO.Directory.GetFiles(outFolder, "*", System.IO.SearchOption.AllDirectories))
                if (!f.EndsWith(".meta")) snapshot1[f] = System.IO.File.ReadAllText(f);

            var r2 = LilToonShaderMerger.Build(settings);
            Assert.That(r2.Success, Is.True);

            foreach (var kv in snapshot1)
            {
                var current = System.IO.File.ReadAllText(kv.Key);
                Assert.That(current, Is.EqualTo(kv.Value), $"file changed between builds: {kv.Key}");
            }

            AssetDatabase.DeleteAsset(outFolder);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void DryRun_PropertyConflict_ErrorOut()
        {
            var settings = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
            settings.shaderName = "Test/Conflict";
            settings.sourceFolders = new[] {
                AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_a"),
                AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/conflict_property"),
            };
            settings.propertyConflict = ConflictStrategy.ErrorOut;

            var result = LilToonShaderMerger.DryRun(settings);
            bool hasErr = false;
            foreach (var d in result.Diagnostics)
                if (d.Severity == Severity.Error && d.Category == "property") { hasErr = true; break; }
            Assert.That(hasErr, Is.True, "expected property error, got: " + string.Join("; ", result.Diagnostics));
            Object.DestroyImmediate(settings);
        }
    }
}

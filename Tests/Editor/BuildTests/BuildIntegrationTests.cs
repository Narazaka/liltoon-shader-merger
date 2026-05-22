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
                snapshot1[f] = System.IO.File.ReadAllText(f);

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

        // Extracts "guid: ..." line value from a .meta file.
        static string ReadMetaGuid(string metaPath)
        {
            foreach (var line in System.IO.File.ReadAllLines(metaPath))
                if (line.StartsWith("guid:")) return line.Substring(5).Trim();
            return null;
        }

        // Snapshot every .meta guid under <folder>, keyed by path relative to <folder>.
        static Dictionary<string, string> SnapshotMetaGuids(string folder)
        {
            var d = new Dictionary<string, string>();
            foreach (var f in System.IO.Directory.GetFiles(folder, "*.meta", System.IO.SearchOption.AllDirectories))
            {
                var rel = f.Substring(folder.Length).TrimStart('/', '\\').Replace('\\', '/');
                d[rel] = ReadMetaGuid(f);
            }
            return d;
        }

        [Test]
        public void Build_SameShaderName_DifferentOutputFolders_ProducesIdenticalGuids()
        {
            var outA = "Assets/_temp_merge_out_a";
            var outB = "Assets/_temp_merge_out_b";
            foreach (var d in new[] { outA, outB })
                if (AssetDatabase.IsValidFolder(d)) AssetDatabase.DeleteAsset(d);
            AssetDatabase.CreateFolder("Assets", "_temp_merge_out_a");
            AssetDatabase.CreateFolder("Assets", "_temp_merge_out_b");

            LilToonShaderMergerSettings MakeSettings(string outFolder)
            {
                var s = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
                s.shaderName = "Test/CrossLocation";
                s.sourceFolders = new[] {
                    AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_a"),
                    AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_b"),
                };
                s.outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(outFolder);
                return s;
            }

            var sa = MakeSettings(outA);
            var sb = MakeSettings(outB);
            var ra = LilToonShaderMerger.Build(sa);
            var rb = LilToonShaderMerger.Build(sb);
            Assert.That(ra.Success, Is.True, string.Join("; ", ra.Diagnostics));
            Assert.That(rb.Success, Is.True, string.Join("; ", rb.Diagnostics));

            var guidsA = SnapshotMetaGuids(outA);
            var guidsB = SnapshotMetaGuids(outB);
            Assert.That(guidsA.Keys, Is.EquivalentTo(guidsB.Keys), "same shaderName must produce the same set of files");
            foreach (var k in guidsA.Keys)
                Assert.That(guidsB[k], Is.EqualTo(guidsA[k]), $"guid mismatch for {k}");

            AssetDatabase.DeleteAsset(outA);
            AssetDatabase.DeleteAsset(outB);
            Object.DestroyImmediate(sa);
            Object.DestroyImmediate(sb);
        }

        [Test]
        public void Build_DifferentShaderName_ProducesDifferentGuids()
        {
            var outA = "Assets/_temp_merge_out_n1";
            var outB = "Assets/_temp_merge_out_n2";
            foreach (var d in new[] { outA, outB })
                if (AssetDatabase.IsValidFolder(d)) AssetDatabase.DeleteAsset(d);
            AssetDatabase.CreateFolder("Assets", "_temp_merge_out_n1");
            AssetDatabase.CreateFolder("Assets", "_temp_merge_out_n2");

            LilToonShaderMergerSettings Mk(string outFolder, string name)
            {
                var s = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
                s.shaderName = name;
                s.sourceFolders = new[] {
                    AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_a"),
                };
                s.outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(outFolder);
                return s;
            }

            var sa = Mk(outA, "Test/NameOne");
            var sb = Mk(outB, "Test/NameTwo");
            Assert.That(LilToonShaderMerger.Build(sa).Success, Is.True);
            Assert.That(LilToonShaderMerger.Build(sb).Success, Is.True);

            var ga = ReadMetaGuid($"{outA}/custom.hlsl.meta");
            var gb = ReadMetaGuid($"{outB}/custom.hlsl.meta");
            Assert.That(ga, Is.Not.EqualTo(gb), "different shaderName must yield different guids");

            AssetDatabase.DeleteAsset(outA);
            AssetDatabase.DeleteAsset(outB);
            Object.DestroyImmediate(sa);
            Object.DestroyImmediate(sb);
        }

        [Test]
        public void Build_OverwritesPreExistingRandomMeta()
        {
            var outFolder = "Assets/_temp_merge_out_overwrite";
            if (AssetDatabase.IsValidFolder(outFolder)) AssetDatabase.DeleteAsset(outFolder);
            AssetDatabase.CreateFolder("Assets", "_temp_merge_out_overwrite");

            var settings = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
            settings.shaderName = "Test/Overwrite";
            settings.sourceFolders = new[] {
                AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_a"),
            };
            settings.outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(outFolder);

            // First build to establish deterministic guid.
            Assert.That(LilToonShaderMerger.Build(settings).Success, Is.True);
            var expectedGuid = ReadMetaGuid($"{outFolder}/custom.hlsl.meta");
            Assert.That(expectedGuid, Is.Not.Null);

            // Overwrite custom.hlsl.meta with a random guid.
            System.IO.File.WriteAllText(
                $"{outFolder}/custom.hlsl.meta",
                "fileFormatVersion: 2\nguid: ffffffffffffffffffffffffffffffff\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");

            // Build again; deterministic guid should be restored.
            Assert.That(LilToonShaderMerger.Build(settings).Success, Is.True);
            var afterGuid = ReadMetaGuid($"{outFolder}/custom.hlsl.meta");
            Assert.That(afterGuid, Is.EqualTo(expectedGuid));

            AssetDatabase.DeleteAsset(outFolder);
            Object.DestroyImmediate(settings);
        }
    }
}

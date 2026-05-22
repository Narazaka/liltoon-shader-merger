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

        static readonly string[] TempOutputDirs =
        {
            "Assets/_batch_merge_tests", "Assets/_motchiri_uzumore_test", "Assets/_temp_merge_out",
            "Assets/_temp_merge_out_det", "Assets/_temp_merge_out_n1", "Assets/_temp_merge_out_n2",
            "Assets/_temp_merge_out_overwrite",
        };

        [SetUp]
        public void SetUp()
        {
            // 無関係なコンソールエラー (プロジェクト内に元から存在する壊れたシェーダーが
            // AssetDatabase.Refresh で再コンパイルされる等) でテストを落とさない。
            // DeleteAsset 自体がその再コンパイルを誘発しうるため、削除より前に設定する。
            LogAssert.ignoreFailingMessages = true;

            // 前回テストやバッチ実行で残った出力フォルダを削除
            foreach (var dir in TempOutputDirs)
            {
                if (AssetDatabase.IsValidFolder(dir)) AssetDatabase.DeleteAsset(dir);
            }
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

            var r1 = LilToonShaderMerger.Build(settings, refreshAssetDatabase: false);
            Assert.That(r1.Success, Is.True, "build 1 failed: " + string.Join("; ", r1.Diagnostics));

            var snapshot1 = new Dictionary<string, string>();
            foreach (var f in System.IO.Directory.GetFiles(outFolder, "*", System.IO.SearchOption.AllDirectories))
                snapshot1[f] = System.IO.File.ReadAllText(f);

            var r2 = LilToonShaderMerger.Build(settings, refreshAssetDatabase: false);
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

        [Test]
        public void Build_MetaGuids_AreDeterministicForShaderNameAndPath()
        {
            // Determinism contract: every generated .meta GUID equals the pure value
            // UUIDv5(shaderName, output-relative path), so the same shaderName/config
            // reproduces identical GUIDs for any developer or CI run.
            // Note: two coexisting copies in ONE project cannot share a GUID — Unity
            // de-dupes asset GUIDs within a project — so determinism is verified against
            // the pure function, not by building the same shader into two folders at once.
            var outFolder = "Assets/_temp_merge_out_det";
            if (AssetDatabase.IsValidFolder(outFolder)) AssetDatabase.DeleteAsset(outFolder);
            AssetDatabase.CreateFolder("Assets", "_temp_merge_out_det");

            var settings = ScriptableObject.CreateInstance<LilToonShaderMergerSettings>();
            settings.shaderName = "Test/Deterministic";
            settings.sourceFolders = new[] {
                AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_a"),
                AssetDatabase.LoadAssetAtPath<DefaultAsset>($"{FixtureRoot}/sample_b"),
            };
            settings.outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(outFolder);

            var r = LilToonShaderMerger.Build(settings, refreshAssetDatabase: false);
            Assert.That(r.Success, Is.True, string.Join("; ", r.Diagnostics));

            var metas = System.IO.Directory.GetFiles(outFolder, "*.meta", System.IO.SearchOption.AllDirectories);
            Assert.That(metas.Length, Is.GreaterThan(0), "build produced no .meta files");
            foreach (var meta in metas)
            {
                var asset = meta.Substring(0, meta.Length - ".meta".Length);
                var rel = MetaGuidEmitter.Relative(outFolder, asset);
                var expected = MetaGuidEmitter.DeterministicGuid(settings.shaderName, rel);
                Assert.That(ReadMetaGuid(meta), Is.EqualTo(expected), $"guid for {rel}");
            }

            AssetDatabase.DeleteAsset(outFolder);
            Object.DestroyImmediate(settings);
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
            Assert.That(LilToonShaderMerger.Build(sa, refreshAssetDatabase: false).Success, Is.True);
            Assert.That(LilToonShaderMerger.Build(sb, refreshAssetDatabase: false).Success, Is.True);

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
            Assert.That(LilToonShaderMerger.Build(settings, refreshAssetDatabase: false).Success, Is.True);
            var expectedGuid = ReadMetaGuid($"{outFolder}/custom.hlsl.meta");
            Assert.That(expectedGuid, Is.Not.Null);

            // Overwrite custom.hlsl.meta with a random guid.
            System.IO.File.WriteAllText(
                $"{outFolder}/custom.hlsl.meta",
                "fileFormatVersion: 2\nguid: ffffffffffffffffffffffffffffffff\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");

            // Build again; deterministic guid should be restored.
            Assert.That(LilToonShaderMerger.Build(settings, refreshAssetDatabase: false).Success, Is.True);
            var afterGuid = ReadMetaGuid($"{outFolder}/custom.hlsl.meta");
            Assert.That(afterGuid, Is.EqualTo(expectedGuid));

            AssetDatabase.DeleteAsset(outFolder);
            Object.DestroyImmediate(settings);
        }
    }
}

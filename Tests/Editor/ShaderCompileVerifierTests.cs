using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class ShaderCompileVerifierTests
    {
        const string Dir = "Assets/_lsm_verifier_test";

        const string Template = @"Shader ""Hidden/LsmVerifierTest/NAME""
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 v : POSITION) : SV_POSITION { return v * BODY; }
            fixed4 frag() : SV_Target { return 1; }
            ENDCG
        }
    }
}";

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Dir);

        static string Write(string name, string body, string sub = "")
        {
            // シェーダーの import でプロジェクト内の無関係なシェーダーのエラーもログに出る。検証結果は戻り値の診断で見るので、ログでは失敗させない
            // (SetUp での設定はテスト開始時に戻されるので、テスト本体から呼ばれるここで設定する)
            LogAssert.ignoreFailingMessages = true;
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "_lsm_verifier_test");
            var dir = Dir;
            if (sub != "")
            {
                dir = $"{Dir}/{sub}";
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Dir, sub);
            }
            var path = $"{dir}/{name}.shader";
            File.WriteAllText(path, Template.Replace("NAME", sub + name).Replace("BODY", body));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        [Test]
        public void VerifyMerged_ErrorAlsoInOriginal_IsWarning()
        {
            Write("Shared", "_Undeclared", "out");
            Write("Shared", "_Undeclared", "src");
            var diags = new List<Diagnostic>();
            ShaderCompileVerifier.VerifyMerged($"{Dir}/out", new[] { ("src", $"{Dir}/src") }, diags, "*.shader");
            Assert.That(diags.Exists(d => d.Severity == Severity.Error), Is.False, string.Join("\n", diags));
            Assert.That(diags.Exists(d => d.Severity == Severity.Warning && d.Message.Contains("_Undeclared") && d.Category == ShaderCompileVerifier.PreExistingCategoryPrefix + "src"), string.Join("\n", diags));
        }

        [Test]
        public void VerifyMerged_ErrorNotInOriginal_IsError()
        {
            Write("Shared", "_Undeclared", "out");
            Write("Shared", "1", "src");
            var diags = new List<Diagnostic>();
            ShaderCompileVerifier.VerifyMerged($"{Dir}/out", new[] { ("src", $"{Dir}/src") }, diags, "*.shader");
            Assert.That(diags.Exists(d => d.Severity == Severity.Error && d.Message.Contains("_Undeclared") && d.Category == ShaderCompileVerifier.MergeCausedCategory), string.Join("\n", diags));
        }

        [Test]
        public void Verify_UndeclaredIdentifier_ReportsError()
        {
            var path = Write("Broken", "_Undeclared");
            var diags = new List<Diagnostic>();
            ShaderCompileVerifier.Verify(new[] { path }, diags);
            Assert.That(diags.Exists(d => d.Severity == Severity.Error && d.Category == "shader-compile" && d.Message.Contains("_Undeclared")), string.Join("\n", diags));
        }

        [Test]
        public void Verify_ValidShader_NoError()
        {
            var path = Write("Valid", "1");
            var diags = new List<Diagnostic>();
            ShaderCompileVerifier.Verify(new[] { path }, diags);
            Assert.That(diags.Exists(d => d.Severity == Severity.Error), Is.False, string.Join("\n", diags));
        }
    }
}

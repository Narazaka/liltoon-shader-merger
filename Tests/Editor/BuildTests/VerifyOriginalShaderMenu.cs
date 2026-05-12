#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public static class VerifyOriginalShaderMenu
    {
        [MenuItem("Tools/lilToon Shader Merger/Test/Verify Original Shaders Compile")]
        public static void Run()
        {
            string[] names = {
                "KuukuuVirtualFactory/HawaseGimmickShader/lilToon",
                "Hidden/KuukuuVirtualFactory/HawaseGimmickShader/Fur",
                "Hidden/KuukuuVirtualFactory/HawaseGimmickShader/FurCutout",
                "Hidden/KuukuuVirtualFactory/HawaseGimmickShader/FurTwoPass",
            };
            var sb = new StringBuilder("=== Original Shader Compile Verify ===\n");
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) { sb.AppendLine($"  '{n}' -> NULL"); continue; }
                var msgs = UnityEditor.ShaderUtil.GetShaderMessages(sh);
                int errs = 0, warns = 0;
                foreach (var m in msgs)
                {
                    if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) errs++;
                    else warns++;
                }
                sb.AppendLine($"  '{n}': {errs} errors, {warns} warnings");
                foreach (var m in msgs)
                {
                    sb.AppendLine($"    [{m.severity}] {m.message.Substring(0, System.Math.Min(200, m.message.Length))}");
                }
            }
            Debug.Log(sb.ToString());
        }
    }
}
#endif

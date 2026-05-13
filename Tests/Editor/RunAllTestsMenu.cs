#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public static class RunAllTestsMenu
    {
        [MenuItem("Tools/lilToon Shader Merger/Test/Run All EditMode Tests")]
        public static void Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Cb());
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "net.narazaka.unity.liltoon-shader-merger.Tests.Editor" }
            }));
            Debug.Log("[Tests] started; results will be written to Assets/_test_results.txt");
        }

        class Cb : ICallbacks
        {
            readonly StringBuilder sb = new StringBuilder();
            int passed, failed;

            public void RunStarted(ITestAdaptor _) { sb.AppendLine("=== EditMode Tests ==="); }

            public void TestStarted(ITestAdaptor _) { }

            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.HasChildren || r.Test.IsSuite) return;
                if (r.TestStatus == TestStatus.Passed) passed++;
                else
                {
                    failed++;
                    sb.AppendLine($"{r.TestStatus}: {r.Test.FullName}");
                    if (!string.IsNullOrEmpty(r.Message))
                        sb.AppendLine("  " + r.Message.Trim().Substring(0, System.Math.Min(400, r.Message.Trim().Length)));
                }
            }

            public void RunFinished(ITestResultAdaptor _)
            {
                sb.Insert(0, $"passed={passed} failed={failed}\n");
                File.WriteAllText(Path.Combine(Application.dataPath, "_test_results.txt"), sb.ToString());
                Debug.Log("[Tests] " + sb.ToString());
            }
        }
    }
}
#endif

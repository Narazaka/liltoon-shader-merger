using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger
{
    // 出力したシェーダーを実際にコンパイルして、エラーを診断に積む。
    // テキストの合成で取りこぼしたものは最終的にここで捕まえる。
    // 既定のキーワード状態のマテリアルで全パスをコンパイルする。キーワードの組み合わせ全部は検証しない
    public static class ShaderCompileVerifier
    {
        public const string MergeCausedCategory = "CAUSED BY MERGE";
        public const string PreExistingCategoryPrefix = "PRE-EXISTING in ";

        // 合成出力の .lilcontainer を検証する。同名の元 .lilcontainer でも同じメッセージが出るなら合成で生じたものではないので
        // "pre-existing in <ソース>" に分類し、エラーも Warning に落とす。合成で生じたものを先に並べる。
        // 元シェーダーはメッセージが出た container についてだけコンパイルする
        public static void VerifyMerged(string outputFolder, IReadOnlyList<(string label, string folder)> sources, List<Diagnostic> diags, string pattern = "*.lilcontainer")
        {
            var seen = new HashSet<string>();
            var mergeCaused = new List<Diagnostic>();
            var preExisting = new List<Diagnostic>();
            foreach (var path in Directory.GetFiles(outputFolder, pattern))
            {
                var shader = Compile(path.Replace('\\', '/'), diags);
                if (shader == null) continue;
                Dictionary<string, string> originalMessages = null; // メッセージ → それが出た元ソース
                foreach (var m in ShaderUtil.GetShaderMessages(shader))
                {
                    var message = m.message.Trim();
                    if (!seen.Add($"{message}|{m.file}|{m.line}")) continue;
                    var d = ToDiagnostic(shader, m);
                    if (originalMessages == null) originalMessages = OriginalMessages(Path.GetFileName(path), sources, diags);
                    if (originalMessages.TryGetValue(message, out var original))
                    {
                        d.Severity = Severity.Warning;
                        d.Category = PreExistingCategoryPrefix + original;
                        preExisting.Add(d);
                    }
                    else
                    {
                        d.Category = MergeCausedCategory;
                        var codeOf = InsertSectionSource(MessageFile(shader, m.file), m.line);
                        if (codeOf != null) d.Message = $"[code: {codeOf}] {d.Message}";
                        mergeCaused.Add(d);
                    }
                }
            }
            diags.AddRange(mergeCaused);
            diags.AddRange(preExisting);
        }

        // 合成後の custom_insert.hlsl はソースごとに "// --- <sourceKey> ---" で区切って連結しているので、行からソースが分かる
        static string InsertSectionSource(string file, int line)
        {
            if (string.IsNullOrEmpty(file) || Path.GetFileName(file) != "custom_insert.hlsl" || !File.Exists(file)) return null;
            var lines = File.ReadAllLines(file);
            for (int i = System.Math.Min(line, lines.Length) - 1; i >= 0; i--)
            {
                var t = lines[i].Trim();
                if (t.StartsWith("// --- ") && t.EndsWith(" ---")) return t.Substring(7, t.Length - 11);
            }
            return null;
        }

        // 渡したシェーダーのメッセージをそのまま診断にする
        public static void Verify(IEnumerable<string> shaderAssetPaths, List<Diagnostic> diags)
        {
            // 同じ include 由来の警告がシェーダーごとに並ばないよう、内容と位置で 1 回にまとめる
            var seen = new HashSet<string>();
            foreach (var path in shaderAssetPaths)
            {
                var shader = Compile(path, diags);
                if (shader == null) continue;
                foreach (var m in ShaderUtil.GetShaderMessages(shader))
                    if (seen.Add($"{m.message.Trim()}|{m.file}|{m.line}")) diags.Add(ToDiagnostic(shader, m));
            }
        }

        static Dictionary<string, string> OriginalMessages(string fileName, IReadOnlyList<(string label, string folder)> sources, List<Diagnostic> diags)
        {
            var messages = new Dictionary<string, string>();
            foreach (var (label, folder) in sources)
            {
                var original = Path.Combine(folder, fileName).Replace('\\', '/');
                if (!File.Exists(original)) continue;
                var shader = Compile(original, diags);
                if (shader == null) continue;
                foreach (var m in ShaderUtil.GetShaderMessages(shader))
                {
                    var message = m.message.Trim();
                    if (!messages.ContainsKey(message)) messages[message] = label;
                }
            }
            return messages;
        }

        static Shader Compile(string path, List<Diagnostic> diags)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                diags.Add(new Diagnostic { Severity = Severity.Error, Category = "shader-compile", Message = $"{path} was not imported as a shader" });
                return null;
            }
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
            return shader;
        }

        static Diagnostic ToDiagnostic(Shader shader, ShaderMessage m) => new Diagnostic
        {
            Severity = m.severity == ShaderCompilerMessageSeverity.Error ? Severity.Error : Severity.Warning,
            Category = "shader-compile",
            Message = $"{shader.name}: {m.message.Trim()} @ {MessageFile(shader, m.file)}({m.line})",
        };

        // Unity は前処理後の内容が同じならコンパイル結果をキャッシュから返すので、メッセージのファイルが
        // 内容の同じ別フォルダ (削除済みのこともある) を指すことがある。シェーダーと同じフォルダの同名ファイルに読み替える
        static string MessageFile(Shader shader, string file)
        {
            if (string.IsNullOrEmpty(file) || File.Exists(file)) return file;
            var sameFolder = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(shader)), Path.GetFileName(file)).Replace('\\', '/');
            return File.Exists(sameFolder) ? sameFolder : Path.GetFileName(file);
        }
    }
}

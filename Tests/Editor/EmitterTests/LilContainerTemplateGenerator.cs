using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Narazaka.Unity.LilToonShaderMerger.Tests
{
    public class LilContainerTemplateGenerator
    {
        // テンプレートから作られたカスタムシェーダーのフォルダ。構造を書き換えたものが混ざっていてよい
        // (2 フォルダ以上で一致した構造だけをテンプレートとみなすので、1 つだけが書き換えた構造は除外される)。
        // 合成出力フォルダは入れないこと (書き換えた構造を元ソースと共有してしまう)
        static readonly string[] SourceFolders =
        {
            "Assets/BekoShop/lilToonCustomClipper/Shaders",
            "Assets/KuukuuVirtualFactory/K2Shader/Customliltoon_ParallelThrough/Shaders",
            "Assets/KuukuuVirtualFactory/K2Shader/HawaseGimmickShader/Shaders",
            "Assets/lilToon_FresnelAlphaEx/Shaders",
            "Assets/lilToon_unebeta/Shaders",
            "Assets/motchiri_shader/Shader/Shaders",
            "Packages/jp.sigmal00.uzumore-shader/Runtime/Shaders",
            "Packages/org.kb10uy.liltoon-msdfmask/Shader",
        };

        const string OutputPath = "Packages/net.narazaka.unity.liltoon-shader-merger/Editor/Emitter/LilContainerTemplate.cs";

        [Test, Explicit("LilContainerTemplate.cs を再生成する開発用ツール")]
        public void Generate()
        {
            var counts = new SortedDictionary<string, Dictionary<string, int>>(System.StringComparer.Ordinal);
            foreach (var folder in SourceFolders)
            {
                Assert.That(Directory.Exists(folder), folder);
                foreach (var f in Directory.GetFiles(folder, "*.lilcontainer"))
                {
                    var name = Path.GetFileName(f);
                    if (!counts.TryGetValue(name, out var byHash)) counts[name] = byHash = new Dictionary<string, int>();
                    var hash = LilContainerEmitter.StructureHash(File.ReadAllText(f));
                    byHash.TryGetValue(hash, out var n);
                    byHash[hash] = n + 1;
                }
            }

            var sb = new StringBuilder();
            sb.Append(@"// 自動生成: Tests/Editor/EmitterTests/LilContainerTemplateGenerator.cs (Explicit テスト) で再生成する
using System.Collections.Generic;

namespace Narazaka.Unity.LilToonShaderMerger
{
    // lilToon カスタムシェーダーテンプレートのままの .lilcontainer 構造 (LilContainerEmitter.StructureHash)
    static class LilContainerTemplate
    {
        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
");
            foreach (var kv in counts)
            {
                var hashes = kv.Value.Where(h => h.Value >= 2).Select(h => h.Key).OrderBy(h => h, System.StringComparer.Ordinal).ToList();
                if (hashes.Count == 0) continue;
                sb.Append($"            {{ \"{kv.Key}\", new[] {{ {string.Join(", ", hashes.Select(h => $"\"{h}\""))} }} }},\n");
            }
            sb.Append(@"        };

        static readonly string[] None = new string[0];

        public static ICollection<string> StructureHashes(string fileName) =>
            Table.TryGetValue(fileName, out var hashes) ? hashes : None;
    }
}
");
            File.WriteAllText(OutputPath, sb.ToString().Replace("\r\n", "\n"));
        }
    }
}
